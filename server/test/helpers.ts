// Test helpers: an in-memory SQLite database with the real schema, throwaway keys, and a fake fetch.
import { DatabaseSync } from "node:sqlite";
import { readFileSync, mkdtempSync, rmSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { tmpdir } from "node:os";
import { join } from "node:path";
import type { Db, SqlValue } from "../src/db.ts";
import { bytesToB64, type Bytes } from "../src/b64.ts";

export function memoryDb(): Db {
  const sql = new DatabaseSync(":memory:");
  sql.exec(readFileSync(new URL("../schema.sql", import.meta.url), "utf8"));
  return {
    async first<T>(q: string, ...p: SqlValue[]) {
      return (sql.prepare(q).get(...p) as T) ?? null;
    },
    async all<T>(q: string, ...p: SqlValue[]) {
      return sql.prepare(q).all(...p) as T[];
    },
    async run(q: string, ...p: SqlValue[]) {
      return Number(sql.prepare(q).run(...p).changes);
    },
  };
}

/** A throwaway RSA key and self-signed certificate standing in for Apple's Game Center key (made with openssl, never committed). */
export function makeGameCenterKey(): { certDer: Bytes; keyPem: string } {
  const dir = mkdtempSync(join(tmpdir(), "rh-gc-"));
  try {
    execFileSync("openssl", ["req", "-x509", "-newkey", "rsa:2048", "-nodes", "-keyout", join(dir, "k.pem"), "-out", join(dir, "c.pem"),
      "-days", "30", "-subj", "/CN=TEST ONLY"], { stdio: "ignore" });
    execFileSync("openssl", ["x509", "-in", join(dir, "c.pem"), "-outform", "DER", "-out", join(dir, "c.cer")], { stdio: "ignore" });
    return { certDer: new Uint8Array(readFileSync(join(dir, "c.cer"))), keyPem: readFileSync(join(dir, "k.pem"), "utf8") };
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
}

export async function signGameCenter(keyPem: string, payload: Bytes): Promise<string> {
  const body = keyPem.replace(/-----[^-]+-----/g, "").replace(/\s+/g, "");
  const der = Uint8Array.from(atob(body), (c) => c.charCodeAt(0));
  const key = await crypto.subtle.importKey("pkcs8", der, { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" }, false, ["sign"]);
  return bytesToB64(new Uint8Array(await crypto.subtle.sign("RSASSA-PKCS1-v1_5", key, payload)));
}

/** A throwaway P-256 key in .p8 (PKCS#8 PEM) form, like the In-App Purchase key from App Store Connect. */
export async function makeAppleKey(): Promise<{ pem: string; publicKey: CryptoKey }> {
  const pair = (await crypto.subtle.generateKey({ name: "ECDSA", namedCurve: "P-256" }, true, ["sign", "verify"])) as CryptoKeyPair;
  const pkcs8 = new Uint8Array(await crypto.subtle.exportKey("pkcs8", pair.privateKey));
  const b64 = bytesToB64(pkcs8).replace(/(.{64})/g, "$1\n");
  return { pem: "-----BEGIN PRIVATE KEY-----\n" + b64 + "\n-----END PRIVATE KEY-----\n", publicKey: pair.publicKey };
}

export type Route = (url: string, init?: RequestInit) => Response | Promise<Response>;

export function fakeFetch(route: Route): typeof fetch {
  return (async (input: RequestInfo | URL, init?: RequestInit) => route(String(input instanceof Request ? input.url : input), init)) as typeof fetch;
}

/** An unsigned-looking JWS (header.payload.signature) carrying a payload, like Apple's signed fields. */
export function fakeJws(payload: unknown): string {
  const enc = (o: unknown) => btoa(JSON.stringify(o)).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
  return enc({ alg: "ES256" }) + "." + enc(payload) + ".sig";
}
