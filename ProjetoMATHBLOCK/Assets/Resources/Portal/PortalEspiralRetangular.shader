Shader "MathBlock/PortalEspiralRetangular"
{
    SubShader
    {
        Tags { "Queue"="Geometry+10" "RenderType"="Opaque" }
        Cull Off ZWrite On ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct Entrada { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Saida { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            Saida vert(Entrada v)
            {
                Saida o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o;
            }
            fixed4 frag(Saida i) : SV_Target
            {
                float2 ponto = i.uv * 2.0 - 1.0;
                // Apenas uma base discreta e imovel. Todo movimento vem das particulas reais.
                half3 cor = lerp(half3(0.09, 0.08, 0.21), half3(0.12, 0.19, 0.34), i.uv.y);
                cor += half3(0.07, 0.035, 0.10) * saturate(1.0 - dot(ponto, ponto));
                // Alpha sempre 1, inclusive nos cantos: nunca revela o void.
                return fixed4(cor, 1.0);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Color"
}
