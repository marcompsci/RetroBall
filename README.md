# RetroBall — Caller Retro Ball

*Call your shot. Build your legacy.*

An original, offline, portrait-first retro arcade 3v3 basketball game for iPhone, built in Unity 6 LTS.

- **Unity project:** [`CallerRetroBall/`](CallerRetroBall/) — setup, controls, iOS build steps, and architecture are in [`CallerRetroBall/README.md`](CallerRetroBall/README.md).
- **Plan and phase log:** [`CallerRetroBall/docs/PLAN.md`](CallerRetroBall/docs/PLAN.md)
- **Design:** [`CallerRetroBall/DESIGN.md`](CallerRetroBall/DESIGN.md)
- **Release checklist and App Store kit:** [`CallerRetroBall/docs/RELEASE_CHECKLIST.md`](CallerRetroBall/docs/RELEASE_CHECKLIST.md), [`CallerRetroBall/docs/APP_STORE.md`](CallerRetroBall/docs/APP_STORE.md)
- **Manual QA checklist:** [`CallerRetroBall/docs/QA_CHECKLIST.md`](CallerRetroBall/docs/QA_CHECKLIST.md)
- **Command-line iOS build (macOS, Unity 6 installed):** `tools/build_ios.sh [simulator|device]`
- **Logic tests without Unity:** `cd tools/LogicTests && dotnet run --project Runner/Runner.csproj`

**Status:** Phases 1–8 implemented (8 = release prep). Game logic passes 198 automated tests on .NET; the Unity layer has not yet been compiled in the Editor or run on a device.

All teams, players, courts, logos, art, and audio are original and generated procedurally in the project.
