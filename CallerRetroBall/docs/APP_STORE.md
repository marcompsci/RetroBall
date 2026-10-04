# App Store submission kit

Draft metadata and answers for App Store Connect. Character counts are checked against Apple's limits. Fill in the items marked **YOU** yourself, because only the account holder can provide them.

## App record

| Field | Value |
|---|---|
| Name (30) | RetroBall |
| Subtitle (30) | Retro 3v3 street basketball *(27)* |
| Bundle ID | `com.marcompsci.retroball` (already set; registered when you ran it on your iPhone) |
| SKU | `retroball-ios-1` (any unique string) |
| Primary category | Games ▸ Sports |
| Secondary category | Games ▸ Arcade |
| Price | Free, or a one-time paid price. The game has no in-app purchases. |
| Version | 1.0.0 (build 1). Raise the build number for every upload. |

## Promotional text (170)

> Heat up, throw the alley-oop, climb the Arcade Ladder, and build your own team. Retro 3v3 hoops with secrets to find. No ads. No purchases. Fully offline.

*(154 characters)*

## Description (4000)

> **Call your shot. Build your legacy.**
>
> RetroBall is a pixel-art 3v3 half-court basketball game made for one thumb on the stick and one on the buttons. Hold to shoot and release on the green. Hit three in a row and you HEAT UP. Lob it to a cutter at the rim for the ALLEY-OOP. When the game slows down, call a play: Pick & Roll, Give & Go, or Clear Out.
>
> On defense, jump to contest, reach for steals, switch onto the ball handler, and box out. Every AI team has its own defense (full-court pressure, zones, packing the paint) and the smart ones adjust when you find a weakness.
>
> **RISE MODE**
> Beat five street crews on The Blacktop Circuit, then play a season in The Caller League with event cards, crew chemistry, recruits, rival crews, and story scenes, all the way to The Gold Signal Cup.
>
> **ARCADE MODES**
> • Arcade Ladder: six stages, three continues, one secret boss
> • King of the Court: win until you lose
> • The Caller Cup: an eight-team knockout
> • Full Court 5-on-5: both baskets, 2s and 3s
> • 1-on-1, Quick Call (winners' ball), and a new Daily Challenge every day
>
> **MAKE IT YOURS**
> Create your player and your own team: name, colours, jersey pattern, shorts, shoes, logo, and home court. Train attributes, unlock kits, and fill your trophy room with badges.
>
> **SECRETS**
> Enter old-school codes for big heads, a rainbow ball, hidden courts, and a secret crew. Hints are earned by playing.
>
> **PRACTICE LAB**
> Free Shoot, Passing Targets, Dribble Lane, 3-Point Contest, Lockdown, and the Shootout against a CPU sharpshooter.
>
> **PLAY YOUR WAY**
> • Touch controls or a Bluetooth controller (menus too)
> • 2 Player on one device
> • Optional CRT scanlines and 120 Hz on ProMotion iPhones
> • English and Spanish
> • Left-handed layout, large buttons, tap-to-shoot, reduce motion, colourblind team patterns
>
> **MADE TO RESPECT YOUR TIME**
> Games last about two minutes. Fully offline. No account, no ads, no in-app purchases, no tracking. Game Center is optional.
>
> All teams, players, courts, art, and music are original.

## Spanish (Mexico) localization

Add **Spanish (Mexico)** as a localization in App Store Connect and paste these.

| Field | Text |
|---|---|
| Name | RetroBall |
| Subtitle (30) | Básquet retro 3v3 callejero *(27)* |
| Promotional text | Ponte al rojo vivo, lanza el alley-oop, sube la Escalera Arcade y crea tu propio equipo. Básquet retro 3v3 con secretos. Sin anuncios ni compras. |
| Keywords (100) | `baloncesto,basquet,arcade,retro,pixel,3v3,callejero,offline,deportes,cancha,temporada,8-bit` |

> **Anuncia tu tiro. Construye tu leyenda.**
>
> RetroBall es un juego de básquet 3v3 de media cancha en pixel art, hecho para jugar con un pulgar en el stick y otro en los botones. Mantén para tirar y suelta en verde. Encesta tres seguidos y te pones AL ROJO VIVO. Lanza el alley-oop a un compañero junto al aro.
>
> Modo Ascenso, Escalera Arcade con jefe secreto, Rey de la Cancha, la Caller Cup, 1 contra 1, Reto Diario y 2 jugadores en un dispositivo. Crea tu jugador y tu propio equipo, descubre códigos secretos y llena tu sala de trofeos.
>
> Controles táctiles o mando Bluetooth. Sin conexión, sin cuenta, sin anuncios, sin compras y sin rastreo. Todo el contenido es original.

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

> RetroBall does not collect, store, or share any personal information. The game works fully offline. It has no accounts, advertising, analytics, or tracking. Game progress (your nickname, settings, and career) is saved on your device and, if iCloud Sync is on, in your own iCloud account through Apple's iCloud service, where the developer can't see it. Optional Game Center scores and achievements are handled by Apple's Game Center. Contact: **YOU: your email**.

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

1. A HEAT CHECK player with flames, mid-jumper
2. An ALLEY-OOP finish (gold pass arrow, then the slam)
3. The Arcade Ladder with THE GLITCH on its neon court
4. Locker Room ▸ TEAM (create-a-team) with a custom kit
5. The Rise Mode hub or a story scene
6. Defense: a block, with the CRT filter on

Use **RetroBall ▸ Release ▸ Capture Store Screenshot** in Play mode (see `docs/LAUNCH_KIT.md`). Captions for each shot are in the launch kit.

Only show real gameplay. Don't add device frames that show non-Apple hardware, and don't include text that mentions other games or leagues.

## App Review notes (paste into "Notes")

> Fully offline game with no login, ads, or purchases. Optional Apple services only: Game Center (Settings ▸ Game Center) and iCloud save sync (Settings ▸ iCloud Sync). All content is original and generated procedurally. To see the full season flow quickly: Main menu ▸ RISE MODE ▸ PLAY NEXT.
