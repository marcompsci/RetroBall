# RetroBall — Design

## Vision
A 90-second-to-fun, one-more-game retro arcade 3v3 half-court basketball game for iPhone.
The player is a *caller*: they call plays, call their shot, read the defense, and build a crew's legacy
from street courts to the league championship. Fully offline, no ads; the only purchase is the optional Live subscription (Phase 33).

## Core loop
Pick a matchup → play a fast 3v3 game (first to 21 or 2:00) → earn Signal Points and Fans →
train an attribute or unlock a cosmetic → next game / next season step.

## Rules (defaults, all tunable in `GameRulesData`)
- 3v3, one hoop, one human-controlled player.
- 1 point inside the arc, 2 points beyond it.
- First to 21, or a 2-minute clock. A tie at the horn goes to sudden death.
- 14-second shot clock. Check-ball reset after every score.
- Possession changes on a make, steal, defensive rebound, or shot-clock violation.

## League bible
- **The Caller League** is the pro tier; its championship is **The Gold Signal Cup**.
- **The Blacktop Circuit** is the street tier, played at Sunset Cage, Pier Nine Blacktop, and Overpass Park.
- **First Call Classic** is the rookie tournament.

| Team | Colours | Logo | Home court | Motto |
|---|---|---|---|---|
| Eastbay Voltage (EBV) | electric yellow / charcoal / deep purple | hexagon + bolt | The Volt Box | Stay charged. |
| Bay City Breakers (BCB) | teal / coral / navy | circle + waves | Breakwater Court | Crash the shore. |
| Harbor Hounds (HHD) | burnt orange / cream / dark brown | shield + paw | Kennel Yard | Never let go. |
| Redwood Runners (RWR) | forest green / gold / black | badge + pine | Grove Gym | Deep roots, quick feet. |
| Metro Comets (MCM) | sky blue / white / violet | diamond + comet | Orbit Dome | Light the lane. |
| Desert Drifters (DSD) | sand / rust / midnight blue | badge + sun & dunes | Mirage Pavilion | Heat checks welcome. |
| Northline Owls (NLO) | ice blue / black / silver | shield + owl | Nightroost Hall | See it first. |
| Solar Crowns (SOL) | magenta / orange / dark navy | circle + crown | Sunspire Court | Shine last. |

**Circuit crews:** Cage Regulars, Pier Pressure, Underpass Union.

**Player crew:** First Callers. It includes **Rook**, the default avatar, whose nickname you can change.

Each league team has a unique colourblind jersey pattern: stripes, diagonal, checker, chevrons, dots, rings, cross, or solid.

## Archetypes (12)
Floor General, Deep Shooter, Rim Runner, Lockdown Wing, Glass Cleaner, Two-Way Spark, Post Anchor,
Quick Cutter, Playmaker, Shot Creator, Hustle Guard, Stretch Forward. Each has a ratings baseline,
strengths and weaknesses, a description, and AI tendencies (shoot, drive, pass, cut, screen, crash boards,
help defense, gamble for steals, preferred range). See `Assets/Scripts/Logic/Content/Archetypes.cs`.

## Controls and feel
- One human player (slot 0) at all times. Offense: SHOOT (hold/release meter), PASS (or ASK when a teammate has it), CALL. Defense: STEAL, JUMP, SWITCH on the same three buttons.
- Presses are buffered for 0.15 s so a tap just before catching the ball still counts.
- Shot meter grades: GREEN, CLEAN LOOK, CONTESTED, TOO EARLY, TOO LATE. Green widens with Shooting and is the best outcome but never guaranteed.
- Feedback: toasts (SWISH, STEAL!, BLOCKED!, BOARD!), procedural SFX, light/medium/success haptics, a small camera shake on blocks (Settings can turn shake and haptics off).

## Defense, rebounding, plays (`DefenseTuning`)
- **Steal:** must be within 1.3 m of the handler; chance from Defense vs. the handler's Playmaking; 1 s cooldown; a miss leaves you stunned for 0.5 s.
- **Jump / block:** a jumping defender contests as if 45 % closer. Blocks need range and timing; chance grows with Defense and height and is capped at 60 %.
- **Switch:** take the ball handler; your old matchup goes to the nearest teammate.
- **Box-out:** standing between your man and the rim near a miss gives a rebound bonus; rebounders are weighted by Rebounding, distance, box-out, and a little luck.
- **Stamina:** drains with sprinting, recovers when idle, and slows a tired player slightly. Rise energy sets starting stamina (never below 60 %).
- **Plays:** Pick & Roll (screener plants, then rolls once the handler uses it), Give & Go (pass, cut, return pass), Clear Out (teammates widen to the corners). The AI calls pick-and-rolls too.

## Progression
- **Rewards:** win 120 / loss 50 SP plus stats (2/pt, 3/ast, 2/reb, 4/stl-or-blk), capped at 400 per game; playoff and title bonuses. Fans from wins, greens, and blowouts. Quick Call pays half; Practice pays nothing.
- **Upgrades:** eight attribute upgrades, cost = base × growth^level, one game of "training time" between purchases, each capped (never above 99).
- **Cosmetics:** jersey palettes, shoes, court banners (visible in games), celebrations and dribble moves (collectible tags). Some need a fan count.
- **Rise Mode:** Circuit (3 crews in order) → 8-team league, 10-game schedule, other games simulated from team strength (seeded) → top 4 → semis → final. Event cards appear after about every other league game and trade energy, chemistry, SP, and fans. Chemistry adds up to +10 % to teammates' release accuracy.

## Data model
Static definitions live in `Logic` as `*Def` classes and are wrapped by `*Data` ScriptableObjects.
Runtime state is kept separate: `MatchSimulation` / `PlayerRuntimeState` / `MatchStats` for a game, `MatchSummary` after it, and `CareerSaveData` (settings, wallet, upgrades, cosmetics, totals, practice bests, `RiseSaveData` with `SeasonSaveData`) on disk. The save is versioned (`version = 1`); unknown fields are ignored and missing ones get defaults.

## Fairness
Difficulty changes only reaction time, decision quality, shot selection threshold, release accuracy, and error rate.
The AI never gets hidden rating boosts, and `ContentValidator` enforces this.

## Future expansion (not in MVP)
- Asynchronous "call-out" challenges between players
- Game Center leaderboards, behind an interface
- More circuits and venues
- Local two-player mode
