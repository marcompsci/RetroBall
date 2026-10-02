# Caller Retro Ball — Design (living document; completed in Phase 7)

## Vision
A 90-second-to-fun, one-more-game retro arcade 3v3 half-court basketball game for iPhone.
The player is a *caller*: they call plays, call their shot, read the defense, and build a crew's legacy
from street courts to the league championship. Fully offline, no ads, no purchases.

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

## Data model
Static definitions live in `Logic` as `*Def` classes and are wrapped by `*Data` ScriptableObjects.
Runtime state is kept separate: `CareerSaveData`, `SeasonSaveData`, `MatchStats`, `PlayerRuntimeState`, and `TeamRuntimeState` arrive in Phases 3–5.

## Fairness
Difficulty changes only reaction time, decision quality, shot selection threshold, release accuracy, and error rate.
The AI never gets hidden rating boosts, and `ContentValidator` enforces this.

## Future expansion (not in MVP)
- Asynchronous "call-out" challenges between players
- Game Center leaderboards, behind an interface
- More circuits and venues
- Local two-player mode
