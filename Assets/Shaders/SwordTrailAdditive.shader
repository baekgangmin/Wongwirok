// 칼날 궤적용 가산(빛나는) 셰이더. 정점 색(투명도 포함)과 칼날 가장자리 부드럽게 처리.
Shader "Wongwirok/SwordTrailAdditive"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Float) = 2
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.25
        _BaseFade ("Base Fade (칼끝 쪽만 진하게)", Range(0, 4)) = 1.8
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _EdgeSoftness;
                half _BaseFade;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.color = input.color;
                o.uv = input.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // uv.y: 0 = 칼날 밑동, 1 = 칼끝. 양쪽 가장자리를 부드럽게.
                half edge = smoothstep(0.0, _EdgeSoftness, i.uv.y) * smoothstep(1.0, 1.0 - _EdgeSoftness * 0.5, i.uv.y);
                half towardTip = pow(saturate(i.uv.y), _BaseFade);   // 손잡이 쪽은 옅게, 칼끝 쪽은 진하게
                half4 c = _Color * i.color;
                c.rgb *= _Intensity;
                c.a *= edge * towardTip;
                return c;
            }
            ENDHLSL
        }
    }
}
