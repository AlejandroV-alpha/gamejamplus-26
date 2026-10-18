Shader "GameJam/Neptune/Storm Sky"
{
    Properties
    {
        _ZenithColor ("Upper sky", Color) = (.045,.035,.15,1)
        _HorizonColor ("Horizon and mist", Color) = (.15,.13,.3,1)
        _CloudColor ("Clouds", Color) = (.26,.21,.43,1)
        _CloudCoverage ("Cloud coverage", Range(0,1)) = .58
        _CloudSpeed ("Cloud speed", Range(0,.1)) = .004
        _StarBrightness ("Star brightness", Range(0,1)) = .24
        _MoonDirection ("Triton direction", Vector) = (.55,.32,.76,0)
        _MoonSize ("Triton apparent radius", Range(.005,.08)) = .028
        _SunDirection ("Distant sun direction", Vector) = (-.45,.2,.87,0)
        _ProteusDirection ("Proteus direction", Vector) = (-.38,.45,.81,0)
        _ProteusSize ("Proteus apparent radius", Range(.002,.04)) = .011
        _NereidDirection ("Nereid direction", Vector) = (.8,.55,-.24,0)
        _CelestialSpeed ("Artistic sky drift (degrees per second)", Range(0,.05)) = .008
        _PlanetBrightness ("Distant planet brightness", Range(0,1)) = .3
        _WindStrength ("Cloud turbulence", Range(0,1)) = .35
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _ZenithColor, _HorizonColor, _CloudColor, _MoonDirection, _SunDirection;
            float _CloudCoverage, _CloudSpeed, _StarBrightness, _MoonSize;
            float4 _ProteusDirection, _NereidDirection;
            float _ProteusSize, _CelestialSpeed, _PlanetBrightness, _WindStrength;
            CBUFFER_END
            struct Attributes { float4 vertex:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 vertex:SV_POSITION; float3 direction:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = TransformObjectToHClip(input.vertex.xyz);
                output.direction = input.vertex.xyz;
                return output;
            }
            float Hash(float3 p)
            {
                p=frac(p*.1031); p+=dot(p,p.yzx+33.33);
                return frac((p.x+p.y)*p.z);
            }
            float Noise(float3 p)
            {
                float3 i=floor(p),f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(Hash(i),Hash(i+float3(1,0,0)),f.x),
                    lerp(Hash(i+float3(0,1,0)),Hash(i+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(Hash(i+float3(0,0,1)),Hash(i+float3(1,0,1)),f.x),
                    lerp(Hash(i+float3(0,1,1)),Hash(i+float3(1,1,1)),f.x),f.y),f.z);
            }
            float Clouds(float3 p)
            {
                return Noise(p)*.57+Noise(p*2.03+13)*.28+Noise(p*4.11+29)*.15;
            }
            float3 Drift(float3 direction, float degrees)
            {
                float angle=radians(degrees);
                float s=sin(angle),c=cos(angle);
                return float3(c*direction.x-s*direction.z,direction.y,s*direction.x+c*direction.z);
            }
            float PointLight(float3 direction,float3 center,float radius)
            {
                center=normalize(center);
                float angularDistance=length(cross(direction,center));
                float aa=max(fwidth(angularDistance),.00012);
                return (1-smoothstep(radius,radius+aa,angularDistance))*step(0,dot(direction,center));
            }
            half4 Frag(Varyings input):SV_Target
            {
                float3 d=normalize(input.direction);
                float3 celestial=Drift(d,_Time.y*_CelestialSpeed);
                float elevation=saturate(d.y);
                float3 color=lerp(_HorizonColor.rgb,_ZenithColor.rgb,pow(elevation,.55));
                float2 skyUV=float2(atan2(celestial.z,celestial.x)/6.2831853+.5,asin(clamp(celestial.y,-1,1))/3.1415927+.5);
                float2 starGrid=skyUV*float2(280,140);
                float2 cell=floor(starGrid),offset=frac(starGrid)-.5;
                float random=Hash(float3(cell,31));
                float star=(1-smoothstep(.015,.11,length(offset))) * step(.994,random);
                float twinkle=.9+.1*sin(_Time.y*(.7+random)+random*70);
                color+=star*twinkle*_StarBrightness*smoothstep(.1,.55,elevation)*float3(.75,.85,1);

                // A small, artistic view of Triton; cloud layers pass in front of it.
                float3 moonDir=normalize(_MoonDirection.xyz);
                float3 right=normalize(cross(float3(0,1,0),moonDir));
                float3 up=cross(moonDir,right);
                float2 moonUV=float2(dot(celestial,right),dot(celestial,up))/max(_MoonSize,.001);
                float radius2=dot(moonUV,moonUV);
                float moonMask=(1-smoothstep(.94,1.02,radius2))*step(.9,dot(celestial,moonDir));
                float3 moonNormal=float3(moonUV,sqrt(saturate(1-radius2)));
                float lit=.09+.7*saturate(dot(moonNormal,normalize(float3(-.8,.3,.5))));
                float lunarPattern=Noise(float3(moonUV*9,17))*.18+.82;
                color=lerp(color,float3(.48,.61,.72)*lit*lunarPattern,moonMask);

                // Proteus is smaller, darker and less spherical than the icy Triton.
                float3 proteusDir=normalize(_ProteusDirection.xyz);
                float3 proteusRight=normalize(cross(float3(0,1,0),proteusDir));
                float3 proteusUp=cross(proteusDir,proteusRight);
                float2 proteusUV=float2(dot(celestial,proteusRight),dot(celestial,proteusUp))/max(_ProteusSize,.001);
                float irregular=.88+Noise(float3(proteusUV*3.5,39))*.18;
                float pr=dot(proteusUV,proteusUV)/(irregular*irregular);
                float proteusMask=(1-smoothstep(.92,1.05,pr))*step(.9,dot(celestial,proteusDir));
                float3 pn=float3(proteusUV,sqrt(saturate(1-pr)));
                float proteusLight=.12+.65*saturate(dot(pn,normalize(float3(-.8,.3,.5))));
                color=lerp(color,float3(.24,.29,.34)*proteusLight,proteusMask);
                // Nereid and remote planets are deliberately faint points, not large globes.
                color+=PointLight(celestial,_NereidDirection.xyz,.00025)*float3(.12,.15,.18);
                color+=PointLight(celestial,float3(-.68,.22,.70),.00035)*float3(.65,.8,.9)*_PlanetBrightness; // Uranus
                color+=PointLight(celestial,float3(-.48,.24,.84),.0003)*float3(.95,.85,.65)*_PlanetBrightness; // Saturn
                color+=PointLight(celestial,float3(-.54,.18,.82),.00035)*float3(1,.88,.73)*_PlanetBrightness; // Jupiter

                float alignment=dot(d,normalize(_SunDirection.xyz));
                float sun=smoothstep(.999991,.999998,alignment);
                float halo=pow(saturate(alignment),650)*.055;
                color+=float3(.65,.79,1)*(sun*1.6+halo);

                float time=_Time.y*_CloudSpeed;
                float3 p=d*float3(3.2,19,3.2)+float3(time,0,time*.25);
                p.xz+=_WindStrength*float2(sin(d.y*19+time*.8),cos(d.y*13-time*.6));
                float cloudNoise=Clouds(p);
                float cloud=smoothstep(.49-_CloudCoverage*.17,.69-_CloudCoverage*.17,cloudNoise);
                cloud*=smoothstep(-.03,.16,d.y)*.86;
                float3 cloudColor=_CloudColor.rgb*lerp(.62,1.12,cloudNoise);
                color=lerp(color,cloudColor,cloud);
                // A second faint moving layer stretches the clouds along the horizon.
                float veil=Clouds(d*float3(2,29,2)+float3(-time*.65,4,time*.18));
                color=lerp(color,_CloudColor.rgb*.8,smoothstep(.60,.8,veil)*.22*smoothstep(0,.2,d.y));
                float highWisps=Noise(d*float3(5,48,5)+float3(time*1.7,17,-time*.3));
                color=lerp(color,_CloudColor.rgb*1.1,smoothstep(.68,.86,highWisps)*.13*smoothstep(.08,.35,d.y));
                return half4(color,1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
