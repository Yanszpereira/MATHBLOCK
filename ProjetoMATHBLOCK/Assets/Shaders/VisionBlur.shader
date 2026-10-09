Shader "Hidden/MathBlock/VisionBlur"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }
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

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _BlurRadius;

            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 offset = _MainTex_TexelSize.xy * _BlurRadius;
                fixed4 color = tex2D(_MainTex, input.uv) * 0.25;
                color += tex2D(_MainTex, input.uv + float2(offset.x, 0)) * 0.125;
                color += tex2D(_MainTex, input.uv - float2(offset.x, 0)) * 0.125;
                color += tex2D(_MainTex, input.uv + float2(0, offset.y)) * 0.125;
                color += tex2D(_MainTex, input.uv - float2(0, offset.y)) * 0.125;
                color += tex2D(_MainTex, input.uv + offset) * 0.0625;
                color += tex2D(_MainTex, input.uv - offset) * 0.0625;
                color += tex2D(_MainTex, input.uv + float2(offset.x, -offset.y)) * 0.0625;
                color += tex2D(_MainTex, input.uv + float2(-offset.x, offset.y)) * 0.0625;
                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
