Shader "GameJam/Neptune/Ice Crystals"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 vertex:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; half fog:TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.world = TransformObjectToWorld(input.vertex.xyz);
                output.vertex = TransformWorldToHClip(output.world);
                output.color = input.color;
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.vertex.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 centered = input.uv * 2 - 1;
                float disc = 1 - smoothstep(.12, 1, dot(centered, centered));
                float nearFade = smoothstep(2, 5, distance(_WorldSpaceCameraPos, input.world));
                half3 color = MixFog(input.color.rgb, input.fog);
                return half4(color, input.color.a * disc * nearFade);
            }
            ENDHLSL
        }
    }
}
