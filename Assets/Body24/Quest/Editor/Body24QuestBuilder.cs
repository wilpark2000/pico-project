using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Body24.Quest;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.OpenXR;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace Body24.Editor
{
    public static class Body24QuestBuilder
    {
        public const string ScenePath = "Assets/Body24/Quest/Scenes/Body24Quest3.unity";
        const string GraphicsRoot = "Assets/Body24/Quest/Graphics";
        static readonly string[] QuestFeatures =
        {
            "UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature",
            "UnityEngine.XR.Hands.OpenXR.HandTracking",
            "UnityEngine.XR.OpenXR.Features.Meta.ARSessionFeature",
            "UnityEngine.XR.OpenXR.Features.Meta.ARCameraFeature",
            "UnityEngine.XR.OpenXR.Features.Meta.BoundaryVisibilityFeature",
            "UnityEngine.XR.OpenXR.Features.Meta.OpenXRLifeCycleFeature",
            "UnityEngine.XR.OpenXR.Features.CompositionLayers.OpenXRCompositionLayersFeature",
            "UnityEngine.XR.OpenXR.Features.Interactions.OculusTouchControllerProfile",
            "UnityEngine.XR.OpenXR.Features.Interactions.MetaQuestTouchPlusControllerProfile"
        };

        [MenuItem("BODY 24/Quest 3/Create or open Quest scene")]
        public static void CreateScene()
        {
            RequireEditMode();
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt))
                if (scene.isDirty) throw new InvalidOperationException("Save your current scene before creating the Quest copy.");
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh();
            if (!AssetDatabase.CopyAsset(Body24Builder.ScenePath, ScenePath))
                throw new InvalidOperationException("Could not copy the PICO scene.");
            var copy = EditorSceneManager.OpenScene(ScenePath);
            var roots = copy.GetRootGameObjects();
            // Only remove SDK components from this scene instance, never from shared prefabs.
            foreach (var component in roots.SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).ToArray())
                if (component && component.GetType().Namespace != null && component.GetType().Namespace.StartsWith("ByteDance.PICO"))
                { Undo.DestroyObjectImmediate(component); }
            var input = roots.SelectMany(g => g.GetComponentsInChildren<TrackedHands>(true)).Single();
            Undo.AddComponent<QuestOpenXRHands>(input.gameObject).input = input;
            var origin = roots.SelectMany(g => g.GetComponentsInChildren<XROrigin>(true)).Single();
            origin.name = "BODY24 Quest 3 OpenXR Origin";
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            var camera = input.head;
            camera.transform.localPosition = Vector3.zero;
            camera.transform.localRotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.allowHDR = false;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            if (!camera.GetComponent<ARCameraManager>()) Undo.AddComponent<ARCameraManager>(camera.gameObject);
            new GameObject("Quest AR Session", typeof(ARSession), typeof(ARInputManager));
            EditorSceneManager.MarkSceneDirty(copy);
            if (!EditorSceneManager.SaveScene(copy)) throw new IOException("Quest scene could not be saved.");
            CreateGraphicsAssets();
            Debug.Log("Quest scene saved. Original PICO scene and default build scene list were preserved.");
        }

        static UniversalRenderPipelineAsset CreateGraphicsAssets()
        {
            const string path = GraphicsRoot + "/QuestPipeline.asset";
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (existing) return existing;
            var source = (QualitySettings.renderPipeline ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if (!source) throw new InvalidOperationException("Expected the existing URP pipeline.");
            Directory.CreateDirectory(GraphicsRoot);
            AssetDatabase.Refresh();
            var pipeline = UnityEngine.Object.Instantiate(source);
            pipeline.name = "BODY24 Quest Pipeline";
            pipeline.supportsHDR = false;
            var serialized = new SerializedObject(pipeline);
            var renderers = serialized.FindProperty("m_RendererDataList");
            for (int i = 0; i < renderers.arraySize; i++)
            {
                var renderer = UnityEngine.Object.Instantiate(renderers.GetArrayElementAtIndex(i).objectReferenceValue);
                renderer.name = "Quest Renderer " + i;
                var rendererSettings = new SerializedObject(renderer);
                var intermediate = rendererSettings.FindProperty("m_IntermediateTextureMode");
                if (intermediate != null) intermediate.intValue = 0; // Auto
                rendererSettings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(renderer, GraphicsRoot + "/QuestRenderer" + i + ".asset");
                renderers.GetArrayElementAtIndex(i).objectReferenceValue = renderer;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(pipeline, path);
            AssetDatabase.SaveAssets();
            return pipeline;
        }

        [MenuItem("BODY 24/Quest 3/Validate Quest setup")]
        public static void Validate()
        {
            RequireEditMode();
            using (var settings = new QuestBuildSettings())
            {
                var issues = ValidationIssues();
                File.WriteAllLines("Assets/Body24/Quest/Validation.txt", issues);
                Debug.Log(issues.Length == 0 ? "Quest OpenXR validation passed." : string.Join("\n", issues));
                if (issues.Any(i => i.StartsWith("ERROR:"))) throw new BuildFailedException("Quest OpenXR validation failed; see Quest/Validation.txt.");
            }
        }

        static string[] ValidationIssues()
        {
            var issues = new List<OpenXRFeature.ValidationRule>();
            OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
            return issues.Select(i => (i.error ? "ERROR: " : "Warning: ") + i.message).ToArray();
        }

        [MenuItem("BODY 24/Quest 3/Build Quest 3 APK")]
        public static void BuildAndroid()
        {
            RequireEditMode();
            if (!File.Exists(ScenePath)) throw new InvalidOperationException("Create the Quest scene first.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Switch the active build target to Android first.");
            Directory.CreateDirectory("Builds/Quest3");
            using (var settings = new QuestBuildSettings())
            {
                var errors = ValidationIssues().Where(i => i.StartsWith("ERROR:")).ToArray();
                if (errors.Length > 0) throw new BuildFailedException(string.Join("\n", errors));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath }, target = BuildTarget.Android,
                    locationPathName = "Builds/Quest3/Body24Quest3.apk", options = BuildOptions.Development
                });
                File.WriteAllText("Builds/Quest3/BuildReport.txt", report.summary.result + "\nErrors: " + report.summary.totalErrors +
                    "\nWarnings: " + report.summary.totalWarnings + "\n" + report.summary.outputPath);
                if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Quest APK build failed. See Console and Builds/Quest3/BuildReport.txt.");
                Debug.Log("Quest APK built: " + report.summary.outputPath);
            }
        }

        static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Exit Play mode and wait for any current build to finish.");
        }

        // Global XR settings cannot be selected by scene. Apply only for this build,
        // then restore all overridden settings even when validation/building fails.
        sealed class QuestBuildSettings : IDisposable
        {
            readonly Dictionary<UnityEngine.Object, string> snapshots = new Dictionary<UnityEngine.Object, string>();
            readonly string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            readonly AndroidSdkVersions minSdk = PlayerSettings.Android.minSdkVersion;
            readonly AndroidArchitecture architectures = PlayerSettings.Android.targetArchitectures;
            readonly AndroidApplicationEntry entry = PlayerSettings.Android.applicationEntry;
            readonly ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
            readonly bool autoGraphics = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
            readonly GraphicsDeviceType[] graphics = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            readonly bool bundle = EditorUserBuildSettings.buildAppBundle;
            readonly RenderPipelineAsset pipeline = GraphicsSettings.defaultRenderPipeline;
            readonly int qualityLevel = QualitySettings.GetQualityLevel();
            readonly RenderPipelineAsset[] qualityPipelines = QualitySettings.names.Select((_, i) => QualitySettings.GetRenderPipelineAssetAt(i)).ToArray();

            void Capture(UnityEngine.Object obj) { if (obj && !snapshots.ContainsKey(obj)) snapshots.Add(obj, EditorJsonUtility.ToJson(obj)); }

            public QuestBuildSettings()
            {
                try
                {
                    var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
                    if (!general || !general.Manager) throw new InvalidOperationException("Android XR settings are missing.");
                    var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
                    Capture(general); Capture(general.Manager); Capture(openxr);
                    var features = openxr.GetFeatures<OpenXRFeature>();
                    foreach (var feature in features) Capture(feature);
                    foreach (var required in QuestFeatures)
                        if (!features.Any(f => f.GetType().FullName == required)) throw new InvalidOperationException("Missing OpenXR feature: " + required);
                    foreach (var feature in features)
                        feature.enabled = QuestFeatures.Contains(feature.GetType().FullName);
                    // Meta handles startup retries until passthrough is visible. This
                    // optional Meta feature is not used by the native PICO loader.
                    var boundary = openxr.GetFeature<UnityEngine.XR.OpenXR.Features.Meta.BoundaryVisibilityFeature>();
                    var boundarySettings = new SerializedObject(boundary);
                    var suppressVisibility = boundarySettings.FindProperty("m_SuppressVisibility");
                    if (suppressVisibility == null) throw new InvalidOperationException("Meta boundary suppression setting is unavailable.");
                    suppressVisibility.boolValue = true;
                    boundarySettings.ApplyModifiedPropertiesWithoutUndo();
                    var loader = AssetDatabase.FindAssets("t:OpenXRLoader").Select(AssetDatabase.GUIDToAssetPath)
                        .Select(AssetDatabase.LoadAssetAtPath<OpenXRLoader>).FirstOrDefault(l => l);
                    if (!loader || !general.Manager.TrySetLoaders(new List<XRLoader> { loader }))
                        throw new InvalidOperationException("Could not assign the OpenXR loader.");
                    general.InitManagerOnStart = true;
                    openxr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
                    PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, id + ".quest3");
                    PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
                    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                    PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
                    PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                    PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                    PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
                    EditorUserBuildSettings.buildAppBundle = false;
                    var questPipeline = CreateGraphicsAssets();
                    GraphicsSettings.defaultRenderPipeline = questPipeline;
                    for (int i = 0; i < qualityPipelines.Length; i++)
                    { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = questPipeline; }
                    QualitySettings.SetQualityLevel(qualityLevel, false);
                    foreach (var obj in snapshots.Keys) EditorUtility.SetDirty(obj);
                    AssetDatabase.SaveAssets();
                }
                catch { Dispose(); throw; }
            }

            public void Dispose()
            {
                foreach (var snapshot in snapshots)
                { EditorJsonUtility.FromJsonOverwrite(snapshot.Value, snapshot.Key); EditorUtility.SetDirty(snapshot.Key); }
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, id);
                PlayerSettings.Android.minSdkVersion = minSdk;
                PlayerSettings.Android.targetArchitectures = architectures;
                PlayerSettings.Android.applicationEntry = entry;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, backend);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, graphics);
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, autoGraphics);
                EditorUserBuildSettings.buildAppBundle = bundle;
                GraphicsSettings.defaultRenderPipeline = pipeline;
                for (int i = 0; i < qualityPipelines.Length; i++)
                { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = qualityPipelines[i]; }
                QualitySettings.SetQualityLevel(qualityLevel, false);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
