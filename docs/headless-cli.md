# Headless CLI (Unity Editor BatchMode)

## Overview
This project now supports headless simulation in Unity Editor batch mode through:
- execute method entry: `HeadlessCliEntry.Execute`
- runtime headless runner: automatic in batch mode with CLI options

It also supports standalone player execution (`YourGame.exe`) with the same runtime CLI options.

## Parameters
All parameters are optional unless noted.

- `scene` or `sceneName`: scene short name from Build Settings. Default: `SampleScene`
- `fallType`: `-1` (random) or `0..7`
- `count` or `runs`: batch count, must be `> 0`
- `duration`: capture duration per run in seconds, must be `> 0`
- `outputDir`: output root directory (relative or absolute). Default: `Output`
- `seed`: integer seed for deterministic randomness
- `cameraMode` or `mode`: `random` or `manual`
- `randomCamera`: boolean alias. `true` -> random, `false` -> manual
- `camPos`: `x,y,z`
- `camRot`: `x,y,z` (Euler angles)
- `camPosX`, `camPosY`, `camPosZ`: optional position components
- `camRotX`, `camRotY`, `camRotZ`: optional rotation components
- `headless`: boolean; defaults to true when CLI options are present

## Exit codes
- `0`: success
- `1`: argument parse error
- `2`: scene not found in Build Settings
- `11`: runtime parse error
- `21`: `BatchFallGenerator` missing
- `22`: capture camera missing
- `23`: `CocoExporter` missing
- `99`: unexpected exception

## PowerShell examples
Use your local Unity editor path.

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\2022.3.54f1c1\Editor\Unity.exe"
$project = "C:\Users\sugar\workspace\tuanjie\袁城林版本\UnityProject"

& $unity `
  -projectPath $project `
  -batchmode -nographics `
  -quit `
  -logFile "$project\Output\headless.log" `
  -executeMethod HeadlessCliEntry.Execute `
  -- `
  scene=SampleScene fallType=3 count=20 duration=3.0 outputDir=Output/headless seed=42 cameraMode=random
```

Standalone player (`.exe`) example:

```powershell
$player = "D:\Build\FallSim\FallSim.exe"

& $player `
  -batchmode -nographics `
  -- `
  scene=SampleScene fallType=3 count=20 duration=3.0 outputDir=D:/sim_out seed=42 cameraMode=random
```

Notes for standalone player:
- `scene` works by scene name and the scene must be included in Build Settings.
- Runtime will switch scene before batch starts when active scene name differs.
- `-executeMethod` is Editor-only and not used for standalone `.exe`.

Manual camera:

```powershell
& $unity `
  -projectPath $project `
  -batchmode -nographics `
  -quit `
  -logFile "$project\Output\headless-manual.log" `
  -executeMethod HeadlessCliEntry.Execute `
  -- `
  scene=SampleScene fallType=5 count=10 duration=2.0 outputDir=Output/manual seed=7 cameraMode=manual camPos=0,1.3,-3 camRot=10,180,0
```
