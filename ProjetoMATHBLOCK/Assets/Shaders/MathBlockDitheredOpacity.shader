Shader "MathBlock/DitheredOpacity"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BaseMap ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _BaseColor ("Color", Color) = (1,1,1,1)
        _DitherAmount ("Effect Amount", Range(0,1)) = 0
        _Opacity ("Opacity", Range(0,1)) = 1
        _DotScale ("Dot Scale", Range(2,40)) = 7
        _DotColor ("Dot Color", Color) = (0.05,0.05,0.05,1)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        CGPROGRAM
        #pragma surface surf Standard alpha:fade fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _DotColor;
        half _DitherAmount;
        half _Opacity;
        half _DotScale;

        struct Input { float2 uv_MainTex; float4 screenPos; };

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            fixed4 source = tex2D(_MainTex, input.uv_MainTex) * _Color;
            float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 0.0001);
            float2 pixels = screenUV * _ScreenParams.xy;
            float2 cell = frac(pixels / max(2.0, _DotScale)) - 0.5;
            float dots = 1.0 - smoothstep(0.20, 0.30, length(cell));
            float strength = dots * saturate(_DitherAmount);

            output.Albedo = lerp(source.rgb, _DotColor.rgb, strength * 0.85);
            output.Metallic = 0;
            output.Smoothness = 0.15;
            output.Alpha = saturate(_Opacity);
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
