#!/usr/bin/env bash
# Double-click: the real compile check, with no signing and no upload. Unity writes the Xcode project,
# Xcode compiles everything (C# via IL2CPP, the Objective-C and Swift plugins) for the iOS Simulator,
# and the game launches in an iPhone simulator. Quit the Unity Editor first.
# The summary Claude reads is ~/RetroHoops/Logs/build_check.txt (full logs sit next to it).
cd "$(dirname "$0")/.." || exit 1
PROJECT="$HOME/RetroHoops"
LOGS="$PROJECT/Logs"
OUT="$LOGS/build_check.txt"
mkdir -p "$LOGS"
exec > >(tee "$OUT") 2>&1
echo "=== $(date '+%Y-%m-%d %H:%M:%S')  Build check (Simulator)"
echo "Repo: $(git log --oneline -1 2>/dev/null)"

# Old logs would be read as this run's errors if this run stops early: keep them as *.prev.log.
for f in unity_build_simulator xcodebuild_simulator; do
  [[ -f "$LOGS/$f.log" ]] && mv -f "$LOGS/$f.log" "$LOGS/$f.prev.log"
done
bash tools/play_on_simulator.sh "$PROJECT"
RESULT=$?

echo
echo "--- Unity compile errors (C#) ---"
grep -E "error CS[0-9]+|Scripts have compiler errors|BuildFailedException|Error building Player" "$LOGS/unity_build_simulator.log" 2>/dev/null | sort -u | head -40 || true
echo "--- Unity linker / IL2CPP errors ---"
grep -iE "UnityLinker|il2cpp.*error|Failed to resolve assembly|Fatal error in Unity CIL Linker" "$LOGS/unity_build_simulator.log" 2>/dev/null | grep -iE "error|fail" | sort -u | head -20 || true
echo "--- Xcode errors (Objective-C, Swift, link) ---"
grep -E "error:|Undefined symbol|ld: |\*\* BUILD FAILED" "$LOGS/xcodebuild_simulator.log" 2>/dev/null | sort -u | head -40 || true
echo "--- Our plugin warnings (RetroStore.swift / Retro*.mm) ---"
grep -E "(RetroStore\.swift|Retro[A-Za-z]+\.mm|Caller[A-Za-z]+\.mm):[0-9]+:[0-9]+: warning" "$LOGS/xcodebuild_simulator.log" 2>/dev/null | sort -u | head -30 || true
echo
if [[ $RESULT -eq 0 ]]; then echo "BUILD CHECK PASSED: compiled and launched in the Simulator."; else echo "BUILD CHECK FAILED (exit $RESULT). The sections above show where."; fi
echo "=== finished $(date '+%H:%M:%S'). You can close this window; Claude reads the logs."
