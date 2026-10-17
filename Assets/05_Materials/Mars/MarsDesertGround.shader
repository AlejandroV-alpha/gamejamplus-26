Shader "GameJam/Mars/DesertGround"
{
    Properties
    {
        _GrainTex("Sand and soil",2D)="white"{}
        _RockTex("Rocky soil",2D)="white"{}
        _SandTint("Sand tint",Color)=(1,0.79,0.64,1)
        _RockTint("Rock tint",Color)=(0.68,0.48,0.35,1)
        _SandColor("Cartoon sand",Color)=(0.59,0.225,0.115,1)
        _StoneColor("Cartoon bedrock",Color)=(0.39,0.125,0.075,1)
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
            float4 _SandTint,_RockTint,_SandColor,_StoneColor;
            float2 TileOffset(float2 cell)
            {
                return frac(sin(float2(dot(cell,float2(127.1,311.7)),dot(cell,float2(269.5,183.3))))*43758.5453)*17;
            }
            // Continuous triangular blending hides the source texture's square repeat.
            // Explicit gradients keep mip selection stable across randomized tile offsets.
            half3 SandWithoutTiles(float2 uv)
            {
                float2 skew=float2(uv.x-uv.y*0.57735027,uv.y*1.15470054);
                float2 cell=floor(skew), f=frac(skew);
                float2 c0,c1,c2;float3 weights;
                if(f.x+f.y>1)
                {
                    c0=cell+1;c1=cell+float2(0,1);c2=cell+float2(1,0);
                    weights=float3(f.x+f.y-1,1-f.x,1-f.y);
                }
                else
                {
                    c0=cell;c1=cell+float2(1,0);c2=cell+float2(0,1);
                    weights=float3(1-f.x-f.y,f.x,f.y);
                }
                float2 dx=ddx(uv),dy=ddy(uv);
                half3 a=SAMPLE_TEXTURE2D_GRAD(_GrainTex,sampler_GrainTex,uv+TileOffset(c0),dx,dy).rgb;
                half3 b=SAMPLE_TEXTURE2D_GRAD(_GrainTex,sampler_GrainTex,uv+TileOffset(c1),dx,dy).rgb;
                half3 c=SAMPLE_TEXTURE2D_GRAD(_GrainTex,sampler_GrainTex,uv+TileOffset(c2),dx,dy).rgb;
                return a*weights.x+b*weights.y+c*weights.z;
            }
            half3 RockWithoutTiles(float2 uv)
            {
                float2 skew=float2(uv.x-uv.y*0.57735027,uv.y*1.15470054);
                float2 cell=floor(skew), f=frac(skew);
                float2 c0,c1,c2;float3 weights;
                if(f.x+f.y>1)
                {
                    c0=cell+1;c1=cell+float2(0,1);c2=cell+float2(1,0);
                    weights=float3(f.x+f.y-1,1-f.x,1-f.y);
                }
                else
                {
                    c0=cell;c1=cell+float2(1,0);c2=cell+float2(0,1);
                    weights=float3(1-f.x-f.y,f.x,f.y);
                }
                // Keep one plate pattern dominant; equal blends were washing out the fissures.
                weights=pow(max(weights,0.001),3);
                weights/=weights.x+weights.y+weights.z;
                // Preserve the supplied illustrated detail.
                float2 dx=ddx(uv),dy=ddy(uv);
                half3 a=SAMPLE_TEXTURE2D_GRAD(_RockTex,sampler_RockTex,uv+TileOffset(c0),dx,dy).rgb;
                half3 b=SAMPLE_TEXTURE2D_GRAD(_RockTex,sampler_RockTex,uv+TileOffset(c1),dx,dy).rgb;
                half3 c=SAMPLE_TEXTURE2D_GRAD(_RockTex,sampler_RockTex,uv+TileOffset(c2),dx,dy).rgb;
                return a*weights.x+b*weights.y+c*weights.z;
            }
            half4 DesertFragment(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #ifdef _ALPHATEST_ON
                    ClipHoles(input.uvMainAndLM.xy);
                #endif
                InputData data;InitializeInputData(input,half3(0,0,1),data);
                float2 p=input.positionWS.xz/10;
                p+=float2(sin(p.y*0.19+p.x*0.07),sin(p.x*0.16-p.y*0.05))*0.38;
                half3 soil=SandWithoutTiles(p);
                half3 rock=RockWithoutTiles(p*0.90+float2(2.1,6.3));
                float2 controlUV=(input.uvMainAndLM.xy*(_Control_TexelSize.zw-1)+0.5)*_Control_TexelSize.xy;
                // Terrain layer 0 is bedrock; layer 1 is sand painted with the Terrain brush.
                half sandMask=SAMPLE_TEXTURE2D(_Control,sampler_Control,controlUV).g;
                float broad=sin(input.positionWS.x*0.014+sin(input.positionWS.z*0.009))*0.06;
                // Keep the supplied artwork colors and cracks rather than flattening to a tint.
                half3 redBedrock=rock*_RockTint.rgb;
                half3 paleSand=soil*_SandTint.rgb;
                soil=lerp(redBedrock,paleSand,sandMask)*(1+broad);
                Light sun=GetMainLight(data.shadowCoord);
                half diffuse=saturate(dot(data.normalWS,sun.direction));
                half toon=0.42+0.25*smoothstep(0.22,0.30,diffuse)+0.33*smoothstep(0.64,0.72,diffuse);
                half3 color=soil*(SampleSH(data.normalWS)*0.8+sun.color*toon*lerp(0.6,1,sun.shadowAttenuation));
                return half4(MixFog(color,data.fogCoord),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Terrain/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Terrain/Lit/DepthOnly"
    }
}

