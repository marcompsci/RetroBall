// App Store subscription checks through Apple's App Store Server API.
// The app sends only the subscription's originalTransactionId; the server asks Apple directly (signed with
// your In-App Purchase key) whether it's active, so a modified app can't fake a subscription.
import { b64ToBytes, b64UrlToStr, bytesToB64Url, sha256Hex, strToB64Url, type Bytes } from "./b64.ts";

export interface AppleConfig {
  issuerId: string;
  keyId: string;
  privateKeyPem: string; // contents of SubscriptionKey_XXXX.p8
  bundleId: string;
  productId: string;
  env: "sandbox" | "production" | "both";
}

export interface SubscriptionState {
  active: boolean;
  status: number; // Apple: 1 active, 2 expired, 3 billing retry, 4 grace period, 5 revoked
  expiresAt: number; // ms
  originalTransactionId: string;
  appAccountToken: string | null;
  environment: string;
}

const HOSTS = {
  production: "https://api.storekit.itunes.apple.com",
  sandbox: "https://api.storekit-sandbox.itunes.apple.com",
};

export function validTransactionId(id: unknown): id is string {
  return typeof id === "string" && /^[0-9]{1,24}$/.test(id);
}

function pemToPkcs8(pem: string): Bytes {
  const body = pem.replace(/-----BEGIN [^-]+-----/, "").replace(/-----END [^-]+-----/, "").replace(/\s+/g, "");
  return b64ToBytes(body);
}

/** The ES256 token Apple's API wants (valid 20 minutes). */
export async function appleApiToken(cfg: AppleConfig, nowSec: number): Promise<string> {
  const key = await crypto.subtle.importKey("pkcs8", pemToPkcs8(cfg.privateKeyPem), { name: "ECDSA", namedCurve: "P-256" }, false, ["sign"]);
  const header = strToB64Url(JSON.stringify({ alg: "ES256", kid: cfg.keyId, typ: "JWT" }));
  const body = strToB64Url(JSON.stringify({ iss: cfg.issuerId, iat: nowSec, exp: nowSec + 20 * 60, aud: "appstoreconnect-v1", bid: cfg.bundleId }));
  const sig = new Uint8Array(await crypto.subtle.sign({ name: "ECDSA", hash: "SHA-256" }, key, new TextEncoder().encode(header + "." + body)));
  return header + "." + body + "." + bytesToB64Url(sig);
}

/** The payload of a JWS from Apple. Only used on data we fetched from Apple ourselves over HTTPS. */
export function jwsPayload<T>(jws: string): T {
  const parts = jws.split(".");
  if (parts.length !== 3) throw new Error("bad JWS");
  return JSON.parse(b64UrlToStr(parts[1])) as T;
}

/**
 * The appAccountToken the app attaches to a purchase for this Game Center player: a UUID made from a hash
 * of the player id, so a transaction can be tied to the player who bought it.
 */
export async function accountToken(playerId: string): Promise<string> {
  const h = await sha256Hex("retrohoops-account:" + playerId);
  const v = h.slice(0, 12) + "5" + h.slice(13, 16) + ((parseInt(h[16], 16) & 0x3) | 0x8).toString(16) + h.slice(17, 32);
  return [v.slice(0, 8), v.slice(8, 12), v.slice(12, 16), v.slice(16, 20), v.slice(20, 32)].join("-");
}

interface StatusResponse {
  bundleId?: string;
  environment?: string;
  data?: { lastTransactions?: { originalTransactionId?: string; status?: number; signedTransactionInfo?: string }[] }[];
}

interface TransactionPayload {
  originalTransactionId?: string;
  productId?: string;
  bundleId?: string;
  expiresDate?: number;
  revocationDate?: number;
  appAccountToken?: string;
  environment?: string;
}

/** Asks Apple for the subscription's status. Null if Apple doesn't know the transaction (in any allowed environment). */
export async function fetchSubscription(cfg: AppleConfig, originalTransactionId: string, fetcher: typeof fetch, nowMs: number): Promise<SubscriptionState | null> {
  if (!validTransactionId(originalTransactionId)) return null;
  const envs: ("production" | "sandbox")[] = cfg.env === "both" ? ["production", "sandbox"] : [cfg.env];
  const token = await appleApiToken(cfg, Math.floor(nowMs / 1000));
  for (const env of envs) {
    const r = await fetcher(HOSTS[env] + "/inApps/v1/subscriptions/" + originalTransactionId, {
      headers: { Authorization: "Bearer " + token },
      redirect: "error",
    });
    if (r.status === 404) continue;
    if (!r.ok) throw new Error("App Store Server API " + r.status);
    const body = (await r.json()) as StatusResponse;
    if (body.bundleId && body.bundleId !== cfg.bundleId) return null;
    for (const group of body.data ?? []) {
      for (const last of group.lastTransactions ?? []) {
        if (!last.signedTransactionInfo) continue;
        const t = jwsPayload<TransactionPayload>(last.signedTransactionInfo);
        if (t.productId !== cfg.productId || (t.bundleId && t.bundleId !== cfg.bundleId)) continue;
        const status = last.status ?? 0;
        const expiresAt = t.expiresDate ?? 0;
        const active = (status === 1 || status === 4) && !t.revocationDate && expiresAt > nowMs;
        return {
          active,
          status,
          expiresAt,
          originalTransactionId: t.originalTransactionId ?? originalTransactionId,
          appAccountToken: t.appAccountToken ?? null,
          environment: t.environment ?? body.environment ?? env,
        };
      }
    }
    return null;
  }
  return null;
}

/** Pulls the originalTransactionId out of an App Store Server Notification (V2). We never trust it further: we re-ask Apple. */
export function notificationTransactionId(signedPayload: string): string | null {
  try {
    const n = jwsPayload<{ data?: { signedTransactionInfo?: string } }>(signedPayload);
    const info = n.data?.signedTransactionInfo;
    if (!info) return null;
    const id = jwsPayload<TransactionPayload>(info).originalTransactionId;
    return validTransactionId(id) ? id : null;
  } catch {
    return null;
  }
}
