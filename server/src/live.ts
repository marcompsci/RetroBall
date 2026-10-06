// Server-side Live ratings. Both players report each game; the rating changes only when the two reports
// agree (same score, same final checksum). If only one report arrives, it settles after a wait; reports
// that disagree are kept as "disputed" and change nobody's rating.
import type { Db } from "./db.ts";

export const START_RATING = 1000;
export const K = 32;
export const SETTLE_AFTER_MS = 10 * 60 * 1000;
export const MAX_RATED_PER_PAIR_PER_DAY = 5;

export function expected(mine: number, theirs: number): number {
  return 1 / (1 + Math.pow(10, (theirs - mine) / 400));
}

/** Rating points the winner gains (and the loser loses). Same formula as LiveMode.Change in the game. */
export function winnerDelta(winnerRating: number, loserRating: number): number {
  return Math.round(K * (1 - expected(winnerRating, loserRating)));
}

export interface Report {
  scoreA: number;
  scoreB: number;
  hash: string; // the game's final SimHash, as 8 hex digits
  outcome: "final" | "quit" | "opponent_left";
  at: number;
}

export interface MatchRow {
  match_key: string;
  player_a: string;
  player_b: string;
  rating_a: number;
  rating_b: number;
  report_a: string | null;
  report_b: string | null;
  state: string;
  winner: number | null;
  delta: number | null;
  created_at: number;
  settled_at: number | null;
}

export interface PlayerRow {
  player_id: string;
  rating: number;
  best: number;
  wins: number;
  losses: number;
  games: number;
  name: string;
}

export async function ensurePlayer(db: Db, playerId: string, name: string, now: number): Promise<PlayerRow> {
  await db.run(
    "INSERT INTO players (player_id, name, created_at, updated_at) VALUES (?, ?, ?, ?) ON CONFLICT(player_id) DO UPDATE SET name = excluded.name, updated_at = excluded.updated_at",
    playerId, name.slice(0, 40), now, now);
  return (await db.first<PlayerRow>("SELECT player_id, rating, best, wins, losses, games, name FROM players WHERE player_id = ?", playerId))!;
}

/** Same key on both phones: both Game Center ids (host first) and the game's seed. */
export function validMatchKey(k: unknown): k is string {
  return typeof k === "string" && /^[0-9a-f]{32}$/.test(k);
}

export type StartResult = { ok: true; match: MatchRow } | { ok: false; reason: string; status: number };

/** Registers a Live game (both phones call this at tip-off; the first call creates it). */
export async function startMatch(db: Db, key: string, me: string, opponent: string, seat: number, now: number): Promise<StartResult> {
  if (me === opponent) return { ok: false, reason: "can't play yourself", status: 400 };
  const a = seat === 0 ? me : opponent;
  const b = seat === 0 ? opponent : me;
  const existing = await db.first<MatchRow>("SELECT * FROM matches WHERE match_key = ?", key);
  if (existing) {
    if (existing.player_a !== a || existing.player_b !== b) return { ok: false, reason: "match belongs to other players", status: 403 };
    return { ok: true, match: existing };
  }
  // Rating farming guard: only a few rated games between the same two players per day.
  const since = now - 24 * 60 * 60 * 1000;
  const pair = await db.first<{ n: number }>(
    "SELECT COUNT(*) AS n FROM matches WHERE ((player_a = ? AND player_b = ?) OR (player_a = ? AND player_b = ?)) AND created_at > ?",
    a, b, b, a, since);
  if ((pair?.n ?? 0) >= MAX_RATED_PER_PAIR_PER_DAY) return { ok: false, reason: "too many rated games against this player today", status: 429 };
  const pa = await db.first<PlayerRow>("SELECT * FROM players WHERE player_id = ?", a);
  const pb = await db.first<PlayerRow>("SELECT * FROM players WHERE player_id = ?", b);
  const ra = pa?.rating ?? START_RATING;
  const rb = pb?.rating ?? START_RATING;
  await db.run("INSERT OR IGNORE INTO matches (match_key, player_a, player_b, rating_a, rating_b, state, created_at) VALUES (?, ?, ?, ?, ?, 'open', ?)",
    key, a, b, ra, rb, now);
  const row = await db.first<MatchRow>("SELECT * FROM matches WHERE match_key = ?", key);
  if (!row || row.player_a !== a || row.player_b !== b) return { ok: false, reason: "match belongs to other players", status: 403 };
  return { ok: true, match: row };
}

export function validReport(r: unknown): r is Omit<Report, "at"> {
  if (!r || typeof r !== "object") return false;
  const x = r as Record<string, unknown>;
  return Number.isInteger(x.scoreA) && Number.isInteger(x.scoreB) && (x.scoreA as number) >= 0 && (x.scoreB as number) >= 0
    && (x.scoreA as number) <= 500 && (x.scoreB as number) <= 500
    && typeof x.hash === "string" && /^[0-9a-f]{8}$/.test(x.hash)
    && (x.outcome === "final" || x.outcome === "quit" || x.outcome === "opponent_left");
}

