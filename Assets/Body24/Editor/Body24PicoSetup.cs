using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ByteDance.PICO.XR;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

namespace Body24.Editor
{
    /// <summary>Configures the native PICO Android target; never starts a build.</summary>
    public static class Body24PicoSetup
    {
        const string GraphicsRoot = "Assets/Body24/Pico/Graphics";

        [MenuItem("BODY 24/PICO/Configure PICO Android")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Exit Play mode and wait for any current build.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Select Android as the active build target first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before switching build configuration.");

            var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            var loader = AssetDatabase.LoadAssetAtPath<PXR_Loader>("Assets/XR/Loaders/PXR_Loader.asset");
            if (!general || !general.Manager || !loader)
                throw new InvalidOperationException("The existing PICO XR loader/settings are missing.");
            if (!general.Manager.TrySetLoaders(new List<XRLoader> { loader }))
                throw new InvalidOperationException("Could not assign the native PICO XR loader.");
            general.InitManagerOnStart = true;
            EditorUtility.SetDirty(general); EditorUtility.SetDirty(general.Manager);

            // OpenXR remains installed for Quest. Its native loader and extensions
            // must not be active in this PICO-native Android configuration.
            var openxr = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            foreach (var feature in openxr.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>())
            { feature.enabled = false; EditorUtility.SetDirty(feature); }

            var pico = AssetDatabase.LoadAssetAtPath<PXR_Settings>("Assets/XR/Settings/PXR_Settings.asset");
            pico.appMode = PXR_Settings.AppMode.XR;
            pico.stereoRenderingModeAndroid = PXR_Settings.StereoRenderingModeAndroid.Multiview;
            pico.systemDisplayFrequency = PXR_Settings.SystemDisplayFrequency.Default;
            pico.optimizeBufferDiscards = true;
            pico.enableAppSpaceWarp = false;
            EditorUtility.SetDirty(pico);
            var project = PXR_ProjectSetting.GetProjectConfig();
            project.handTracking = true;
            project.videoSeeThrough = true;
            project.openMRC = true;
            project.stageMode = false;
            project.isSpatialAdapter = false;
            EditorUtility.SetDirty(project);

            string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            while (id.EndsWith(".quest3", StringComparison.Ordinal)) id = id.Substring(0, id.Length - 7);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, id);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.runInBackground = true;
            PlayerSettings.graphicsJobs = false;
            PlayerSettings.use32BitDisplayBuffer = true;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            EditorUserBuildSettings.buildAppBundle = false;

            var pipeline = EnsurePipeline();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int quality = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = pipeline; QualitySettings.pixelLightCount = 1; }
            QualitySettings.SetQualityLevel(quality, false);

            var scene = EditorSceneManager.OpenScene(Body24Builder.ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var app = roots.SelectMany(g => g.GetComponentsInChildren<Body24App>(true)).Single();
            if (roots.SelectMany(g => g.GetComponentsInChildren<Body24.Quest.QuestOpenXRHands>(true)).Any())
                throw new InvalidOperationException("The PICO scene unexpectedly contains the Quest input provider.");
            var manager = roots.SelectMany(g => g.GetComponentsInChildren<PXR_Manager>(true)).Single();
            Undo.RecordObject(manager, "Configure PICO native scene");
            Undo.RecordObject(app.input, "Use native PICO hand input");
            Undo.RecordObject(app.input.head, "Transparent PICO passthrough camera");
            manager.enabled = true;
            app.input.useInjectedInput = false;
            app.input.head.clearFlags = CameraClearFlags.SolidColor;
            app.input.head.backgroundColor = Color.clear;
            app.input.head.allowHDR = false;
            var cameraData = app.input.head.GetUniversalAdditionalCameraData();
            Undo.RecordObject(cameraData, "Disable passthrough camera post-processing");
            cameraData.renderPostProcessing = false;
            var origin = roots.SelectMany(g => g.GetComponentsInChildren<XROrigin>(true)).Single();
            Undo.RecordObject(origin, "PICO floor tracking");
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            Undo.FlushUndoRecordObjects();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the PICO scene.");
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != Body24Builder.ScenePath)
                .Select(s => new EditorBuildSettingsScene(s.path, false)).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(Body24Builder.ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Body24.Editor.QuestOpenXRPluginFilter.Register();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = app.gameObject;
            Debug.Log("PICO Android configured: native PICO XR, XR mode, hand tracking, passthrough, ARM64/IL2CPP, Vulkan, and PICO scene only. No build started.");
        }

        static UniversalRenderPipelineAsset EnsurePipeline()
        {
            string path = GraphicsRoot + "/PicoPipeline.asset";
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (existing) return existing;
            var source = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Body24/Quest/Graphics/QuestPipeline.asset");
            if (!source) source = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            if (!source) throw new InvalidOperationException("The existing mobile URP pipeline is missing.");
            Directory.CreateDirectory(GraphicsRoot); AssetDatabase.Refresh();
            var pipeline = UnityEngine.Object.Instantiate(source);
            pipeline.name = "PicoPipeline";
            pipeline.supportsHDR = false;
            var settings = new SerializedObject(pipeline);
            var renderers = settings.FindProperty("m_RendererDataList");
            for (int i = 0; i < renderers.arraySize; i++)
            {
                var renderer = UnityEngine.Object.Instantiate(renderers.GetArrayElementAtIndex(i).objectReferenceValue);
                renderer.name = "PicoRenderer" + i;
                AssetDatabase.CreateAsset(renderer, GraphicsRoot + "/PicoRenderer" + i + ".asset");
                renderers.GetArrayElementAtIndex(i).objectReferenceValue = renderer;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(pipeline, path);
            return pipeline;
        }
    }
}
