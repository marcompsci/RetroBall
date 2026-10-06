// Game Center sign-in check: proves the request comes from the Game Center player it claims to be.
// The app calls GKLocalPlayer.fetchItemsForIdentityVerificationSignature and sends the results here.
// We fetch Apple's public certificate (only from https://*.apple.com), check it's in date, and verify
// the RSA-SHA256 signature over: teamPlayerID + bundleID + timestamp (8 bytes, big-endian) + salt.
import { b64ToBytes, concat, sha256Hex, type Bytes } from "./b64.ts";
import type { Db } from "./db.ts";

export interface IdentityProof {
  playerId: string; // teamPlayerID
  publicKeyUrl: string;
  signature: string; // base64
  salt: string; // base64
  timestamp: number; // ms since epoch, as returned by Game Center
}

export const MAX_SIGNATURE_AGE_MS = 10 * 60 * 1000;

/** Only Apple's certificate host over HTTPS (no user info, no odd ports). */
export function trustedKeyUrl(url: string): boolean {
  let u: URL;
  try {
    u = new URL(url);
  } catch {
    return false;
  }
  if (u.protocol !== "https:" || u.username || u.password || (u.port && u.port !== "443")) return false;
  const host = u.hostname.toLowerCase();
  return host === "apple.com" || host.endsWith(".apple.com");
}

// ---------------------------------------------------------------- minimal DER reader

interface Tlv {
  tag: number;
  start: number; // first byte of the TLV
  body: number; // first byte of the value
  end: number; // one past the last byte
}

function tlv(b: Bytes, at: number): Tlv {
  if (at + 2 > b.length) throw new Error("DER: truncated");
  const tag = b[at];
  let len = b[at + 1];
  let body = at + 2;
  if (len & 0x80) {
    const n = len & 0x7f;
    if (n < 1 || n > 4 || body + n > b.length) throw new Error("DER: bad length");
    len = 0;
    for (let i = 0; i < n; i++) len = len * 256 + b[body + i];
    body += n;
  }
  if (body + len > b.length) throw new Error("DER: truncated value");
  return { tag, start: at, body, end: body + len };
}

function children(b: Bytes, parent: Tlv): Tlv[] {
  const out: Tlv[] = [];
  let at = parent.body;
  while (at < parent.end) {
    const t = tlv(b, at);
    out.push(t);
    at = t.end;
  }
  return out;
}

function derTime(b: Bytes, t: Tlv): number {
  const s = new TextDecoder().decode(b.subarray(t.body, t.end));
  let y: number, rest: string;
  if (t.tag === 0x17) {
    const yy = parseInt(s.slice(0, 2), 10);
    y = yy >= 50 ? 1900 + yy : 2000 + yy;
    rest = s.slice(2);
  } else if (t.tag === 0x18) {
    y = parseInt(s.slice(0, 4), 10);
    rest = s.slice(4);
  } else throw new Error("DER: bad time");
  const n = (i: number) => parseInt(rest.slice(i, i + 2), 10);
  return Date.UTC(y, n(0) - 1, n(2), n(4), n(6), n(8));
}

export interface ParsedCert {
  spki: Bytes;
  notBefore: number;
  notAfter: number;
}

/** Pulls the public key (SubjectPublicKeyInfo) and validity dates out of an X.509 certificate (DER). */
export function parseCertificate(der: Bytes): ParsedCert {
  const cert = tlv(der, 0);
  const tbs = children(der, cert)[0];
  const f = children(der, tbs);
  let i = 0;
  if (f[0].tag === 0xa0) i++; // [0] version
  // serialNumber, signature, issuer, validity, subject, subjectPublicKeyInfo
  const validity = children(der, f[i + 3]);
  const spki = f[i + 5];
  return { spki: der.slice(spki.start, spki.end), notBefore: derTime(der, validity[0]), notAfter: derTime(der, validity[1]) };
}

// ---------------------------------------------------------------- verification

const certCache = new Map<string, { cert: ParsedCert; until: number }>();

async function loadCert(url: string, fetcher: typeof fetch, now: number): Promise<ParsedCert> {
  const cached = certCache.get(url);
  if (cached && cached.until > now) return cached.cert;
  const r = await fetcher(url, { redirect: "error" });
  if (!r.ok) throw new Error("certificate download failed: " + r.status);
  const buf = new Uint8Array(await r.arrayBuffer());
  if (buf.length > 16384) throw new Error("certificate too large");
  const cert = parseCertificate(buf);
  certCache.set(url, { cert, until: now + 6 * 60 * 60 * 1000 });
  return cert;
}

export function clearCertCache(): void {
  certCache.clear();
}

export type VerifyResult = { ok: true } | { ok: false; reason: string };

export async function verifyIdentity(p: IdentityProof, bundleId: string, db: Db, fetcher: typeof fetch, now: number): Promise<VerifyResult> {
  if (!trustedKeyUrl(p.publicKeyUrl)) return { ok: false, reason: "untrusted key url" };
  if (!Number.isFinite(p.timestamp) || Math.abs(now - p.timestamp) > MAX_SIGNATURE_AGE_MS) return { ok: false, reason: "stale signature" };
  let salt: Bytes, sig: Bytes;
  try {
    salt = b64ToBytes(p.salt);
    sig = b64ToBytes(p.signature);
  } catch {
    return { ok: false, reason: "bad encoding" };
  }
  if (salt.length < 4 || salt.length > 64 || sig.length < 64 || sig.length > 1024) return { ok: false, reason: "bad sizes" };

  let cert: ParsedCert;
  try {
    cert = await loadCert(p.publicKeyUrl, fetcher, now);
  } catch (e) {
    return { ok: false, reason: "certificate: " + (e as Error).message };
  }
  if (now < cert.notBefore || now > cert.notAfter) return { ok: false, reason: "certificate out of date" };

  const ts = new Uint8Array(8);
  new DataView(ts.buffer).setBigUint64(0, BigInt(Math.trunc(p.timestamp)), false);
  const payload = concat(new TextEncoder().encode(p.playerId), new TextEncoder().encode(bundleId), ts, salt);
  let valid = false;
  try {
    const key = await crypto.subtle.importKey("spki", cert.spki, { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" }, false, ["verify"]);
    valid = await crypto.subtle.verify("RSASSA-PKCS1-v1_5", key, sig, payload);
  } catch {
    return { ok: false, reason: "unsupported key" };
  }
  if (!valid) return { ok: false, reason: "bad signature" };

  // Each signature works once (replay protection inside the time window).
  const saltHash = await sha256Hex(concat(salt, sig.subarray(0, 32)));
  await db.run("DELETE FROM used_salts WHERE used_at < ?", now - 30 * 60 * 1000);
  const fresh = await db.run("INSERT OR IGNORE INTO used_salts (salt_hash, used_at) VALUES (?, ?)", saltHash, now);
  if (fresh === 0) return { ok: false, reason: "signature already used" };
  return { ok: true };
}
