# BODY 24 implementation report

The playable prototype is saved in `Assets/Body24/Scenes/Body24.unity`. The original SampleScene remains available. The scene is first in Android Build Settings.

## Implemented

Calibration and floor placement; stable snap-follow prompts; hand lanes; shared one-second hand/fingertip holds; earliest-hand arbitration; optional first-launch tutorial; both specified tutorial puzzles; four alternating shoulder-tap proxy gestures; exact rational 24 arithmetic and recursive solver; solvable random generation and rerolls; reset; number/operator/number selection; second-to-first merges; intermediate/gold styling; optional timed opposite-hand lift bonuses; multiplicative scoring and active-time speed factor; endless Classic; settings and operator reach adjustment; recalibration; plank countdown; HMD presence pause/resume; timed or explicit exit; session results and saved local preferences.

The scene uses editable prefabs, serialized references and a shared configuration asset. Editor mouse/key simulation feeds the same tracked-pose/contact/gameplay code as device input. It is not a separate puzzle implementation.

## Created assets

All paths below are relative to the Unity project.

- Scene: `Assets/Body24/Scenes/Body24.unity`.
- Configuration: `Assets/Body24/Body24Config.asset`.
- Runtime scripts under `Assets/Body24/Runtime/`: `Body24App.cs`, `Body24Config.cs`, `PuzzleEngine.cs`, `InteractionRules.cs`, `TrackedHands.cs`, `HandContactController.cs`, `HoldTarget.cs`, `PlayzoneView.cs`, `SnapInstructionPanel.cs`, `EditorFloorPreview.cs`, `EditorSimulatorOverlay.cs`.
- Editor tools under `Assets/Body24/Editor/`: `Body24Builder.cs`, `Body24Validation.cs`.
- Prefabs under `Assets/Body24/Prefabs/`: `NumberCard`, `OperatorButton`, `GroundActionButton`, `ResetButton`, `HoldProgress`, `SelectionHighlight`, `InstructionPanel`, `OKGesturePrompt`, `ShoulderTapPrompt`, `Countdown`, `PlayzoneOutline`, `ScorePopup`, `SessionResults`.
- Procedural transparent hand/OK/progress sprites and URP materials under `Assets/Body24/Art/`.
- Unity-bundled TextMeshPro Essential Resources under `Assets/TextMesh Pro/`.
- Run/device instructions: `Assets/Body24/README.md`.
- Rule and live test evidence and camera captures: `Assets/Body24/Reports/`.
- Android output, if successful: `Builds/Android/Body24.apk`; actual outcome is recorded in `Assets/Body24/Reports/AndroidBuild.txt`.

## Configuration changes

The user explicitly selected native PICO XR with passthrough after inspection found Spatial SDK mode selected while Android already used the native PXR loader. The existing PXR Android loader was retained. `Assets/Resources/PXR_ProjectSetting.asset` now selects native XR, hand tracking, video see-through and the installed SDK's documented MR Safeguard policy. Android scripting symbols include `ENABLE_PICO_XR_SDK`. No guardian-disable or boundary-bypass calls were added.

`ProjectSettings/EditorBuildSettings.asset` includes BODY24 first. `ProjectSettings/ProjectSettings.asset` uses the BODY 24 product name and PICO's required Android **Activity** entry point, while retaining IL2CPP/ARM64. The first build attempt exposed the existing GameActivity mismatch; the retry uses the SDK validation rule's exact Activity setting. No package versions or manifest dependencies were edited.

## Verified integration

Installed versions: Unity 6000.3.14f1, URP 17.3.0, PICO XR SDK 6.1.1. API signatures were verified against installed package source and compiled by Unity.

- Native passthrough: `PXR_Manager.EnableVideoSeeThrough` with transparent camera clear.
- Hands: `PXR_HandTracking.GetJointLocations`, `HandJointLocations.isActive` and per-joint `PositionTracked` flags.
- Palm proxy: midpoint of middle/ring proximal finger-base joints, indices 12 and 17, diameter 0.05 m. Index tip 10 also contacts menu targets.
- OK fallback: thumb/index tip proximity plus extended remaining fingers. Calibration palm-up orientation is a configurable-code heuristic requiring headset tuning.
- Floor: XR Floor tracking-origin request plus configurable floor offset; local floor frame and explicit recalibration. No room-mesh scanning or persistent platform anchor service is implemented.
- MR policy: installed SDK `mrSafeguard` emits `enable_mr_safeguard`; the SDK tooltip documents MR safety policies rather than VR policies. Hardware policy behavior is unverified.

## Validation completed

- **174 rule assertions passed**, covering exact arithmetic, negative/fractional intermediates, divide-by-zero rejection, both fixed puzzles, ordered selection, reset, generated ranges 10–13, solvable changed-value rerolls, continuous holds, exact lift-window edge cases, gesture rearming/alternation and score thresholds. See `Reports/Rules.txt`.
- **29 live Play Mode assertions passed**, using injected tracked poses through actual target volumes and the contact controller. They cover calibration, OK placement, first-launch offer, shared countdown, guided and independent tutorial solutions, reroll, cancellation, right-hand-first arbitration, one-shot confirmation, actual lift qualification, invalid tracking, Classic score/reward/next puzzle, pause/resume integrity, frozen active timers, OK exit, 3-second tutorial timeout, 15-second Classic timeout, zero-solve results, settings persistence and duplicate-free recalibration. See `Reports/LivePlayMode.txt`.
- Player frame counters advanced during live verification. Runtime console checks found no prototype exceptions. Visual inspection checked transparent illustrations, dark card surfaces and timer/score/reset/grid hierarchy from a simulated plank camera.
- A later menu-only layout adjustment places Settings in the upper-left, as specified. Android compilation includes that adjustment.
- **Android development APK succeeded**, with zero build errors. Output: `Builds/Android/Body24.apk` (63,119,498 bytes). The build report contains 17 warnings from existing SDK/project dependencies, including an unconfigured optional PICO platform app ID, unused bundled controller-prefab scripts/shaders, desktop-only USD exclusion, and PICO spatial-audio native libraries without 16 KB alignment. The last warning may affect ARM64 devices running Android 15 or later; verify compatibility on the target headset before distribution. Package dependencies were preserved rather than patched or upgraded. Full evidence: `Reports/AndroidBuild.json` and `Reports/AndroidBuild.txt`.

## Remaining hardware work / prototype limits

ADB reported no connected devices. No installation, headset execution, physical tracking validation, passthrough verification, ergonomic approval or device performance test is claimed. The Android build outcome is reported separately; a successful build alone does not prove those device behaviors.

The implementation uses a runtime floor and local frame, not a scanned-room floor or persistent spatial anchor. Full rendered hand meshes are replaced by small visual interaction probes. The shoulder cue is text-based; gesture icons and visual styling remain prototype-level. Standing reach, palm-up/OK recognition, near-floor joint confidence, optional lift thresholds, operator distances, presence tolerances and MR safeguards need the focused headset acceptance checks in the README. Head/hand tracking does not establish actual plank form or weight-bearing.

Open the scene and press Play to run the Editor prototype. Hold O to confirm placement, hold the mouse on controls, use Space for plank/standing, Q/E for alternating taps, B for opposite-hand lift and Escape for immediate exit. The BODY 24 menu exposes repeatable validation and Android build commands.
