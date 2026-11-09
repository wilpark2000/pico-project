# BODY 24 prototype

Open `Assets/Body24/Scenes/Body24.unity` and press Play. The original `Assets/Scenes/SampleScene.unity` is preserved. BODY24 is the first enabled Android build scene.

## Editor controls

The Editor simulator is enabled on `BODY24 Application > TrackedHands`. It is automatically excluded from device input by `Application.isEditor`.

This is BODY24's custom keyboard/mouse simulator, not a separate PICO headset emulator. Click inside Unity's **Game** window while Play Mode is running so it receives input. **Right-click and drag** to look around, use **WASD** to move horizontally, **R/F** to move up/down, and **H** to reset the camera. **Space** switches standing/plank poses and resets the camera offset. Moving outside the plank presence zone during a session will pause it.

1. Wait for the simulated standing measurement. Hold **O** for 0.5 seconds to confirm clear floor placement.
2. Hold the **left mouse button** over a floor control for 0.5 seconds. Hold **Left Shift** to use the right hand instead. Move onto another control to select it; the previous hand may remain on its button.
3. Choose Tutorial, or Play > Classic. Press **Space** to toggle between standing and the simulated plank. Both hands rest in their lanes; the shared three-second countdown and one-second START follow.
4. Use number → operator → second number. Selecting another number before an operator replaces the first selection. The second card moves into the first card's slot; the result is unselected.
5. During a number/operator hold, press **B** within 0.5 seconds to lift the opposite hand; keep B held until confirmation to earn the optional bonus. The other hand must start supported.
6. Press/release **Q**, **E**, **Q**, **E** to simulate four alternating near-head/shoulder gestures. Either side may start. Reroll is available with four uncombined cards.
7. **Space** stands up and triggers presence-based pause. Return to resume. Hold **O** during pause to finish, or let its countdown expire. **Escape** exits immediately. Hold **T** to simulate loss of headset tracking.

Mouse selection is an Editor-only input simulation. On the headset all controls use tracked-hand contact; gaze, mouse, voice, and controller rays are not gameplay selection inputs.

## What is implemented

- Stable-sample standing height/reach calibration; configurable floor offset, lane width, operator reach and snap-follow instructions.
- Floor frame, playzone outline, support lanes, OK hold, optional first-launch tutorial, menu, Classic mode, maximum-number settings, reach adjustment and recalibration.
- Shared continuous one-second hold with cancellation, exit/re-entry latch, deterministic earlier-hand arbitration and disabled invalid-tracking probes.
- Exact normalized rational arithmetic, ordered operations, recursive solvability search, solver-validated generation for ranges 10/11/12/13, reset, and duplicate-first/highest-value solvable rerolls.
- Guided `(3+5)×(6÷2)` tutorial; unsolvable `3,5,8,10` tutorial with four-tap `10 → 4`, then independent solving.
- Optional lift-after-contact bonus, multiplicative scoring, 15-second active solve speed bonus, second-to-first merges, intermediate styling, gold 24, point flight and score increment.
- Shared plank-start countdown, HMD presence estimation with hysteresis/grace, three-second tutorial exit, fifteen-second Classic pause, intact resume, OK/button/timeout end, results with zero-solve handling.
- Persistent tutorial offer status, maximum initial number and adjustable operator distance.
- Editable common target/progress/prompt/outline/results prefabs, procedural transparent hand and OK art, configuration asset, test runners and Editor simulation.

## Runtime and SDK evidence

Unity 6000.3.14f1; URP 17.3.0; installed PICO XR SDK 6.1.1. The user selected **native PICO XR with passthrough** after inspection found Spatial mode selected while Android already used the PXR loader. The existing Android loader was retained. `isSpatialAdapter` is now false and `ENABLE_PICO_XR_SDK` is enabled; no package manifest edits or package version changes were made.

