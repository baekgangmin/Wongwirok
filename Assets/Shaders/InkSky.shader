// 손그림 하늘: 지평선 종이색 → 위쪽 옅은 쪽빛 그라데이션 + 먹선 테두리 구름 (천천히 흐름)
Shader "Wongwirok/InkSky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.62, 0.72, 0.8, 1)
        _HorizonColor ("Horizon", Color) = (0.94, 0.91, 0.84, 1)
        _CloudColor ("Cloud", Color) = (0.98, 0.96, 0.92, 1)
        _CloudShade ("Cloud Shade", Color) = (0.82, 0.84, 0.88, 1)
        _InkColor ("Ink", Color) = (0.25, 0.25, 0.28, 1)
        _CloudScale ("Cloud Scale", Float) = 0.75
        _CloudCover ("Cloud Cover", Range(0, 1)) = 0.55
        _Speed ("Drift Speed", Float) = 0.004
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            half4 _TopColor, _HorizonColor, _CloudColor, _CloudShade, _InkColor;
            float _CloudScale, _CloudCover, _Speed;
            struct A { float4 pos : POSITION; };
            struct V { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.dir = i.pos.xyz; return o; }

            float H(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float N(float2 p)
            {
                float2 i = floor(p), f = frac(p); float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(H(i), H(i + float2(1, 0)), u.x), lerp(H(i + float2(0, 1)), H(i + 1), u.x), u.y);
            }
            float SMin(float a, float b, float k) { float h = saturate(0.5 + 0.5 * (b - a) / k); return lerp(b, a, h) - k * h * (1 - h); }

            static float gPx;   // 구름 평면 좌표에서 화면 1픽셀 크기 (선 굵기를 화면 기준으로)

            // 몽우리 안에 말려 들어가는 소용돌이 선 (전통 구름무늬)
            float Curl(float2 v, float R, float dir, float rot)
            {
                float rr = length(v) / R;
                if (rr > 0.86 || rr < 0.06) return 0;
                float th = atan2(v.y, v.x) * dir + rot;
                float turns = 1.55;
                float s = rr * turns - th / 6.2832;
                float distUV = abs(frac(s + 0.5) - 0.5) / turns * R;      // 가장 가까운 소용돌이 선까지 거리
                float w = gPx * 1.6;
                float ln = smoothstep(w + gPx, w - gPx * 0.5, distUV);
                return ln * smoothstep(0.86, 0.7, rr) * smoothstep(0.06, 0.2, rr);
            }

            // 바람에 흐르는 꼬리 선: x0~x1 구간에서 굽이치며 끝으로 갈수록 가늘어짐
            float Wisp(float2 q, float x0, float x1, float amp, float freq, float phase, float yOff)
            {
                if (q.x < x0 || q.x > x1) return 0;
                float t = (q.x - x0) / (x1 - x0);
                float y = yOff + amp * sin(t * freq + phase) * (0.3 + t);
                float w = gPx * (1.6 * (1 - t) + 0.4);
                return smoothstep(w + gPx, w - gPx * 0.5, abs(q.y - y));
            }

            // 구름 하나 (칸 c) → x: 몸통 거리장(음수=안), y: 안쪽 무늬 선(소용돌이·꼬리)
            float2 OneCloud(float2 p, float2 c)
            {
                float2 ctr = c + 0.5 + (float2(H(c + 3.1), H(c + 7.7)) - 0.5) * 0.5;
                float ang = (H(c + 9.3) - 0.5) * 1.2;                       // 대체로 가로로 흐르되 조금씩 기울게
                float sn = sin(ang), cs = cos(ang);
                float2 q = p - ctr; q = float2(cs * q.x - sn * q.y, sn * q.x + cs * q.y);
                float flip = H(c + 4.4) < 0.5 ? -1 : 1;                     // 좌우 뒤집기
                q.x *= flip;
                float kind = H(c + 11.1);                                    // 0~0.35 꼬리 긴 구름, 0.35~0.7 뭉게, 나머지 작은 소용돌이 구름
                float w = 0.3 + 0.3 * H(c + 1.7);
                int n = kind < 0.35 ? 3 : (kind < 0.7 ? 5 : 2);
                float d = 1e3, ink = 0;
                [loop] for (int k = 0; k < 5; k++)
                {
                    if (k >= n) break;
                    float t = n > 1 ? k / (float)(n - 1) - 0.5 : 0;
                    float r = (0.2 + 0.1 * H(c + k * 2.3)) * w * (1 - abs(t) * 0.35);
                    float2 o = float2(t * w * (kind < 0.35 ? 0.55 : (kind < 0.7 ? 0.75 : 0.95)), (H(c + k * 5.1) - 0.35) * 0.18 * w + (1 - abs(t) * 2) * 0.06 * w);
                    d = SMin(d, length(q - o) - r, 0.015);
                    if (H(c + k * 8.8) < 0.92)
                        ink = max(ink, Curl(q - o, r, H(c + k * 3.7) < 0.5 ? 1 : -1, H(c + k * 6.3) * 6.28));
                }
                if (kind < 0.35)
                {
                    // 길게 흐르는 꼬리 두세 가닥
                    float x0 = 0.2 * w;
                    ink = max(ink, Wisp(q, x0, x0 + 0.9 * w, 0.05 * w, 6.0, H(c) * 6.28, -0.04 * w));
                    ink = max(ink, Wisp(q, x0, x0 + 0.65 * w, 0.04 * w, 7.0, H(c + 1) * 6.28, 0.02 * w));
                    if (H(c + 2.2) < 0.5) ink = max(ink, Wisp(q, -x0 - 0.5 * w, -x0, 0.03 * w, 5.0, H(c + 2) * 6.28, -0.03 * w));
                }
                return float2(d, ink);
            }

            // 구름 거리장 + 무늬 선 (cover 가 클수록 구름이 많음)
            float2 CloudField(float2 p, float cover)
            {
                float2 cell = floor(p);
                float d = 1e3, ink = 0;
                for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    float2 c = cell + float2(i, j);
                    if (H(c) > cover) continue;                             // 이 칸은 빈 하늘
                    float2 r = OneCloud(p, c);
                    d = min(d, r.x);
                    ink = max(ink, r.y);
                }
                return float2(min(d, 0.5), ink);
            }

            // 몸통은 색면 + 붓 테두리, 안쪽엔 소용돌이·꼬리 먹선
            half3 PaintCloud(half3 col, float2 f, float2 uv, float alpha, half3 fill)
            {
                float sd = f.x;
                float aa = max(gPx, 1e-5);
                float body = smoothstep(aa, -aa, sd);
                float shade = smoothstep(-gPx * 10, 0.0, sd);
                float brush = gPx * 1.8 * (0.6 + 0.8 * N(uv * 14));
                float inkEdge = smoothstep(brush + aa, brush - aa * 0.5, abs(sd + brush * 0.5));
                half3 cloud = lerp(fill, _CloudShade.rgb, shade * 0.6);
                col = lerp(col, cloud, body * alpha);
                float lines = max(inkEdge, f.y);
                return lerp(col, _InkColor.rgb, saturate(lines) * alpha * 0.9);
            }

            half4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = saturate(d.y);
                half3 sky = lerp(_HorizonColor.rgb, _TopColor.rgb, smoothstep(0.0, 0.55, h));
                if (d.y < 0) return half4(_HorizonColor.rgb, 1);

                // 하늘에 누운 평면으로 투영: 지평선 쪽일수록 납작하게
                float2 drift = _Time.y * _Speed * float2(1, 0.3);
                float2 uvFar = d.xz / (d.y + 0.05) * _CloudScale * 1.35 + drift * 0.6 + 37.0;
                float2 uvNear = d.xz / (d.y + 0.05) * _CloudScale + drift;
                float fade = smoothstep(0.06, 0.25, h);                     // 지평선에선 구름 사라짐
                half3 col = sky;
                gPx = length(fwidth(uvFar)) * 0.7;
                col = PaintCloud(col, CloudField(uvFar, _CloudCover * 0.5), uvFar, fade * 0.5, lerp(_CloudColor.rgb, _TopColor.rgb, 0.25));
                gPx = length(fwidth(uvNear)) * 0.7;
                col = PaintCloud(col, CloudField(uvNear, _CloudCover), uvNear, fade, _CloudColor.rgb);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
