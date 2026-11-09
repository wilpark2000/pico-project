using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Body24.UIEffects
{
    /// <summary>
    /// Builds a sampled Gaussian halo behind the Image's actual generated triangles.
    /// Keeping Unity's geometry/UVs preserves slicing, PPU multipliers, atlas UVs and fill.
    /// Use with the Body24/UI/Shape Glow material. No mask or post-process is required.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(UnityEngine.UI.Image))]
    [AddComponentMenu("UI/Effects/Shape Glow")]
    public sealed class UIShapeGlow : BaseMeshEffect
    {
        [SerializeField, Min(0)] float radius = 12;
        [SerializeField, Range(8, 64)] int samples = 48;
        readonly List<UIVertex> source = new List<UIVertex>();
        readonly List<UIVertex> output = new List<UIVertex>();

        public float Radius
        {
            get => radius;
            set { radius = Mathf.Max(0, value); if (graphic) graphic.SetVerticesDirty(); }
        }
        public int Samples
        {
            get => samples;
            set { samples = Mathf.Clamp(value, 8, 64); if (graphic) graphic.SetVerticesDirty(); }
        }

        void EnsureChannels()
        {
            if (graphic && graphic.canvas)
                graphic.canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }

        protected override void OnEnable() { base.OnEnable(); EnsureChannels(); }
        protected override void OnCanvasHierarchyChanged() { base.OnCanvasHierarchyChanged(); EnsureChannels(); }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            EnsureChannels();
            source.Clear(); output.Clear();
            vh.GetUIVertexStream(source);
            // VertexHelper uses 16-bit indices. Reduce quality automatically for complex meshes.
            int count = Mathf.Min(Mathf.Clamp(samples, 8, 64), 64000 / source.Count - 1);
            bool compatibleMaterial = graphic.material && graphic.material.shader &&
                graphic.material.shader.name == "Body24/UI/Shape Glow";
            if (radius > 0 && count > 0 && compatibleMaterial)
            {
                float weightSum = 0;
                for (int i = 0; i < count; i++) weightSum += Mathf.Exp(-3 * (i + .5f) / count);
                // Sunflower disk samples, with weights normalized independently of quality.
                for (int i = 0; i < count; i++)
                {
                    float fraction = (i + .5f) / count;
                    float angle = i * 2.39996323f;
                    float distance = radius * Mathf.Sqrt(fraction);
                    Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * distance;
                    float weight = Mathf.Exp(-3 * fraction) / weightSum;
                    foreach (var original in source)
                    {
                        var v = original;
                        v.position += offset;
                        v.uv1 = new Vector4(1, weight, 0, 0);
                        output.Add(v);
                    }
                }
            }
            // The original mesh is last so an opaque image covers its interior halo.
            foreach (var original in source)
            {
                var v = original; v.uv1 = Vector4.zero; output.Add(v);
            }
            vh.Clear(); vh.AddUIVertexTriangleStream(output);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            radius = Mathf.Max(0, radius); samples = Mathf.Clamp(samples, 8, 64);
            base.OnValidate(); EnsureChannels();
        }
#endif
    }
}
