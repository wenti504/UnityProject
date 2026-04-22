---
name: fallsim-cli
description: 'Run FallSim standalone player on machines without Unity. Use for headless batch simulation, Windows CLI command examples, parameter templates, output checks, and exit code troubleshooting.'
argument-hint: 'Describe your target run: scene, fallType, count, duration, outputDir, and camera mode.'
user-invocable: true
---

# FallSim CLI Skill

## When to Use

- You need to run simulations without Unity Editor installed.
- You want ready-to-run Windows command templates for `fallsim.cmd`.
- You need parameter recommendations for random or manual camera runs.
- You want quick diagnosis from process exit codes.

## Procedure

1. Confirm player artifacts exist in `Output/Player` and keep required folders next to `FallSim.exe`.
2. Use the lowercase command `fallsim.cmd` from the player directory.
3. Pass runtime options after `--` using `key=value` format.
4. Check exit code and output directory for generated data.

## Command Templates

Random camera:

```powershell
Set-Location "<PlayerDir>"
.\fallsim.cmd -batchmode -nographics -- scene=SampleScene fallType=3 count=20 duration=3.0 outputDir=D:/sim_out seed=42 cameraMode=random
```

Manual camera:

```powershell
Set-Location "<PlayerDir>"
.\fallsim.cmd -batchmode -nographics -- scene=SampleScene fallType=5 count=10 duration=2.0 outputDir=D:/sim_out seed=7 cameraMode=manual camPos=0,1.3,-3 camRot=10,180,0
```

## Exit Code Guide

- `0`: success
- `1` / `11`: argument parse issues
- `2`: invalid scene for built player
- `21` / `22` / `23`: required runtime component missing
- `99`: unexpected runtime exception

## Reference

- CLI details: `docs/headless-cli.md`
- Player usage guide: `Output/Player/README.md`
