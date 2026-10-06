-- Retro Hoops Live backend schema (Cloudflare D1 / SQLite).
CREATE TABLE IF NOT EXISTS players (
  player_id   TEXT PRIMARY KEY,          -- Game Center teamPlayerID (verified at sign-in)
  rating      INTEGER NOT NULL DEFAULT 1000,
  best        INTEGER NOT NULL DEFAULT 1000,
  wins        INTEGER NOT NULL DEFAULT 0,
  losses      INTEGER NOT NULL DEFAULT 0,
  games       INTEGER NOT NULL DEFAULT 0,
  name        TEXT NOT NULL DEFAULT '',  -- Game Center display name, for the leaderboard
  created_at  INTEGER NOT NULL,
  updated_at  INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS players_rating ON players(rating DESC);

-- One App Store subscription (originalTransactionId) belongs to one player.
CREATE TABLE IF NOT EXISTS entitlements (
  original_transaction_id TEXT PRIMARY KEY,
  player_id   TEXT NOT NULL,
  expires_at  INTEGER NOT NULL,          -- ms since epoch, from Apple
  status      INTEGER NOT NULL,          -- Apple subscription status (1 active, 2 expired, 3 billing retry, 4 grace, 5 revoked)
  checked_at  INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS entitlements_player ON entitlements(player_id);

CREATE TABLE IF NOT EXISTS matches (
  match_key   TEXT PRIMARY KEY,
  player_a    TEXT NOT NULL,             -- seat 0 (host)
  player_b    TEXT NOT NULL,             -- seat 1
  rating_a    INTEGER NOT NULL,
  rating_b    INTEGER NOT NULL,
  report_a    TEXT,                      -- JSON report from player A
  report_b    TEXT,
  state       TEXT NOT NULL,             -- open | settled | disputed | void
  winner      INTEGER,                   -- 0, 1, or NULL
  delta       INTEGER,                   -- rating change for the winner (loser gets -delta)
  created_at  INTEGER NOT NULL,
  settled_at  INTEGER
);
CREATE INDEX IF NOT EXISTS matches_open ON matches(state, created_at);
CREATE INDEX IF NOT EXISTS matches_pair ON matches(player_a, player_b, created_at);

-- Game Center signature salts already used (replay protection), kept 30 minutes.
CREATE TABLE IF NOT EXISTS used_salts (
  salt_hash   TEXT PRIMARY KEY,
  used_at     INTEGER NOT NULL
);

-- Fixed-window rate limits.
CREATE TABLE IF NOT EXISTS rate_limits (
  bucket      TEXT PRIMARY KEY,
  window      INTEGER NOT NULL,
  count       INTEGER NOT NULL
);
