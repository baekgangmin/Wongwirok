// 바닥 원형 예고: 테두리 고리 + 가운데서 퍼지는 물결 + _Fill(0~1)만큼 안쪽이 차오름. 가산 발광.
// 사각형(Quad) uv 기준, 가운데(0.5, 0.5)에서의 거리로 원을 그린다.
Shader "Wongwirok/GroundRing"
{
    Properties
    {
        _Color ("Tint", Color) = (0.35, 0.75, 1, 1)
        _Intensity ("Intensity", Float) = 1.4
        _Fill ("Fill (예고 진행도)", Range(0, 1)) = 0
        _RippleSpeed ("Ripple Speed", Float) = 5
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

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Fill;
                half _RippleSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half d = length(i.uv - 0.5) * 2.0;           // 0 = 가운데, 1 = 테두리
                if (d > 1.0) discard;
                half ring = smoothstep(0.86, 0.95, d) * (1.0 - smoothstep(0.97, 1.0, d));
                half ripple = 0.5 + 0.5 * sin(d * 22.0 - _Time.y * _RippleSpeed);
                ripple = pow(ripple, 4.0) * (1.0 - d) * 0.6;
                half fill = (1.0 - smoothstep(_Fill - 0.04, _Fill, d)) * (0.25 + 0.35 * _Fill);
                half a = saturate(ring * 0.9 + ripple * (0.3 + _Fill) + fill);
                half4 c = _Color;
                c.rgb *= _Intensity;
                c.a *= a;
                return c;
            }
            ENDHLSL
        }
    }
}
