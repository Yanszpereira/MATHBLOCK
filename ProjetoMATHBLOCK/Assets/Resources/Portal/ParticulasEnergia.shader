Shader "MathBlock/ParticulasEnergiaPortal"
{
    Properties { _MainTex ("Ponto suave", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        Cull Off ZWrite Off ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct Entrada { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Saida { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            Saida vert(Entrada v)
            {
                Saida o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color; o.uv = v.uv; return o;
            }
            fixed4 frag(Saida i) : SV_Target { return tex2D(_MainTex, i.uv) * i.color; }
            ENDCG
        }
    }
}
