// 보스 물기둥: 위로 흘러 올라가는 반투명 물 원통.
// - 물결 무늬(코드로 만든 노이즈)가 위로 흐르고, 가장자리(프레넬)는 밝게, 흰 물줄기 띠가 섞임
// - 정점을 노이즈로 살짝 흔들어 표면이 출렁임
// - _Rise(0~1): 아래부터 이 높이까지만 보임 (솟을 때 0→1, 무너질 때 1→0)
// 메시 uv: x = 둘레(0~1), y = 높이(0 아래 ~ 1 꼭대기)
Shader "Wongwirok/WaterColumn"
{
    Properties
    {
        _DeepColor ("Deep Color", Color) = (0.05, 0.22, 0.28, 1)
        _EdgeColor ("Edge / Foam Color", Color) = (0.65, 0.9, 1, 1)
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
                half4 _EdgeColor;
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

                // 솟아오르는/무너지는 높이 컷. 꼭대기 경계는 들쭉날쭉하게 (물이 부서지는 끝)
                float jag = (fbm(float2(i.uv.x * 8.0, t * 0.7), 8.0) - 0.5) * 0.16;
                half rise = smoothstep(_Rise + 0.02 + jag, _Rise - 0.05 + jag, i.uv.y);
                if (rise <= 0.001) discard;

                float2 flowUV = float2(i.uv.x * _NoiseScale.x, i.uv.y * _NoiseScale.y - t);
                float n = fbm(flowUV, _NoiseScale.x);
                // 세로로 길게 늘린 노이즈 → 위로 솟구치는 물줄기 결
                float2 streakUV = float2(i.uv.x * _NoiseScale.x * 2.0, i.uv.y * _NoiseScale.y * 0.35 - t * 1.3);
                float n2 = fbm(streakUV + 7.3, _NoiseScale.x * 2.0);

                // 위로 길게 늘어진 흰 물줄기
                half streak = smoothstep(0.52, 0.72, n2) * _StreakAmount;

                float3 N = normalize(i.normalWS) * (isFront ? 1.0 : -1.0);
                float3 V = normalize(i.viewDirWS);
                half fresnel = pow(1.0 - saturate(abs(dot(N, V))), _FresnelPower);

                half3 col = lerp(_DeepColor.rgb, _EdgeColor.rgb, saturate(fresnel * 0.85 + n * 0.25));
                col = lerp(col, _EdgeColor.rgb * 1.2, saturate(streak));

                half alpha = _Alpha * saturate(0.22 + fresnel * 0.7 + streak * 0.6 + (n - 0.5) * 0.4);
                // 안쪽 면은 옅게 (두께감)
                alpha *= isFront ? 1.0 : 0.45;
                // 바닥은 물에 섞여 들어가듯 옅게, 꼭대기 근처는 거품처럼 밝게
                alpha *= smoothstep(0.0, 0.08, i.uv.y);
                half nearTop = smoothstep(_Rise - 0.18, _Rise - 0.02, i.uv.y);
                col = lerp(col, _EdgeColor.rgb, nearTop * 0.6);
                alpha *= rise;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
