# Manual QA checklist

Run on a real iPhone (and once in the Editor). Tick each item; note device, iOS version, and build number at the top of your copy. **None of these have been run yet.**

Device: ______  iOS: ______  Build: ______  Date: ______

## First launch
- [ ] Project opens in Unity 6 LTS with no compile errors; Project Setup runs and logs a clean content report.
- [ ] EditMode and PlayMode tests pass in the Test Runner.
- [ ] App boots to the main menu in under 3 seconds; no flash of unstyled text.
- [ ] Portrait only; layout respects the notch / Dynamic Island and home indicator.
- [ ] Music starts quietly; button clicks make a sound.

## Quick Call
- [ ] Team cards highlight the selected team; CHANGE OPPONENT never offers your own team.
- [ ] Difficulty choice is remembered next time.
- [ ] Match starts 0–0 with your ball at the check spot.

## Offense
- [ ] Joystick appears where the thumb lands and moves the player in 8 directions.
- [ ] Hold SHOOT: meter fills; release in green shows GREEN; early/late show TOO EARLY / TOO LATE.
- [ ] Shots inside the arc score 1, beyond the arc 2.
- [ ] PASS aims with the stick; receiver arrow shows the target. With a teammate holding the ball the button reads ASK and they pass back.
- [ ] CALL opens the play menu only on offense with your team holding the ball; each play shows its name and teammates move (screen set, cut, clear-out).
- [ ] After a steal or defensive rebound, SHOOT reads CLEAR until you dribble beyond the arc.
- [ ] Shot clock violation flips possession with a whistle.

## Defense
- [ ] Buttons read STEAL / JUMP / SWITCH on defense and return to SHOOT / PASS / DEF on offense.
- [ ] STEAL near the handler sometimes strips; spamming is limited by the cooldown; a miss briefly freezes you.
- [ ] JUMP lifts the sprite; jumping on a close shot can BLOCK (camera shakes if Screen Shake is on).
- [ ] SWITCH moves you onto the ball handler.
- [ ] BOX OUT appears when you're sealing your man during a rebound.

## End of game
- [ ] Buzzer; post-game shows score, player of the game, both box scores (PTS AST REB STL BLK FG%), and rewards.
- [ ] REMATCH starts a fresh game; HOME returns to the menu.
- [ ] Rewards appear in the wallet once. Backgrounding the app on the post-game screen and returning does not grant them again.
- [ ] A tie at the horn goes to sudden death.

## Rise Mode
- [ ] Hub shows stage, objective, energy, chemistry, next opponent and court.
- [ ] Beating all three circuit crews shows CIRCUIT CLEARED and starts the league.
- [ ] Standings update after each game (W-L, DIFF, streak); your row is gold.
- [ ] An event card appears before some games; each choice changes the numbers it describes.
- [ ] Top 4 makes the playoffs; bracket shows semis then final; winning the final shows CHAMPIONS and adds a championship.
- [ ] START NEXT SEASON works after the season ends.
- [ ] Quitting mid-game and returning does not advance the season.

## Practice Lab
- [ ] Free Shoot: 60 s timer, makes/streak shown; teammates hand the ball back.
- [ ] Passing Targets: a gold arrow marks the target; hitting it moves the target and scores.
- [ ] Dribble Lane: next cone is bright, done cones turn green; time stops at the last cone.
- [ ] End card shows NEW PERSONAL BEST when beaten; Locker Room ▸ Career shows the best.

## Locker Room
- [ ] Nickname edits save (max 14 characters, trimmed) and show on the main menu.
- [ ] Training: cost, level, and the reason a purchase is blocked; buying raises the rating; next purchase needs a game first.
- [ ] Style: buy, equip, and owned states; fan-locked items say how many fans they need; equipped jersey/shoes/banner show up in the next game.
- [ ] Career totals match games played.

## Settings
- [ ] Music and SFX sliders change volume immediately and persist.
- [ ] Haptics off: no vibration anywhere. Screen Shake off: no shake on blocks.
- [ ] UI Scale rebuilds the screen larger/smaller; all text still fits.
- [ ] Team Patterns gives each side a distinct pattern in games.
- [ ] RESET SAVE asks for confirmation; CANCEL keeps everything; RESET returns to a fresh career.
- [ ] Credits and licences text is readable.

## Tutorial, Daily, 2 Player, Game Center
- [ ] Fresh install: the main menu offers the tutorial. Each of the 8 steps advances only when done. The end card grants +100 SP once.
- [ ] Daily Challenge shows today's goal. Completing it pays the bonus once and builds the streak on consecutive days. The streak resets after a missed day.
- [ ] 2 Player: both people move independently (keyboard, and two controllers). P2 has a cyan ring and can shoot, pass, steal, and jump. No rewards are given.
- [ ] Game Center (device): turning on Sign in shows Apple's sign-in. After a win, the leaderboard updates. OPEN GAME CENTER shows the dashboard.

## Robustness
- [ ] Incoming call / home swipe pauses the match; RESUME continues.
- [ ] Airplane mode: everything works (no network use).
- [ ] Rapid double taps never load a scene twice.
- [ ] Corrupt `career.json` (edit it on a dev build) shows the SAVE RESET dialog and keeps a backup.
- [ ] 10 consecutive games: steady 60 fps, no memory growth in Xcode's memory gauge.
- [ ] Device heat and battery drain are reasonable over 15 minutes.

## Accessibility
- [ ] Text readable at default UI scale on the smallest supported iPhone.
- [ ] Teams distinguishable in grayscale with Team Patterns on.
- [ ] Every action has visible and audible feedback, not colour alone.
- [ ] COLOR FILTER: RED-GREEN turns the shot meter blue / yellow / violet; BLUE-YELLOW turns it green / amber / red. A red home team against a green away team switches the away team to its other kit with RED-GREEN on.
- [ ] CAPTIONS: announcer calls ("HEATING UP!", "ALLEY-OOP!") appear as a caption bar, even with sound effects muted. Turning on iOS Settings ▸ Accessibility ▸ Subtitles & Captioning ▸ Closed Captions turns them on too.
- [ ] VoiceOver (triple-click the side button if set as the Accessibility Shortcut): swiping right reads menu text and buttons top to bottom; double-tap presses the focused button; a dialog hides the menu behind it; long Settings lists scroll to the focused item. In a match, the stick and buttons still work.

## Phase 19
- [ ] Tabletop 2 Player (no controllers): P1's controls on the bottom half, P2's upside down on the top half. P2 pushing "up" (towards the bottom of the screen) moves their player down the screen. P2's P&R button calls pick and roll.
- [ ] Shootout vs Friend: P1 shoots 60 s, "PASS THE PHONE TO P2", P2 sees "BEAT P1'S n", the result names the winner (or TIE GAME). Nothing is added to your practice bests.
- [ ] iCloud (two devices on the same Apple Account): play a game on device A, open the app on device B (fresh install) → "Your career was loaded from iCloud". With progress on both, the further-along one is offered with LOAD FROM ICLOUD / KEEP THIS ONE. Settings ▸ RESET SAVE removes the iCloud copy too.
- [ ] Season 4: Rise Season 4 brings the Paper Cranes (Juno Vale) with the chapter 4 scenes; the crane logo draws in Locker Room ▸ TEAM ▸ LOGO ICON.
- [ ] TestFlight: `tools/ship_testflight.sh` uploads a build that appears in App Store Connect ▸ TestFlight and installs from the TestFlight app.
