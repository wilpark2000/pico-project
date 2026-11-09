Shader "Body24/UI/Animated Voronoi"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _CellColor ("Cell Color", Color) = (0.015,0.12,0.18,1)
        [HDR] _EdgeColor ("Edge Color", Color) = (0.3,1,0.75,1)
        _CellSize ("Cell Size (UI Units)", Float) = 36
        _Speed ("Animation Speed", Range(0,5)) = 0.7
        _Flow ("Flow Direction XY", Vector) = (0.1,0.05,0,0)
        _EdgeWidth ("Edge Width", Range(0.005,0.3)) = 0.06
        _EdgeSoftness ("Edge Softness", Range(0.001,0.15)) = 0.025
        _CellVariation ("Cell Brightness Variation", Range(0,1)) = 0.45
        _Intensity ("Brightness", Range(0,10)) = 1.5
        _SpriteColorInfluence ("Sprite RGB Influence", Range(0,1)) = 0
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
            Name "AnimatedVoronoi"
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex UIEffectsVert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UIEffectsCommon.cginc"
            float4 _CellColor, _EdgeColor, _Flow;
            float _CellSize, _Speed, _EdgeWidth, _EdgeSoftness;
            float _CellVariation, _Intensity, _SpriteColorInfluence;

            float2 Hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1,311.7)), dot(p, float2(269.5,183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float4 frag(UIEffectsVaryings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float4 sprite = tex2D(_MainTex, i.uv) + _TextureSampleAdd;
                // Local position keeps cell size constant through slicing and fillAmount changes.
                float time = _Time.y * _Speed;
                float2 p = i.localPosition / max(_CellSize, 0.001) + time * _Flow.xy;
                float2 cell = floor(p), local = frac(p);
                float nearest = 100, second = 100, variation = 0;
                [unroll] for (int y = -1; y <= 1; y++)
                {
                    [unroll] for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbour = float2(x,y);
                        float2 seed = Hash22(cell + neighbour);
                        // Bounded motion avoids cells popping between lattice neighbourhoods.
                        float2 site = 0.5 + 0.35 * sin(time + 6.2831853 * seed);
                        float distanceToPoint = length(neighbour + site - local);
                        if (distanceToPoint < nearest)
                        {
                            second = nearest; nearest = distanceToPoint; variation = seed.x;
                        }
                        else second = min(second, distanceToPoint);
                    }
                }
                float edgeDistance = second - nearest;
                float aa = max(fwidth(edgeDistance), _EdgeSoftness);
                float edge = 1 - smoothstep(_EdgeWidth, _EdgeWidth + aa, edgeDistance);
                float brightness = lerp(1, 0.45 + variation * 0.8, _CellVariation);
                float4 pattern = lerp(_CellColor * float4(brightness, brightness, brightness, 1), _EdgeColor, edge);
                float alpha = saturate(sprite.a * i.color.a * pattern.a);
                float3 rgb = pattern.rgb * i.color.rgb * _Intensity * lerp(float3(1,1,1), sprite.rgb, _SpriteColorInfluence);
                return UIEffectsClip(float4(rgb * alpha, alpha), i);
            }
            ENDCG
        }
    }
    Fallback Off
}
