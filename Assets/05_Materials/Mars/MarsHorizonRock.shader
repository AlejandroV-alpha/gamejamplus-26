Shader "GameJam/Mars/HorizonRock"
{
    Properties
    {
        _BaseMap("Rock grain", 2D) = "white" {}
        _BaseColor("Rock tint", Color) = (0.48,0.34,0.27,1)
        _TextureScale("Grain scale", Float) = 0.12
        _GrainStrength("Grain strength", Range(0,1)) = 1
        _StrataStrength("Strata strength", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _TextureScale;
                float _GrainStrength;
                float _StrataStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float fog:TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.fog=ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float3 n=normalize(input.normalWS);
                float3 w=pow(abs(n),4); w/=max(w.x+w.y+w.z,0.001);
                float3 p=input.positionWS*_TextureScale;
                half3 grain=half3(1,1,1);
                if(_GrainStrength>0.001) grain=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.yz).rgb*w.x
                    +SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xz).rgb*w.y
                    +SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xy).rgb*w.z;
                float bands=0.82+0.18*smoothstep(-0.2,0.2,sin(input.positionWS.y*0.9+grain.r*1.5));
                Light sun=GetMainLight();
                float diffuse=saturate(dot(n,sun.direction));
                float toon=0.38+0.28*smoothstep(0.22,0.28,diffuse)+0.34*smoothstep(0.65,0.71,diffuse);
                half3 lighting=SampleSH(n)*0.8+sun.color*toon;
                half3 color=lerp(half3(1,1,1),grain,_GrainStrength)*_BaseColor.rgb*lerp(1,bands,_StrataStrength)*lighting;
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
    }
}
