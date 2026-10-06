// Retro Hoops Live API: Cloudflare Worker entry point. Routes and security notes are in app.ts.
// (A Worker module may only export handlers, so everything else lives in other files.)
import { d1 } from "./db.ts";
import type { AppleConfig } from "./appstore.ts";
import { createApp, fail } from "./app.ts";
import { settleStale } from "./live.ts";

interface Env {
  DB: D1Database;
  SESSION_SECRET: string;
  APPLE_ISSUER_ID: string;
  APPLE_KEY_ID: string;
  APPLE_PRIVATE_KEY: string;
  BUNDLE_ID: string;
  PRODUCT_ID: string;
  APPLE_ENV: string;
}

function appleConfig(env: Env): AppleConfig {
  const e = env.APPLE_ENV === "production" || env.APPLE_ENV === "both" ? env.APPLE_ENV : "sandbox";
  return { issuerId: env.APPLE_ISSUER_ID, keyId: env.APPLE_KEY_ID, privateKeyPem: env.APPLE_PRIVATE_KEY, bundleId: env.BUNDLE_ID, productId: env.PRODUCT_ID, env: e };
}

export default {
  async fetch(req: Request, env: Env): Promise<Response> {
    if (!env.SESSION_SECRET || env.SESSION_SECRET.length < 32) return fail(500, "server not configured");
    const app = createApp({ db: d1(env.DB), fetch: (i, init) => fetch(i, init), now: () => Date.now(), sessionSecret: env.SESSION_SECRET, apple: appleConfig(env) });
    try {
      return await app(req);
    } catch (e) {
      console.error("unhandled", (e as Error).message);
      return fail(500, "server error");
    }
  },
  async scheduled(_event: ScheduledEvent, env: Env): Promise<void> {
    await settleStale(d1(env.DB), Date.now());
  },
};
