Shader "GameJam/Neptune/Diamond Rain"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 vertex:POSITION; float3 normal:NORMAL; half4 color:COLOR; };
            struct Varyings { float4 vertex:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; half4 color:COLOR; half fog:TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.world=TransformObjectToWorld(input.vertex.xyz);
                output.vertex=TransformWorldToHClip(output.world);
                output.normal=TransformObjectToWorldNormal(input.normal);
                output.color=input.color;
                output.fog=ComputeFogFactor(output.vertex.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                clip(distance(_WorldSpaceCameraPos,input.world)-1.5);
                half3 normal=normalize(input.normal);
                half3 view=GetWorldSpaceNormalizeViewDir(input.world);
                Light sun=GetMainLight();
                half diffuse=saturate(dot(normal,sun.direction));
                half glint=pow(saturate(dot(normal,normalize(sun.direction+view))),72);
                half edge=pow(1-saturate(dot(normal,view)),3);
                half3 color=input.color.rgb*(.18+diffuse*.55)+sun.color*glint*1.3+edge*half3(.1,.2,.3);
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
    }
}
