# BODY 24 on Quest 3

Open `Assets/Body24/Quest/Scenes/Body24Quest3.unity` for the Quest version.
The original `Assets/Body24/Scenes/Body24.unity` remains the PICO scene.

## Build

Use **BODY 24 > Quest 3 > Build Quest 3 APK** with Android as the active build target.
Output: `Builds/Quest3/Body24Quest3.apk`.

Use this command rather than the default Build button: XR loader selection is a
project setting, not a scene setting. The command temporarily selects OpenXR,
Meta Quest support, XR Hands, AR Session, and Camera (Passthrough), and the separate
Quest URP assets. It builds only the Quest scene, then restores the previous
Android XR, player, graphics, and quality settings, including on a failed build.
The default build scene list and the original PICO build command are unchanged.
The Quest application identifier adds `.quest3` to the existing identifier.

**BODY 24 > Quest 3 > Validate Quest setup** checks OpenXR configuration with the
same temporary settings. Results are saved to `Quest/Validation.txt`.
Unity recommends GameActivity; Activity is retained for this build because the
installed PICO SDK's build validator requires it. OpenXR accepts Activity with a warning.

## Input and scene

`QuestOpenXRHands` feeds joint data into the existing `TrackedHands.Inject` API.
The shared puzzle, calibration, palm selection, OK gesture, and UI behavior remain
in the existing gameplay scripts. The shared input script skips its PICO startup
when an external input provider owns it. The original PICO scene uses its existing
input path. The Quest copy has no PICO components; it has AR Session/AR Input
Manager and an AR Camera Manager for passthrough instead.

Editor keyboard/mouse simulation remains available in the Quest scene. This
simulation does not verify device tracking or passthrough. For on-device testing,
enable Quest developer mode/USB debugging, install the APK, enable hand tracking,
and test calibration, palms-up detection, OK gestures, floor alignment, tracking
loss/recovery, and passthrough visibility on Quest 3.

## Dependencies

Uses Unity OpenXR, Unity XR Hands, Unity OpenXR Meta, and the latter's AR Foundation
and Composition Layers dependencies. PICO SDK packages remain installed for PICO.
Quest rendering assets are copies, so PICO rendering assets are not edited.

## Quest boundary visibility

The Quest Android OpenXR configuration enables **Meta Quest: Boundary Visibility**
with **Suppress Visibility**. Unity's Meta feature requests suppression at startup
and retries while passthrough becomes visible. Meta's runtime decides whether the
request is allowed; this does not disable the headset's boundary globally.
The Quest build helper preserves this configuration. PICO's native loader does
not initialize this Meta OpenXR feature, and PICO scene/settings assets are unchanged.
