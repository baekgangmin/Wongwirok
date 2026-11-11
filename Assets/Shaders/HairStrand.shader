// 보스 머리카락 다발: 반투명 검은 띠 + 가운데 젖은 윤기 + 끝으로 갈수록 옅어짐.
// LineRenderer(텍스처 모드 Stretch) 기준: uv.x = 뿌리(0) → 끝(1), uv.y = 폭 방향(0~1)
Shader "Wongwirok/HairStrand"
{
    Properties
    {
        _Color ("Hair Color", Color) = (0.03, 0.03, 0.04, 1)
        _SheenColor ("Wet Sheen", Color) = (0.25, 0.3, 0.35, 1)
        _Sheen ("Sheen Strength", Range(0, 2)) = 0.6
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.3
        _TipFade ("Tip Fade", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
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
                half4 _SheenColor;
                half _Sheen;
                half _EdgeSoftness;
                half _TipFade;
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
                half across = abs(i.uv.y * 2.0 - 1.0);              // 0 = 가운데, 1 = 가장자리
                half edge = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, across);
                half tip = 1.0 - smoothstep(1.0 - _TipFade, 1.0, i.uv.x);
                half sheen = pow(saturate(1.0 - across), 6.0) * _Sheen;
                half4 c = _Color * i.color;
                c.rgb += _SheenColor.rgb * sheen;
                c.a *= edge * tip;
                return c;
            }
            ENDHLSL
        }
    }
}
