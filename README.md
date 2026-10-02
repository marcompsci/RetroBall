# RetroBall — RetroBall

*Call your shot. Build your legacy.*

An original, offline, portrait-first retro arcade 3v3 basketball game for iPhone, built in Unity 6 LTS.

- **Unity project:** [`CallerRetroBall/`](CallerRetroBall/) — setup, controls, iOS build steps, and architecture are in [`CallerRetroBall/README.md`](CallerRetroBall/README.md).
- **Plan and phase log:** [`CallerRetroBall/docs/PLAN.md`](CallerRetroBall/docs/PLAN.md)
- **Design:** [`CallerRetroBall/DESIGN.md`](CallerRetroBall/DESIGN.md)
- **Release checklist and App Store kit:** [`CallerRetroBall/docs/RELEASE_CHECKLIST.md`](CallerRetroBall/docs/RELEASE_CHECKLIST.md), [`CallerRetroBall/docs/APP_STORE.md`](CallerRetroBall/docs/APP_STORE.md)
- **Game Center setup:** [`CallerRetroBall/docs/GAME_CENTER.md`](CallerRetroBall/docs/GAME_CENTER.md)
- **Manual QA checklist:** [`CallerRetroBall/docs/QA_CHECKLIST.md`](CallerRetroBall/docs/QA_CHECKLIST.md)
- **Command-line iOS build (macOS, Unity 6 installed):** `tools/build_ios.sh [simulator|device]`
- **Logic tests without Unity:** `cd tools/LogicTests && dotnet run --project Runner/Runner.csproj`

**Status:** Phases 1–16 implemented. Game logic passes 342 automated tests on .NET. Phases 11–15 compiled in Unity 6000.6.3f1 with 0 errors; Phase 16 has not been compiled in Unity yet. Spanish is partial. There has been no iOS build or device run.

All teams, players, courts, logos, art, and audio are original and generated procedurally in the project.
