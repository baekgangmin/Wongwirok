// 맞았을 때 몸 위에 잠깐 덧그리는 발광 막. 조명과 상관없이 밝게 보인다.
// 가장자리(실루엣)는 더 진하게, 안쪽도 은은하게 빛남. 가산 혼합.
Shader "Wongwirok/HitFlashOverlay"
{
    Properties
    {
        _Color ("Flash Color", Color) = (1, 0.25, 0.2, 1)
        _Intensity ("Intensity", Float) = 1.5
        _RimPower ("Rim Power", Range(0.5, 6)) = 2
        _InnerAmount ("Inner Glow", Range(0, 1)) = 0.45
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Back

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewDirWS : TEXCOORD1; };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _RimPower;
                half _InnerAmount;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.viewDirWS = GetWorldSpaceViewDir(posWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half ndv = saturate(abs(dot(normalize(i.normalWS), normalize(i.viewDirWS))));
                half rim = pow(1.0 - ndv, _RimPower);
                half glow = saturate(rim + _InnerAmount);
                return half4(_Color.rgb * _Intensity * glow, 1);
            }
            ENDHLSL
        }
    }
}
