// Fixed-window rate limits stored in the database (per IP and per player).
import type { Db } from "./db.ts";

export async function allow(db: Db, bucket: string, limit: number, windowMs: number, now: number): Promise<boolean> {
  const window = Math.floor(now / windowMs);
  await db.run(
    "INSERT INTO rate_limits (bucket, window, count) VALUES (?, ?, 1) " +
      "ON CONFLICT(bucket) DO UPDATE SET count = CASE WHEN rate_limits.window = excluded.window THEN rate_limits.count + 1 ELSE 1 END, window = excluded.window",
    bucket, window);
  const row = await db.first<{ count: number }>("SELECT count FROM rate_limits WHERE bucket = ?", bucket);
  return (row?.count ?? 0) <= limit;
}
