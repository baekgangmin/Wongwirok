// 손그림 시험용: 빛을 3단계(밝음/중간/그림자)로만 칠하고, 물체 둘레에 붓 외곽선
Shader "Wongwirok/ToonInk"
{
    Properties
    {
        _BaseMap ("Base", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _ShadowColor ("Shadow Tint", Color) = (0.42, 0.45, 0.6, 1)
        _MidColor ("Mid Tint", Color) = (0.78, 0.76, 0.8, 1)
        _OutlineWidth ("Outline Width (m)", Float) = 0.05
        _OutlineColor ("Outline Color", Color) = (0.08, 0.07, 0.06, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _ShadowColor, _MidColor, _OutlineColor;
            float _OutlineWidth;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float3 ws : TEXCOORD0; float3 n : TEXCOORD1; float2 uv : TEXCOORD2; float fog : TEXCOORD3; };
            V vert(A i)
            {
                V o; VertexPositionInputs p = GetVertexPositionInputs(i.pos.xyz);
                o.pos = p.positionCS; o.ws = p.positionWS; o.n = TransformObjectToWorldNormal(i.n);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap); o.fog = ComputeFogFactor(p.positionCS.z); return o;
            }
            half4 frag(V i) : SV_Target
            {
                half3 alb = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                float3 n = normalize(i.n);
                Light l = GetMainLight(TransformWorldToShadowCoord(i.ws));
                float lit = saturate(dot(n, l.direction) * 0.5 + 0.5) * lerp(0.65, 1, l.shadowAttenuation);
                // 3단계 띠 (경계만 살짝 부드럽게)
                float hi = smoothstep(0.62, 0.66, lit), mid = smoothstep(0.38, 0.42, lit);
                half3 tint = lerp(_ShadowColor.rgb, lerp(_MidColor.rgb, 1, hi), mid);
                half3 col = alb * tint * saturate(l.color * 0.9 + 0.25);
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv3 : TEXCOORD3; };
            struct V { float4 pos : SV_POSITION; float fog : TEXCOORD0; };
            float H(float3 p) { return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453); }
            float VNoise(float3 p)
            {
                float3 c = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(H(c), H(c + float3(1,0,0)), f.x), lerp(H(c + float3(0,1,0)), H(c + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(H(c + float3(0,0,1)), H(c + float3(1,0,1)), f.x), lerp(H(c + float3(0,1,1)), H(c + float3(1,1,1)), f.x), f.y), f.z);
            }
            V vert(A i)
            {
                V o;
                float3 ws = TransformObjectToWorld(i.pos.xyz);
                float3 n = normalize(TransformObjectToWorldNormal(i.n));
                // 붓선처럼 굵기가 부드럽게 굵어졌다 가늘어짐 (큰 붓 흐름 + 잔떨림)
                float brush = VNoise(ws * 0.8 + 11.0) * 0.6 + VNoise(ws * 3.0) * 0.4;
                float w = _OutlineWidth * (0.35 + 1.3 * brush);
                // UV4.x = 외곽선 줄이기 (1 = 없음). 값이 없는 모델은 0 이라 그대로. 소매 속 손목 외곽선이 소매를 뚫고 나오지 않게
                w *= 1 - saturate(i.uv3.x);
                ws += n * w;
                o.pos = TransformWorldToHClip(ws); o.fog = ComputeFogFactor(o.pos.z); return o;
            }
            half4 frag(V i) : SV_Target { return half4(MixFog(_OutlineColor.rgb, i.fog), 1); }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }
}
