Shader "Body24/UI/Shape Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _GlowColor ("Glow Color", Color) = (0.15,1,0.7,1)
        _GlowIntensity ("Glow Brightness", Range(0,10)) = 3
        _GlowOpacity ("Glow Opacity", Range(0,4)) = 1.5
        _GlowTintByImage ("Tint Glow By Image Color", Range(0,1)) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            Name "ShapeGlow"
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex UIEffectsVert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UIEffectsCommon.cginc"
            float4 _GlowColor;
            float _GlowIntensity, _GlowOpacity, _GlowTintByImage;

            float4 frag(UIEffectsVaryings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float4 sprite = tex2D(_MainTex, i.uv) + _TextureSampleAdd;
                float4 color;
                if (i.effect.x > 0.5)
                {
                    float alpha = saturate(sprite.a * i.color.a * _GlowColor.a * _GlowOpacity * i.effect.y);
                    float3 tint = lerp(float3(1,1,1), i.color.rgb, _GlowTintByImage);
                    color = float4(_GlowColor.rgb * tint * _GlowIntensity * alpha, alpha);
                }
                else
                {
                    color = sprite * i.color;
                    color.rgb *= color.a;
                }
                return UIEffectsClip(color, i);
            }
            ENDCG
        }
    }
    Fallback Off
}
