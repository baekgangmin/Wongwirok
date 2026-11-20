// 지형용: 지형 레이어 4장(_Splat0~3)을 섞어 그리되, 같은 그림이 바둑판처럼 반복돼 보이지 않게
// 각 그림을 크기·각도가 다른 두 번째 배치와 넓은 얼룩 무늬로 섞는다. (지형 '그리기 인스턴싱'은 꺼야 함)
Shader "Wongwirok/InkTerrain"
{
    Properties
    {
        [HideInInspector] _Control ("Control", 2D) = "red" {}
        [HideInInspector] _Splat0 ("Layer 0", 2D) = "grey" {}
        [HideInInspector] _Splat1 ("Layer 1", 2D) = "grey" {}
        [HideInInspector] _Splat2 ("Layer 2", 2D) = "grey" {}
        [HideInInspector] _Splat3 ("Layer 3", 2D) = "grey" {}
        _AntiTileScale ("Second Layout Scale", Float) = 0.43
        _NoiseScale ("Blend Blotch Size (1/m)", Float) = 0.06
    }
    SubShader
    {
        Tags { "Queue" = "Geometry-100" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "TerrainCompatible" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _Control_ST, _Splat0_ST, _Splat1_ST, _Splat2_ST, _Splat3_ST;
            float _AntiTileScale, _NoiseScale;
        CBUFFER_END
        TEXTURE2D(_Control); SAMPLER(sampler_Control);
        TEXTURE2D(_Splat0); SAMPLER(sampler_Splat0);
        TEXTURE2D(_Splat1); TEXTURE2D(_Splat2); TEXTURE2D(_Splat3);

        float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float VNoise(float2 p)
        {
            float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
            return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 ws : TEXCOORD1; float3 n : TEXCOORD2; float fog : TEXCOORD3; };
            V vert(A i)
            {
                V o; o.ws = TransformObjectToWorld(i.pos.xyz); o.pos = TransformWorldToHClip(o.ws);
                o.n = TransformObjectToWorldNormal(i.n); o.uv = i.uv; o.fog = ComputeFogFactor(o.pos.z); return o;
            }

            // 한 레이어: 원래 배치 + 크기·각도 다른 배치를 얼룩 무늬(b)로 섞음
            #define LAYER(tex, st, b) lerp(SAMPLE_TEXTURE2D(tex, sampler_Splat0, uv * st.xy + st.zw).rgb, \
                SAMPLE_TEXTURE2D(tex, sampler_Splat0, mul(float2x2(0.8, -0.6, 0.6, 0.8), uv * st.xy * _AntiTileScale) + 0.37).rgb, b)

            half4 frag(V i) : SV_Target
            {
                float2 uv = i.uv;
                half4 c = SAMPLE_TEXTURE2D(_Control, sampler_Control, uv * _Control_ST.xy + _Control_ST.zw);
                float b = smoothstep(0.3, 0.7, VNoise(i.ws.xz * _NoiseScale) * 0.7 + VNoise(i.ws.xz * _NoiseScale * 3.1) * 0.3);
                half3 alb = c.r * LAYER(_Splat0, _Splat0_ST, b) + c.g * LAYER(_Splat1, _Splat1_ST, b)
                          + c.b * LAYER(_Splat2, _Splat2_ST, b) + c.a * LAYER(_Splat3, _Splat3_ST, b);
                float3 n = normalize(i.n);
                Light l = GetMainLight(TransformWorldToShadowCoord(i.ws));
                half ao = 1;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.pos)).indirectAmbientOcclusion;
                #endif
                half3 col = alb * (l.color * saturate(dot(n, l.direction)) * l.shadowAttenuation + SampleSH(n) * ao);
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
            struct A { float4 pos : POSITION; float3 n : NORMAL; };
            float4 vert(A i) : SV_POSITION
            {
                float3 ws = TransformObjectToWorld(i.pos.xyz);
                return TransformWorldToHClip(ApplyShadowBias(ws, TransformObjectToWorldNormal(i.n), _LightDirection));
            }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(float4 pos : POSITION) : SV_POSITION { return TransformObjectToHClip(pos.xyz); }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "DepthNormals" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct A { float4 pos : POSITION; float3 n : NORMAL; };
            struct V { float4 pos : SV_POSITION; float3 n : TEXCOORD0; };
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.n = TransformObjectToWorldNormal(i.n); return o; }
            half4 frag(V i) : SV_Target { return half4(normalize(i.n), 0); }
            ENDHLSL
        }
    }
}
