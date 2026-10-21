Shader "Kagemusha/NinjaToon" {
 Properties { _Color("Color",Color)=(.2,.7,.6,1) }
 SubShader { Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
 Pass { Cull Back Blend SrcAlpha OneMinusSrcAlpha ZWrite On
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
 struct V {float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;float3 positionWS:TEXCOORD1;};
 CBUFFER_START(UnityPerMaterial) float4 _Color; CBUFFER_END
 V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(i.normalOS);return o;}
 half4 frag(V i):SV_Target {float light=dot(normalize(i.normalWS),normalize(float3(-.4,.8,-.6)));float band=light>.5?1:light>-.15?.72:.43;float rim=pow(1-saturate(dot(normalize(i.normalWS),normalize(_WorldSpaceCameraPos-i.positionWS))),3);return half4(_Color.rgb*(band+rim*.16),_Color.a);}
 ENDHLSL
 }
 }
}
