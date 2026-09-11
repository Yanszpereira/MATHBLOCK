Shader "MathBlock/MatteDottedCube"
{
    Properties
    {
        _Color ("Color", Color) = (0.72,0.74,0.77,1)
        _DotColor ("Dot Color", Color) = (0.16,0.10,0.20,1)
        _DotStrength ("Dot Opacity", Range(0,0.2)) = 0.065
        _DotScale ("Dot Spacing", Range(2,16)) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back ZWrite On ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _DotColor;
            float _DotStrength;
            float _DotScale;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 position : SV_POSITION; };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 cell = frac(input.position.xy / max(2.0, _DotScale)) - 0.5;
                float dotMask = 1.0 - smoothstep(0.18, 0.29, length(cell));
                fixed3 color = lerp(_Color.rgb, _DotColor.rgb, dotMask * saturate(_DotStrength));
                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
