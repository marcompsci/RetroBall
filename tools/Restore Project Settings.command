#!/usr/bin/env bash
# Double-click: puts back ~/RetroHoops/ProjectSettings if it has gone missing, then runs the Build Check.
# Tries, in order: (1) the latest Time Machine backup, (2) Unity Version Control (the project's .plastic
# workspace, if the `cm` command is installed), (3) rebuilding it: Unity recreates default settings and
# Retro Hoops' own setup code applies everything the game needs (bundle ID, team, input system, scenes...).
# Never overwrites an existing ProjectSettings folder. Log: ~/RetroHoops/Logs/restore_settings.txt
cd "$(dirname "$0")/.." || exit 1
PROJECT="$HOME/RetroHoops"
PS="$PROJECT/ProjectSettings"
LOGS="$PROJECT/Logs"
mkdir -p "$LOGS"
exec > >(tee "$LOGS/restore_settings.txt") 2>&1
echo "=== $(date '+%Y-%m-%d %H:%M:%S')  Restore Project Settings"
VERSION="6000.6.3f1"
UNITY="/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity"
TEAM="$(tr -d '[:space:]' < tools/apple_team.txt 2>/dev/null)"

if [[ -f "$PS/ProjectSettings.asset" ]]; then
  echo "ProjectSettings is already there: nothing to restore."
else
  restored=""
  # 1. Time Machine.
  if command -v tmutil >/dev/null 2>&1; then
    while read -r backup; do
      [[ -z "$backup" ]] && continue
      for root in "$backup/Macintosh HD - Data" "$backup/Data" "$backup"; do
        cand="$root$PS"
        if [[ -f "$cand/ProjectSettings.asset" ]]; then
          mkdir -p "$PS" && cp -Rp "$cand/." "$PS/" && restored="Time Machine ($backup)"; break 2
        fi
      done
    done < <(tmutil listbackups 2>/dev/null | sort -r | head -20)
  fi
  [[ -n "$restored" ]] || echo "Time Machine: no backup of ProjectSettings found (or Time Machine isn't set up)."
  # 2. Unity Version Control.
  if [[ -z "$restored" ]]; then
    CM=""
    for c in "$(command -v cm 2>/dev/null)" /usr/local/bin/cm /opt/homebrew/bin/cm \
             /Applications/PlasticSCM.app/Contents/Applications/cm.app/Contents/MacOS/cm \
             "/Applications/Unity DevOps Version Control.app/Contents/Applications/cm.app/Contents/MacOS/cm"; do
      [[ -n "$c" && -x "$c" ]] && { CM="$c"; break; }
    done
    if [[ -n "$CM" && -d "$PROJECT/.plastic" ]]; then
      echo "Unity Version Control: $CM"
      (cd "$PROJECT" && "$CM" undo -r ProjectSettings) && [[ -f "$PS/ProjectSettings.asset" ]] && restored="Unity Version Control"
      [[ -n "$restored" ]] || echo "Unity Version Control couldn't bring it back."
    else
      echo "Unity Version Control: the cm command isn't installed."
    fi
  fi
  # 3. Rebuild.
  if [[ -z "$restored" ]]; then
    if [[ ! -x "$UNITY" ]]; then echo "STOPPED: Unity $VERSION not found at $UNITY."; exit 1; fi
    if ps -axo args= | grep "Unity.app/Contents/MacOS/Unity " | grep -v -- "-batchmode" | grep -qi -- "-projectpath $PROJECT"; then
      echo "STOPPED: Unity has this project open. Quit the Unity Editor (Cmd+Q) and run this again."; exit 1
    fi
    mkdir -p "$PS"
    printf 'm_EditorVersion: %s\nm_EditorVersionWithRevision: %s (45d8eee7de74)\n' "$VERSION" "$VERSION" > "$PS/ProjectVersion.txt"
    echo "Rebuilding the settings with Unity (a few minutes)..."
    "$UNITY" -batchmode -quit -nographics -buildTarget iOS -projectPath "$PROJECT" ${TEAM:+-teamId "$TEAM"} \
      -executeMethod CallerRetroBall.EditorTools.ProjectSetup.RestoreSettingsBatch -logFile "$LOGS/unity_restore_settings.log"
    code=$?
    grep -A8 "Project settings restored" "$LOGS/unity_restore_settings.log" | head -12
    grep -E "error CS[0-9]+|executeMethod.*(could not|failed)|Exception" "$LOGS/unity_restore_settings.log" | sort -u | head -20
    if [[ $code -ne 0 || ! -f "$PS/ProjectSettings.asset" ]]; then echo "STOPPED: Unity couldn't rebuild the settings (exit $code)."; exit 1; fi
    restored="rebuilt by Unity + Retro Hoops setup"
  fi
  echo "RESTORED: $restored"
fi

# The generated assets (scenes, content data, TextMeshPro essentials) come from Retro Hoops' own setup code.
if [[ ! -f "$PROJECT/Assets/Scenes/BootScene.unity" || ! -d "$PROJECT/Assets/TextMesh Pro" ]]; then
  if [[ ! -x "$UNITY" ]]; then echo "STOPPED: Unity $VERSION not found at $UNITY."; exit 1; fi
  echo "Scenes or TextMeshPro essentials missing: running Project Setup in Unity (a few minutes)..."
  for pass in 1 2; do
    "$UNITY" -batchmode -quit -nographics -buildTarget iOS -projectPath "$PROJECT" \
      -executeMethod CallerRetroBall.EditorTools.ProjectSetup.SetupBatch -logFile "$LOGS/unity_setup_$pass.log"
    grep -E "TextMeshPro essentials" "$LOGS/unity_setup_$pass.log" | head -2
    grep -A12 "Project setup finished" "$LOGS/unity_setup_$pass.log" | head -14
    grep -E "error CS[0-9]+|Exception" "$LOGS/unity_setup_$pass.log" | sort -u | head -10
    [[ -f "$PROJECT/Assets/Scenes/BootScene.unity" && -d "$PROJECT/Assets/TextMesh Pro" ]] && break
  done
  if [[ ! -f "$PROJECT/Assets/Scenes/BootScene.unity" ]]; then echo "STOPPED: Project Setup didn't create the scenes."; exit 1; fi
  echo "SETUP: scenes and content created."
fi
echo
echo "=== Now the Build Check"
bash "tools/Build Check (Simulator).command"
echo "=== finished $(date '+%H:%M:%S'). You can close this window; Claude reads the logs."
