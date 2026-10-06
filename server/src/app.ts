// Retro Hoops Live API: request handling (the Worker entry point is index.ts).
//
//   POST /v1/session               Game Center sign-in → session token
//   POST /v1/subscription          check the Retro Hoops Live subscription with Apple (and link it to the player)
//   GET  /v1/me                    your rating, record and subscription state
//   POST /v1/me/delete             erase your Live data from the server
//   POST /v1/match/start           register a Live game at tip-off (both players)
//   POST /v1/match/result          report a finished Live game (both players)
//   GET  /v1/leaderboard           top 50 ratings
//   POST /v1/apple/notifications   App Store Server Notifications V2 (renewals, refunds, expiries)
//   GET  /v1/health
//
// Security: every write needs a session from a verified Game Center signature; subscriptions are checked with
// Apple's server API, never trusted from the app; ratings change only on the server; bodies are size-limited and
// validated; match requests are signed and time-limited (sign.ts); per-IP and per-player rate limits; parameterised SQL only; no CORS (native app only); no secrets in code.
import type { Db } from "./db.ts";
import { verifyIdentity, type IdentityProof } from "./gamecenter.ts";
import { accountToken, fetchSubscription, notificationTransactionId, validTransactionId, type AppleConfig } from "./appstore.ts";
import { issueSession, readSession, SESSION_SECONDS } from "./session.ts";
import { ensurePlayer, report, startMatch, validMatchKey, validReport, type PlayerRow } from "./live.ts";
import { allow } from "./ratelimit.ts";
import { checkSigned, resultFields, startFields } from "./sign.ts";

export interface Deps {
  db: Db;
  fetch: typeof fetch;
  now: () => number;
  sessionSecret: string;
  apple: AppleConfig;
}

export const MAX_BODY = 8 * 1024;
const RECHECK_MS = 6 * 60 * 60 * 1000;

const SECURITY_HEADERS: Record<string, string> = {
  "content-type": "application/json; charset=utf-8",
  "cache-control": "no-store",
  "x-content-type-options": "nosniff",
  "referrer-policy": "no-referrer",
  "x-frame-options": "DENY",
  "content-security-policy": "default-src 'none'; frame-ancestors 'none'",
};

function json(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: SECURITY_HEADERS });
}

export const fail = (status: number, error: string) => json(status, { error });

async function readJson(req: Request, max = MAX_BODY): Promise<Record<string, unknown> | null> {
  const type = req.headers.get("content-type") ?? "";
  if (!type.toLowerCase().startsWith("application/json")) return null;
  const declared = Number(req.headers.get("content-length") ?? "0");
  if (declared > max) return null;
  const buf = await req.arrayBuffer();
  if (buf.byteLength > max) return null;
  try {
    const v = JSON.parse(new TextDecoder().decode(buf));
    return v && typeof v === "object" && !Array.isArray(v) ? (v as Record<string, unknown>) : null;
  } catch {
    return null;
  }
}

function str(v: unknown, max: number): string | null {
  return typeof v === "string" && v.length > 0 && v.length <= max ? v : null;
}

function clientIp(req: Request): string {
  return req.headers.get("cf-connecting-ip") ?? "unknown";
}

function playerView(p: PlayerRow) {
  return { rating: p.rating, best: p.best, wins: p.wins, losses: p.losses, games: p.games };
}

async function entitlement(deps: Deps, playerId: string): Promise<{ active: boolean; expiresAt: number }> {
  const now = deps.now();
  const row = await deps.db.first<{ original_transaction_id: string; expires_at: number; status: number; checked_at: number }>(
    "SELECT original_transaction_id, expires_at, status, checked_at FROM entitlements WHERE player_id = ? ORDER BY expires_at DESC LIMIT 1", playerId);
  if (!row) return { active: false, expiresAt: 0 };
  let expiresAt = row.expires_at;
  let active = (row.status === 1 || row.status === 4) && expiresAt > now;
  // Re-ask Apple now and then (renewals, refunds), or when the stored period has run out.
  if (now - row.checked_at > RECHECK_MS || (!active && now - row.checked_at > 60 * 1000)) {
    try {
      const s = await fetchSubscription(deps.apple, row.original_transaction_id, deps.fetch, now);
      if (s) {
        await deps.db.run("UPDATE entitlements SET expires_at = ?, status = ?, checked_at = ? WHERE original_transaction_id = ?",
          s.expiresAt, s.status, now, row.original_transaction_id);
        expiresAt = s.expiresAt;
        active = s.active;
      }
    } catch {
      // Apple unreachable: keep the stored answer.
    }
  }
  return { active, expiresAt };
}

