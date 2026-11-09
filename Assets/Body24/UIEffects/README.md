# UI effects

For Unity uGUI `UnityEngine.UI.Image`, including the project's URP canvases.

## Shape glow

1. Assign **UIShapeGlow.mat** to an Image's Material.
2. Add **UI > Effects > Shape Glow** (`UIShapeGlow`) to that same GameObject.
3. Set Radius in local UI units and Samples on the component. Adjust Glow Color,
   Brightness and Opacity on the material. Use 16–32 samples for cheaper XR UI;
   48–64 gives a smoother halo. Radius scales with the Canvas/RectTransform.

The component copies and offsets the actual Image triangles into a weighted
Gaussian halo, then draws the original Image over it. It respects sprite alpha,
Sliced borders, Fill Center, Pixels Per Unit Multiplier, sprite atlas UVs, and
Filled geometry. There is no auxiliary mask, camera, render texture, or texture
readability requirement. TexCoord1 is enabled on its Canvas automatically.
Use this as the only mesh effect on the Image: other Shadow/Outline/UV1 effects
can modify the source geometry or overwrite its halo markers.

The material by itself draws the original image: the component supplies geometry
outside its bounds, which a fragment shader cannot create on its own. Image and
CanvasGroup alpha fade both the image and halo. Brightness affects halo RGB while
Opacity affects its coverage. The halo follows alpha holes as well as outer edges.

This is a direct, visible glow on Overlay, Camera and World Space canvases. HDR
colors can also feed camera Bloom when that canvas is rendered into the camera's
HDR target before post-processing; Screen Space Overlay is usually rendered after
post-processing, so camera Bloom will not affect it. No Bloom setup is needed for
the direct halo. Existing parent masks are respected if present, and can clip it.

Cost scales with mesh triangles × samples and transparent overdraw. The helper
reduces samples on very complex meshes to stay below uGUI's vertex limit. Avoid
large, overlapping glowing panels on mobile XR; profile on the target headset.
It does not change raycast hit areas or layout dimensions.

## Animated Voronoi

1. Assign **UIAnimatedVoronoi.mat** directly to a standard Image.
2. Set Image Type to **Filled** and choose Horizontal, Vertical, Radial90,
   Radial180 or Radial360. Drive the normal `Image.fillAmount` property.
3. Adjust Cell Color, Edge Color, Cell Size (local UI units), Animation Speed,
   Flow Direction XY, Edge Width, Edge Softness, Variation and Brightness.

No helper component is needed. Sprite alpha supplies the shape and Unity's fill
mesh supplies the clipping. The pattern uses local position rather than atlas
UVs so filling does not stretch or restart it; Cell Size stays consistent across
sliced regions too. Set Animation Speed to zero for a static pattern. The shader
also works with Simple and Sliced Images. Sprite RGB Influence blends in source
sprite color; it is zero by default so only the source alpha shapes the effect.

Both materials support UI tint, CanvasGroup fades, stencil Mask, RectMask2D soft
clipping, UI alpha clipping and XR stereo vertex setup. Neither requires a mask.
They do not alter the existing gameplay UI until you assign them.
