using System;
using System.IO;
using System.Linq;
using Body24;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.EventSystems;
using Unity.XR.CoreUtils;

namespace Body24.Editor
{
    public static class Body24Builder
    {
        const string Root = "Assets/Body24";
        public const string ScenePath = Root + "/Scenes/Body24.unity";
        static TMP_FontAsset font;
        static Body24Config config;
        static Sprite disk, handIcon, okIcon;
        static string PrefabPath(string name) => Root + "/Prefabs/" + name + ".prefab";
        [MenuItem("BODY 24/1. Create prototype scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before building the scene.");
            foreach (string folder in new[] { "Prefabs", "Scenes", "Art", "Reports" }) Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh();
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); Debug.Log("BODY24 scene already exists; opened without overwriting it."); return; }
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (!font) throw new InvalidOperationException("Import TMP Essential Resources first using BODY 24/Import text resources.");
            config = AssetDatabase.LoadAssetAtPath<Body24Config>(Root + "/Body24Config.asset");
            if (!config) { config = ScriptableObject.CreateInstance<Body24Config>(); AssetDatabase.CreateAsset(config, Root + "/Body24Config.asset"); }
            disk = MakeIcon("ProgressDisk", 0); handIcon = MakeIcon("OpenHand", 1); okIcon = MakeIcon("OKHand", 2);
            MakeProgressPrefabs();
            MakeTargetPrefab("NumberCard", 180, 160, 61);
            MakeTargetPrefab("OperatorButton", 145, 160, 66);
            MakeTargetPrefab("GroundActionButton", 570, 120, 35);
            MakeTargetPrefab("ResetButton", 330, 70, 25);
            MakeTextPrefab("Countdown", 90, 850, 140);
            MakeTextPrefab("ShoulderTapPrompt", 23, 900, 65);
            MakeTextPrefab("ScorePopup", 42, 450, 95);
            MakeTextPrefab("SessionResults", 30, 800, 430);
            MakeGuidePrefab(); MakeInstructionPrefab(); MakeOKPrefab();
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.isDirty) EditorSceneManager.SaveScene(scene);
            // Save a copy of the existing scene, preserving its original camera and render configuration.
            EditorSceneManager.SaveScene(scene, ScenePath, true);
            scene = EditorSceneManager.OpenScene(ScenePath);
            var camera = Camera.main;
            if (!camera) { var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); go.tag = "MainCamera"; camera = go.GetComponent<Camera>(); }
            var rig = new GameObject("BODY24 XR Origin"); Undo.RegisterCreatedObjectUndo(rig, "Create BODY24 rig");
            var offset = new GameObject("Camera Offset"); offset.transform.SetParent(rig.transform, false);
            camera.transform.SetParent(offset.transform, false); camera.transform.localPosition = new Vector3(0, 1.7f, -.12f);
            camera.transform.localRotation = Quaternion.Euler(40, 0, 0); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0, 0, 0, 0); camera.nearClipPlane = .05f; camera.fieldOfView = 75;
            var origin = rig.AddComponent<XROrigin>(); origin.Camera = camera; origin.CameraFloorOffsetObject = offset; origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            var driver = camera.gameObject.GetComponent<TrackedPoseDriver>() ?? camera.gameObject.AddComponent<TrackedPoseDriver>();
            driver.positionInput = new InputActionProperty(new InputAction("HMD Position", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            driver.rotationInput = new InputActionProperty(new InputAction("HMD Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
#if ENABLE_PICO_XR_SDK
            rig.AddComponent<ByteDance.PICO.XR.PXR_Manager>();
#endif
            var appRoot = new GameObject("BODY24 Application");
            var app = appRoot.AddComponent<Body24App>(); app.config = config;
            var input = appRoot.AddComponent<TrackedHands>(); input.config = config; input.head = camera; input.trackingSpace = offset.transform;
            var interaction = appRoot.AddComponent<HandContactController>(); interaction.config = config; interaction.input = input;
            app.input = input; app.contact = interaction;
            var frame = new GameObject("Floor Frame").transform; app.frame = frame; input.playFrame = frame;
            var canvas = Canvas("Ground Canvas", frame, new Vector2(1300, 1800));
            canvas.transform.localPosition = new Vector3(0, .018f, 0); canvas.transform.localRotation = Quaternion.Euler(90, 0, 0); canvas.sortingOrder = 10;
            app.title = Label(canvas.transform, "Brand", "BODY <color=#64FFC0>24</color>", new Vector2(0, 1470), new Vector2(1100, 120), 78);
            app.title.fontStyle = FontStyles.Bold;
            Label(canvas.transform, "Eyebrow", "CORE / CLASSIC", new Vector2(0, 1560), new Vector2(700, 40), 21).color = config.mint;
            app.instruction = Label(canvas.transform, "Instruction", "MIND IN FOCUS. BODY IN BALANCE.", new Vector2(0, 1300), new Vector2(1050, 150), 30);
            app.timerLabel = Label(canvas.transform, "ActiveTimer", "", new Vector2(0, 1130), new Vector2(800, 65), 37);
            app.scoreLabel = Label(canvas.transform, "Score", "", new Vector2(0, 1045), new Vector2(950, 60), 29);
            app.reset = InstanceTarget("ResetButton", canvas.transform, new Vector2(0, 940));
            app.cards = new HoldTarget[4]; app.operators = new HoldTarget[4];
            for (int i = 0; i < 4; i++) app.cards[i] = InstanceTarget("NumberCard", canvas.transform, new Vector2(i % 2 == 0 ? -98 : 98, i < 2 ? 750 : 550));
            string[] symbols = { "+", "−", "×", "÷" };
            for (int i = 0; i < 4; i++) { app.operators[i] = InstanceTarget("OperatorButton", canvas.transform, new Vector2(i < 2 ? -440 : 440, i % 2 == 0 ? 750 : 550)); app.operators[i].SetLabel(symbols[i], "OPERATOR"); }
            app.actions = new HoldTarget[8];
            for (int i = 0; i < 8; i++) { app.actions[i] = InstanceTarget("GroundActionButton", canvas.transform, new Vector2(0, 740 - i * 160)); app.actions[i].gameObject.SetActive(false); }
            app.tapLabel = InstanceText("ShoulderTapPrompt", canvas.transform, new Vector2(0, 385));
            app.footer = Label(canvas.transform, "Footer", "Hold to select", new Vector2(0, 120), new Vector2(1100, 100), 20);
            app.footer.color = new Color(.65f, .79f, .8f);
            app.countdown = InstanceText("Countdown", canvas.transform, new Vector2(0, 1030));
            app.resultsLabel = InstanceText("SessionResults", canvas.transform, new Vector2(0, 760));
            app.reward = InstanceText("ScorePopup", canvas.transform, new Vector2(0, 650)); app.reward.color = config.gold; app.reward.gameObject.SetActive(false);
            var ok = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("OKGesturePrompt")), canvas.transform);
            ok.transform.localPosition = new Vector3(0, 570, 0); app.okProgress = ok.transform.GetChild(1).GetComponent<UnityEngine.UI.Image>();
            app.okPrompt = ok;
            app.guides = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("PlayzoneOutline")), frame)).GetComponent<PlayzoneView>();
            app.guides.Layout(1.7f, .57f, .26f);
            app.instructionPanel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("InstructionPanel")));
            var snap = app.instructionPanel.GetComponent<SnapInstructionPanel>(); snap.head = camera.transform; snap.config = config;
            app.floatingInstruction = app.instructionPanel.GetComponentInChildren<TMP_Text>();
            input.probeVisuals = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Sphere); probe.name = i == 0 ? "LeftPalmProbe" : "RightPalmProbe";
                probe.transform.SetParent(appRoot.transform); probe.transform.localScale = Vector3.one * (config.probeRadius * 2);
                UnityEngine.Object.DestroyImmediate(probe.GetComponent<Collider>());
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); mat.color = i == 0 ? config.mint : new Color(.3f, .7f, 1);
                AssetDatabase.CreateAsset(mat, Root + "/Art/" + probe.name + ".mat"); probe.GetComponent<Renderer>().sharedMaterial = mat;
                input.probeVisuals[i] = probe.transform; probe.SetActive(false);
            }
            interaction.targets = app.cards.Concat(app.operators).Concat(app.actions).Concat(new[] { app.reset }).ToArray();
            if (!UnityEngine.Object.FindAnyObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            // Editor floor is hidden on the headset, where real-floor passthrough remains visible.
            var preview = GameObject.CreatePrimitive(PrimitiveType.Plane); preview.name = "Editor Floor Preview"; preview.transform.position = new Vector3(0, -.015f, 1);
            preview.AddComponent<EditorFloorPreview>(); var floorMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); floorMat.color = new Color(.055f, .09f, .105f); AssetDatabase.CreateAsset(floorMat, Root + "/Art/EditorFloor.mat"); preview.GetComponent<Renderer>().sharedMaterial = floorMat;
            appRoot.AddComponent<EditorSimulatorOverlay>().input = input;
            app.LayoutGameplay();
            EditorUtility.SetDirty(app); EditorUtility.SetDirty(input); EditorUtility.SetDirty(interaction);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var existing = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList(); existing.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = existing.ToArray();
            AssetDatabase.SaveAssets(); Selection.activeGameObject = appRoot;
            Debug.Log("BODY24 prototype scene and reusable prefabs created and saved: " + ScenePath);
        }
        [MenuItem("BODY 24/Import text resources")]
        public static void ImportText() { TMP_PackageResourceImporter.ImportResources(true, false, false); }
        static Canvas Canvas(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = size; rect.pivot = new Vector2(.5f, 0);
            go.transform.localScale = Vector3.one * .001f;
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            return canvas;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0); rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        static UnityEngine.UI.Image Image(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        { var rect = Rect(name, parent, position, size); var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return image; }
        static TMP_Text Label(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize)
        {
            var rect = Rect(name, parent, position, size); var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.text = text; label.fontSize = fontSize; label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.Normal; return label;
        }
        static void MakeTargetPrefab(string name, float width, float height, float size)
        {
            var root = Rect(name, null, Vector2.zero, new Vector2(width, height));
            var target = root.gameObject.AddComponent<HoldTarget>(); target.surface = root;
            target.border = root.gameObject.AddComponent<UnityEngine.UI.Image>(); target.border.color = new Color(.22f, .38f, .43f); target.border.raycastTarget = true;
            target.background = Image("Surface", root, new Vector2(0, height / 2), new Vector2(width - 6, height - 6), config.ink);
            var fillObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("HoldProgress")), root);
            target.progress = fillObject.GetComponent<UnityEngine.UI.Image>(); ((RectTransform)fillObject.transform).anchoredPosition = new Vector2(0, height / 2); ((RectTransform)fillObject.transform).sizeDelta = new Vector2(width - 6, height - 6);
            target.label = Label(root, "Value", name, new Vector2(0, height * .59f), new Vector2(width - 16, height * .62f), size); target.label.fontStyle = FontStyles.Bold;
            target.caption = Label(root, "Caption", "HOLD 1 SEC", new Vector2(0, height * .19f), new Vector2(width - 16, height * .22f), name == "ResetButton" ? 12 : 17); target.caption.color = config.mint;
            target.hitVolume = root.gameObject.AddComponent<BoxCollider>(); target.hitVolume.size = new Vector3(width, height, 60); target.hitVolume.isTrigger = true;
            // Child anchors are bottom-centred; collider remains centred on the visual root.
            target.hitVolume.center = Vector3.zero;
            target.normalColor = config.ink;
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath(name)); UnityEngine.Object.DestroyImmediate(root.gameObject);
        }
        static void MakeProgressPrefabs()
        {
            var image = Image("HoldProgress", null, Vector2.zero, new Vector2(170, 150), new Color(.2f, .9f, .6f, .4f)); image.sprite = disk; image.type = UnityEngine.UI.Image.Type.Filled; image.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; image.fillAmount = 0;
            PrefabUtility.SaveAsPrefabAsset(image.gameObject, PrefabPath("HoldProgress")); UnityEngine.Object.DestroyImmediate(image.gameObject);
            image = Image("SelectionHighlight", null, Vector2.zero, new Vector2(180, 160), config.mint);
            PrefabUtility.SaveAsPrefabAsset(image.gameObject, PrefabPath("SelectionHighlight")); UnityEngine.Object.DestroyImmediate(image.gameObject);
        }
        static void MakeTextPrefab(string name, float size, float width, float height)
        { var text = Label(null, name, "", Vector2.zero, new Vector2(width, height), size); PrefabUtility.SaveAsPrefabAsset(text.gameObject, PrefabPath(name)); UnityEngine.Object.DestroyImmediate(text.gameObject); }
        static HoldTarget InstanceTarget(string name, Transform parent, Vector2 position)
        { var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name)), parent); ((RectTransform)go.transform).anchoredPosition = position; return go.GetComponent<HoldTarget>(); }
        static TMP_Text InstanceText(string name, Transform parent, Vector2 position)
        { var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name)), parent); ((RectTransform)go.transform).anchoredPosition = position; return go.GetComponent<TMP_Text>(); }
        static void MakeGuidePrefab()
        {
            var go = new GameObject("PlayzoneOutline"); var view = go.AddComponent<PlayzoneView>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.color = new Color(.2f, .6f, .52f, .45f);
            material.SetFloat("_Surface", 1); material.SetFloat("_SrcBlend", 5); material.SetFloat("_DstBlend", 10); material.SetFloat("_ZWrite", 0); material.renderQueue = 3000; material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            AssetDatabase.CreateAsset(material, Root + "/Art/Guides.mat");
            LineRenderer Line(string name, float width) { var child = new GameObject(name); child.transform.SetParent(go.transform, false); var line = child.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.sharedMaterial = material; line.startWidth = line.endWidth = width; line.numCapVertices = 4; line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return line; }
            view.outline = Line("Boundary", .006f); view.centre = Line("Centre", .004f); view.leftLane = Line("LeftLane", .045f); view.rightLane = Line("RightLane", .045f); view.Layout(1.7f, .57f, .26f);
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath("PlayzoneOutline")); UnityEngine.Object.DestroyImmediate(go);
        }
        static void MakeInstructionPrefab()
        {
            var canvas = Canvas("InstructionPanel", null, new Vector2(600, 330)); canvas.sortingOrder = 20; canvas.GetComponent<RectTransform>().pivot = new Vector2(.5f, .5f); canvas.transform.localScale = Vector3.one * .0007f;
            var bg = Image("Panel", canvas.transform, new Vector2(0, 165), new Vector2(600, 330), config.ink);
            Label(canvas.transform, "Prompt", "Stand tall\nArms forward · palms up", new Vector2(0, 205), new Vector2(550, 180), 30);
            var left = Image("LeftHandOutline", canvas.transform, new Vector2(-75, 70), new Vector2(90, 90), config.mint); left.sprite = handIcon;
            var right = Image("RightHandOutline", canvas.transform, new Vector2(75, 70), new Vector2(90, 90), config.mint); right.sprite = handIcon;
            canvas.gameObject.AddComponent<SnapInstructionPanel>(); PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, PrefabPath("InstructionPanel")); UnityEngine.Object.DestroyImmediate(canvas.gameObject);
        }
        static void MakeOKPrefab()
        {
            var root = Rect("OKGesturePrompt", null, Vector2.zero, new Vector2(150, 150));
            var track = Image("Track", root, new Vector2(0, 75), new Vector2(150, 150), new Color(.1f, .25f, .26f)); track.sprite = disk;
            var fill = Image("Progress", root, new Vector2(0, 75), new Vector2(150, 150), config.mint); fill.sprite = disk; fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360; fill.fillAmount = 0;
            var icon = Image("OKOutline", root, new Vector2(0, 75), new Vector2(110, 110), Color.white); icon.sprite = okIcon;
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath("OKGesturePrompt")); UnityEngine.Object.DestroyImmediate(root.gameObject);
        }
        static Sprite MakeIcon(string name, int shape)
        {
            int size = 128; var tex = new Texture2D(size, size, TextureFormat.RGBA32, false); var pixels = new Color[size * size];
            void Dot(int x, int y) { for (int dx = -2; dx <= 2; dx++) for (int dy = -2; dy <= 2; dy++) if (x + dx >= 0 && x + dx < size && y + dy >= 0 && y + dy < size) pixels[(y + dy) * size + x + dx] = Color.white; }
            void Segment(int x, int y, int xx, int yy) { int steps = Math.Max(Math.Abs(xx - x), Math.Abs(yy - y)); for (int t = 0; t <= steps; t++) Dot((int)Mathf.Lerp(x, xx, steps == 0 ? 0 : (float)t / steps), (int)Mathf.Lerp(y, yy, steps == 0 ? 0 : (float)t / steps)); }
            if (shape == 0) { for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) if ((x - 64) * (x - 64) + (y - 64) * (y - 64) < 61 * 61) pixels[y * size + x] = Color.white; }
            else if (shape == 1)
            {
                int[,] path = { { 43, 10 }, { 30, 37 }, { 13, 61 }, { 14, 72 }, { 25, 70 }, { 37, 55 }, { 34, 96 }, { 41, 102 }, { 48, 96 }, { 49, 59 }, { 50, 114 }, { 58, 119 }, { 65, 112 }, { 65, 62 }, { 69, 105 }, { 77, 110 }, { 84, 102 }, { 81, 60 }, { 88, 91 }, { 96, 95 }, { 102, 86 }, { 96, 41 }, { 84, 10 } };
                for (int i = 1; i < path.GetLength(0); i++) Segment(path[i - 1, 0], path[i - 1, 1], path[i, 0], path[i, 1]); Segment(84, 10, 43, 10);
            }
            else
            {
                for (int a = 0; a < 360; a++) Dot(43 + (int)(22 * Mathf.Cos(a * Mathf.Deg2Rad)), 60 + (int)(22 * Mathf.Sin(a * Mathf.Deg2Rad)));
                Segment(57, 77, 64, 112); Segment(64, 112, 75, 110); Segment(75, 110, 77, 70); Segment(77, 70, 86, 103); Segment(86, 103, 97, 98); Segment(97, 98, 93, 57); Segment(93, 57, 104, 80); Segment(104, 80, 114, 75); Segment(114, 75, 102, 31); Segment(102, 31, 85, 13); Segment(85, 13, 42, 13); Segment(42, 13, 26, 43);
            }
            tex.SetPixels(pixels); tex.Apply(); string pathName = Root + "/Art/" + name + ".png"; File.WriteAllBytes(pathName, tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(pathName); var importer = (TextureImporter)AssetImporter.GetAtPath(pathName); importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(pathName);
        }
        [MenuItem("BODY 24/Refresh visual assets")]
        public static void RefreshVisualAssets()
        {
            disk = MakeIcon("ProgressDisk", 0); handIcon = MakeIcon("OpenHand", 1); okIcon = MakeIcon("OKHand", 2);
            foreach (string name in new[] { "HoldProgress", "InstructionPanel", "OKGesturePrompt" })
            {
                var root = PrefabUtility.LoadPrefabContents(PrefabPath(name));
                foreach (var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                {
                    if (image.name == "HoldProgress" || image.name == "Progress" || image.name == "Track") image.sprite = disk;
                    if (image.name.Contains("HandOutline")) image.sprite = handIcon;
                    if (image.name == "OKOutline") image.sprite = okIcon;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(name)); PrefabUtility.UnloadPrefabContents(root);
            }
            Camera.main.fieldOfView = 75;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
        }
        [MenuItem("BODY 24/Build Android APK")]
        public static void BuildAndroid()
        {
            Body24PicoSetup.Configure();
            Directory.CreateDirectory("Builds/Android");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, target = BuildTarget.Android, locationPathName = "Builds/Android/Body24.apk", options = BuildOptions.Development });
            File.WriteAllText(Root + "/Reports/AndroidBuild.txt", report.summary.result + "\nErrors: " + report.summary.totalErrors + "\nWarnings: " + report.summary.totalWarnings + "\n" + report.summary.outputPath);
            Debug.Log("BODY24 Android build: " + report.summary.result);
        }
    }
}
