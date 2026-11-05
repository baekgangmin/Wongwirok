Shader "Custom/GhostFireOrb"
{
    Properties
    {
        _BaseMap ("Base Texture", 2D) = "white" {}
        _CoreColor ("Core Color", Color) = (0.2, 0.5, 1.0, 1)
        _RimColor ("Rim Color", Color) = (0.7, 0.95, 1.0, 1)
        _EmissionIntensity ("Emission Intensity", Range(0, 10)) = 2.5
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _FlickerSpeed ("Flicker Speed", Range(0, 20)) = 8
        _FlickerStrength ("Flicker Strength", Range(0, 1)) = 0.4
        _NoiseScale ("Flame Noise Scale", Float) = 6
        _FlowSpeed ("Flame Flow Speed", Float) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
                float2 uv          : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _CoreColor;
                float4 _RimColor;
                float _EmissionIntensity;
                float _RimPower;
                float _FlickerSpeed;
                float _FlickerStrength;
                float _NoiseScale;
                float _FlowSpeed;
            CBUFFER_END

            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(hash(i + float3(0,0,0)), hash(i + float3(1,0,0)), f.x),
                         lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x), f.y),
                    lerp(lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x),
                         lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x), f.y),
                    f.z);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = positionInputs.positionCS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(IN.viewDirWS);

                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);

                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                float3 flowUV = float3(IN.uv * _NoiseScale, _Time.y * _FlowSpeed);
                float flameNoise = noise(flowUV) * 0.6 + noise(flowUV * 2.1 + 10.0) * 0.4;

                float flicker = sin(_Time.y * _FlickerSpeed + flameNoise * 6.2831) * 0.5 + 0.5;
                flicker = lerp(1.0 - _FlickerStrength, 1.0, flicker);

                float3 coreGlow = _CoreColor.rgb * baseTex.rgb * flameNoise;
                float3 rimGlow = _RimColor.rgb * fresnel;

                float3 emission = (coreGlow + rimGlow) * _EmissionIntensity * flicker;

                half4 color = half4(emission, 1.0);
                return color;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
