Shader "Kagemusha/NinjaFlame" {
 Properties { _Color("Flame tint",Color)=(1,.65,.25,.8) }
 SubShader { Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent"}
 Pass { Cull Off Blend SrcAlpha One ZWrite Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
 struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
 CBUFFER_START(UnityPerMaterial) float4 _Color; CBUFFER_END
 V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;}
 half4 frag(V i):SV_Target {
  float u=i.uv.x*6.2831853,v=i.uv.y,t=_Time.y;
  float flow=sin(u*5+v*21-t*19)+.52*sin(u*9-v*35+t*27)+.3*sin(u*13+v*48-t*31);
  float tongue=smoothstep(-.45,.5,flow),heat=saturate((1-v)*.7+tongue*.5);
  float3 hot=lerp(float3(2.5,.12,.008),float3(3.2,1.5,.12),smoothstep(.15,.8,heat));
  hot=lerp(hot,float3(3.4,2.8,1.25),smoothstep(.7,1,heat));
  float edge=1-smoothstep(.62+flow*.08,1,v);
  return half4(hot*_Color.rgb,edge*(.18+tongue*.55)*_Color.a);
 }
 ENDHLSL
 }
 }
}
