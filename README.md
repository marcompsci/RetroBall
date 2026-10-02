# RetroBall — Caller Retro Ball

*Call your shot. Build your legacy.*

An original, offline, portrait-first retro arcade 3v3 basketball game for iPhone, built in Unity 6 LTS.

- **Unity project:** [`CallerRetroBall/`](CallerRetroBall/) — setup, controls, iOS build steps, and architecture are in [`CallerRetroBall/README.md`](CallerRetroBall/README.md).
- **Plan and phase log:** [`CallerRetroBall/docs/PLAN.md`](CallerRetroBall/docs/PLAN.md)
- **Design:** [`CallerRetroBall/DESIGN.md`](CallerRetroBall/DESIGN.md)
- **Manual QA checklist:** [`CallerRetroBall/docs/QA_CHECKLIST.md`](CallerRetroBall/docs/QA_CHECKLIST.md)
- **Logic tests without Unity:** `cd tools/LogicTests && dotnet run --project Runner/Runner.csproj`

**Status:** all seven phases implemented. Game logic passes 192 automated tests on .NET; the Unity layer has not yet been compiled in the Editor or run on a device.

All teams, players, courts, logos, art, and audio are original and generated procedurally in the project.
