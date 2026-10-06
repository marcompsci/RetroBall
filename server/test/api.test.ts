import { test, describe, beforeEach } from "node:test";
import assert from "node:assert/strict";
import { createApp, type Deps } from "../src/app.ts";
import { clearCertCache, parseCertificate, trustedKeyUrl } from "../src/gamecenter.ts";
import { accountToken, appleApiToken, fetchSubscription, notificationTransactionId } from "../src/appstore.ts";
import { issueSession, readSession } from "../src/session.ts";
import { winnerDelta, settleStale, SETTLE_AFTER_MS } from "../src/live.ts";
import { b64UrlToStr, b64ToBytes, bytesToB64, concat } from "../src/b64.ts";
import { requestTag, resultFields, startFields, REQUEST_WINDOW_MS } from "../src/sign.ts";
import { memoryDb, makeGameCenterKey, signGameCenter, makeAppleKey, fakeFetch, fakeJws } from "./helpers.ts";

const SECRET = "test-secret-0123456789-abcdefghijklmnopqrstuvwxyz";
const BUNDLE = "com.phoronomicstudios.retrohoops";
const PRODUCT = "com.phoronomicstudios.retrohoops.live.monthly";
const KEY_URL = "https://static.gc.apple.com/public-key/gc-prod-10.cer";

const gc = makeGameCenterKey();

interface World {
  deps: Deps;
  app: (r: Request) => Promise<Response>;
  clock: { t: number };
  subs: Map<string, { status: number; expires: number; token?: string; revoked?: number }>;
  appleCalls: number;
}

async function world(): Promise<World> {
  clearCertCache();
  const apple = await makeAppleKey();
  const clock = { t: Date.UTC(2026, 9, 6, 12, 0, 0) };
  const subs = new Map<string, { status: number; expires: number; token?: string; revoked?: number }>();
  const w = { clock, subs, appleCalls: 0 } as World;
  const fetcher = fakeFetch((url, init) => {
    if (url === KEY_URL) return new Response(gc.certDer);
    const m = url.match(/^https:\/\/api\.storekit-sandbox\.itunes\.apple\.com\/inApps\/v1\/subscriptions\/(\d+)$/);
    if (m) {
      w.appleCalls++;
      const auth = new Headers(init?.headers).get("authorization") ?? "";
      if (!auth.startsWith("Bearer ")) return new Response("", { status: 401 });
      const s = subs.get(m[1]);
      if (!s) return new Response("", { status: 404 });
      const info = fakeJws({ originalTransactionId: m[1], productId: PRODUCT, bundleId: BUNDLE, expiresDate: s.expires, appAccountToken: s.token, revocationDate: s.revoked, environment: "Sandbox" });
      return Response.json({ bundleId: BUNDLE, environment: "Sandbox", data: [{ lastTransactions: [{ originalTransactionId: m[1], status: s.status, signedTransactionInfo: info }] }] });
    }
    return new Response("", { status: 599 });
  });
  w.deps = {
    db: memoryDb(),
    fetch: fetcher,
    now: () => clock.t,
    sessionSecret: SECRET,
    apple: { issuerId: "issuer", keyId: "KEY123", privateKeyPem: apple.pem, bundleId: BUNDLE, productId: PRODUCT, env: "sandbox" },
  };
  w.app = createApp(w.deps);
  return w;
}

function post(path: string, body: unknown, token?: string, ip = "1.2.3.4"): Request {
  const headers: Record<string, string> = { "content-type": "application/json", "cf-connecting-ip": ip };
  if (token) headers.authorization = "Bearer " + token;
  return new Request("https://api.test" + path, { method: "POST", headers, body: JSON.stringify(body) });
}

function get(path: string, token?: string): Request {
  const headers: Record<string, string> = { "cf-connecting-ip": "1.2.3.4" };
  if (token) headers.authorization = "Bearer " + token;
  return new Request("https://api.test" + path, { headers });
}

async function proof(playerId: string, now: number, salt = crypto.getRandomValues(new Uint8Array(8))) {
  const ts = new Uint8Array(8);
  new DataView(ts.buffer).setBigUint64(0, BigInt(now), false);
  const payload = concat(new TextEncoder().encode(playerId), new TextEncoder().encode(BUNDLE), ts, salt);
  return { playerId, name: "Player " + playerId, publicKeyUrl: KEY_URL, signature: await signGameCenter(gc.keyPem, payload), salt: bytesToB64(salt), timestamp: now };
}

