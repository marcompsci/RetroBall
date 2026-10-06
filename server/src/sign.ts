// Signed match requests (Phase 32, matching Backend.RequestTag in the game). Each /v1/match/start and
// /v1/match/result body carries "t" (ms since 1970) and "tag" = HMAC-SHA256(key = the match key,
// message = "{t}|{the request's fields}"), lowercase hex. The server rejects a missing or wrong tag and a
// time more than REQUEST_WINDOW_MS away from its own clock. This is defence in depth on top of the
// session token and HTTPS: a body can't be altered or replayed later for a different game or moment.

export const REQUEST_WINDOW_MS = 5 * 60 * 1000;

export function startFields(matchKey: string, opponentId: string, seat: number): string {
  return "start|" + matchKey + "|" + opponentId + "|" + seat;
}

export function resultFields(matchKey: string, scoreA: number, scoreB: number, hash: string, outcome: string): string {
  return "result|" + matchKey + "|" + scoreA + "|" + scoreB + "|" + hash + "|" + outcome;
}

export async function requestTag(key: string, fields: string, t: number): Promise<string> {
  const k = await crypto.subtle.importKey("raw", new TextEncoder().encode(key), { name: "HMAC", hash: "SHA-256" }, false, ["sign"]);
  const mac = new Uint8Array(await crypto.subtle.sign("HMAC", k, new TextEncoder().encode(String(Math.floor(t)) + "|" + fields)));
  let hex = "";
  for (const b of mac) hex += b.toString(16).padStart(2, "0");
  return hex;
}

/** Null when the body's signature is good and fresh; otherwise why it isn't. Constant-time tag comparison. */
export async function checkSigned(body: Record<string, unknown>, key: string, fields: string, now: number): Promise<string | null> {
  const t = body.t;
  const tag = body.tag;
  if (typeof t !== "number" || !Number.isFinite(t) || typeof tag !== "string" || !/^[0-9a-f]{64}$/.test(tag)) return "unsigned request";
  if (Math.abs(now - t) > REQUEST_WINDOW_MS) return "request expired";
  const want = await requestTag(key, fields, t);
  let diff = 0;
  for (let i = 0; i < 64; i++) diff |= want.charCodeAt(i) ^ tag.charCodeAt(i);
  return diff === 0 ? null : "bad signature";
}
