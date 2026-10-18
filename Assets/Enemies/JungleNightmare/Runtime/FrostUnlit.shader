Shader "JungleNightmare/FrostUnlit" {
 Properties { _MainTex("Texture",2D)="white"{} _Tint("Tint",Color)=(1,1,1,1) _Soft("Soft particle",Float)=0 }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" }
  Pass {
   
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off Cull Off
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex; float4 _Tint;float _Soft;
   struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
   fixed4 frag(v2f i):SV_Target {fixed4 c=tex2D(_MainTex,i.uv)*_Tint*i.color;float r=length(i.uv*2-1);c.a*=lerp(1,pow(saturate(1-r),2),_Soft);return c;}
   ENDCG
  }
 }
}

