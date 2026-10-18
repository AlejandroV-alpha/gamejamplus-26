Shader "GameJam/Lobby/DestinationLED"
{
    Properties { _PanelRect("World XY screen bounds", Vector) = (1.47,2.1,3,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _PanelRect;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes v) { Varyings o; float3 world=TransformObjectToWorld(v.positionOS.xyz); o.positionCS=TransformWorldToHClip(world); o.uv=(world.xy-_PanelRect.xy)/_PanelRect.zw; return o; }
            half4 frag(Varyings i) : SV_Target
            {
                float2 uv=i.uv;
                float3 color=float3(.012,.032,.048);
                // A flat Mars diagram, embedded in the display rather than a floating globe.
                float2 p=(uv-float2(.22,.5))*float2(3,1);
                float radius=length(p);
                float disc=1-smoothstep(.30,.305,radius);
                float bands=.5+.5*sin(p.y*48+sin(p.x*23)*1.2);
                float light=saturate(.8-p.x*1.2+p.y*.5);
                color=lerp(color,lerp(float3(.28,.055,.025),float3(.95,.32,.10),light)*(.82+.18*bands),disc);
                float ring=(1-smoothstep(.002,.006,abs(radius-.34)))*.5;
                color+=ring*float3(.1,.55,.65);
                float border=step(uv.y,.035)+step(.965,uv.y);
                color+=border*float3(.02,.28,.32);
                // Fade the LED scanlines when distant to avoid moire patterns.
                float scanFade=saturate(1-fwidth(uv.y)*600);
                color*=.96+.04*sin(uv.y*600)*scanFade;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
