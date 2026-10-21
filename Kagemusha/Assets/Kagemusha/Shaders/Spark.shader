Shader "Kagemusha/Spark" {
 Properties { _Color("Color",Color)=(1,1,1,1) }
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent"} Pass {
 Blend SrcAlpha One
 ZWrite Off
 Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 CBUFFER_START(UnityPerMaterial) float4 _Color; CBUFFER_END
 V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.color=i.color;return o;}
 half4 frag(V i):SV_Target{float d=length(i.uv-.5)*2;float a=saturate(1-d);return half4(i.color.rgb*_Color.rgb,i.color.a*_Color.a*a*a);}
 ENDHLSL
 } }
}
