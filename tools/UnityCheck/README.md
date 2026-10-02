# UnityCheck

This tool compiles the five `CallerRetroBall.*` assemblies with Roslyn, using Unity's real reference assemblies. It does not need the Unity Editor, and it catches the errors Unity would report, for example CS0165.

Setup, once per Unity version:

1. Collect every `-r:` reference listed in Unity's `Library/Bee/artifacts/*/CallerRetroBall.*.rsp` files, along with the `.rsp` files themselves.
2. Put them in one `refs/` folder, with the `.rsp` files in a `refs/rsp/` subfolder.

Run it:

```
cd tools/UnityCheck
dotnet run -c Release -- <refs folder> ../../CallerRetroBall/Assets
```

It exits non-zero if any assembly has errors. Unity's own analyzers (UAC warnings) are not run.
