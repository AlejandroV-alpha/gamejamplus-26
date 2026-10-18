Shader "GameJam/Lobby/Portal"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            V vert(A a) { V o; o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;return o; }
            half4 frag(V i):SV_Target
            {
                float2 p=i.uv-.5;
                float edge=pow(saturate(max(abs(p.x),abs(p.y))*2),12);
                float wave=.5+.5*sin(p.y*28+sin(p.x*16+_Time.y*2)*2-_Time.y*4);
                float3 color=lerp(float3(.18,.035,.018),float3(.68,.21,.04),wave*.6);
                color+=edge*float3(.05,1.2,1.4);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
