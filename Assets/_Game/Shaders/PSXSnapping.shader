// =============================================================================
// PSXSnapping.shader (URP, Unity 6 / URP 17)
// -----------------------------------------------------------------------------
// PSX tarzı fragment shader: posterize (renk derinliği azaltma) +
// 4x4 Bayer ordered dithering.
//
// Blitter.BlitTexture ile çağrılır; Blitter kendi vertex'lerini sağlar
// (tüm ekranı kaplayan quad). Bu shader sadece fragment tarafında
// renkleri dönüştürür; vertex snap BURADA YAPILMAZ (Blitter zaten
// düzgün vertex sağlıyor ve vertex snap eklenince sahne bozuluyor).
//
// Düşük çözünürlük hissi istenirse ColorBits'i düşürün (4-5 PSX, 6-7 retro).
// =============================================================================

Shader "GoodNight/PSXSnapping"
{
    Properties
    {
        _ColorBits ("Color Bits Per Channel", Range(2, 8)) = 5
        _Dither ("Dither Amount", Range(0, 0.2)) = 0.06
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "PSXSnapping"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _ColorBits;
            float _Dither;

            // Blit.hlsl zaten Vert, Varyings ve GetFullScreenTriangle* sağlıyor

            // 4x4 Bayer dithering matrisi
            static const float ditherTable[16] = {
                 0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                 3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
            };

            float bayer4x4(int2 p)
            {
                int x = p.x & 3;
                int y = p.y & 3;
                return ditherTable[y * 4 + x];
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 uv = i.texcoord;
                // Screen pixel pos for dither
                int2 px = int2(uv * _ScreenParams.xy);

                // Sample (linear) — Blitter _BlitTexture'i linear sample eder
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // Posterize + Bayer dithering
                float levels = pow(2.0, _ColorBits);
                float d = bayer4x4(px) - 0.5;
                col.rgb = floor(col.rgb * levels + d * _Dither * levels) / levels;
                col.rgb = saturate(col.rgb);

                return col;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
