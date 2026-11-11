// 바닥 공격 예고선: 가산 발광 띠. _Fill(0~1)만큼 뿌리 쪽부터 차오르며 진해진다.
// LineRenderer(텍스처 모드 Stretch) 기준: uv.x = 시작(0) → 끝(1), uv.y = 폭 방향(0~1)
Shader "Wongwirok/GroundTelegraph"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 0.15, 0.1, 1)
        _Intensity ("Intensity", Float) = 1.5
        _Fill ("Fill (예고 진행도)", Range(0, 1)) = 0
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.35
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

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Fill;
                half _EdgeSoftness;
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
                half across = abs(i.uv.y * 2.0 - 1.0);
                half edge = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, across);
                half outline = smoothstep(0.75, 0.95, across) * edge;          // 테두리는 처음부터 보임
                half filled = step(i.uv.x, _Fill) * edge;                     // 차오른 부분
                half4 c = _Color * i.color;
                c.rgb *= _Intensity;
                c.a *= saturate(outline * 0.6 + filled * (0.35 + 0.5 * _Fill));
                return c;
            }
            ENDHLSL
        }
    }
}
