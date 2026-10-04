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

## Full Court and winners' ball
- [ ] Quick Call / Rise: after you score you check the ball again; after they score, they keep it.
- [ ] 1-on-1: possession still alternates after a basket.
- [ ] Full Court: tip-off at half court; after a basket the other team inbounds under that basket and brings it up. The camera follows the ball up and down; both hoops are drawn; shots at the bottom hoop arc into it.
- [ ] Full Court: on a steal or defensive rebound nobody jumps or teleports; the stick still moves your player the same way on screen at both ends.
- [ ] Full Court post-game box score lists all ten players (scrolls).

## Phase 20
- [ ] Locker Room ▸ KIT: the preview loops (idle, run both ways, back view, jumper). Every colour chip opens the picker; palette taps and RGB sliders preview live; CANCEL restores.
- [ ] Each style row changes the preview (sleeves, collar, stripes, chest, pattern, shorts length/stripe/waistband, shoe height/stripe/laces/sole). Locked styles list how to unlock them.
- [ ] RANDOMIZE, UNDO (several steps, across slots), RESET, presets. SAVE KITS, leave, come back: kits kept. Scroll position stays put after RANDOMIZE/UNDO.
- [ ] In a game your crew wears the chosen kit; against a team in a similar colour the Away kit goes on with a toast.
- [ ] SHARE KIT: QR + code. Scanning the QR with another iPhone's camera offers to open Retro Hoops and loads the kit into ALT. COPY CODE → ENTER CODE → PASTE → LOAD KIT works; a mistyped code says it doesn't read.
- [ ] TRADING CARD: card shows your player, kit, OVR, stats; SHARE CARD saves to Photos / AirDrop.
- [ ] Play ▸ HOLIDAY GAMES: all four courts load with decorations; the in-season one (October = Halloween) is on top and on the main menu.
- [ ] Full Court: after a basket the inbounder stands on the baseline and must pass ("INBOUND" label); 8-second and backcourt violations are called; tired AI players sub out ("SUB: … IN"); the box score lists subs; the Rise final is Full Court.

## Phase 21
- [ ] Play ▸ FRANCHISE: CHANGE CLUB cycles the eight clubs; START FRANCHISE opens HOME with week 1. PLAY GAME starts a Full Court game against the right opponent on the right court; after it, CONTINUE returns to the front office with the result counted and the rest of the week simulated.
- [ ] SIM GAME / SIM TO PLAYOFFS / SIM PLAYOFFS advance; LEAGUE shows standings, the playoff line, results, top scorers.
- [ ] ROSTER: UP/DOWN changes the depth chart (the top player is the one you control next game); CUT asks first, leaves dead money, and is blocked at 7 players in season.
- [ ] TRADE: NEXT TEAM cycles; ADD players on both sides; the verdict line updates; PROPOSE TRADE only lights up when they'd accept; after the deadline (week 10) trades are closed.
- [ ] Off-season: DRAFT shows the lottery result, SCOUT narrows ranges (visits left counts down), DRAFT picks when you're on the clock; RE-SIGN, then FREE AGENCY (SIGN, cap-room errors), FINISH FREE AGENCY, PRESEASON → START SEASON 2. HISTORY lists the season, champion, awards, transactions. Quit and relaunch mid-off-season: everything is still there.
- [ ] Play ▸ ALL-STAR CONTESTS ▸ DUNK CONTEST: pick a dunk, the combo arrows light up as you press them (on-screen pad, arrow keys/WASD or d-pad), the timer bar runs down, a wrong arrow or timeout gives one retry, the slam meter + JAM! (or Space / A), the dunker runs, jumps, spins on 360s, the rim shakes, judges' cards flip up one by one, total out of 50. Two dunks a round; the final; the winner gets +150 SP.
- [ ] 3-POINT CONTEST: SHOOT opens the Shootout with "BEAT … n" set to the score you need; CONTINUE returns to the contest; round one → final → result.
- [ ] ALL-STAR GAME: Team Sunrise (you at the top of the lineup) vs Team Moonlight, Full Court; CONTINUE returns to the All-Star screen.
- [ ] Rise: from mid-season the hub shows ALL-STAR WEEKEND with n/3 done; events done there are marked DONE; the button goes away when the regular season ends.
- [ ] Locker Room ▸ COURT: every option changes the preview; SAVE COURT; the court is in Quick Call's COURT list and Locker Room ▸ TEAM ▸ home court; PLAY HERE plays on it (also in Full Court if it's your home court); DELETE COURT removes it and a team using it goes back to a street court.
- [ ] Settings ▸ MUSIC PLAYER: every track plays (new songs take about a second the first time), the meter moves, STOP stops; MENU MUSIC and MATCH MUSIC (and SHUFFLE) are used on the menu and in games and survive a relaunch.
- [ ] Back (Escape / controller B) closes the Franchise, All-Star and Music Player panels without also leaving the screen behind them.

