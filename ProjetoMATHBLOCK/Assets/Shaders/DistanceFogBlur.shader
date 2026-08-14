Shader "Hidden/MathBlock/DistanceFogBlur"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            sampler2D _ExclusionMask;
            float4 _MainTex_TexelSize;
            float4 _ExclusionMask_TexelSize; // preencher via material.SetTextureOffset/SetVector no script, ou deixar Unity popular automaticamente se o nome bater com uma textura setada via SetTexture
            float _StartDistance, _FullDistance, _BlurRadius;
            float _FogColorStrength, _DotStrength, _DotScale, _MobileQuality;
            fixed4 _FogColor;

            // 1 / 0.84, pré-calculado para trocar divisão por multiplicação no branch mobile.
            static const half INV_MOBILE_WEIGHT = 1.190476h;

            fixed4 frag(v2f_img input) : SV_Target
            {
                float rawDepth = UNITY_SAMPLE_DEPTH(tex2D(_CameraDepthTexture, input.uv));
                float distanceFromCamera = LinearEyeDepth(rawDepth);
                half fog = smoothstep(_StartDistance, _FullDistance, distanceFromCamera);

                fixed4 center = tex2D(_MainTex, input.uv);

                // Early-out: se não há neblina base neste pixel, a exclusão só pode
                // reduzir ainda mais (nunca aumentar), então o resultado final já é 0.
                // Evita 7 leituras de textura da máscara + todo o blur em pixels próximos.
                if (fog <= 0.001)
                {
                    return center;
                }

                // Expande levemente a máscara para o blur não vazar pelas bordas da nuvem.
                float2 maskPixel = _ExclusionMask_TexelSize.xy * 3.0;
                half exclusion = tex2D(_ExclusionMask, input.uv).r;
                exclusion = max(exclusion, tex2D(_ExclusionMask, input.uv + float2(maskPixel.x, 0)).r);
                exclusion = max(exclusion, tex2D(_ExclusionMask, input.uv - float2(maskPixel.x, 0)).r);
                exclusion = max(exclusion, tex2D(_ExclusionMask, input.uv + float2(0, maskPixel.y)).r);
                exclusion = max(exclusion, tex2D(_ExclusionMask, input.uv - float2(0, maskPixel.y)).r);
                exclusion = max(exclusion, tex2D(_ExclusionMask, input.uv + maskPixel).r);
                exclusion = max(exclusion, tex2D(_ExclusionMask, input.uv - maskPixel).r);
                fog *= 1.0 - saturate(exclusion);

                float2 offset = _MainTex_TexelSize.xy * _BlurRadius * fog;

                half4 blurred = center * 0.32h;
                blurred += tex2D(_MainTex, input.uv + float2(offset.x, 0)) * 0.13h;
                blurred += tex2D(_MainTex, input.uv - float2(offset.x, 0)) * 0.13h;
                blurred += tex2D(_MainTex, input.uv + float2(0, offset.y)) * 0.13h;
                blurred += tex2D(_MainTex, input.uv - float2(0, offset.y)) * 0.13h;

                // Diagonais são removidas no Android (_MobileQuality alto) para reduzir leituras de textura.
                if (_MobileQuality < 0.5)
                {
                    blurred += tex2D(_MainTex, input.uv + offset) * 0.04h;
                    blurred += tex2D(_MainTex, input.uv - offset) * 0.04h;
                    blurred += tex2D(_MainTex, input.uv + float2(offset.x, -offset.y)) * 0.04h;
                    blurred += tex2D(_MainTex, input.uv + float2(-offset.x, offset.y)) * 0.04h;
                }
                else
                {
                    blurred *= INV_MOBILE_WEIGHT;
                }

                half3 color = lerp(center.rgb, blurred.rgb, fog);
                color = lerp(color, _FogColor.rgb, fog * _FogColorStrength);

                // Pontos discretos: modulam levemente a neblina, sem virar ruído forte.
                float2 cell = frac(input.pos.xy / max(2.0, _DotScale)) - 0.5;
                half dot = 1.0 - smoothstep(0.18, 0.29, length(cell));
                color *= 1.0 - dot * fog * _DotStrength;

                return fixed4(color, center.a);
            }
            ENDCG
        }
    }
    Fallback Off
}