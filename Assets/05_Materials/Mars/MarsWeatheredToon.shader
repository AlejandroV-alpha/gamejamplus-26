Shader "GameJam/Mars/WeatheredToonPlaster"
{
    Properties
    {
        _BaseColor("Plaster",Color)=(0.73,0.61,0.42,1)
        _BleachedColor("Sun bleached plaster",Color)=(0.95,0.84,0.63,1)
        _ExposedColor("Exposed earth",Color)=(0.46,0.25,0.13,1)
        _GroundHeight("Ground height",Float)=15
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Cull Off
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor,_BleachedColor,_ExposedColor;
            float _GroundHeight;
            CBUFFER_END
            struct A { float4 positionOS:POSITION;float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION;float3 p:TEXCOORD0;float3 n:TEXCOORD1; };
            float Hash(float3 p) { return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453); }
            float Noise(float3 p)
            {
                float3 i=floor(p),f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(Hash(i),Hash(i+float3(1,0,0)),f.x),lerp(Hash(i+float3(0,1,0)),Hash(i+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(Hash(i+float3(0,0,1)),Hash(i+float3(1,0,1)),f.x),lerp(Hash(i+float3(0,1,1)),Hash(i+float3(1,1,1)),f.x),f.y),f.z);
            }
            V Vert(A input)
            {
                V o;o.p=TransformObjectToWorld(input.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.p);o.n=TransformObjectToWorldNormal(input.normalOS);return o;
            }
            half4 Frag(V i):SV_Target
            {
                float3 n=normalize(i.n);Light sun=GetMainLight();
                float height=max(0,i.p.y-_GroundHeight);
                float wear=Noise(i.p*3.2)+Noise(i.p*8.0)*0.35;
                float chips=(1-smoothstep(0.25,0.32,wear))*lerp(0.85,0.25,smoothstep(1.4,4.8,height));
                float bleach=saturate(n.y*0.6+height*0.09)*smoothstep(0.25,0.75,Noise(i.p*0.4));
                float3 color=lerp(_BaseColor.rgb,_BleachedColor.rgb,bleach);
                color=lerp(color,_ExposedColor.rgb,chips*0.85);
                color*=lerp(0.68,1,smoothstep(0.15,1.4,height));
                float ndl=dot(n,sun.direction);
                float band=0.50+0.25*smoothstep(-0.06,0.06,ndl)+0.25*smoothstep(0.50,0.60,ndl);
                float3 lighting=SampleSH(n)*0.6+sun.color*band;
                return half4(color*lighting,1);
            }
            ENDHLSL
        }
    }
}
