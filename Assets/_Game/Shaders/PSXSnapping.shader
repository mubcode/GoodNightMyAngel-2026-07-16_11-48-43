// =============================================================================
// PSXSnapping.shader (URP)
// -----------------------------------------------------------------------------
// PSX tarzı render: düşük çözünürlüklü RT'yi al, vertex snap (UV quantization),
// renk derinliği azalt ve ordered dithering uygula. Sonra ekrana bas.
//
// Bu shader Blitter.BlitCameraTexture ile çağrılır:
//   - Source: low-res RT
//   - Destination: ekran
// =============================================================================

Shader "GoodNight/PSXSnapping"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _ColorBits ("Color Bits Per Channel", Float) = 5
        _Dither ("Dither Amount", Range(0,0.5)) = 0.06
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_MainTex);

            float _ColorBits;
            float _Dither;
            float2 _LowRes;

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

            // Blitter tarafından sağlanan tam ekran quad köşeleri
            static const float2 kVertices[4] =
            {
                float2(-1, -1),
                float2( 1, -1),
                float2(-1,  1),
                float2( 1,  1)
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Vertex snap: UV'yi low-res'e snap et (her köşe bir piksele oturur)
                float2 pos = kVertices[input.vertexID];
                float2 uv = (pos + 1.0) * 0.5;
                uv = floor(uv * _LowRes) / _LowRes;
                pos = uv * 2.0 - 1.0;

                o.positionCS = float4(pos, 0, 1);
                o.uv = uv;
                return o;
            }

            // 4x4 ordered dithering matrisini (Bayer) oluştur
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

            float4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                // Piksel koordinatı (dithering için)
                int2 px = int2(uv * _LowRes);

                // Düşük çözünürlükten sample (point filtering)
                float4 col = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, uv);

                // 1) Renk derinliği azalt (posterize)
                float levels = pow(2.0, _ColorBits);
                // Dithering ekle (ordered Bayer)
                float d = bayer4x4(px) - 0.5;
                col.rgb = floor(col.rgb * levels + d * _Dither * levels) / levels;
                col.rgb = saturate(col.rgb);

                return col;
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
