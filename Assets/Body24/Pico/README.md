# BODY 24: PICO Android target

Scene: `Assets/Body24/Scenes/Body24.unity`.

The scene uses the same shared gameplay, placement, calibration UI, and hand
capsule interaction scripts as the Quest version. It retains `PXR_Manager` and
native PICO hand input; it does not use the Quest hand provider or Meta AR camera.

The current project settings select:

- Android with only the PICO scene enabled in the build scene list.
- Native PICO XR loader; PICO app mode **XR**, with Multiview rendering.
- PICO hand tracking, passthrough, floor tracking, and MRC.
- ARM64, IL2CPP, Vulkan, Activity entry point, and minimum Android API 29.
- Separate PICO URP assets with HDR disabled and a transparent camera.
- PICO's loader AAR included; Unity OpenXR's loader AAR excluded.

Use Unity's normal **Build** button to create a test APK. No build was run while
applying this configuration. **BODY 24 > PICO > Configure PICO Android** reapplies
the settings without building. The existing **BODY 24 > Build Android APK** command
also applies this configuration before building when you explicitly select it.

Quest's scene, packages, and rendering assets are retained. Its dedicated build
helper can select the Quest configuration and restore the previous PICO settings.
The previous project settings are backed up under `Backups/BeforePicoConfiguration`.

## Validation and device testing

The installed PICO SDK's build-blocking configuration checks pass. Its full
Project Validation UI additionally requests a custom publishing keystore/key;
those have not been created or changed. Local test APKs retain Unity's default
debug signing. Configure your own release signing before distribution.

The installed PICO XR package is version 6.1.1. Actual device compatibility and
minimum firmware must be checked for the intended headset model. No PICO device
runtime test was performed, and this setup does not claim support for every PICO
model. In particular, this experience needs working hand tracking and passthrough.
