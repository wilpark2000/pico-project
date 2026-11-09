using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Body24.UIEffects;

namespace Body24.Editor
{
    /// <summary>Isolated GPU preview and regression checks; does not edit the active scene.</summary>
    public static class UIEffectsValidation
    {
        const string DirectoryPath = "Assets/Body24/UIEffects";
        static readonly List<string> checks = new List<string>();
        static void Check(bool value, string message)
        {
            if (!value) throw new Exception("UI effects: " + message);
            checks.Add("PASS: " + message);
        }

        [MenuItem("BODY 24/Validate UI effects")]
        public static void Run()
        {
            checks.Clear();
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("UI Effects Preview");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            Texture2D texture = null, output = null;
            Sprite sprite = null;
            RenderTexture target = null;
            Material glow = null, voronoi = null;
            var previousTarget = RenderTexture.active;
            var previousTime = Shader.GetGlobalVector("_Time");
            try
            {
                var glowShader = AssetDatabase.LoadAssetAtPath<Shader>(DirectoryPath + "/UIShapeGlow.shader");
                var voronoiShader = AssetDatabase.LoadAssetAtPath<Shader>(DirectoryPath + "/UIAnimatedVoronoi.shader");
                Check(glowShader && !ShaderUtil.ShaderHasError(glowShader) && glowShader.isSupported, "Glow shader compiles and is supported");
                Check(voronoiShader && !ShaderUtil.ShaderHasError(voronoiShader) && voronoiShader.isSupported, "Voronoi shader compiles and is supported");
                glow = new Material(glowShader); voronoi = new Material(voronoiShader);
                CreateMaterial("UIShapeGlow", glowShader); CreateMaterial("UIAnimatedVoronoi", voronoiShader);

                var cameraObject = new GameObject("Preview Camera", typeof(Camera)); cameraObject.transform.SetParent(root.transform);
                var camera = cameraObject.GetComponent<Camera>();
                camera.scene = scene; camera.orthographic = true; camera.orthographicSize = 320;
                camera.transform.position = new Vector3(0, 0, -1000);
                camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.012f,.022f,.035f,1);
                camera.enabled = false;
                target = new RenderTexture(1024, 640, 24, RenderTextureFormat.ARGB32);
                target.Create(); camera.targetTexture = target;
                var canvasObject = new GameObject("Preview Canvas", typeof(RectTransform), typeof(Canvas)); canvasObject.transform.SetParent(root.transform);
                var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
                canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(1024,640);

                // A rounded alpha shape, with a transparent hole: exercises shape rather than bounds.
                texture = new Texture2D(128,128,TextureFormat.RGBA32,false);
                texture.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color[128*128];
                for (int y=0; y<128; y++) for (int x=0; x<128; x++)
                {
                    var p = new Vector2(x-63.5f,y-63.5f);
                    var q = new Vector2(Mathf.Abs(p.x)-42, Mathf.Abs(p.y)-42);
                    float rounded = new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude + Mathf.Min(Mathf.Max(q.x,q.y),0) - 18;
                    float alpha = Mathf.Clamp01(.5f-rounded) * Mathf.Clamp01(p.magnitude-13.5f);
                    pixels[y*128+x] = new Color(.2f,.45f,.6f,alpha);
                }
                texture.SetPixels(pixels); texture.Apply();
                sprite = Sprite.Create(texture,new Rect(0,0,128,128),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(24,24,24,24));
                var first = Image(canvas.transform,"Glow PPU 1",sprite,glow,new Vector2(-260,170),new Vector2(360,140));
                first.type = UnityEngine.UI.Image.Type.Sliced; first.pixelsPerUnitMultiplier=1;
                var firstGlow = first.gameObject.AddComponent<UIShapeGlow>(); firstGlow.Radius=18; firstGlow.Samples=64;
                var second = Image(canvas.transform,"Glow PPU 2",sprite,glow,new Vector2(235,170),new Vector2(360,140));
                second.type = UnityEngine.UI.Image.Type.Sliced; second.pixelsPerUnitMultiplier=2;
                var secondGlow = second.gameObject.AddComponent<UIShapeGlow>(); secondGlow.Radius=18; secondGlow.Samples=64;
                var bar = Image(canvas.transform,"Horizontal Fill",sprite,voronoi,new Vector2(-260,-100),new Vector2(360,130));
                bar.type=UnityEngine.UI.Image.Type.Filled; bar.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal; bar.fillAmount=.68f;
                var radial = Image(canvas.transform,"Radial Fill",sprite,voronoi,new Vector2(235,-100),new Vector2(200,200));
                radial.type=UnityEngine.UI.Image.Type.Filled; radial.fillMethod=UnityEngine.UI.Image.FillMethod.Radial360; radial.fillAmount=.72f;
                Canvas.ForceUpdateCanvases(); camera.Render();
                output = Read(target);
                File.WriteAllBytes(DirectoryPath + "/UIEffectsPreview.png", output.EncodeToPNG());
                Color background = output.GetPixel(0,0);
                Check((canvas.additionalShaderChannels & AdditionalCanvasShaderChannels.TexCoord1) != 0, "Glow enables its required Canvas vertex channel");
                Check(!root.GetComponentInChildren<UnityEngine.UI.Mask>() && !root.GetComponentInChildren<RectMask2D>(), "Effects render without a mask");
                Check(ColorDistance(output.GetPixel(252,565), background) > .005f, "Glow is visible outside sliced Image bounds");
                Check(ColorDistance(output.GetPixel(800,175), background) < .005f, "Radial fill leaves unfilled pixels clear");
                var mainMesh = first.canvasRenderer.GetMesh();
                Check(mainMesh.vertexCount > 100, "Glow expands the sliced Image mesh");
                File.WriteAllBytes(DirectoryPath + "/UIEffectsPreview.png", output.EncodeToPNG());

                // Fill 0 must remove every Voronoi pixel; changing fill must not regenerate pattern coordinates.
                bar.fillAmount=0; radial.fillAmount=0; Canvas.ForceUpdateCanvases(); camera.Render();
                UnityEngine.Object.DestroyImmediate(output); output=Read(target);
                Check(ColorDistance(output.GetPixel(180,220),background) < .005f, "Zero fill removes Voronoi output");
                bar.fillAmount=1; Canvas.ForceUpdateCanvases(); camera.Render();
                UnityEngine.Object.DestroyImmediate(output); output=Read(target);
                Check(ColorDistance(output.GetPixel(180,220),background) > .005f, "Full fill renders Voronoi output");
                // Freeze time to verify filling clips a stationary pattern rather than stretching it.
                voronoi.SetFloat("_Speed",0);
                Canvas.ForceUpdateCanvases(); camera.Render();
                UnityEngine.Object.DestroyImmediate(output); output=Read(target);
                Color fullPattern=output.GetPixel(180,220);
                bar.fillAmount=.5f; Canvas.ForceUpdateCanvases(); camera.Render();
                UnityEngine.Object.DestroyImmediate(output); output=Read(target);
                Check(ColorDistance(output.GetPixel(180,220),fullPattern) < .003f, "Changing fill does not stretch or move the pattern");
                // Render two deterministic shader times to exercise animated site motion and flow.
                voronoi.SetFloat("_Speed",.7f);
                Shader.SetGlobalVector("_Time",Vector4.zero); camera.Render();
                UnityEngine.Object.DestroyImmediate(output); output=Read(target);
                var firstFrame=output.GetPixels32();
                Shader.SetGlobalVector("_Time",new Vector4(.1f,2,4,6)); camera.Render();
                UnityEngine.Object.DestroyImmediate(output); output=Read(target);
                var secondFrame=output.GetPixels32(); int changed=0;
                for(int i=0; i<firstFrame.Length; i++)
                    if(!firstFrame[i].Equals(secondFrame[i])) changed++;
                Check(changed > 100, "Voronoi pattern animates over time");
                File.WriteAllText(DirectoryPath + "/Validation.txt", string.Join("\n",checks));
                AssetDatabase.SaveAssets();
                Debug.Log("UI effects: " + checks.Count + " checks passed. Preview: " + DirectoryPath + "/UIEffectsPreview.png");
            }
            finally
            {
                RenderTexture.active=previousTarget;
                Shader.SetGlobalVector("_Time",previousTime);
                UnityEngine.Object.DestroyImmediate(root);
                if(target) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if(output) UnityEngine.Object.DestroyImmediate(output);
                if(sprite) UnityEngine.Object.DestroyImmediate(sprite);
                if(texture) UnityEngine.Object.DestroyImmediate(texture);
                if(glow) UnityEngine.Object.DestroyImmediate(glow);
                if(voronoi) UnityEngine.Object.DestroyImmediate(voronoi);
                EditorSceneManager.ClosePreviewScene(scene);
            }
            AssetDatabase.Refresh();
        }

        static void CreateMaterial(string name, Shader shader)
        {
            string path=DirectoryPath+"/"+name+".mat";
            if(!AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.CreateAsset(new Material(shader),path);
        }
        static UnityEngine.UI.Image Image(Transform parent,string name,Sprite sprite,Material material,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent,false);
            var image=go.GetComponent<UnityEngine.UI.Image>(); image.sprite=sprite; image.material=material; image.raycastTarget=false;
            image.rectTransform.anchoredPosition=position; image.rectTransform.sizeDelta=size;
            return image;
        }
        static Texture2D Read(RenderTexture target)
        {
            RenderTexture.active=target;
            var texture=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0); texture.Apply(); return texture;
        }
        static float ColorDistance(Color a,Color b) => Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Max(Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b)));
    }
}
