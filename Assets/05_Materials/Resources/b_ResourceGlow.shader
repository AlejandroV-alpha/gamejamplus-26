Shader "GameJam/Resources/Soft Crystal Glow"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 vertex:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 vertex:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; half fog:TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.world=TransformObjectToWorld(input.vertex.xyz);
                output.vertex=TransformWorldToHClip(output.world);
                output.color=input.color;
                output.uv=input.uv;
                output.fog=ComputeFogFactor(output.vertex.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=input.uv*2-1;
                float glow=pow(saturate(1-dot(p,p)),2);
                float distanceToCamera=distance(_WorldSpaceCameraPos,input.world);
                float visible=smoothstep(.2,.7,distanceToCamera)*(1-smoothstep(25,45,distanceToCamera));
                half fog=ComputeFogIntensity(input.fog);
                return half4(input.color.rgb,input.color.a*glow*visible*fog);
            }
            ENDHLSL
        }
    }
}
