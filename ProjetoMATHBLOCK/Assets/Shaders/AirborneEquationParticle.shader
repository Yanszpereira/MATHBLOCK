Shader "MathBlock/Airborne Equation Particle"
{
    Properties
    {
        _MainTex("Equation Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _Brightness("Brightness", Range(.25,2)) = .9
        _AlphaFeather("Edge Feather", Range(.01,.5)) = .14
        _Softness("Geometry Softness", Range(.1,8)) = 2.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off Lighting Off ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_particles
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D_float _CameraDepthTexture;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Brightness, _AlphaFeather, _Softness;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 projectedPosition : TEXCOORD1;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                output.projectedPosition = ComputeScreenPos(output.position);
                // COMPUTE_EYEDEPTH depende do nome de variável `v` em algumas
                // versões do Unity. O cálculo explícito funciona com este appdata.
                output.projectedPosition.z = -UnityObjectToViewPos(input.vertex).z;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 textureColor = tex2D(_MainTex, input.uv);
                float textureAlpha = smoothstep(0.0, max(.01, _AlphaFeather), textureColor.a);
                float sceneDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(
                    _CameraDepthTexture,
                    UNITY_PROJ_COORD(input.projectedPosition)));
                float intersectionFade = saturate((sceneDepth - input.projectedPosition.z) * _Softness);
                float alpha = saturate(textureAlpha * input.color.a * intersectionFade);
                fixed3 color = textureColor.rgb * input.color.rgb * _Brightness;
                // Cor pré-multiplicada evita a franja escura típica de billboards alfa.
                return fixed4(color * alpha, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