- `PXR_HandTracking.GetJointLocations(HandType, ref HandJointLocations)` is the verified installed joint API. Both `isActive` and required joints' `PositionTracked` flags gate input. Local SDK poses use its `ToVector3()` / `ToQuat()` helpers and the rig's tracking-space transform.
- The 5 cm palm proxy is the midpoint of **JointMiddleProximal (12)** and **JointRingProximal (17)**, the stable finger-base equivalents. Menus also accept **JointIndexTip (10)** proximity.
- OK is a configurable thumb-tip/index-tip proximity heuristic with extended remaining fingers. Open-palm calibration uses extended fingertips and the palm's orientation. These are provisional pose heuristics requiring device tuning.
- `PXR_Manager.EnableVideoSeeThrough` enables native passthrough. The camera clears to transparent black. The selected SDK project settings enable hand tracking and video see-through.
- XR tracking-origin mode requests **Floor**. This prototype uses the runtime floor plus an Inspector-adjustable `floorHeight`; it does not scan a room mesh, infer an unknown floor, or persist platform spatial anchors. Recalibration is available each run.
- The documented installed **MR Safeguard** option is enabled. The SDK's own tooltip states that it selects MR safety policies instead of VR policies. No guardian-disable or boundary-bypass APIs are used. Runtime policy behavior remains a headset acceptance check.
- A single XR Origin wraps the existing scene camera, with Input System tracked pose and one PXR manager. The source scene had no XR rig to reuse. No locomotion or controller interactor is installed.

## Tuning and boundaries

Edit `Assets/Body24/Body24Config.asset` for hold/bonus timing, floor offset, support/lift hysteresis, shoulder distance/cooldown, calibration bounds, presence limits and presentation. `BODY24 Application` exposes calibrated height, reach and confidence through runtime properties for inspection. Settings adjust lateral operator reach conservatively.

Hand/head tracking approximates support, lifted-hand state and presence. It does not measure actual weight-bearing, shoulder contact, knee position, hip alignment or plank form. High-plank target reach and near-floor tracking have not been validated on physical hardware. The outline art and progress UI are prototype visuals; the contact probes are simple visible spheres rather than full rendered hand skeletons.

The prototype has local calibration, not durable room anchors. Palm-up axis, OK pose, finger confidence thresholds and support height may need tuning for the actual headset/runtime. Reroll is intentionally unavailable once a pair has combined. Reset restores the current puzzle's original four values, including an accepted reroll, without resetting active solve/session time.

## Validation

Use the **BODY 24** menu:

- **2. Validate rules** runs exact-math, generation/reroll, selection, hold, lift, tap and score assertions. It writes `Reports/Rules.txt`.
- Enter Play Mode, then **3. Validate live Play Mode**. It injects abstract tracked poses through the real contact engine, exercises calibration, tutorial and session flows, then writes `Reports/LivePlayMode.txt`. Do not operate the simulator during this automated run. Saved tutorial/max-number preferences are restored afterward.

The reports distinguish Editor simulation from physical-device testing. Screenshots in `Reports/` are taken from the running camera, including a simulated plank sightline. The external AI/model services' license/network messages are unrelated to this app; no generated-asset service is required at runtime.

## Android build and device run

Use **BODY 24 > Build Android APK**. This builds only the BODY24 scene to `Builds/Android/Body24.apk` and writes `Reports/AndroidBuild.txt`. Android is the target; IL2CPP/ARM64 and the existing PXR Android loader are preserved. See the build report for the actual outcome.

The delivered development APK built successfully with zero errors. Existing SDK/project dependencies emitted 17 warnings, recorded in `Reports/AndroidBuild.json`; these include PICO spatial-audio libraries without 16 KB alignment, which Unity warns may affect Android 15+ ARM64 devices. This needs target-device verification. No device was connected during this implementation.

With a developer-enabled PICO headset connected and authorized:

```powershell
adb devices -l
adb install -r 'Builds/Android/Body24.apk'
```

Launch the installed app from the headset library. Complete standing calibration and choose clear floor space before getting into a plank. Exit at any time by moving out of the presence zone, then use the pause end button/OK gesture or wait for timeout. The platform system menu remains available.

## Headset acceptance checklist

1. Confirm passthrough, transparent clear, correct floor height, tracking-origin/recenter behavior and MR safety/obstacle protections.
2. Check palm-up calibration and OK recognition with each hand; verify invalid tracking resets holds and grants no bonuses.
3. Check the 5 cm probes near the floor and target legibility from a comfortable plank. Tune lane/target reach and support/lift thresholds before exercising.
4. Run both tutorials, including four alternating tap proxies and fixed 10 → 4 replacement.
5. Verify early/late opposite-hand lift and early return, with and without tracking occlusion. Normal holds must always work without a bonus.
6. Confirm presence tolerates ordinary shifts, pauses on leaving, resumes intact, and ends via OK/button/timeouts. Recenter or recalibrate if the runtime floor moves.
7. Run several Classic puzzles; check thermal/frame stability, reward animations and results. Editor success alone does not establish device performance or exercise safety.
