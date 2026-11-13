// 손그림 물: 깊이에 따라 옅은 쪽빛→짙은 먹물빛, 붓으로 그은 물결 줄, 물가에 종이색 거품 띠와 먹선
Shader "Wongwirok/InkWater"
{
    Properties
    {
        _Shallow ("Shallow", Color) = (0.55, 0.68, 0.7, 1)
        _Deep ("Deep", Color) = (0.2, 0.3, 0.36, 1)
        _Foam ("Shore Foam", Color) = (0.94, 0.92, 0.86, 1)
        _InkColor ("Ink", Color) = (0.12, 0.13, 0.15, 1)
        _DepthRange ("Depth Range (m)", Float) = 2.5
        _FoamWidth ("Foam Width (m)", Float) = 0.35
        _RippleScale ("Ripple Scale", Float) = 0.35
        _Alpha ("Alpha", Range(0, 1)) = 0.88
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Shallow, _Deep, _Foam, _InkColor;
                float _DepthRange, _FoamWidth, _RippleScale, _Alpha;
            CBUFFER_END
            struct A { float4 pos : POSITION; };
            struct V { float4 pos : SV_POSITION; float3 ws : TEXCOORD0; float4 sp : TEXCOORD1; float fog : TEXCOORD2; };
            V vert(A i)
            {
                V o; VertexPositionInputs p = GetVertexPositionInputs(i.pos.xyz);
                o.pos = p.positionCS; o.ws = p.positionWS; o.sp = ComputeScreenPos(p.positionCS); o.fog = ComputeFogFactor(p.positionCS.z); return o;
            }
            float H(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float N(float2 p)
            {
                float2 i = floor(p), f = frac(p); float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(H(i), H(i + float2(1, 0)), u.x), lerp(H(i + float2(0, 1)), H(i + 1), u.x), u.y);
            }
            half4 frag(V i) : SV_Target
            {
                float2 suv = i.sp.xy / i.sp.w;
                float scene = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float surf = LinearEyeDepth(i.pos.z, _ZBufferParams);
                float depth = max(0, scene - surf);

                half3 col = lerp(_Shallow.rgb, _Deep.rgb, saturate(depth / _DepthRange));

                // 물결: 가로로 길쭉한 잡음 띠를 얇게 잘라 붓 획처럼
                float2 w = i.ws.xz * _RippleScale;
                float t = _Time.y * 0.15;
                float r = N(float2(w.x * 0.6 + t, w.y * 2.2)) + 0.5 * N(float2(w.x * 1.3 - t, w.y * 4.0));
                float stroke = smoothstep(0.06, 0.0, abs(r - 0.9)) * smoothstep(0.3, 0.7, N(w * 0.5 + 7));
                col = lerp(col, _Foam.rgb, stroke * 0.55);

                // 물가 거품 띠 + 바깥 먹선
                float foam = 1 - smoothstep(_FoamWidth * 0.6, _FoamWidth, depth);
                float edge = smoothstep(_FoamWidth * 0.55, _FoamWidth * 0.75, depth) * (1 - smoothstep(_FoamWidth * 0.75, _FoamWidth * 1.0, depth));
                col = lerp(col, _Foam.rgb, foam);
                col = lerp(col, _InkColor.rgb, edge * 0.8);

                col = MixFog(col, i.fog);
                return half4(col, saturate(_Alpha + foam * 0.12));
            }
            ENDHLSL
        }
    }
}
