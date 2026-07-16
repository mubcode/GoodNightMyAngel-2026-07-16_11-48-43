// =============================================================================
// PSXSnapping.shader (URP, Render Graph uyumlu)
// -----------------------------------------------------------------------------
// PSX tarzı fragment shader: vertex snap (UV quantization) + renk derinliği
// azaltma (posterize) + ordered dithering.
//
// Blitter.BlitTexture ile çağrılır:
//   - _MainTex (veya _BlitTexture): camera color
//   - Destination: low-res RT veya swap texture
// =============================================================================

Shader "GoodNight/PSXSnapping"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _ColorBits ("Color Bits Per Channel", Float) = 5
        _Dither ("Dither Amount", Range(0,0.2)) = 0.06
        _LowRes ("Low Res (w,h)", Vector) = (480, 270, 0, 0)
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

            // Blitter global texture ve sampler
            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_PointClamp);

            float _ColorBits;
            float _Dither;
            float4 _LowRes;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Fullscreen quad için köşe pozisyonları (Blitter uyumlu)
            static const float2 kVertices[4] = {
                float2(-1, -1),
                float2( 1, -1),
                float2(-1,  1),
                float2( 1,  1)
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Fullscreen quad: 4 köşe
                float2 pos = kVertices[input.vertexID % 4];
                float2 uv = (pos + 1.0) * 0.5;
                #if UNITY_UV_STARTS_AT_TOP
                uv.y = 1.0 - uv.y;
                #endif

                // Vertex snap: UV'yi low-res piksel grid'ine snap et
                uv = floor(uv * _LowRes.xy) / _LowRes.xy;
                // Snap sonrası pozisyonu da güncelle (vertex snap)
                pos = uv * 2.0 - 1.0;
                #if UNITY_UV_STARTS_AT_TOP
                pos.y = -pos.y;
                #endif

                o.positionCS = float4(pos, 0, 1);
                o.uv = uv;
                return o;
            }

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

                float2 uv = i.uv;
                int2 px = int2(uv * _LowRes.xy);

                // Snap edilmiş UV'den sample (point filtering)
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);

                // 1) Renk derinliği azalt (posterize)
                float levels = pow(2.0, _ColorBits);
                // 2) Bayer dithering
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
