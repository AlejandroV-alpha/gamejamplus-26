Shader "GameJam/Neptune/Natural Ice"
{
    Properties
    {
        _IceColor ("Ice tint", Color) = (0.22,0.57,0.73,1)
        _FrostColor ("Opaque frost", Color) = (0.79,0.9,0.94,1)
        _DepthAppearance ("Ice depth appearance", Range(0,1)) = 0.76
        _FrostCoverage ("Frost coverage", Range(0,1)) = 0.57
        _PatchScale ("Patch size variation", Range(0.03,2)) = 0.24
        _IceSmoothness ("Ice smoothness", Range(0,1)) = 0.92
        _FrostSmoothness ("Frost smoothness", Range(0,1)) = 0.28
        [Normal] _BumpMap ("Ice surface detail", 2D) = "bump" {}
        _BumpScale ("Surface detail strength", Range(0,1)) = 0.18
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "NaturalIce"
            Tags { "LightMode"="UniversalForward" }
            Blend One Zero
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _IceColor, _FrostColor, _BumpMap_ST;
                float _DepthAppearance, _FrostCoverage, _PatchScale;
                float _IceSmoothness, _FrostSmoothness, _BumpScale;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                half fog : TEXCOORD4;
            };
            float Hash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }
            float Noise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f*f*(3-2*f);
                return lerp(lerp(lerp(Hash(i), Hash(i+float3(1,0,0)), f.x),
                    lerp(Hash(i+float3(0,1,0)),Hash(i+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(Hash(i+float3(0,0,1)),Hash(i+float3(1,0,1)),f.x),
                    lerp(Hash(i+float3(0,1,1)),Hash(i+float3(1,1,1)),f.x),f.y),f.z);
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = n.normalWS;
                output.tangentWS = half4(n.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BumpMap);
                output.fog = ComputeFogFactor(p.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 geometricNormal = normalize(input.normalWS);
                half3 tangent = normalize(input.tangentWS.xyz);
                half3 bitangent = cross(geometricNormal, tangent) * input.tangentWS.w;
                half3 detail = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 normal = NormalizeNormalPerPixel(TransformTangentToWorld(detail,
                    half3x3(tangent, bitangent, geometricNormal)));
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 p = input.positionWS * _PatchScale;
                float patch = Noise(p) * .75 + Noise(p * 3.7) * .25;
                // Frost gathers on upward-facing surfaces; patches remain continuous across UV seams.
                float frost = smoothstep(.42, .64, patch + (_FrostCoverage-.5)*.65
                    + saturate(geometricNormal.y)*.17);
                float edge = pow(1-saturate(dot(normal,view)), 3);
                // Simulate depth inside solid ice using view-dependent color, without exposing hollow meshes.
                half3 deepIce = _IceColor.rgb * lerp(.85, .5, _DepthAppearance);
                half3 clearIce = lerp(deepIce, _IceColor.rgb * 1.18, .35 + edge*.65);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = lerp(clearIce, _FrostColor.rgb, frost);
                surface.metallic = 0;
                surface.specular = half3(.04,.04,.04);
                surface.smoothness = lerp(_IceSmoothness, _FrostSmoothness, frost);
                surface.normalTS = detail;
                surface.occlusion = 1;
                surface.alpha = 1;
                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.normalWS = normal;
                lighting.viewDirectionWS = view;
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                lighting.bakedGI = SampleSH(normal);
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask = half4(1,1,1,1);
                half4 result = UniversalFragmentPBR(lighting, surface);
                result.rgb = MixFog(result.rgb, input.fog);
                result.a = 1;
                return result;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
