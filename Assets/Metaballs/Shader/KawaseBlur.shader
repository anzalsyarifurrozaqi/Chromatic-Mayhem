Shader "Hidden/KawaseBlur"
{
    Properties
    {
         _MainTex("Base Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }        

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        #pragma multi_compile _SAMPLES_LOW _SAMPLES_MEDIUM _SAMPLES_HIGH

        #pragma exclude_renderers gles gles3 glcore
        #pragma target 4.5

        CBUFFER_START(UnityPerMaterial)
            real4 _MainTex_TexelSize;            
        CBUFFER_END

        ENDHLSL

        Pass
        {
            Name "Mesh_Draw"

            Cull Off
            ZTest LEqual
            ZWrite Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                real4 positionOS : POSITION;
            };

            struct Varyings
            {
                real4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings Out;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                Out.positionCS = positionInputs.positionCS;

                return Out;
            }

            real frag(Varyings IN) : SV_Target
            {
                return 1.0;
            }

            ENDHLSL
        }

        Pass
        {
            Name "Kawase Blur"

            ZTest Always
            ZWrite Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            real4 _MainTex_ST;

            TEXTURE2D(_MetaballDepthRT);
            SAMPLER(sampler_MetaballDepthRT);
            real4 _MetaballDepthRT_ST;

            real _BlurDistance;
            real _Offset;

            real _offset;

            struct Attributes
            {
                real4 positionOS        : POSITION;
                real2 uv                : TEXCOORD0;
            };

            struct Varyings
            {
                real4 positionCS        : SV_POSITION;
                real2 uv                : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings Out;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                Out.positionCS = positionInputs.positionCS;
                Out.uv = TRANSFORM_TEX(IN.uv, _MainTex);

                return Out;
            }

            real4 applyBlur(const real4 color, const real2 uv, const real2 texelResolution, const real offset)
            {
                real4 result = color;
                
                result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + half2( offset,  offset) * texelResolution);
                result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + half2(-offset,  offset) * texelResolution);
                result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + half2(-offset, -offset) * texelResolution);
                result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + half2( offset, -offset) * texelResolution);
                result /= 5.0;

                return result;
            }

            real applyAlphaBlur(const real4 color, const real2 uv, const real2 texelResolution, const real offset)
            {
                 real result = color.a;
                 
                 result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + real2( offset,  offset) * texelResolution).a;
                 result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + real2( offset, -offset) * texelResolution).a;
                 result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + real2(-offset,  offset) * texelResolution).a;
                 result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + real2(-offset, -offset) * texelResolution).a;
                 result /= 5.0;
 
                 return result;               
            }

            real4 frag(Varyings IN) : SV_Target
            {
                real2 res = _MainTex_TexelSize.xy;
                real i = _offset;
    
                real4 col;                
                col.rgb = SAMPLE_TEXTURE2D( _MainTex, sampler_MainTex, IN.uv ).rgb;
                col.rgb += SAMPLE_TEXTURE2D( _MainTex, sampler_MainTex, IN.uv + real2( i, i ) * res ).rgb;
                col.rgb += SAMPLE_TEXTURE2D( _MainTex, sampler_MainTex, IN.uv + real2( i, -i ) * res ).rgb;
                col.rgb += SAMPLE_TEXTURE2D( _MainTex, sampler_MainTex, IN.uv + real2( -i, i ) * res ).rgb;
                col.rgb += SAMPLE_TEXTURE2D( _MainTex, sampler_MainTex, IN.uv + real2( -i, -i ) * res ).rgb;
                col.rgb /= 5.0f;
                
                return col;
            }

            ENDHLSL
        }
    }
}