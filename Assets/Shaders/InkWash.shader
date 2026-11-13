// 수묵화 화면 효과 (URP Full Screen Pass, 후처리 뒤)
// 1) Kuwahara 로 사진 결을 붓 칠한 색면으로 뭉갬  2) 깊이·면 방향 경계에 먹선
// 3) 색 정리(담채) + 먼 곳은 종이색으로 번짐(여백)  4) 한지 결
// 전역값 _InkAmount 가 0 이면 그대로 통과 (InkStyle 컴포넌트가 씬별로 켬)
Shader "Wongwirok/InkWash"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "InkWash"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float _InkAmount;
            float4 _InkPaper;     // 종이색
            float4 _InkColor;     // 먹색
            float _InkTint;       // 원래 색을 얼마나 남길지 (담채 정도)
            float _InkFarStart, _InkFarEnd; // 이 거리부터 종이색으로 번짐

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), u.x), u.y);
            }

            half3 Src(float2 uv) { return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb; }

            // 4분면 Kuwahara: 분산이 가장 작은 쪽 평균 → 붓 칠한 색면
            half3 Kuwahara(float2 uv, int r)
            {
                float2 px = _BlitTexture_TexelSize.xy;
                float3 m[4] = { (float3)0, (float3)0, (float3)0, (float3)0 };
                float3 s[4] = { (float3)0, (float3)0, (float3)0, (float3)0 };
                int2 o[4] = { int2(-r, -r), int2(0, -r), int2(-r, 0), int2(0, 0) };
                float n = (r + 1) * (r + 1);
                [unroll] for (int q = 0; q < 4; q++)
                {
                    [loop] for (int y = 0; y <= r; y++)
                    [loop] for (int x = 0; x <= r; x++)
                    {
                        float3 c = Src(uv + (float2(o[q] + int2(x, y))) * px);
                        m[q] += c; s[q] += c * c;
                    }
                }
                float best = 1e9; float3 col = 0;
                [unroll] for (int k = 0; k < 4; k++)
                {
                    float3 mean = m[k] / n;
                    float3 v = abs(s[k] / n - mean * mean);
                    float sv = v.r + v.g + v.b;
                    if (sv < best) { best = sv; col = mean; }
                }
                return col;
            }

            float EyeDepth(float2 uv) { return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams); }

            // 깊이·노멀 경계 → 먹선 세기
            float Edge(float2 uv, float w)
            {
                float2 px = _BlitTexture_TexelSize.xy * w;
                float d = EyeDepth(uv);
                float dl = EyeDepth(uv - float2(px.x, 0)), dr = EyeDepth(uv + float2(px.x, 0));
                float du = EyeDepth(uv + float2(0, px.y)), dd = EyeDepth(uv - float2(0, px.y));
                float depthEdge = (abs(dl + dr - 2 * d) + abs(du + dd - 2 * d)) / max(d, 0.5);
                float3 n = SampleSceneNormals(uv);
                float3 nl = SampleSceneNormals(uv - float2(px.x, 0)), nr = SampleSceneNormals(uv + float2(px.x, 0));
                float3 nu = SampleSceneNormals(uv + float2(0, px.y)), nd = SampleSceneNormals(uv - float2(0, px.y));
                float normalEdge = length(nl - nr) + length(nu - nd);
                float e = saturate(depthEdge * 18 - 0.15) + saturate(normalEdge * 0.6 - 0.55);
                return saturate(e) * saturate(1 - d / 160); // 아주 먼 곳은 선 없이 번짐만
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;
                half3 src = Src(uv);
                if (_InkAmount <= 0.001) return half4(src, 1);

                float2 sp = uv * _ScreenParams.xy;
                // 붓 결: 좌표를 잡음으로 살짝 흔들어 색면 경계가 붓질처럼 번지게
                float2 wob = (float2(Noise(sp * 0.02), Noise(sp * 0.02 + 17)) - 0.5) * _BlitTexture_TexelSize.xy * 3;
                half3 col = Kuwahara(uv + wob, 3);

                // 담채: 채도를 낮추고 밝은 곳은 종이색으로
                float lum = dot(col, float3(0.299, 0.587, 0.114));
                float3 washed = lerp(lum.xxx, col, _InkTint);
                float3 inked = lerp(_InkColor.rgb, _InkPaper.rgb, smoothstep(0.05, 0.85, lum));
                col = lerp(inked, inked * washed / max(lum, 0.05), _InkTint);

                // 먹선: 굵기와 진하기를 잡음으로 들쭉날쭉하게
                float brush = Noise(sp * 0.05);
                float e = Edge(uv, 1.0 + brush * 1.2) * smoothstep(0.15, 0.6, Noise(sp * 0.012 + 3) + 0.35);
                col = lerp(col, _InkColor.rgb, e * 0.85);

                // 여백: 먼 곳은 종이색으로 번짐 (하늘 포함)
                float d = EyeDepth(uv);
                float far = smoothstep(_InkFarStart, _InkFarEnd, d + (Noise(sp * 0.008) - 0.5) * 25);
                col = lerp(col, _InkPaper.rgb, far * 0.85);

                // 한지 결: 고운 잡음 + 섬유
                float grain = Noise(sp * 0.9) * 0.5 + Noise(sp * 0.25) * 0.5;
                float fiber = Noise(float2(sp.x * 0.04, sp.y * 0.6)) ;
                col *= 0.93 + 0.07 * grain;
                col = lerp(col, col * (0.95 + 0.05 * fiber), 0.6);

                return half4(lerp(src, col, _InkAmount), 1);
            }
            ENDHLSL
        }
    }
}
