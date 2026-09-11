Shader "Hidden/MathBlock/ScreenSpaceOutlineMask"
{
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always ColorMask R
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _CameraDepthNormalsTexture;
            struct A { float4 vertex:POSITION; };
            struct V { float4 pos:SV_POSITION; float4 screen:TEXCOORD0; float eye:TEXCOORD1; };
            V vert(A i) { V o; o.pos=UnityObjectToClipPos(i.vertex); o.screen=ComputeScreenPos(o.pos); o.eye=-UnityObjectToViewPos(i.vertex).z; return o; }
            fixed4 frag(V i):SV_Target
            {
                float depth; float3 normal; float2 uv=i.screen.xy/i.screen.w;
                DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture,uv),depth,normal);
                float scene=depth*_ProjectionParams.z;
                clip(scene+max(0.03,scene*0.002)-i.eye);
                return fixed4(1,0,0,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
