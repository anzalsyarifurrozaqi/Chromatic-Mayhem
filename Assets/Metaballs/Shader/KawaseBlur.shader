Shader "Hidden/KawaseBlur"
{
    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"   
    
    #define E 2.71828f

    CBUFFER_START(UnityPerMaterial)
        TEXTURE2D_X(_MainTex);
	    float4 _BlitTexture_TexelSize;
	    uint _GridSize;
	    float _Spread;
        float _ColorStep;
        float _ColorStep2;
        float _AlphaStep;
        float _AlphaStep2;
        float _Clip;
    CBUFFER_END

    float gaussian(int x)
    {
        float sigmaSqu = _Spread * _Spread;
        return (1 / sqrt(TWO_PI * sigmaSqu)) * pow(E, -(x * x) / (2 * sigmaSqu));
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline"}

        Pass
        {
            Name "Depth"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            struct MeshData
            {
                float4 vertex       : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Interpolars
            {
                float4 clipPos       : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Interpolars Vert (MeshData IN)
            {
                Interpolars OUT = (Interpolars) 0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
        
                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.vertex.xyz);
                OUT.clipPos = vertexInput.positionCS;

                return OUT;
            }

            float Frag (Interpolars IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                return IN.clipPos.z;
            }

            ENDHLSL
        }

        Pass
        {
            Name "Normal"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            struct MeshData
            {
                float4 vertex       : POSITION;
                float3 normal       : NORMAL;
                float4 tangent      : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Interpolars
            {
                float4 clipPos      : SV_POSITION;
                float3 normalWS     : TEXCOORD0;
                float3 viewDirWS    : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Interpolars Vert(MeshData IN)
            {
                Interpolars OUT = (Interpolars) 0;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.vertex.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(IN.normal, IN.tangent);

                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);

                OUT.normalWS = normalInput.normalWS;
                OUT.viewDirWS = viewDirWS;

                OUT.clipPos = vertexInput.positionCS;

                return OUT;
            }

            half4 Frag(Interpolars IN) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float3 normalWS = IN.normalWS;

                return half4(NormalizeNormalPerPixel(normalWS), 0.0);
            }

            ENDHLSL
        }

        Pass
        {
            Name "Filter Horizontal"

            // Cull Off
            // ZTest Always
            // ZWrite Off

            HLSLPROGRAM            

            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"                   
            
            #pragma vertex Vert
            #pragma fragment frag_horizontal

            float4 frag_horizontal(Varyings IN) : SV_TARGET
            {
                float3 col = float3(0.0f, 0.0f, 0.0f);
                float gridSum = 0.0f;

                int upper = ((_GridSize - 1) / 2);
                int lower = -upper;

                for (int x = lower; x <= upper; ++x)
                {
                    float gauss = gaussian(x);
                    gridSum += gauss;
                    float2 uv = IN.texcoord + float2(_BlitTexture_TexelSize.x * x, 0.0f);
                     col += gauss * SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).xyz;
                }

                col /= gridSum;
                return float4(col, 1.0f);
            }

            ENDHLSL
        }

        Pass
        {
            Name "Filter Vertical"

            // Cull Off
            // ZTest Always
            // ZWrite Off

            HLSLPROGRAM            

            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"         
            
            #pragma vertex Vert
            #pragma fragment frag_vertical

            float4 frag_vertical(Varyings IN) : SV_TARGET
            {
                float3 col = float3(0.0f, 0.0f, 0.0f);
                float gridSum = 0.0f;

                int upper = ((_GridSize - 1) / 2);
                int lower = -upper;

                for (int y = lower; y <= upper; ++y)
                {
                    float gauss = gaussian(y);
                    gridSum += gauss;
                    float2 uv = IN.texcoord + float2(0.0f, _BlitTexture_TexelSize.y * y);
                     col += gauss * SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).xyz;
                }

                col /= gridSum;
                return float4(col, 1.0f);
            }

            ENDHLSL
        }

        Pass
        {
            Name "Smoothstep"

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"     

            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings IN) : SV_TARGET
            {
                float4 background = SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, IN.texcoord);                

                float4 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, IN.texcoord);


                float4 color_step = _ColorStep;
                float4 color_step2 = _ColorStep2;

                float4 final_color = smoothstep(color_step, color_step2, color);

                float4 alpha_step = _AlphaStep;
                float4 alpha_step2 = _AlphaStep2;

                float final_alpha = smoothstep(alpha_step, alpha_step2, color.a);

                if (final_color.r > 0.0)
                {
                    return float4(final_color.rrr, final_alpha);
                }

                return background;
            }            

            ENDHLSL
        }
    }
}