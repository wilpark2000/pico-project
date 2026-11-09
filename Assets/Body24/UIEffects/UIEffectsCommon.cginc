#ifndef BODY24_UI_EFFECTS_INCLUDED
#define BODY24_UI_EFFECTS_INCLUDED
#include "UnityCG.cginc"
#include "UnityUI.cginc"

struct UIEffectsVertex
{
    float4 vertex : POSITION;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
    float2 effect : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct UIEffectsVaryings
{
    float4 position : SV_POSITION;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
    float2 localPosition : TEXCOORD1;
    float4 mask : TEXCOORD2;
    float2 effect : TEXCOORD3;
    UNITY_VERTEX_OUTPUT_STEREO
};

sampler2D _MainTex;
float4 _TextureSampleAdd;
float4 _Color;
float4 _ClipRect;
float _UIMaskSoftnessX, _UIMaskSoftnessY;

UIEffectsVaryings UIEffectsVert(UIEffectsVertex v)
{
    UIEffectsVaryings o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.position = UnityObjectToClipPos(v.vertex);
    o.color = v.color * _Color;
    o.uv = v.uv;
    o.localPosition = v.vertex.xy;
    o.effect = v.effect;
    float2 pixelSize = o.position.w;
    pixelSize /= max(abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy)), 0.00001);
    float4 rect = clamp(_ClipRect, -2e10, 2e10);
    o.mask = float4(v.vertex.xy * 2 - rect.xy - rect.zw,
        0.25 / (0.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
    return o;
}

// Both shaders output premultiplied color, so clipping must affect RGB as well.
float4 UIEffectsClip(float4 color, UIEffectsVaryings i)
{
    #ifdef UNITY_UI_CLIP_RECT
    float2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.mask.xy)) * i.mask.zw);
    color *= mask.x * mask.y;
    #endif
    #ifdef UNITY_UI_ALPHACLIP
    clip(color.a - 0.001);
    #endif
    return color;
}
#endif
