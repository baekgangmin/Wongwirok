// 2D 그림판 (나무·풀): 세로축 고정으로 카메라를 향하고, 위쪽일수록 바람에 흔들림.
// 사각형(Quad) 메시 기준: 피벗이 바닥 가운데가 되도록 오브젝트를 놓는다 (InkBillboardSprite 가 만들어 줌)
Shader "Wongwirok/InkBillboard"
{
    Properties
    {
        _BaseMap ("Sprite", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _SwayAmount ("Sway (m)", Float) = 0.25
        _SwaySpeed ("Sway Speed", Float) = 1.2
        _ShadowTint ("Shade Tint", Color) = (0.7, 0.72, 0.8, 1)
        _OutlinePx ("Outline Width (texels)", Float) = 0
        _InkColor ("Outline Ink", Color) = (0.08, 0.08, 0.09, 1)
        _OutlineVary ("Outline Brush Variation", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" "DisableBatching" = "True" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BaseMap_TexelSize; half4 _Tint, _ShadowTint, _InkColor; float _Cutoff, _SwayAmount, _SwaySpeed, _OutlinePx, _OutlineVary;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        // 오브젝트 원점 기준, 세로축만 남기고 카메라를 향하게 + 바람
        float3 BillboardWS(float3 posOS, out float swayW)
        {
            float3 origin = TransformObjectToWorld(float3(0, 0, 0));
            float3 sx = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), 1);
            float3 toCam = _WorldSpaceCameraPos - origin; toCam.y = 0;
            float3 fwd = normalize(toCam + float3(1e-4, 0, 0));
            float3 right = normalize(cross(float3(0, 1, 0), fwd));
            float h = posOS.y * sx.y;
            swayW = saturate(posOS.y);                                  // 바닥 0 → 꼭대기 1
            float ph = dot(origin.xz, float2(0.37, 0.61));              // 나무마다 다른 박자
            float t = _Time.y * _SwaySpeed + ph;
            float sway = (sin(t) * 0.7 + sin(t * 2.3 + 1.7) * 0.3) * _SwayAmount * swayW * swayW;
            return origin + right * (posOS.x * sx.x + sway) + float3(0, h, 0) + fwd * 0.01 * posOS.y;
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 ws : TEXCOORD1; float fog : TEXCOORD2; };
            V vert(A i)
            {
                V o; float w; float3 ws = BillboardWS(i.pos.xyz, w);
                o.pos = TransformWorldToHClip(ws); o.ws = ws; o.uv = TRANSFORM_TEX(i.uv, _BaseMap); o.fog = ComputeFogFactor(o.pos.z); return o;
            }
            half4 frag(V i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _Tint;
                clip(c.a - _Cutoff);
                Light l = GetMainLight(TransformWorldToShadowCoord(i.ws));
                half3 col = c.rgb * lerp(_ShadowTint.rgb, 1, step(0.5, l.shadowAttenuation)); // 그림자 속은 한 단계만 어둡게
                // 바깥 윤곽 먹선: 주변 8방향 중 하나라도 투명이면 가장자리
                if (_OutlinePx > 0)
                {
                    // 붓 강약(_OutlineVary=1): 아래쪽 가장자리는 굵게·위쪽은 가늘게, 얼룩 무늬로 굵기가 들쭉날쭉하고 군데군데 끊김
                    float2 cell = floor(i.uv * 14), f = frac(i.uv * 14); f = f * f * (3 - 2 * f);
                    #define HSH(p) frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453)
                    float nz = lerp(lerp(HSH(cell), HSH(cell + float2(1, 0)), f.x), lerp(HSH(cell + float2(0, 1)), HSH(cell + 1), f.x), f.y);
                    float brush = lerp(1, smoothstep(0.18, 0.35, nz) * (0.4 + 1.4 * nz), _OutlineVary);
                    float2 o = _BaseMap_TexelSize.xy * _OutlinePx * brush; half edge = 1;
                    [unroll] for (int k = 0; k < 8; k++)
                    {
                        float a = k * 0.7854;
                        float down = lerp(1, sin(a) < -0.1 ? 1.8 : (sin(a) > 0.1 ? 0.35 : 1), _OutlineVary);
                        edge = min(edge, SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv + float2(cos(a), sin(a)) * o * down).a);
                    }
                    edge = brush > 0.01 ? edge : 1;
                    col = lerp(col, _InkColor.rgb, step(edge, _Cutoff));
                }
                return half4(MixFog(col, i.fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection;
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i)
            {
                V o; float w; float3 ws = BillboardWS(i.pos.xyz, w);
                o.pos = TransformWorldToHClip(ApplyShadowBias(ws, float3(0, 1, 0), _LightDirection));
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap); return o;
            }
            half4 frag(V i) : SV_Target { clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _Tint.a - _Cutoff); return 0; }
            ENDHLSL
        }
    }
}
