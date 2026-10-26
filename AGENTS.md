# Agent instructions (Codex) - Hatched (pennycookvr) study build

Unity 2022.3.37f1, com.unity.xr.oculus 4.2.0, FieldDay framework (FieldDay.XR input layer). Target: Meta Quest 3 (Android).
Branch flow: work on feature branches off `study-build`; never push to `main`.

## Commands (PowerShell, Unity Editor must be closed)
- Compile check: `powershell -ExecutionPolicy Bypass -File tools\ci.ps1`
- Compile + tests: `powershell -ExecutionPolicy Bypass -File tools\ci.ps1 -Tests`
- Logs: `Builds\ci\*.log`

## Loop
Run ci.ps1 -> read the first error in Builds\ci\compile.log -> fix the root cause -> rerun. Stop after 6 failed rounds and write `Builds/AGENT_REPORT.md`.

## Do not touch
`Assets/FieldDay/`, `Assets/FieldDayModules/` (shared framework), third-party folders under `Assets/_Assets/` and `Assets/Plugins/`, `ProjectSettings/` (unless the task says so), any `.unity` scene or `.prefab` file unless the task says so.
Do not git push. Commit only when the task asks.

## Project notes
- XR input: `FieldDayModules/FieldDayXR/Input/XRInputUpdateSystem.cs` fills shared `XRInputState`; everything reads that state.
- Desktop test rig (T0b): `Assets/Code/DesktopSim/DesktopXRSimSystem.cs` - adds itself next to XRInputUpdateSystem at runtime, disables it while no headset is active, writes simulated head/hands/buttons. Editor/Development builds only.
- Systems are MonoBehaviours registered by `SystemBoot` (Boot.prefab) or `Game.Systems.Register`.
