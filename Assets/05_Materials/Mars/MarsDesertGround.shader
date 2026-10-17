Shader "GameJam/Mars/DesertGround"
{
    Properties
    {
        _GrainTex("Sand and soil",2D)="white"{}
        _RockTex("Rocky soil",2D)="white"{}
        _SandTint("Sand tint",Color)=(1,0.79,0.64,1)
        _RockTint("Rock tint",Color)=(0.68,0.48,0.35,1)
        [HideInInspector] _Control("Control",2D)="red"{}
        [HideInInspector] _Splat0("Layer 0",2D)="white"{}
        [HideInInspector] _Splat1("Layer 1",2D)="white"{}
        [HideInInspector] _Splat2("Layer 2",2D)="white"{}
        [HideInInspector] _Splat3("Layer 3",2D)="white"{}
        [HideInInspector] _MainTex("Base",2D)="white"{}
        [HideInInspector] _BaseColor("Base color",Color)=(1,1,1,1)
        [HideInInspector] _TerrainHolesTexture("Holes",2D)="white"{}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "TerrainCompatible"="True" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SplatmapVert
            #pragma fragment DesertFragment
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog
            #pragma shader_feature_local _TERRAIN_INSTANCED_PERPIXEL_NORMAL
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitPasses.hlsl"
            TEXTURE2D(_GrainTex);SAMPLER(sampler_GrainTex);
            TEXTURE2D(_RockTex);SAMPLER(sampler_RockTex);
            float4 _SandTint,_RockTint;
            half4 DesertFragment(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #ifdef _ALPHATEST_ON
                    ClipHoles(input.uvMainAndLM.xy);
                #endif
                InputData data;InitializeInputData(input,half3(0,0,1),data);
                float2 p=input.positionWS.xz/10;
                float variation=sin(p.x*0.23)+sin(p.y*0.17)+sin((p.x+p.y)*0.11);
                float blend=smoothstep(-0.7,0.7,variation);
                half3 a=SAMPLE_TEXTURE2D(_GrainTex,sampler_GrainTex,p).rgb;
                float2 rotated=float2(p.x*0.8-p.y*0.6,p.x*0.6+p.y*0.8)*0.63+float2(13.7,7.3);
                half3 b=SAMPLE_TEXTURE2D(_GrainTex,sampler_GrainTex,rotated).rgb;
                half3 soil=lerp(a,b,blend)*_SandTint.rgb;
                half3 rock=SAMPLE_TEXTURE2D(_RockTex,sampler_RockTex,p*0.71+float2(2.1,6.3)).rgb*_RockTint.rgb;
                float slope=1-saturate(data.normalWS.y);
                soil=lerp(soil,rock,smoothstep(0.04,0.25,slope)*0.65);
                float broad=sin(input.positionWS.x*0.014+sin(input.positionWS.z*0.009))*0.07;
                soil*=1+broad;
                Light sun=GetMainLight(data.shadowCoord);
                half diffuse=saturate(dot(data.normalWS,sun.direction));
                half3 color=soil*(SampleSH(data.normalWS)+sun.color*(0.15+diffuse*0.85)*sun.shadowAttenuation);
                return half4(MixFog(color,data.fogCoord),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Terrain/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Terrain/Lit/DepthOnly"
    }
}
