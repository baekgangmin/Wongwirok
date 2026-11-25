// 보스 물기둥 (수묵화풍): 위로 흘러 올라가는 물 원통.
// - 색은 2~3단계 평평한 띠(셀), 실루엣과 부서지는 꼭대기에 굵기가 출렁이는 먹 선
// - 물결 노이즈의 등고선이 끝이 또렷한 흰 붓 결이 되어 위로 흐름
// - 정점을 노이즈로 살짝 흔들어 표면이 출렁임
// - _Rise(0~1): 아래부터 이 높이까지만 보임 (솟을 때 0→1, 무너질 때 1→0)
// 메시 uv: x = 둘레(0~1), y = 높이(0 아래 ~ 1 꼭대기)
Shader "Wongwirok/WaterColumn"
{
    Properties
    {
        _DeepColor ("Deep Color", Color) = (0.05, 0.22, 0.28, 1)
        _MidColor ("Mid Color", Color) = (0.25, 0.55, 0.62, 1)
        _EdgeColor ("Edge / Foam Color", Color) = (0.65, 0.9, 1, 1)
        _InkColor ("Ink Outline Color", Color) = (0.06, 0.06, 0.08, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.6)) = 0.28
        _LineWidth ("Flow Line Width", Range(0, 0.1)) = 0.03
        _Alpha ("Alpha", Range(0, 1)) = 0.75
        _FlowSpeed ("Flow Speed", Float) = 2.2
        _NoiseScale ("Noise Scale (둘레, 높이)", Vector) = (6, 3, 0, 0)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.5
        _StreakAmount ("Foam Streaks", Range(0, 2)) = 0.9
        _Wobble ("Surface Wobble", Range(0, 0.3)) = 0.06
        _Rise ("Rise (보이는 높이)", Range(0, 1)) = 1
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _MidColor;
                half4 _EdgeColor;
                half4 _InkColor;
                half _OutlineWidth;
                half _LineWidth;
                half _Alpha;
                half _FlowSpeed;
                float4 _NoiseScale;
                half _FresnelPower;
                half _StreakAmount;
                half _Wobble;
                half _Rise;
            CBUFFER_END

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // 둘레 방향(x)은 주기 period로 이어지게 하는 값 노이즈
            float valueNoise(float2 p, float period)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float2 i1 = float2(fmod(i.x + 1.0, period), i.y);
                float2 i0 = float2(fmod(i.x, period), i.y);
                float a = hash21(i0);
                float b = hash21(i1);
                float c = hash21(i0 + float2(0, 1));
                float d = hash21(i1 + float2(0, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p, float period)
            {
                float v = 0.0;
                float amp = 0.55;
                for (int k = 0; k < 4; k++)
                {
                    v += valueNoise(p, period) * amp;
                    p *= 2.0;
                    period *= 2.0;
                    amp *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                float t = _Time.y * _FlowSpeed;
                float2 np = float2(input.uv.x * _NoiseScale.x, input.uv.y * _NoiseScale.y - t * 0.6);
                float n = fbm(np, _NoiseScale.x) - 0.5;
                float3 pos = input.positionOS.xyz;
                float3 radial = normalize(float3(pos.x, 0, pos.z) + 1e-5);
                pos += radial * n * _Wobble * 2.0;
                o.positionCS = TransformObjectToHClip(pos);
                o.uv = input.uv;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.viewDirWS = GetWorldSpaceViewDir(TransformObjectToWorld(pos));
                return o;
            }

            half4 frag(Varyings i, bool isFront : SV_IsFrontFace) : SV_Target
            {
                float t = _Time.y * _FlowSpeed;

                // 솟아오르는/무너지는 높이 컷. 꼭대기 경계는 들쭉날쭉 (물이 부서지는 끝)
                float jag = (fbm(float2(i.uv.x * 8.0, t * 0.7), 8.0) - 0.5) * 0.16;
                float topDist = (_Rise + jag) - i.uv.y;           // 꼭대기 경계까지 남은 높이
                if (topDist < 0.0) discard;

                float2 flowUV = float2(i.uv.x * _NoiseScale.x, i.uv.y * _NoiseScale.y - t);
                float n = fbm(flowUV, _NoiseScale.x);
                float2 streakUV = float2(i.uv.x * _NoiseScale.x * 2.0, i.uv.y * _NoiseScale.y * 0.35 - t * 1.3);
                float n2 = fbm(streakUV + 7.3, _NoiseScale.x * 2.0);

                float3 N = normalize(i.normalWS) * (isFront ? 1.0 : -1.0);
                float3 V = normalize(i.viewDirWS);
                float ndv = saturate(abs(dot(N, V)));
                half fresnel = pow(1.0 - ndv, _FresnelPower);

                // 1) 셀: 밝기를 3단계 평평한 색으로
                half shade = saturate(fresnel * 0.85 + n * 0.35);
                half3 col = shade < 0.38 ? _DeepColor.rgb : (shade < 0.62 ? _MidColor.rgb : _EdgeColor.rgb);

                // 2) 흰 붓 결: 물줄기 노이즈의 등고선(끝이 또렷한 선) + 진한 곳은 흰 면
                half lineMask = step(abs(n2 - 0.56), _LineWidth) + step(0.74, n2);
                col = lerp(col, _EdgeColor.rgb * 1.1, saturate(lineMask) * _StreakAmount * 0.75);

                // 3) 먹 외곽선: 실루엣 쪽(시선과 면이 비스듬한 곳). 굵기를 높이 따라 출렁이고 군데군데 끊김
                float brush = fbm(float2(i.uv.y * 4.0 - t * 0.3, i.uv.x * 2.0), 2.0);
                half width = _OutlineWidth * (0.6 + brush * 0.8) * step(0.22, brush);
                half ink = step(ndv, width) * (isFront ? 1.0 : 0.0);

                // 4) 꼭대기 부서지는 경계에도 먹 선
                ink = max(ink, step(topDist, 0.025 + brush * 0.02) * step(0.05, i.uv.y));
                col = lerp(col, _InkColor.rgb, ink);

                // 물은 거의 불투명한 그림처럼, 안쪽 면은 옅게, 바닥은 땅에 섞이게
                half alpha = _Alpha * (isFront ? 1.0 : 0.5);
                alpha = max(alpha, ink);
                alpha *= smoothstep(0.0, 0.06, i.uv.y);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
