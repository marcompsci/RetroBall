# App Store submission kit

Draft metadata and answers for App Store Connect. Character counts are checked against Apple's limits. Fill in the items marked **YOU** yourself, because only the account holder can provide them.

## App record

| Field | Value |
|---|---|
| Name (30) | RetroBall |
| Subtitle (30) | Retro 3v3 & 5v5 basketball *(26)* |
| Bundle ID | `com.marcompsci.retroball` (already set; registered when you ran it on your iPhone) |
| SKU | `retroball-ios-1` (any unique string) |
| Primary category | Games ▸ Sports |
| Secondary category | Games ▸ Arcade |
| Price | Free, or a one-time paid price. The game has no in-app purchases. |
| Version | 1.0.0 (build 1). Raise the build number for every upload. |

## Promotional text (170)

> Run a franchise, win the Dunk Contest, build your own court and kit. Retro 3v3 and 5v5 hoops with an original chiptune soundtrack. No ads. No purchases. Offline.

*(161 characters)*

## Description (4000)

> **Call your shot. Build your legacy.**
>
> RetroBall is a pixel-art basketball game made for one thumb on the stick and one on the buttons. Hold to shoot and release on the green. Hit three in a row and you HEAT UP. Lob it to a cutter for the ALLEY-OOP. When the game slows down, call a play: Pick & Roll, Give & Go, or Clear Out.
>
> On defense, contest, reach for steals, switch onto the ball handler, and box out. Every AI team has its own defense, and the smart ones adjust when you find a weakness.
>
> **FRANCHISE**
> Be the GM of a league club, season after season. Set the rotation, trade with seven other GMs, re-sign your players, chase free agents under the salary cap, scout prospects, and win the draft lottery. Players age, improve, decline, and retire into the Hall of Fame. Play every game Full Court 5-on-5, or simulate.
>
> **LEGACY**
> Take your own player from a high-school senior to the pros: recruiting stars, college or straight to the draft, draft night, contracts, sponsors, a skill tree, story choices, and a Hall of Fame vote.
>
> **THE PARK**
> Call out twelve street legends, 1-on-1 up to 4-on-4. Street rules, make it take it, and crossovers that break ankles.
>
> **RISE MODE**
> Beat the street crews of The Blacktop Circuit, then play a season in The Caller League with event cards, crew chemistry, recruits, rivals and story scenes. Halfway through, it's the All-Star Weekend.
>
> **ALL-STAR WEEKEND**
> • Dunk Contest: pick a dunk, enter its combo in the air, time the slam, and face five judges
> • 3-Point Contest against the league's best shooters
> • The All-Star Game, Full Court
>
> **MORE WAYS TO PLAY**
> • Full Court 5-on-5 and half-court 3v3 (winners' ball)
> • Arcade Ladder with a secret boss, King of the Court, The Caller Cup
> • Holiday Games on Christmas, Halloween, Easter and Fourth of July courts
> • 1-on-1, a Daily Challenge, H-O-R-S-E, 21 and more party games
> • Tournament Builder: your own 4, 8 or 16-team bracket
> • 2 Player on one iPhone, face to face
>
> **MAKE IT YOURS**
> Create your player and your team. Design Home, Away and Alt kits in the Kit Studio and share them with a code or QR. Build up to three courts of your own: floor, paint, lines, stands, sky and a centre-court logo. Pick your music in the Music Player: nine original chiptune tracks.
>
> **PLAY YOUR WAY**
> • Touch controls or a Bluetooth controller
> • English and Spanish
> • Colour filters, captions, VoiceOver menus, left-handed layout, large buttons, tap-to-shoot, reduce motion
> • Optional CRT scanlines and 120 Hz on ProMotion iPhones
> • Optional Game Center and iCloud save sync
>
> **MADE TO RESPECT YOUR TIME**
> Fully offline. No account, no ads, no in-app purchases, no tracking.
>
> All teams, players, courts, art and music are original.

*(2,715 characters without the quote markers.)*

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
> Modo Franquicia (traspasos, agencia libre, draft y tope salarial), Fin de Semana de las Estrellas con concurso de clavadas y de triples, Cancha completa 5 contra 5, constructor de canchas, estudio de uniformes y nueve temas chiptune originales. Modo Ascenso, Escalera Arcade con jefe secreto, Rey de la Cancha, la Caller Cup, 1 contra 1, Reto Diario y 2 jugadores en un dispositivo. Crea tu jugador y tu propio equipo, descubre códigos secretos y llena tu sala de trofeos.
>
> Controles táctiles o mando Bluetooth. Sin conexión, sin cuenta, sin anuncios, sin compras y sin rastreo. Todo el contenido es original.

## Keywords (100)

```
basketball,arcade,retro,pixel,3v3,5v5,hoops,streetball,offline,sports,franchise,dunk,chiptune,8-bit
```

*(99 characters.)* Don't add the names of real leagues, teams, players, or other games. Apple rejects keywords that use trademarks you don't own.

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
2. Franchise ▸ TRADE with a deal the other GM accepts
3. The Dunk Contest: a 360 slam with the judges' cards up
4. Full Court 5-on-5 on a custom court from the Court Builder
5. Locker Room ▸ KIT (Kit Studio) with the live preview
6. An ALLEY-OOP finish (gold pass arrow, then the slam)
7. Franchise ▸ DRAFT with scouted prospects and the lottery result
8. A Holiday Game (Christmas court) or the Arcade Ladder boss
9. The Music Player with the level meter moving
10. The Rise Mode hub with the ALL-STAR WEEKEND button

Use **RetroBall ▸ Release ▸ Capture Store Screenshot** in Play mode (see `docs/LAUNCH_KIT.md`). Captions for each shot are in the launch kit.

Only show real gameplay. Don't add device frames that show non-Apple hardware, and don't include text that mentions other games or leagues.

## App Review notes (paste into "Notes")

> Fully offline game with no login, ads, or purchases. Optional Apple services only: Game Center (Settings ▸ Game Center) and iCloud save sync (Settings ▸ iCloud Sync). All content is original and generated procedurally. To see the main modes quickly: Main menu ▸ PLAY ▸ FRANCHISE (START FRANCHISE, then SIM GAME or PLAY GAME) and PLAY ▸ ALL-STAR CONTESTS ▸ DUNK CONTEST. Rise Mode: Main menu ▸ RISE MODE ▸ PLAY NEXT.
