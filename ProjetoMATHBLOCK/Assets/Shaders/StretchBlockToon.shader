Shader "MathBlock/Stretch Block Toon"
{
Properties { _MainTex("Texture",2D)="white"{} _BaseColor("Base Color",Color)=(1,1,1,1) [HideInInspector]_Color("Color",Color)=(1,1,1,1) _ShadeColor("Shade Tint",Color)=(.48,.55,.72,1) _ShadeSteps("Shade Steps",Range(2,6))=3 _ShadeSmoothness("Band Antialiasing",Range(.25,2))=1 _MinBrightness("Shadow Brightness",Range(0,1))=.4 _MaxBrightness("Light Brightness",Range(0,2))=1 _AmbientStrength("Ambient",Range(0,1))=.42 [Toggle(_SPECULAR_ON)]_EnableSpecular("Specular",Float)=1 _SpecularColor("Specular Color",Color)=(1,.96,.85,1) _Glossiness("Specular Size",Range(.02,1))=.24 [Toggle(_RIM_ON)]_EnableRim("Rim",Float)=1 _RimColor("Rim Color",Color)=(1,.96,.86,1) _RimAmount("Rim Amount",Range(0,1))=.75 [Toggle(_OUTLINE_ON)]_EnableOutline("Outline",Float)=1 _OutlineColor("Outline Color",Color)=(.035,.04,.055,1) _OutlinePixels("Outline Width (Pixels)",Range(0,8))=1.75 _DitherAmount("Dither Amount",Range(0,1))=0 _Opacity("Opacity",Range(0,1))=1 _DotScale("Dot Scale",Range(2,40))=7 _DotColor("Dot Color",Color)=(.04,.04,.04,1) [HideInInspector]_SrcBlend("Src Blend",Float)=1 [HideInInspector]_DstBlend("Dst Blend",Float)=0 [HideInInspector]_ZWrite("ZWrite",Float)=1 }
SubShader { Tags{"RenderType"="Opaque" "Queue"="Geometry"}
Pass { Name "OUTLINE" Cull Front Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite] CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#pragma shader_feature_local _OUTLINE_ON
#include "UnityCG.cginc"
float _OutlinePixels;fixed4 _OutlineColor;struct A{float4 vertex:POSITION;float3 normal:NORMAL;};struct V{float4 pos:SV_POSITION;};
V vert(A i){V o;o.pos=UnityObjectToClipPos(i.vertex);float3 nv=normalize(mul((float3x3)UNITY_MATRIX_IT_MV,i.normal));float2 d=normalize(nv.xy+float2(1e-6,0));o.pos.xy+=d*(2.0/_ScreenParams.xy)*_OutlinePixels*o.pos.w;return o;}
fixed4 frag(V i):SV_Target{
#ifndef _OUTLINE_ON
discard;
#endif
// A silhueta permanece sólida para o bloco continuar legível durante a sobreposição.
return _OutlineColor;} ENDCG }
Pass { Name "FORWARD" Tags{"LightMode"="ForwardBase"} Cull Back Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite] CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_fwdbase
#pragma shader_feature_local _SPECULAR_ON
#pragma shader_feature_local _RIM_ON
#include "UnityCG.cginc"
#include "Lighting.cginc"
#include "AutoLight.cginc"
sampler2D _MainTex;float4 _MainTex_ST;fixed4 _BaseColor,_ShadeColor,_SpecularColor,_RimColor,_DotColor;float _ShadeSteps,_ShadeSmoothness,_MinBrightness,_MaxBrightness,_AmbientStrength,_Glossiness,_RimAmount,_DitherAmount,_Opacity,_DotScale;
struct A{float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};struct V{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 n:TEXCOORD1;float3 wp:TEXCOORD2;SHADOW_COORDS(3)};
V vert(A i){V o;o.pos=UnityObjectToClipPos(i.vertex);o.uv=TRANSFORM_TEX(i.uv,_MainTex);o.n=UnityObjectToWorldNormal(i.normal);o.wp=mul(unity_ObjectToWorld,i.vertex).xyz;TRANSFER_SHADOW(o)return o;}
fixed4 frag(V i):SV_Target{fixed4 a=tex2D(_MainTex,i.uv)*_BaseColor;float3 n=normalize(i.n),l=normalize(UnityWorldSpaceLightDir(i.wp)),v=normalize(UnityWorldSpaceViewDir(i.wp));UNITY_LIGHT_ATTENUATION(att,i,i.wp);float x=saturate(dot(n,l))*att;float s=max(2,round(_ShadeSteps)),z=x*(s-1),aa=max(fwidth(z)*_ShadeSmoothness,.0001);float q=saturate((floor(z)+smoothstep(1-aa,1,frac(z)))/(s-1));float3 c=a.rgb*(lerp(_ShadeColor.rgb*_MinBrightness,_MaxBrightness.xxx,q)*_LightColor0.rgb+ShadeSH9(float4(n,1)).rgb*_AmbientStrength);
#ifdef _SPECULAR_ON
float sp=pow(saturate(dot(n,normalize(l+v))),lerp(8,128,_Glossiness)),wa=max(fwidth(sp),.0001);c+=_SpecularColor.rgb*smoothstep(.5-wa,.5+wa,sp)*att;
#endif
#ifdef _RIM_ON
float r=1-saturate(dot(n,v)),wr=max(fwidth(r),.0001);c+=_RimColor.rgb*smoothstep(_RimAmount-wr,_RimAmount+wr,r)*q;
#endif
float2 cell=frac(i.pos.xy/max(2.0,_DotScale))-.5;float dots=1-smoothstep(.20,.30,length(cell));c=lerp(c,_DotColor.rgb,dots*saturate(_DitherAmount)*.85);return fixed4(c,a.a*saturate(_Opacity));} ENDCG }
UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
} Fallback "Diffuse" }
