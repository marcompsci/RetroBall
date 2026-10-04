# Retro Hoops — Retro Hoops

*Call your shot. Build your legacy.*

An original, offline, portrait-first retro arcade 3v3 basketball game for iPhone, built in Unity 6 LTS.

- **Unity project:** [`RetroHoops/`](RetroHoops/) — setup, controls, iOS build steps, and architecture are in [`RetroHoops/README.md`](RetroHoops/README.md).
- **Plan and phase log:** [`RetroHoops/docs/PLAN.md`](RetroHoops/docs/PLAN.md)
- **Design:** [`RetroHoops/DESIGN.md`](RetroHoops/DESIGN.md)
- **Release checklist and App Store kit:** [`RetroHoops/docs/RELEASE_CHECKLIST.md`](RetroHoops/docs/RELEASE_CHECKLIST.md), [`RetroHoops/docs/APP_STORE.md`](RetroHoops/docs/APP_STORE.md)
- **Game Center setup:** [`RetroHoops/docs/GAME_CENTER.md`](RetroHoops/docs/GAME_CENTER.md)
- **Manual QA checklist:** [`RetroHoops/docs/QA_CHECKLIST.md`](RetroHoops/docs/QA_CHECKLIST.md)
- **Command-line iOS build (macOS, Unity 6 installed):** `tools/build_ios.sh [simulator|device]`
- **Logic tests without Unity:** `cd tools/LogicTests && dotnet run --project Runner/Runner.csproj`

**Status:** Phases 1–23 implemented (Phase 23: Sunset Swish icon, iPad + Mac (Designed for iPad) layout, Weekly Challenges, the free Hoops Pass, photo mode). Game logic passes 488 automated tests on .NET, and every assembly compiles against Unity 6000.6.3f1's reference assemblies with 0 errors, including the iOS player code paths (tools/UnityCheck). Spanish is partial. Runs in the iOS Simulator and on an iPhone (Phase 18 build); Phases 19–23 have not run on a device yet (iPad and Mac never tested), and nothing has been uploaded to App Store Connect yet.

All teams, players, courts, logos, art, and audio are original and generated procedurally in the project.
