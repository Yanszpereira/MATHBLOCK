Shader "Custom/AbyssCylinder3Colors"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.64,0.82,0.97,1)
        _MiddleColor ("Middle Color", Color) = (0.43,0.66,0.88,1)
        _BottomColor ("Bottom Color", Color) = (0.03,0.07,0.13,1)

        _Height ("Gradient Height", Float) = 500
    }

    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }

        Cull Front
        ZWrite On
        ZTest LEqual

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float worldY : TEXCOORD0;
            };

            fixed4 _TopColor;
            fixed4 _MiddleColor;
            fixed4 _BottomColor;
            float _Height;

            v2f vert(appdata v)
            {
                v2f o;

                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);

                o.worldY = abs(worldPos.y);
                o.pos = UnityObjectToClipPos(v.vertex);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = saturate(i.worldY / _Height);

                // deixa a transição mais suave
                t = smoothstep(0.0, 1.0, t);

                // 90% azul, 10% azul quase preto
                if (t < 0.90)
                {
                    return lerp(
                        _TopColor,
                        _MiddleColor,
                        smoothstep(0.0, 1.0, t / 0.90)
                    );
                }
                else
                {
                    return lerp(
                        _MiddleColor,
                        _BottomColor,
                        smoothstep(0.0, 1.0, (t - 0.90) / 0.10)
                    );
                }
            }

            ENDCG
        }
    }
}