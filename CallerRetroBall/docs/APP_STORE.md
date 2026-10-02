# App Store submission kit

Draft metadata and answers for App Store Connect. Character counts are checked against Apple's limits. Fill in the items marked **YOU** yourself, because only the account holder can provide them.

## App record

| Field | Value |
|---|---|
| Name (30) | RetroBall |
| Subtitle (30) | Retro 3v3 street basketball *(27)* |
| Bundle ID | **YOU:** replace the placeholder `com.retroball.game` in Player Settings ▸ iOS and register the same ID in your Apple Developer account |
| SKU | `retroball-ios-1` (any unique string) |
| Primary category | Games ▸ Sports |
| Secondary category | Games ▸ Arcade |
| Price | Free, or a one-time paid price. The game has no in-app purchases. |
| Version | 1.0.0 (build 1). Raise the build number for every upload. |

## Promotional text (170)

> Call your shot. Run pick-and-rolls, lock down the lane, and take the First Callers from street courts to The Gold Signal Cup. No ads. No purchases. Fully offline.

*(162 characters)*

## Description (4000)

> **Call your shot. Build your legacy.**
>
> RetroBall is a pixel-art 3v3 half-court basketball game made for one thumb on the stick and one on the buttons. Hold to shoot and release on the green. Thread passes to an open teammate, or call for the ball. When the game slows down, call a play: Pick & Roll, Give & Go, or Clear Out.
>
> On defense, jump to contest, reach for steals, switch onto the ball handler, and box out for the rebound. The AI plays the same rules you do. Rookie, Caller, and Legend change how quickly and how smartly it decides, never how good its players are.
>
> **RISE MODE**
> Start on The Blacktop Circuit and beat five street crews on their home courts. Earn a spot in The Caller League, play a 10-game season with event cards between games, manage your crew's energy and chemistry, and fight through the playoffs for The Gold Signal Cup.
>
> **BUILD YOUR PLAYER**
> Earn Signal Points and Fans every game. Train your player's attributes, unlock jersey palettes, shoes, and court banners, and track your career stats.
>
> **FIRST CALL CLASSIC**
> A four-team knockout for your crew. Two wins and the trophy is yours.
>
> **DAILY CHALLENGE & 2 PLAYER**
> A new challenge every day with streak bonuses, and head-to-head games on one device with a keyboard or two controllers.
>
> **PRACTICE LAB**
> Free Shoot, Passing Targets, and a timed Dribble Lane, each with a personal best to chase.
>
> **MADE TO RESPECT YOUR TIME**
> • Games last about two minutes
> • Fully offline. No account, no ads, no in-app purchases
> • No tracking or data collection (Game Center is optional)
> • Haptics, screen shake, UI scale, and colourblind team patterns are all adjustable
>
> All teams, players, courts, and music are original.

## Keywords (100)

```
basketball,arcade,retro,pixel,3v3,hoops,streetball,offline,sports,court,season,shot,8-bit,16-bit
```

*(96 characters.)* Don't add the names of real leagues, teams, players, or other games. Apple rejects keywords that use trademarks you don't own.

## URLs

| Field | Value |
|---|---|
| Support URL | **YOU:** required. A simple page with a contact email is enough (for example a GitHub Pages page or the repo's README). |
| Marketing URL | Optional |
| Privacy Policy URL | **YOU:** required for every app. You can host the text below. |

### Privacy policy text (ready to host)

> RetroBall does not collect, store, or share any personal information. The game works fully offline. It has no accounts, advertising, analytics, or tracking. Game progress (your nickname, settings, and career) is saved only on your device and is deleted when you delete the app. Contact: **YOU: your email**.

## App Privacy ("nutrition label")

- **Data collection:** answer **No, we do not collect data from this app.** The result is **Data Not Collected**.
- **Tracking:** none. `PrivacyInfo.xcprivacy` (in `Assets/Plugins/iOS`) declares no tracking, no collected data types, and no tracking domains for the game's own code. Unity's engine framework ships its own privacy manifest.

## Age rating questionnaire

Answer **None** to everything: violence, sexual content, profanity, drugs, horror, gambling, contests, medical, and so on.

| Question | Answer |
|---|---|
| Unrestricted web access | No |
| User-generated content | No. The nickname is local only and never shown to anyone else. |
| Messaging or chat | No |
| In-app purchases | No |
| Advertising | No |

The expected rating is the lowest (4+).

## Export compliance

The app uses no encryption beyond what iOS itself provides. The build post-processor sets `ITSAppUsesNonExemptEncryption = NO` in Info.plist, so App Store Connect won't ask about encryption on each upload.

## Screenshots (YOU, after a device or simulator build)

The 6.9" iPhone screenshots are required; App Store Connect scales them down for smaller iPhones. Take them on an iPhone Pro Max–class simulator with **⌘S**. Use portrait 1320×2868 (6.9"), or 1290×2796 (6.7") if Apple still accepts that size for your submission. Suggested set:

1. A shot release with the GREEN callout
2. Defense: a block or steal toast
3. The CALL menu open on offense
4. The Rise Mode hub with standings
5. The post-game box score with rewards
6. Locker Room ▸ Style

Only show real gameplay. Don't add device frames that show non-Apple hardware, and don't include text that mentions other games or leagues.

## App Review notes (paste into "Notes")

> Fully offline single-player game with no login, network use, ads, or purchases. All content is original and generated procedurally. To see the full season flow quickly: Main menu ▸ RISE MODE ▸ PLAY NEXT.
