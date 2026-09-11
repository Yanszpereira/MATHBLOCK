Shader "Custom/URPToonShader" {
Properties { _MainTex("Texture",2D)="white"{} _BaseColor("Base Color",Color)=(1,1,1,1) [HideInInspector]_Color("Color",Color)=(1,1,1,1) _EmissionColor("Emission Color",Color)=(0,0,0,1) _EmissionStrength("Emission Strength",Range(0,8))=0 _ShadeColor("Shade Tint",Color)=(.48,.55,.72,1) _ShadeSteps("Shade Steps",Range(2,6))=3 _ShadeSmoothness("Band Softness",Range(.25,2))=1 _MinBrightness("Shadow Brightness",Range(0,1))=.38 _MaxBrightness("Light Brightness",Range(0,2))=1 [Toggle(_SPECULAR_ON)]_EnableSpecular("Specular",Float)=1 _SpecularColor("Specular Color",Color)=(1,.96,.85,1) _Glossiness("Specular Size",Range(.02,1))=.28 _SpecularSmoothness("Specular Softness",Range(.001,.25))=.045 [Toggle(_RIM_ON)]_EnableRim("Rim",Float)=1 _RimColor("Rim Color",Color)=(1,.96,.86,1) _RimAmount("Rim Amount",Range(0,1))=.72 _RimSmoothness("Rim Softness",Range(.001,.5))=.12 _AmbientStrength("Ambient",Range(0,1))=.42 [Toggle(_OUTLINE_ON)]_EnableOutline("Outline",Float)=1 _OutlineColor("Outline Color",Color)=(.035,.04,.055,1) [HideInInspector]_OutlineWidth("Legacy Outline Width",Range(0,.03))=.006 _OutlinePixels("Outline Width (Pixels)",Range(0,8))=2.5 _OutlineMinWidthMul("Far Outline Multiplier",Range(.1,1))=.4 [Toggle(_OUTLINE_GROUND_MASK_ON)]_EnableOutlineGroundMask("Ground Contact Mask",Float)=0 _OutlineDownCutoff("Downward Cutoff",Range(-1,.3))=-.35 _OutlineDownSoft("Downward Softness",Range(.01,1))=.35 [Toggle(_USE_SMOOTHED_NORMALS)]_UseSmoothedNormals("Use Smoothed Normals (UV4)",Float)=0 _DitherAmount("Dither Amount",Range(0,1))=0 _Opacity("Opacity",Range(0,1))=1 _DotScale("Dot Scale",Range(2,40))=7 _DotColor("Dot Color",Color)=(.04,.04,.04,1) _Cutoff("Cutoff",Range(0,1))=.5 [HideInInspector]_Mode("Mode",Float)=0 [HideInInspector]_SrcBlend("Src Blend",Float)=1 [HideInInspector]_DstBlend("Dst Blend",Float)=0 [HideInInspector]_ZWrite("ZWrite",Float)=1 }
SubShader { Tags{"RenderType"="Opaque" "Queue"="Geometry"}
Pass { Name "OUTLINE" Cull Front Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite] CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma shader_feature_local _OUTLINE_ON
#pragma shader_feature_local _USE_SMOOTHED_NORMALS
#pragma shader_feature_local _OUTLINE_GROUND_MASK_ON
#include "UnityCG.cginc"
float _OutlinePixels,_OutlineMinWidthMul,_OutlineDownCutoff,_OutlineDownSoft; fixed4 _OutlineColor; struct A{float4 vertex:POSITION;float3 normal:NORMAL;float3 smoothNormal:TEXCOORD3;}; struct V{float4 pos:SV_POSITION;};
V vert(A i){V o;float3 normalOS=i.normal;
#ifdef _USE_SMOOTHED_NORMALS
float smoothLength=dot(i.smoothNormal,i.smoothNormal);normalOS=smoothLength>1e-6?normalize(i.smoothNormal):normalOS;
#endif
o.pos=UnityObjectToClipPos(i.vertex);float3 normalWS=UnityObjectToWorldNormal(normalOS);float3 nv=normalize(mul((float3x3)UNITY_MATRIX_V,normalWS));float2 d=normalize(TransformViewToProjection(nv.xy)+float2(1e-6,0));float widthFactor=lerp(_OutlineMinWidthMul,1.0,saturate(1.0/max(o.pos.w,0.0001)));
#ifdef _OUTLINE_GROUND_MASK_ON
float downMask=smoothstep(_OutlineDownCutoff,_OutlineDownCutoff+_OutlineDownSoft,normalWS.y);widthFactor*=downMask;
#endif
o.pos.xy+=d*(2.0/_ScreenParams.y)*_OutlinePixels*widthFactor*o.pos.w;return o;}
fixed4 frag(V i):SV_Target{
#ifndef _OUTLINE_ON
discard;
#endif
return _OutlineColor;} ENDCG }
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
sampler2D _MainTex; float4 _MainTex_ST; fixed4 _BaseColor,_ShadeColor,_SpecularColor,_RimColor,_DotColor,_EmissionColor; float _EmissionStrength,_ShadeSteps,_ShadeSmoothness,_MinBrightness,_MaxBrightness,_Glossiness,_SpecularSmoothness,_RimAmount,_RimSmoothness,_AmbientStrength,_DitherAmount,_Opacity,_DotScale;
struct A{float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;}; struct V{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 n:TEXCOORD1;float3 wp:TEXCOORD2;SHADOW_COORDS(3)};
V vert(A v){V o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.n=UnityObjectToWorldNormal(v.normal);o.wp=mul(unity_ObjectToWorld,v.vertex).xyz;TRANSFER_SHADOW(o)return o;}
fixed4 frag(V i):SV_Target{fixed4 a=tex2D(_MainTex,i.uv)*_BaseColor;float3 n=normalize(i.n),l=normalize(UnityWorldSpaceLightDir(i.wp)),viewDir=normalize(UnityWorldSpaceViewDir(i.wp));UNITY_LIGHT_ATTENUATION(att,i,i.wp);float x=saturate(dot(n,l))*att;float s=max(2,round(_ShadeSteps)),z=x*(s-1),aa=max(fwidth(z)*_ShadeSmoothness,.0001);float q=saturate((floor(z)+smoothstep(1-aa,1,frac(z)))/(s-1));float3 toonLight=lerp(_ShadeColor.rgb,float3(1,1,1),q)*lerp(_MinBrightness,_MaxBrightness,q);float3 c=a.rgb*(toonLight*_LightColor0.rgb+ShadeSH9(float4(n,1)).rgb*_AmbientStrength);
#ifdef _SPECULAR_ON
float sp=pow(saturate(dot(n,normalize(l+viewDir))),lerp(8,128,_Glossiness));c+=_SpecularColor.rgb*smoothstep(.5-_SpecularSmoothness,.5+_SpecularSmoothness,sp)*att;
#endif
#ifdef _RIM_ON
float rim=1-saturate(dot(n,viewDir));c+=_RimColor.rgb*smoothstep(_RimAmount-_RimSmoothness,_RimAmount+_RimSmoothness,rim)*q;
#endif
float2 cell=frac(i.pos.xy/max(2.0,_DotScale))-.5;float dots=1-smoothstep(.20,.30,length(cell));c=lerp(c,_DotColor.rgb,dots*saturate(_DitherAmount)*.85);c+=_EmissionColor.rgb*_EmissionStrength;return fixed4(c,a.a*saturate(_Opacity));} ENDCG }
UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
} Fallback "Diffuse" }
