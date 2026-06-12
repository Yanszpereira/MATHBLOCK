Shader "Custom/SkyCylinderHighAltitude"
{
    Properties
    {
        _BottomColor ("Horizon Color", Color) = (0.78,0.91,1,1)
        _MiddleColor ("Middle Color", Color) = (0.49,0.71,0.90,1)
        _TopColor ("Zenith Color", Color) = (0.14,0.36,0.60,1)

        _Height ("Sky Height", Float) = 1000
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

            fixed4 _BottomColor;
            fixed4 _MiddleColor;
            fixed4 _TopColor;

            float _Height;

            v2f vert(appdata v)
            {
                v2f o;

                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);

                o.worldY = worldPos.y;
                o.pos = UnityObjectToClipPos(v.vertex);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = saturate(i.worldY / _Height);

                // Mantém o azul claro por bastante tempo
                t = pow(t, 3);

                if (t < 0.60)
                {
                    return lerp(
                        _BottomColor,
                        _MiddleColor,
                        smoothstep(0.0, 1.0, t / 0.60)
                    );
                }
                else
                {
                    return lerp(
                        _MiddleColor,
                        _TopColor,
                        smoothstep(0.0, 1.0, (t - 0.60) / 0.40)
                    );
                }
            }

            ENDCG
        }
    }
}