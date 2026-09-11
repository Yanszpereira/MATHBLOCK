Shader "Hidden/MathBlock/ScreenSpaceOutline"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex, _CameraDepthNormalsTexture, _OutlineObjectMask;
            float4 _MainTex_TexelSize;
            fixed4 _OutlineColor;
            float _OutlineThickness, _DepthSensitivity;
            float EyeDepth(float2 uv) { float d; float3 n; DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, uv), d, n); return d * _ProjectionParams.z; }
            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                float2 o = _MainTex_TexelSize.xy * max(_OutlineThickness, 0.5);
                float c = EyeDepth(uv), l = EyeDepth(uv-float2(o.x,0)), r = EyeDepth(uv+float2(o.x,0));
                float u = EyeDepth(uv+float2(0,o.y)), d = EyeDepth(uv-float2(0,o.y));
                float edge = smoothstep(_DepthSensitivity, _DepthSensitivity*1.35,
                    (abs(l+r-2*c)+abs(u+d-2*c))/max(c,5.0));
                float selected = max(tex2D(_OutlineObjectMask,uv).r,
                    max(max(tex2D(_OutlineObjectMask,uv-float2(o.x,0)).r,tex2D(_OutlineObjectMask,uv+float2(o.x,0)).r),
                        max(tex2D(_OutlineObjectMask,uv-float2(0,o.y)).r,tex2D(_OutlineObjectMask,uv+float2(0,o.y)).r)));
                fixed4 scene = tex2D(_MainTex,uv);
                return lerp(scene,fixed4(_OutlineColor.rgb,scene.a),edge*saturate(selected*2)*_OutlineColor.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