export function createApp(deps: Deps) {
  return async function handle(req: Request): Promise<Response> {
    const url = new URL(req.url);
    const path = url.pathname;
    const now = deps.now();
    const ip = clientIp(req);

    if (!(await allow(deps.db, "ip:" + ip, 120, 60_000, now))) return fail(429, "slow down");

    if (req.method === "GET" && path === "/v1/health") return json(200, { ok: true });

    if (req.method === "GET" && path === "/v1/leaderboard") {
      const rows = await deps.db.all<{ name: string; rating: number; games: number }>(
        "SELECT name, rating, games FROM players WHERE games > 0 ORDER BY rating DESC LIMIT 50");
      return json(200, { players: rows.map((r, i) => ({ rank: i + 1, name: r.name || "Player", rating: r.rating, games: r.games })) });
    }

    if (req.method === "POST" && path === "/v1/apple/notifications") {
      const body = await readJson(req, 64 * 1024); // Apple's signed payloads are larger than app requests
      const signed = body ? str(body.signedPayload, 64 * 1024) : null;
      const id = signed ? notificationTransactionId(signed) : null;
      if (id) {
        try {
          const s = await fetchSubscription(deps.apple, id, deps.fetch, now);
          if (s) await deps.db.run("UPDATE entitlements SET expires_at = ?, status = ?, checked_at = ? WHERE original_transaction_id = ?", s.expiresAt, s.status, now, id);
        } catch {
          return fail(503, "try again");
        }
      }
      return json(200, { ok: true });
    }

    if (req.method === "POST" && path === "/v1/session") {
      if (!(await allow(deps.db, "session:" + ip, 10, 60_000, now))) return fail(429, "slow down");
      const body = await readJson(req);
      if (!body) return fail(400, "bad request");
      const playerId = str(body.playerId, 128);
      const proof: IdentityProof | null = playerId && typeof body.timestamp === "number"
        ? { playerId, publicKeyUrl: str(body.publicKeyUrl, 512) ?? "", signature: str(body.signature, 2048) ?? "", salt: str(body.salt, 256) ?? "", timestamp: body.timestamp }
        : null;
      if (!proof) return fail(400, "bad request");
      const v = await verifyIdentity(proof, deps.apple.bundleId, deps.db, deps.fetch, now);
      if (!v.ok) return fail(401, "sign-in failed");
      const name = (str(body.name, 64) ?? "").replace(/[\u0000-\u001f\u007f]/g, "");
      const player = await ensurePlayer(deps.db, proof.playerId, name, now);
      const token = await issueSession(deps.sessionSecret, proof.playerId, Math.floor(now / 1000));
      return json(200, { token, expiresIn: SESSION_SECONDS, accountToken: await accountToken(proof.playerId), player: playerView(player) });
    }

    // ---- everything below needs a session
    const auth = req.headers.get("authorization") ?? "";
    const session = auth.startsWith("Bearer ") ? await readSession(deps.sessionSecret, auth.slice(7), Math.floor(now / 1000)) : null;
    if (!session) return fail(401, "sign in again");
    const me = session.sub;
    if (!(await allow(deps.db, "player:" + me, 60, 60_000, now))) return fail(429, "slow down");

    if (req.method === "GET" && path === "/v1/me") {
      const p = await deps.db.first<PlayerRow>("SELECT * FROM players WHERE player_id = ?", me);
      if (!p) return fail(404, "unknown player");
      const e = await entitlement(deps, me);
      return json(200, { player: playerView(p), subscription: e });
    }

    if (req.method === "POST" && path === "/v1/me/delete") {
      // Erase everything stored about this player: rating and record, the subscription link, and their
      // name/id in past games (replaced with "deleted"). The App Store subscription itself is untouched.
      await deps.db.run("DELETE FROM players WHERE player_id = ?", me);
      await deps.db.run("DELETE FROM entitlements WHERE player_id = ?", me);
      await deps.db.run("UPDATE matches SET player_a = 'deleted', report_a = NULL WHERE player_a = ?", me);
      await deps.db.run("UPDATE matches SET player_b = 'deleted', report_b = NULL WHERE player_b = ?", me);
      return json(200, { deleted: true });
    }

    if (req.method === "POST" && path === "/v1/subscription") {
      const body = await readJson(req);
      const id = body ? body.originalTransactionId : null;
      if (!validTransactionId(id)) return fail(400, "bad request");
      let s;
      try {
        s = await fetchSubscription(deps.apple, id, deps.fetch, now);
      } catch {
        return fail(503, "the App Store didn't answer, try again");
      }
      if (!s) return json(200, { active: false, expiresAt: 0 });
      // The purchase must belong to this player: its appAccountToken (when the app set one) must match,
      // and a subscription already linked to someone else can't be reused.
      if (s.appAccountToken && s.appAccountToken.toLowerCase() !== (await accountToken(me))) return fail(403, "this subscription belongs to another player");
      const owner = await deps.db.first<{ player_id: string }>("SELECT player_id FROM entitlements WHERE original_transaction_id = ?", s.originalTransactionId);
      if (owner && owner.player_id !== me) return fail(403, "this subscription belongs to another player");
      await deps.db.run(
        "INSERT INTO entitlements (original_transaction_id, player_id, expires_at, status, checked_at) VALUES (?, ?, ?, ?, ?) " +
          "ON CONFLICT(original_transaction_id) DO UPDATE SET expires_at = excluded.expires_at, status = excluded.status, checked_at = excluded.checked_at",
        s.originalTransactionId, me, s.expiresAt, s.status, now);
      return json(200, { active: s.active, expiresAt: s.expiresAt });
    }

    if (req.method === "POST" && path === "/v1/match/start") {
      const body = await readJson(req);
      if (!body) return fail(400, "bad request");
      const key = body.matchKey;
      const opponent = str(body.opponentId, 128);
      const seat = body.seat;
      if (!validMatchKey(key) || !opponent || (seat !== 0 && seat !== 1)) return fail(400, "bad request");
      const startSig = await checkSigned(body, key, startFields(key, opponent, seat), now);
      if (startSig) return fail(401, startSig);
      if (!(await entitlement(deps, me)).active) return fail(402, "Retro Hoops Live subscription needed");
      if (!(await entitlement(deps, opponent)).active) return fail(409, "opponent isn't verified: this game won't be rated");
      const r = await startMatch(deps.db, key, me, opponent, seat, now);
      if (!r.ok) return fail(r.status, r.reason);
      return json(200, { state: r.match.state, ratingA: r.match.rating_a, ratingB: r.match.rating_b });
    }

    if (req.method === "POST" && path === "/v1/match/result") {
      const body = await readJson(req);
      const key = body ? body.matchKey : null;
      if (!body || !validMatchKey(key) || !validReport(body)) return fail(400, "bad request");
      const resultSig = await checkSigned(body, key, resultFields(key, body.scoreA as number, body.scoreB as number, body.hash as string, body.outcome as string), now);
      if (resultSig) return fail(401, resultSig);
      const r = await report(deps.db, key, me, {
        scoreA: body.scoreA as number, scoreB: body.scoreB as number, hash: body.hash as string,
        outcome: body.outcome as "final" | "quit" | "opponent_left", at: now,
      }, now);
      if (!r.ok || !r.match) return fail(r.status, r.reason ?? "error");
      const p = await deps.db.first<PlayerRow>("SELECT * FROM players WHERE player_id = ?", me);
      const seat = r.match.player_a === me ? 0 : 1;
      const change = r.match.state === "settled" && r.match.delta !== null ? (r.match.winner === seat ? r.match.delta : -r.match.delta) : 0;
      return json(200, { state: r.match.state, change, player: p ? playerView(p) : null });
    }

    return fail(404, "not found");
  };
}

