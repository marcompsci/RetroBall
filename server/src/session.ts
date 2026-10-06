// Short-lived session tokens (HS256 JWT) issued after a verified Game Center sign-in.
import { b64ToBytes, b64UrlToStr, bytesToB64Url, strToB64Url } from "./b64.ts";

export const SESSION_SECONDS = 60 * 60; // 1 hour; the app signs in again when it expires

export interface Session {
  sub: string; // Game Center teamPlayerID
  iat: number;
  exp: number;
}

async function hmacKey(secret: string): Promise<CryptoKey> {
  if (!secret || secret.length < 32) throw new Error("SESSION_SECRET must be at least 32 characters");
  return crypto.subtle.importKey("raw", new TextEncoder().encode(secret), { name: "HMAC", hash: "SHA-256" }, false, ["sign", "verify"]);
}

export async function issueSession(secret: string, playerId: string, nowSec: number): Promise<string> {
  const header = strToB64Url(JSON.stringify({ alg: "HS256", typ: "JWT" }));
  const body = strToB64Url(JSON.stringify({ sub: playerId, iat: nowSec, exp: nowSec + SESSION_SECONDS }));
  const sig = new Uint8Array(await crypto.subtle.sign("HMAC", await hmacKey(secret), new TextEncoder().encode(header + "." + body)));
  return header + "." + body + "." + bytesToB64Url(sig);
}

/** The session in a valid, unexpired token, or null. The signature check is constant-time (WebCrypto verify). */
export async function readSession(secret: string, token: string, nowSec: number): Promise<Session | null> {
  if (!token || token.length > 2048) return null;
  const parts = token.split(".");
  if (parts.length !== 3) return null;
  let ok = false;
  try {
    ok = await crypto.subtle.verify("HMAC", await hmacKey(secret), b64ToBytes(parts[2]), new TextEncoder().encode(parts[0] + "." + parts[1]));
  } catch {
    return null;
  }
  if (!ok) return null;
  try {
    const header = JSON.parse(b64UrlToStr(parts[0]));
    if (header.alg !== "HS256") return null;
    const s = JSON.parse(b64UrlToStr(parts[1])) as Session;
    if (typeof s.sub !== "string" || typeof s.exp !== "number" || typeof s.iat !== "number") return null;
    if (s.exp <= nowSec || s.iat > nowSec + 60) return null;
    return s;
  } catch {
    return null;
  }
}
