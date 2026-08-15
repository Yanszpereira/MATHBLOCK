Shader "MathBlock/Animated Sky Cylinder"
{
    Properties
    {
        _BottomColor("Horizon Color", Color) = (.78,.91,1,1)
        _MiddleColor("Middle Color", Color) = (.49,.71,.90,1)
        _TopColor("Zenith Color", Color) = (.14,.36,.60,1)
        _Height("Sky Height", Float) = 500
        _GradientAmplitude("Vertical Drift", Range(0,100)) = 38
        _GradientSpeed("Drift Speed", Range(0,.2)) = .035
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Cull Front ZWrite On ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _BottomColor, _MiddleColor, _TopColor;
            float _Height, _GradientAmplitude, _GradientSpeed;
            struct A { float4 vertex : POSITION; };
            struct V { float4 position : SV_POSITION; float worldY : TEXCOORD0; };
            V vert(A input)
            {
                V output;
                float4 worldPosition = mul(unity_ObjectToWorld, input.vertex);
                output.worldY = worldPosition.y;
                output.position = UnityObjectToClipPos(input.vertex);
                return output;
            }
            fixed4 frag(V input) : SV_Target
            {
                // Aproximadamente tres minutos por ciclo completo.
                float drift = sin(_Time.y * _GradientSpeed) * _GradientAmplitude;
                float t = saturate((input.worldY + drift) / max(1.0, _Height));
                t = pow(t, 3.0);
                if (t < .60)
                    return lerp(_BottomColor, _MiddleColor, smoothstep(0, 1, t / .60));
                return lerp(_MiddleColor, _TopColor, smoothstep(0, 1, (t - .60) / .40));
            }
            ENDCG
        }
    }
    Fallback Off
}