async function signIn(w: World, playerId: string): Promise<string> {
  const r = await w.app(post("/v1/session", await proof(playerId, w.clock.t)));
  assert.equal(r.status, 200, await r.clone().text());
  return ((await r.json()) as { token: string }).token;
}

async function subscribe(w: World, playerId: string, token: string, txn: string): Promise<Response> {
  w.subs.set(txn, { status: 1, expires: w.clock.t + 30 * 86400_000, token: await accountToken(playerId) });
  return w.app(post("/v1/subscription", { originalTransactionId: txn }, token));
}

describe("Game Center sign-in", () => {
  let w: World;
  beforeEach(async () => { w = await world(); });

  test("a real signature gets a session; the response is locked down", async () => {
    const r = await w.app(post("/v1/session", await proof("T:alice", w.clock.t)));
    assert.equal(r.status, 200);
    assert.equal(r.headers.get("cache-control"), "no-store");
    assert.equal(r.headers.get("x-content-type-options"), "nosniff");
    assert.equal(r.headers.get("access-control-allow-origin"), null, "no CORS");
    const body = (await r.json()) as { token: string; player: { rating: number }; accountToken: string };
    assert.equal(body.player.rating, 1000);
    assert.match(body.accountToken, /^[0-9a-f]{8}-[0-9a-f]{4}-5[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
    const s = await readSession(SECRET, body.token, Math.floor(w.clock.t / 1000));
    assert.equal(s?.sub, "T:alice");
  });

  test("forged, replayed, stale and off-site signatures are refused", async () => {
    const p = await proof("T:alice", w.clock.t);
    assert.equal((await w.app(post("/v1/session", { ...p, playerId: "T:mallory" }))).status, 401, "someone else's signature");
    assert.equal((await w.app(post("/v1/session", p))).status, 200);
    assert.equal((await w.app(post("/v1/session", p))).status, 401, "replayed");
    const old = await proof("T:alice", w.clock.t - 11 * 60_000);
    assert.equal((await w.app(post("/v1/session", old))).status, 401, "stale");
    const evil = { ...(await proof("T:alice", w.clock.t)), publicKeyUrl: "https://static.gc.apple.com.evil.example/k.cer" };
    assert.equal((await w.app(post("/v1/session", evil))).status, 401, "key not from apple.com");
    assert.equal((await w.app(post("/v1/session", { playerId: "x" }))).status, 400);
  });

  test("key URL rules", () => {
    assert.ok(trustedKeyUrl("https://static.gc.apple.com/public-key/gc-prod-10.cer"));
    assert.ok(!trustedKeyUrl("http://static.gc.apple.com/k.cer"));
    assert.ok(!trustedKeyUrl("https://apple.com.evil.example/k.cer"));
    assert.ok(!trustedKeyUrl("https://user:pw@static.gc.apple.com/k.cer"));
    assert.ok(!trustedKeyUrl("https://static.gc.apple.com:8443/k.cer"));
    assert.ok(!trustedKeyUrl("not a url"));
  });

  test("the certificate parser finds the key and dates", () => {
    const c = parseCertificate(gc.certDer);
    assert.ok(c.spki.length > 200);
    assert.ok(c.notAfter > c.notBefore);
    assert.throws(() => parseCertificate(new Uint8Array([0x30, 0x82, 0xff])));
  });

  test("sign-in is rate limited per IP", async () => {
    let last = 0;
    for (let i = 0; i < 12; i++) last = (await w.app(post("/v1/session", await proof("T:p" + i, w.clock.t)))).status;
    assert.equal(last, 429);
  });
});

describe("sessions", () => {
  test("tampered and expired tokens are rejected", async () => {
    const now = 1_800_000_000;
    const t = await issueSession(SECRET, "T:alice", now);
    assert.equal((await readSession(SECRET, t, now + 10))?.sub, "T:alice");
    assert.equal(await readSession(SECRET, t, now + 3601), null, "expired");
    assert.equal(await readSession("another-secret-0123456789-abcdefghijklmn", t, now), null, "wrong key");
    const [h, , s] = t.split(".");
    const forged = h + "." + btoa(JSON.stringify({ sub: "T:mallory", iat: now, exp: now + 999 })).replace(/=+$/, "") + "." + s;
    assert.equal(await readSession(SECRET, forged, now), null, "edited payload");
    const none = btoa(JSON.stringify({ alg: "none" })) + "." + t.split(".")[1] + ".";
    assert.equal(await readSession(SECRET, none, now), null, "alg none");
    await assert.rejects(issueSession("short", "x", now));
  });
});

describe("subscriptions", () => {
  let w: World;
  beforeEach(async () => { w = await world(); });

  test("checked with Apple, linked to one player, and never taken from the app's word", async () => {
    const alice = await signIn(w, "T:alice");
    const r = await subscribe(w, "T:alice", alice, "2000000123456789");
    assert.equal(r.status, 200);
    assert.equal(((await r.json()) as { active: boolean }).active, true);
    const me = (await (await w.app(get("/v1/me", alice))).json()) as { subscription: { active: boolean } };
    assert.equal(me.subscription.active, true);

    // Bob tries to use Alice's subscription.
    const bob = await signIn(w, "T:bob");
    assert.equal((await w.app(post("/v1/subscription", { originalTransactionId: "2000000123456789" }, bob))).status, 403);
    // A made-up transaction id gets nothing.
    assert.equal(((await (await w.app(post("/v1/subscription", { originalTransactionId: "999" }, bob))).json()) as { active: boolean }).active, false);
    assert.equal((await w.app(post("/v1/subscription", { originalTransactionId: "1 OR 1=1" }, bob))).status, 400);
    assert.equal((await w.app(post("/v1/subscription", { originalTransactionId: "1" }))).status, 401, "no session");
  });

  test("a purchase tagged for another player is refused even if never seen before", async () => {
    const bob = await signIn(w, "T:bob");
    w.subs.set("777", { status: 1, expires: w.clock.t + 86400_000, token: await accountToken("T:alice") });
    assert.equal((await w.app(post("/v1/subscription", { originalTransactionId: "777" }, bob))).status, 403);
  });

  test("expiry, refunds and Apple's notifications are picked up", async () => {
    const alice = await signIn(w, "T:alice");
    await subscribe(w, "T:alice", alice, "555");
    // Refunded (revoked): Apple notifies; the server re-asks Apple and stores the new state.
    w.subs.set("555", { status: 5, expires: w.clock.t + 86400_000, revoked: w.clock.t });
    const note = fakeJws({ notificationType: "REFUND", data: { signedTransactionInfo: fakeJws({ originalTransactionId: "555" }) } });
    assert.equal((await w.app(post("/v1/apple/notifications", { signedPayload: note }))).status, 200);
    const me = (await (await w.app(get("/v1/me", alice))).json()) as { subscription: { active: boolean } };
    assert.equal(me.subscription.active, false);
    // A forged notification can only make the server re-check with Apple: it can't grant anything.
    assert.equal(notificationTransactionId("garbage"), null);
  });

  test("the App Store API token is a proper ES256 JWT", async () => {
    const t = await appleApiToken(w.deps.apple, 1_800_000_000);
    const [h, p] = t.split(".");
    assert.deepEqual(JSON.parse(b64UrlToStr(h)), { alg: "ES256", kid: "KEY123", typ: "JWT" });
    const body = JSON.parse(b64UrlToStr(p));
    assert.equal(body.aud, "appstoreconnect-v1");
    assert.equal(body.bid, BUNDLE);
    assert.ok(body.exp - body.iat <= 3600);
    assert.equal(b64ToBytes(t.split(".")[2]).length, 64, "raw r||s signature");
    assert.equal(await fetchSubscription(w.deps.apple, "abc", w.deps.fetch, w.clock.t), null);
  });
});

describe("Live ratings", () => {
  let w: World;
  let alice: string, bob: string;
  const KEY = "0123456789abcdef0123456789abcdef";
  beforeEach(async () => {
    w = await world();
    alice = await signIn(w, "T:alice");
    bob = await signIn(w, "T:bob");
    await subscribe(w, "T:alice", alice, "100");
    await subscribe(w, "T:bob", bob, "200");
  });

  // The game signs match requests (Backend.StartBody / ResultBody with a timestamp); so do these helpers.
  const start = async (token: string, opponent: string, seat: number, key = KEY) => {
    const t = w.clock.t;
    return w.app(post("/v1/match/start", { matchKey: key, opponentId: opponent, seat, t, tag: await requestTag(key, startFields(key, opponent, seat), t) }, token));
  };
  const result = async (token: string, scoreA: number, scoreB: number, hash: string, outcome = "final", key = KEY) => {
    const t = w.clock.t;
    return w.app(post("/v1/match/result", { matchKey: key, scoreA, scoreB, hash, outcome, t, tag: await requestTag(key, resultFields(key, scoreA, scoreB, hash, outcome), t) }, token));
  };

  test("the monthly leaderboard counts this month's settled games only (Phase 33)", async () => {
    assert.equal((await start(alice, "T:bob", 0)).status, 200);
    assert.equal((await start(bob, "T:alice", 1)).status, 200);
    await result(alice, 21, 15, "deadbeef");
    await result(bob, 21, 15, "deadbeef");
    const board = (await (await w.app(new Request("https://api.test/v1/leaderboard?period=month"))).json()) as { month: number; players: { name: string; points: number; games: number }[] };
    assert.equal(board.month, 202610);
    assert.equal(board.players.length, 2);
    assert.ok(board.players[0].points > 0 && board.players[1].points < 0, "the winner gained, the loser lost");
    assert.equal(board.players[0].points, -board.players[1].points);
    w.clock.t = Date.UTC(2026, 10, 2); // November: a new month, nobody on it yet
    const next = (await (await w.app(new Request("https://api.test/v1/leaderboard?period=month"))).json()) as { month: number; players: unknown[] };
    assert.equal(next.month, 202611);
    assert.equal(next.players.length, 0);
  });

  test("agreeing reports settle the game once; ratings change only on the server", async () => {
    assert.equal((await start(alice, "T:bob", 0)).status, 200);
    assert.equal((await start(bob, "T:alice", 1)).status, 200);
    const first = (await (await result(alice, 21, 15, "deadbeef")).json()) as { state: string };
    assert.equal(first.state, "open", "waits for the other report");
    const second = (await (await result(bob, 21, 15, "deadbeef")).json()) as { state: string; change: number; player: { rating: number; losses: number } };
    assert.equal(second.state, "settled");
    assert.equal(second.change, -16);
    assert.equal(second.player.rating, 984);
    const me = (await (await w.app(get("/v1/me", alice))).json()) as { player: { rating: number; wins: number } };
    assert.equal(me.player.rating, 1016);
    assert.equal(me.player.wins, 1);
    // Reporting again changes nothing.
    await result(alice, 21, 0, "deadbeef");
    const again = (await (await w.app(get("/v1/me", alice))).json()) as { player: { rating: number } };
    assert.equal(again.player.rating, 1016);
  });

  test("reports that disagree change nobody's rating", async () => {
    await start(alice, "T:bob", 0);
    await start(bob, "T:alice", 1);
    await result(alice, 21, 3, "deadbeef");
    const r = (await (await result(bob, 15, 21, "cafebabe")).json()) as { state: string; change: number };
    assert.equal(r.state, "disputed");
    assert.equal(r.change, 0);
  });

  test("a lone report settles after the wait; quitting is a loss", async () => {
    await start(alice, "T:bob", 0);
    await start(bob, "T:alice", 1);
    await result(bob, 0, 0, "00000000", "quit");
    w.clock.t += SETTLE_AFTER_MS + 1000;
    assert.equal(await settleStale(w.deps.db, w.clock.t), 1);
    const me = (await (await w.app(get("/v1/me", alice))).json()) as { player: { wins: number } };
    assert.equal(me.player.wins, 1);
  });

  test("you can't report other people's games, play yourself, or play without a subscription", async () => {
    await start(alice, "T:bob", 0);
    const carol = await signIn(w, "T:carol");
    assert.equal((await result(carol, 21, 0, "deadbeef")).status, 403);
    assert.equal((await start(carol, "T:alice", 1)).status, 402, "carol has no subscription");
    await subscribe(w, "T:carol", carol, "300");
    assert.equal((await start(carol, "T:alice", 1)).status, 403, "the key already belongs to alice and bob");
    assert.equal((await start(alice, "T:alice", 0, "ffffffffffffffffffffffffffffffff")).status, 400);
    assert.equal((await result(alice, 21, 0, "not-hex")).status, 400);
    assert.equal((await result(alice, -1, 0, "deadbeef")).status, 400);
  });

  test("rating farming between the same two players is capped per day", async () => {
    let last = 0;
    for (let i = 0; i < 6; i++) {
      const key = (i.toString(16) + "0123456789abcdef0123456789abcde").slice(0, 32);
      last = (await start(alice, "T:bob", 0, key)).status;
    }
    assert.equal(last, 429);
  });

  test("oversized and non-JSON bodies are refused", async () => {
    const big = new Request("https://api.test/v1/match/result", {
      method: "POST", headers: { "content-type": "application/json", authorization: "Bearer " + alice, "cf-connecting-ip": "9.9.9.9" },
      body: JSON.stringify({ pad: "x".repeat(9000) }),
    });
    assert.equal((await w.app(big)).status, 400);
    const text = new Request("https://api.test/v1/match/result", { method: "POST", headers: { "content-type": "text/plain", authorization: "Bearer " + alice }, body: "{}" });
    assert.equal((await w.app(text)).status, 400);
  });

  test("deleting your data erases it from the server", async () => {
    await start(alice, "T:bob", 0);
    await result(alice, 21, 15, "deadbeef");
    const del = await w.app(post("/v1/me/delete", {}, alice));
    assert.equal(del.status, 200);
    assert.equal((await w.app(get("/v1/me", alice))).status, 404);
    const rows = await w.deps.db.all<{ player_a: string; report_a: string | null }>("SELECT player_a, report_a FROM matches");
    assert.deepEqual(rows.map((r) => ({ ...r })), [{ player_a: "deleted", report_a: null }]);
    const ent = await w.deps.db.first<{ n: number }>("SELECT COUNT(*) AS n FROM entitlements WHERE player_id = 'T:alice'");
    assert.equal(ent?.n, 0);
  });

  test("Elo matches the game's formula", () => {
    assert.equal(winnerDelta(1000, 1000), 16);
    assert.ok(winnerDelta(1000, 1400) > 16);
    assert.ok(winnerDelta(1400, 1000) < 16);
  });

  test("leaderboard is public and shows names and ratings only", async () => {
    await start(alice, "T:bob", 0);
    await start(bob, "T:alice", 1);
    await result(alice, 21, 15, "deadbeef");
    await result(bob, 21, 15, "deadbeef");
    const lb = (await (await w.app(get("/v1/leaderboard"))).json()) as { players: Record<string, unknown>[] };
    assert.equal(lb.players.length, 2);
    assert.deepEqual(Object.keys(lb.players[0]).sort(), ["games", "name", "rank", "rating"]);
    assert.equal(lb.players[0].rating, 1016);
  });
});

test("the appAccountToken matches the game's (C#) version", async () => {
  // Same value is asserted in the Unity tests (BackendTests.AccountToken_MatchesServer).
  assert.equal(await accountToken("T:12345"), ACCOUNT_TOKEN_T12345);
});

const ACCOUNT_TOKEN_T12345 = "d2742be6-91fe-55df-9feb-32f4c5b398c4";

describe("signed match requests (Phase 32)", () => {
  const KEY = "0123456789abcdef0123456789abcdef";

  test("tags match the game's Backend.RequestTag byte for byte", async () => {
    // Same vectors as SignedRequests_MatchTheServer in the game's Phase32Tests.cs.
    assert.equal(await requestTag(KEY, startFields(KEY, "T:opp", 0), 1791250000000), "2e1abf95571a7a23b34a4b7bd646ada437cbc907198e0c7dd807ec1ac0dee222");
    assert.equal(await requestTag(KEY, resultFields(KEY, 21, 15, "deadbeef", "final"), 1791250000000), "7cdaa7411f36ca4ae7cc371072db939704365cab0e750d0382b08e2091d421d7");
  });

  test("unsigned, tampered, stale and replayed-later requests are refused", async () => {
    const w = await world();
    const alice = await signIn(w, "T:alice");
    const bob = await signIn(w, "T:bob");
    await subscribe(w, "T:alice", alice, "100");
    await subscribe(w, "T:bob", bob, "200");
    const t = w.clock.t;
    const tag = await requestTag(KEY, startFields(KEY, "T:bob", 0), t);
    assert.equal((await w.app(post("/v1/match/start", { matchKey: KEY, opponentId: "T:bob", seat: 0 }, alice))).status, 401, "unsigned");
    assert.equal((await w.app(post("/v1/match/start", { matchKey: KEY, opponentId: "T:bob", seat: 1, t, tag }, alice))).status, 401, "a changed field breaks the tag");
    w.clock.t = t + REQUEST_WINDOW_MS + 1;
    assert.equal((await w.app(post("/v1/match/start", { matchKey: KEY, opponentId: "T:bob", seat: 0, t, tag }, alice))).status, 401, "too old");
    w.clock.t = t;
    assert.equal((await w.app(post("/v1/match/start", { matchKey: KEY, opponentId: "T:bob", seat: 0, t, tag }, alice))).status, 200, "the real one");
    const rTag = await requestTag(KEY, resultFields(KEY, 21, 15, "deadbeef", "final"), t);
    assert.equal((await w.app(post("/v1/match/result", { matchKey: KEY, scoreA: 30, scoreB: 15, hash: "deadbeef", outcome: "final", t, tag: rTag }, alice))).status, 401, "a changed score breaks the tag");
    assert.equal((await w.app(post("/v1/match/result", { matchKey: KEY, scoreA: 21, scoreB: 15, hash: "deadbeef", outcome: "final", t, tag: rTag }, alice))).status, 200);
  });
});

