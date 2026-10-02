# Game Center setup

Game Center in RetroBall is **optional and off by default**. The player turns it on in **Settings ▸ Game Center ▸ Sign in**. Without it, the game works exactly the same offline.

## What's in the project

- **Native bridge:** `Assets/Plugins/iOS/CallerGameCenter.mm` (GameKit). It handles sign-in, leaderboard scores, achievements, and the Game Center dashboard.
- **C# service:** `Core/GameCenterService.cs`. `GameKitGameCenterService` is used on iOS builds and `NullGameCenterService` everywhere else (Editor, other platforms).
- **What gets reported:** `Logic/Progression/Daily.cs` ▸ `Achievements` decides which achievements are earned and which scores are reported. It's pure logic and unit-tested.
- **When it reports:** after every rewarded game and after the tutorial. Nothing is sent unless the player opted in and is signed in.
- **Xcode setup:** the iOS build post-processor links `GameKit.framework` and adds the **Game Center** capability automatically.

## You need to create these in App Store Connect

In App Store Connect, open **App ▸ Features ▸ Game Center**. The IDs must match exactly.

### Leaderboards (Classic, "High score to low")

| Leaderboard ID | Name | Score format |
|---|---|---|
| `retroball.lb.career_wins` | Career Wins | Integer |
| `retroball.lb.career_greens` | Green Releases | Integer |
| `retroball.lb.daily_best_streak` | Best Daily Streak | Integer |

### Achievements

Achievements report 100% when earned.

| Achievement ID | Title | How it's earned |
|---|---|---|
| `retroball.ach.first_win` | First W | Win any game |
| `retroball.ach.first_green` | Called It | Hit your first GREEN release |
| `retroball.ach.ten_wins` | Double Digits | Win 10 games |
| `retroball.ach.hundred_greens` | Green Machine | 100 GREEN releases |
| `retroball.ach.circuit_cleared` | Off the Blacktop | Clear The Blacktop Circuit |
| `retroball.ach.cup_champion` | Gold Signal | Win The Gold Signal Cup |
| `retroball.ach.classic_champion` | First Call | Win the First Call Classic |
| `retroball.ach.daily_streak_7` | Every Day | 7-day Daily Challenge streak |
| `retroball.ach.tutorial_done` | Ready to Call | Finish How to Play |

Each achievement needs a title, a description, point value(s) totalling 1,000 or less, and a 512×512 or 1024×1024 image.

## Testing

- **Device or simulator:** Game Center sign-in works on an iPhone or the iOS Simulator with a sandbox Apple Account. In the iPhone's Settings app, go to **Game Center** and sign in.
- **Before submission:** leaderboards and achievements show up only after they're created in App Store Connect and the build uses the same bundle ID.
- **Status:** none of this has been tested on a device yet.

## Privacy

The game sends only scores and achievement progress, and only through Apple's Game Center. App Privacy answers stay **Data Not Collected** for the developer. Confirm this against Apple's current guidance when you submit.
