Shader "MathBlock/World Text Depth"
{
    Properties
    {
        _MainTex ("Font Atlas", 2D) = "white" {}
        _Color ("Text Color", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            fixed SampleFontAlpha(float2 uv)
            {
                return tex2D(_MainTex, uv).a;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed fillAlpha = SampleFontAlpha(input.uv);
                float2 offset = _MainTex_TexelSize.xy * _OutlineWidth;

                fixed outlineAlpha = 0;
                outlineAlpha = max(outlineAlpha, SampleFontAlpha(input.uv + float2( offset.x, 0)));
                outlineAlpha = max(outlineAlpha, SampleFontAlpha(input.uv + float2(-offset.x, 0)));
                outlineAlpha = max(outlineAlpha, SampleFontAlpha(input.uv + float2(0,  offset.y)));
                outlineAlpha = max(outlineAlpha, SampleFontAlpha(input.uv + float2(0, -offset.y)));
                outlineAlpha = max(outlineAlpha, SampleFontAlpha(input.uv + offset));
                outlineAlpha = max(outlineAlpha, SampleFontAlpha(input.uv - offset));
                outlineAlpha = max(outlineAlpha, SampleFontAlpha(input.uv + float2( offset.x, -offset.y)));
                outlineAlpha = max(outlineAlpha, SampleFontAlpha(input.uv + float2(-offset.x,  offset.y)));

                outlineAlpha *= (1 - fillAlpha);
                fixed combinedAlpha = max(fillAlpha * _Color.a, outlineAlpha * _OutlineColor.a);
                fixed3 combinedColor = fillAlpha > 0 ? _Color.rgb : _OutlineColor.rgb;
                return fixed4(combinedColor, combinedAlpha);
            }
            ENDCG
        }
    }
}