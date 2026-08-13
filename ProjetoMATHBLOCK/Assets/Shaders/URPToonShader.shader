Shader "Custom/URPToonShader" {
Properties { _MainTex("Texture",2D)="white"{} _BaseColor("Base Color",Color)=(1,1,1,1) [HideInInspector]_Color("Color",Color)=(1,1,1,1) _ShadeColor("Shade Tint",Color)=(.48,.55,.72,1) _ShadeSteps("Shade Steps",Range(2,6))=3 _ShadeSmoothness("Band Softness",Range(.001,.25))=.08 _MinBrightness("Shadow Brightness",Range(0,1))=.38 _MaxBrightness("Light Brightness",Range(0,2))=1 [Toggle(_SPECULAR_ON)]_EnableSpecular("Specular",Float)=1 _SpecularColor("Specular Color",Color)=(1,.96,.85,1) _Glossiness("Specular Size",Range(.02,1))=.28 _SpecularSmoothness("Specular Softness",Range(.001,.25))=.045 [Toggle(_RIM_ON)]_EnableRim("Rim",Float)=1 _RimColor("Rim Color",Color)=(1,.96,.86,1) _RimAmount("Rim Amount",Range(0,1))=.72 _RimSmoothness("Rim Softness",Range(.001,.5))=.12 _AmbientStrength("Ambient",Range(0,1))=.42 [Toggle(_OUTLINE_ON)]_EnableOutline("Outline",Float)=1 _OutlineColor("Outline Color",Color)=(.035,.04,.055,1) _OutlineWidth("Outline Width",Range(0,.03))=.006 _DitherAmount("Dither Amount",Range(0,1))=0 _Opacity("Opacity",Range(0,1))=1 _DotScale("Dot Scale",Range(2,40))=7 _DotColor("Dot Color",Color)=(.04,.04,.04,1) _Cutoff("Cutoff",Range(0,1))=.5 [HideInInspector]_Mode("Mode",Float)=0 [HideInInspector]_SrcBlend("Src Blend",Float)=1 [HideInInspector]_DstBlend("Dst Blend",Float)=0 [HideInInspector]_ZWrite("ZWrite",Float)=1 }
SubShader { Tags{"RenderType"="Opaque" "Queue"="Geometry"}
Pass { Name "OUTLINE" Cull Front Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite] CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma shader_feature_local _OUTLINE_ON
#include "UnityCG.cginc"
float _OutlineWidth; fixed4 _OutlineColor, _BaseColor; struct A{float4 vertex:POSITION;float3 normal:NORMAL;}; struct V{float4 pos:SV_POSITION;};
V vert(A i){V o; float3 n=normalize(mul((float3x3)UNITY_MATRIX_IT_MV,i.normal)); o.pos=UnityObjectToClipPos(i.vertex); o.pos.xy+=TransformViewToProjection(n.xy)*_OutlineWidth*o.pos.w; return o;}
fixed4 frag(V i):SV_Target{
#ifndef _OUTLINE_ON
discard;
#endif
return fixed4(_OutlineColor.rgb,_OutlineColor.a*_BaseColor.a);} ENDCG }
Pass { Name "FORWARD" Tags{"LightMode"="ForwardBase"} Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite] CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_fwdbase
#pragma shader_feature_local _SPECULAR_ON
#pragma shader_feature_local _RIM_ON
#include "UnityCG.cginc"
#include "Lighting.cginc"
#include "AutoLight.cginc"
sampler2D _MainTex; float4 _MainTex_ST; fixed4 _BaseColor,_ShadeColor,_SpecularColor,_RimColor,_DotColor; float _ShadeSteps,_ShadeSmoothness,_MinBrightness,_MaxBrightness,_Glossiness,_SpecularSmoothness,_RimAmount,_RimSmoothness,_AmbientStrength,_DitherAmount,_Opacity,_DotScale;
struct A{float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;}; struct V{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 n:TEXCOORD1;float3 wp:TEXCOORD2;SHADOW_COORDS(3)};
V vert(A i){V o;o.pos=UnityObjectToClipPos(i.vertex);o.uv=TRANSFORM_TEX(i.uv,_MainTex);o.n=UnityObjectToWorldNormal(i.normal);o.wp=mul(unity_ObjectToWorld,i.vertex).xyz;TRANSFER_SHADOW(o)return o;}
fixed4 frag(V i):SV_Target{fixed4 a=tex2D(_MainTex,i.uv)*_BaseColor;float3 n=normalize(i.n),l=normalize(UnityWorldSpaceLightDir(i.wp)),v=normalize(UnityWorldSpaceViewDir(i.wp));UNITY_LIGHT_ATTENUATION(att,i,i.wp);float x=saturate(dot(n,l))*att;float steps=max(2,round(_ShadeSteps));float q=floor(x*steps)/max(1,steps-1);q+=smoothstep(.5-_ShadeSmoothness,.5+_ShadeSmoothness,frac(x*steps))/max(1,steps-1);q=saturate(q);float3 c=a.rgb*(lerp(_ShadeColor.rgb,float3(1,1,1),q)*lerp(_MinBrightness,_MaxBrightness,q)*_LightColor0.rgb+ShadeSH9(float4(n,1)).rgb*_AmbientStrength);
#ifdef _SPECULAR_ON
float sp=pow(saturate(dot(n,normalize(l+v))),lerp(8,128,_Glossiness));c+=_SpecularColor.rgb*smoothstep(.5-_SpecularSmoothness,.5+_SpecularSmoothness,sp)*att;
#endif
#ifdef _RIM_ON
float rim=1-saturate(dot(n,v));c+=_RimColor.rgb*smoothstep(_RimAmount-_RimSmoothness,_RimAmount+_RimSmoothness,rim)*q;
#endif
float2 cell=frac(i.pos.xy/max(2.0,_DotScale))-.5;float dots=1-smoothstep(.20,.30,length(cell));c=lerp(c,_DotColor.rgb,dots*saturate(_DitherAmount)*.85);return fixed4(c,a.a*saturate(_Opacity));} ENDCG }
UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
} Fallback "Diffuse" }
