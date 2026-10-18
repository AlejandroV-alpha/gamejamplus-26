Shader "GameJam/Mars/SoftFootprint"
{
    Properties
    {
        _BaseMap("Package footprint", 2D) = "white" {}
        _Strength("Impression strength", Range(0,2)) = 1.1
        _Opacity("Fade", Range(0,1)) = 1
        _DrawDistance("Draw distance", Float) = 35
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Blend DstColor Zero
        ZWrite Off
        Cull Off
        Offset -1,-1
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            float _Strength, _Opacity, _DrawDistance;
            float4 _BaseMap_ST;
            struct A { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float distance:TEXCOORD1; };
            V Vert(A i) { V o; float3 p=TransformObjectToWorld(i.vertex.xyz); o.position=TransformWorldToHClip(p);o.uv=i.uv;o.distance=distance(p,_WorldSpaceCameraPos);return o; }
            half4 Frag(V i):SV_Target
            {
                half4 stamp=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv*_BaseMap_ST.xy+_BaseMap_ST.zw);
                // A white/transparent background contributes no color, hiding the texture's rectangle.
                float ink=saturate(1-max(stamp.r,max(stamp.g,stamp.b)))*stamp.a;
                float fade=1-smoothstep(_DrawDistance*.8,_DrawDistance,i.distance);
                float shade=1-saturate(ink*_Strength*_Opacity*fade);
                return half4(shade,shade,shade,1);
            }
            ENDHLSL
        }
    }
}
