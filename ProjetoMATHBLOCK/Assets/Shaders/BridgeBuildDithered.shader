Shader "MathBlock/BridgeBuildDithered"
{
    Properties
    {
        _Color ("Deep Sea Blue", Color) = (0.043, 0.286, 0.408, 1)
        _Fade ("Build Fade", Range(0, 1)) = 1
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Glossiness ("Smoothness", Range(0, 1)) = 0.18
        _DitherCellSize ("Dither Cell Size", Range(0.01, 1)) = 0.075
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        fixed4 _Color;
        half _Fade;
        half _Metallic;
        half _Glossiness;
        float _DitherCellSize;

        struct Input
        {
            float3 worldPos;
        };

        float StableDitherNoise(float3 worldPosition)
        {
            float cellSize = max(0.01, _DitherCellSize);
            float3 cell = floor(worldPosition / cellSize);
            return frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
        }

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            clip(_Fade - StableDitherNoise(input.worldPos));

            output.Albedo = _Color.rgb;
            output.Metallic = _Metallic;
            output.Smoothness = _Glossiness;
            output.Alpha = 1;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
