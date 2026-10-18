Shader "JungleNightmare/CreatureUnlit" {
 Properties { _MainTex("Texture",2D)="white"{} _Tint("Tint",Color)=(1,1,1,1) }
 SubShader { Tags {"RenderType"="Opaque"} Pass {
 Cull Off
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex;float4 _Tint;
 struct a {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 struct f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 f vert(a v){f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
 fixed4 frag(f i):SV_Target{return tex2D(_MainTex,i.uv)*_Tint*i.color;}
 ENDCG
 } }
}

