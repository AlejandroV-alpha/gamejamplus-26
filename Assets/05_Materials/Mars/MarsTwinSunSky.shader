Shader "GameJam/Mars/TwinSunSky"
{
    Properties
    {
        _Zenith("Upper sky",Color)=(0.30,0.12,0.15,1)
        _Horizon("Horizon haze",Color)=(0.94,0.49,0.29,1)
        _SunA("Primary sun direction",Vector)=(-0.4,0.14,0.906,0)
        _SunB("Secondary sun direction",Vector)=(-0.21,0.07,0.975,0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _Zenith, _Horizon, _SunA, _SunB;
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.direction=input.positionOS.xyz;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float3 d=normalize(input.direction);
                float altitude=saturate(d.y);
                float3 color=lerp(_Horizon.rgb,_Zenith.rgb,pow(altitude,0.6));
                float a=length(d-normalize(_SunA.xyz));
                float b=length(d-normalize(_SunB.xyz));
                color+=float3(1.0,0.42,0.10)*exp(-a*13)*0.7;
                color+=float3(1.0,0.30,0.10)*exp(-b*17)*0.35;
                float diskA=1-smoothstep(0.018,0.021,a);
                float diskB=1-smoothstep(0.012,0.015,b);
                color=lerp(color,float3(2.8,2.3,1.45),diskA);
                color=lerp(color,float3(2.5,1.4,0.65),diskB);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
