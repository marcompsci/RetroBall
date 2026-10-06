// Base64 / base64url helpers that work in Workers and Node (no Buffer).

export type Bytes = Uint8Array<ArrayBuffer>;

export function b64ToBytes(b64: string): Bytes {
  const clean = b64.replace(/-/g, "+").replace(/_/g, "/").replace(/\s/g, "");
  const padded = clean + "===".slice((clean.length + 3) % 4);
  const bin = atob(padded);
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}

export function bytesToB64(bytes: Uint8Array): string {
  let bin = "";
  for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]);
  return btoa(bin);
}

export function bytesToB64Url(bytes: Uint8Array): string {
  return bytesToB64(bytes).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

export function strToB64Url(s: string): string {
  return bytesToB64Url(new TextEncoder().encode(s));
}

export function b64UrlToStr(s: string): string {
  return new TextDecoder().decode(b64ToBytes(s));
}

export async function sha256Hex(data: Bytes | string): Promise<string> {
  const bytes = typeof data === "string" ? new TextEncoder().encode(data) : data;
  const digest = new Uint8Array(await crypto.subtle.digest("SHA-256", bytes));
  return Array.from(digest, (b) => b.toString(16).padStart(2, "0")).join("");
}

export function concat(...parts: Uint8Array[]): Bytes {
  const total = parts.reduce((n, p) => n + p.length, 0);
  const out = new Uint8Array(total);
  let o = 0;
  for (const p of parts) {
    out.set(p, o);
    o += p.length;
  }
  return out;
}
