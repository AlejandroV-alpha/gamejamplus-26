Shader "GameJam/b_efecto/CinturonAsteroides"
{
    Properties { _OrbitSpeed("Orbital drift (degrees per second)", Range(-0.3,0.3)) = 0.06 }
    SubShader
    {
        Tags { "Queue"="Transparent-100" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _OrbitSpeed;
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; float4 center:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION; half3 color:COLOR; };
            float3 Spin(float3 p,float angle)
            {
                float s=sin(angle), c=cos(angle);
                return float3(c*p.x+s*p.z,p.y,-s*p.x+c*p.z);
            }
            Varyings vert(Attributes input)
            {
                Varyings output;
                float speed=lerp(0.018,0.055,frac(input.center.w*0.37));
                float angle=_Time.y*speed+input.center.w;
                float3 position=input.center.xyz+Spin(input.positionOS.xyz-input.center.xyz,angle);
                float orbit=radians(fmod(_Time.y*_OrbitSpeed,360.0));
                position=Spin(position,orbit);
                output.positionCS=TransformObjectToHClip(position);
                // Keep the belt behind all terrain, while preserving skybox depth.
                #if UNITY_REVERSED_Z
                    output.positionCS.z=output.positionCS.w*0.00001;
                #else
                    output.positionCS.z=output.positionCS.w*0.99999;
                #endif
                float light=saturate(dot(Spin(input.normalOS,angle+orbit),normalize(float3(-0.5,0.8,-0.3))));
                output.color=input.color.rgb*(0.55+light*0.75);
                return output;
            }
            half4 frag(Varyings input):SV_Target { return half4(input.color,1); }
            ENDHLSL
        }
    }
}