## Phase 22
- [ ] Play menu shows four sections (PLAY NOW, CAREERS, EVENTS, WITH FRIENDS) and every mode still opens.
- [ ] Play ▸ LEGACY ▸ START LEGACY: senior year; PLAY GAME starts Full Court with your player first in the lineup (your look); after the game CONTINUE reopens Legacy with the grade, XP and the week advanced. SIM GAME works.
- [ ] Story moments pop up between games; the next game waits until you choose.
- [ ] End of high school: stars and college offers (COMMIT, or DECLARE to skip college). College season, then DRAFT NIGHT ▸ HEAR YOUR NAME names a team and rookie deal.
- [ ] Pro: role/salary line, SKILLS (learn tier 1 then tier 2; points and ratings update), SPONSORS (offers appear with fans; three max; DROP), trainer costs cash. Contract up → agent offers → SIGN. RETIRE from 33; Hall of Fame vote. Quit and relaunch mid-season: everything kept.
- [ ] Play ▸ THE PARK: locked callers show "???"; CALL OUT plays their format (2-on-2 and 4-on-4 look right: everyone placed, plays work); sharp cuts near a defender sometimes show "ANKLES!" and the defender stops for a moment. After the game: rep change, then HOME reopens the Park.
- [ ] Play ▸ TOURNAMENT BUILDER: SIZE / FORMAT / RANDOM FIELD / ADD / MINE; START only lights up with the right count; bracket, PLAY, results; win it → "<NAME> CHAMPIONS"; NEW TOURNAMENT / ABANDON.
- [ ] Settings ▸ MUSIC PLAYER: a new song shows LOADING then plays without the screen freezing; switching songs repeatedly doesn't stutter.
- [ ] Upload: double-click `tools/Ship Retro Hoops.command` (Unity closed) → `Logs/ship_console.log` is written whatever happens.


## Phase 23
- [ ] Home screen icon is the Sunset Swish icon (ball in the net, striped sun, neon grid).
- [ ] iPad (device or simulator): launches portrait full screen; menus fit top to bottom with nothing cut off; in a game the whole half court and both stands fit above the touch controls; Full Court scrolls.
- [ ] Mac (Apple silicon, from TestFlight/App Store as "Designed for iPad"): window opens, keyboard plays (WASD, K shoot, J pass, L steal, C call, Esc pause), touch controls hide after the first key, keyboard hint shows at tip-off; a controller works.
- [ ] Play ▸ WEEKLY & HOOPS PASS: three goals with bars, days left; play a game and the bars move; finishing a goal shows "WEEKLY DONE" on the post-game screen and pays SP.
- [ ] Hoops Pass: tier and XP bar move after games and the daily; reaching tier 5 unlocks gear that shows as OWNED in the Locker Room cosmetics list (and pass gear you don't have shows "HOOPS PASS TIER n" with no BUY).
- [ ] Pause ▸ PHOTO MODE: HUD and controls disappear; drag moves the view, - / + zoom; < > cycles filters; FRAME ON/OFF; SNAP opens the share sheet with a sharp PNG (Save Image → Photos); DONE puts the camera back and shows the pause menu; RESUME carries on.

## Phase 24
- [ ] Launch: the start screen shows the sunset court, the RETRO HOOPS logo and a spinning, bobbing ball with LOADING..., then the main menu.
- [ ] In a game: ring buttons with icons in the lower-right (SHOOT, PASS, DUNK, LAYUP, CALL); on defence they read BLOCK, SWITCH, STEAL and LAYUP/CALL disappear.
- [ ] DUNK next to the rim with a good finisher: goes straight up and dunks without holding anything. From the arc: runs at the rim and finishes. A weak finisher's DUNK becomes a layup. Pulling the stick away mid-drive stops the drive.
- [ ] LAYUP: same, always a layup.
- [ ] With the ball up top, a teammate sometimes cuts to the corner and runs the baseline; PASS turns gold "OOP"; tapping it lobs to them and they dunk it (ALLEY-OOP!).
- [ ] Settings ▸ CUSTOMIZE CONTROLS: drag each button, resize with − / +, overlap warning, RESET, SAVE; the new layout is used in the next game and survives a relaunch; CANCEL discards.
- [ ] Play ▸ FULL COURT: the phone turns to landscape, baskets left and right, players face the way they run, the hoops look right from the side, the camera follows the ball, buttons and stick fit; pause menu and the post-game screen fit; HOME returns to a portrait menu. Rematch stays landscape.
- [ ] Legacy / Franchise / All-Star games (Full Court) also play in landscape.
- [ ] Keyboard U / I and controller RB / LB dunk and lay up.