/** Winner seat from one report, from the reporter's point of view (or null if it can't decide). */
function winnerFrom(r: Report, reporterSeat: number): number | null {
  if (r.outcome === "quit") return 1 - reporterSeat;
  if (r.outcome === "opponent_left") return reporterSeat;
  if (r.scoreA === r.scoreB) return null;
  return r.scoreA > r.scoreB ? 0 : 1;
}

export async function report(db: Db, key: string, me: string, r: Report, now: number): Promise<{ ok: boolean; status: number; reason?: string; match?: MatchRow }> {
  const m = await db.first<MatchRow>("SELECT * FROM matches WHERE match_key = ?", key);
  if (!m) return { ok: false, status: 404, reason: "no such match" };
  const seat = m.player_a === me ? 0 : m.player_b === me ? 1 : -1;
  if (seat < 0) return { ok: false, status: 403, reason: "not your match" };
  if (m.state !== "open") return { ok: true, status: 200, match: m };
  const col = seat === 0 ? "report_a" : "report_b";
  // A report can't be changed once sent.
  await db.run("UPDATE matches SET " + col + " = ? WHERE match_key = ? AND " + col + " IS NULL", JSON.stringify(r), key);
  await trySettle(db, key, now, false);
  return { ok: true, status: 200, match: (await db.first<MatchRow>("SELECT * FROM matches WHERE match_key = ?", key))! };
}

/** Settles a game: when both reports are in, or when one is in and the wait is over (force). */
export async function trySettle(db: Db, key: string, now: number, force: boolean): Promise<void> {
  const m = await db.first<MatchRow>("SELECT * FROM matches WHERE match_key = ?", key);
  if (!m || m.state !== "open") return;
  const ra = m.report_a ? (JSON.parse(m.report_a) as Report) : null;
  const rb = m.report_b ? (JSON.parse(m.report_b) as Report) : null;
  let winner: number | null = null;
  let state = "open";
  if (ra && rb) {
    const wa = winnerFrom(ra, 0);
    const wb = winnerFrom(rb, 1);
    const sameGame = ra.outcome !== "final" || rb.outcome !== "final" || (ra.scoreA === rb.scoreA && ra.scoreB === rb.scoreB && ra.hash === rb.hash);
    if (wa !== null && wa === wb && sameGame) { winner = wa; state = "settled"; }
    else state = "disputed";
  } else if (force && (ra || rb) && now - m.created_at >= SETTLE_AFTER_MS) {
    // Only one side reported. A lone "I quit" or a lone final score settles; a lone "they left" counts
    // too (the other phone vanished), but disputed claims never reach here.
    const w = ra ? winnerFrom(ra, 0) : winnerFrom(rb!, 1);
    if (w !== null) { winner = w; state = "settled"; } else state = "void";
  } else if (force && now - m.created_at >= 2 * SETTLE_AFTER_MS) {
    state = "void";
  }
  if (state === "open") return;
  if (state !== "settled" || winner === null) {
    await db.run("UPDATE matches SET state = ?, settled_at = ? WHERE match_key = ? AND state = 'open'", state, now, key);
    return;
  }
  const winnerRating = winner === 0 ? m.rating_a : m.rating_b;
  const loserRating = winner === 0 ? m.rating_b : m.rating_a;
  const delta = winnerDelta(winnerRating, loserRating);
  // Claim the settlement first so two concurrent requests can't apply it twice.
  const claimed = await db.run("UPDATE matches SET state = 'settled', winner = ?, delta = ?, settled_at = ? WHERE match_key = ? AND state = 'open'", winner, delta, now, key);
  if (claimed === 0) return;
  const winnerId = winner === 0 ? m.player_a : m.player_b;
  const loserId = winner === 0 ? m.player_b : m.player_a;
  await db.run("INSERT OR IGNORE INTO players (player_id, created_at, updated_at) VALUES (?, ?, ?)", winnerId, now, now);
  await db.run("INSERT OR IGNORE INTO players (player_id, created_at, updated_at) VALUES (?, ?, ?)", loserId, now, now);
  await db.run("UPDATE players SET rating = rating + ?, best = MAX(best, rating + ?), wins = wins + 1, games = games + 1, updated_at = ? WHERE player_id = ?", delta, delta, now, winnerId);
  await db.run("UPDATE players SET rating = MAX(100, rating - ?), losses = losses + 1, games = games + 1, updated_at = ? WHERE player_id = ?", delta, now, loserId);
}

/** Cron: settle or void games whose second report never came. */
export async function settleStale(db: Db, now: number): Promise<number> {
  const stale = await db.all<{ match_key: string }>("SELECT match_key FROM matches WHERE state = 'open' AND created_at < ? LIMIT 200", now - SETTLE_AFTER_MS);
  for (const s of stale) await trySettle(db, s.match_key, now, true);
  return stale.length;
}
