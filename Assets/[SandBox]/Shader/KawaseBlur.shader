Shader "Custom/RenderFeature/KawaseBlur"
{
    Properties
    {
        _MainTex("Base Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
                
        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                real4 _MainTex_TexelSize;            
            CBUFFER_END

            struct appdata
            {
                real4 vertex       : POSITION;
                real2 uv           : TEXCOORD0;
            };

            struct v2f
            {
                real2 uv           : TEXCOORD0;
                real4 vertex       : SV_POSITION;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            real4 _MainTex_ST;
            
            real _offset;

            v2f vert (appdata IN)
            {
                v2f Out;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.vertex.xyz);
                Out.vertex = positionInputs.positionCS;
                Out.uv = TRANSFORM_TEX(IN.uv, _MainTex);

                return Out;
            }

            real4 frag (v2f input) : SV_Target
            {
                real2 res = _MainTex_TexelSize.xy;
                real i = _offset;
    
                real4 col;                
                col.rgb = SAMPLE_TEXTURE2D( _MainTex, sampler_MainTex, input.uv ).rgb;
                col.rgb += SAMPLE_TEXTURE2D( _MainTex, sampler_MainTex, input.uv + real2( i, i ) * res ).rgb;
                col.rgb += SAMPLE_TEXTURE2D( _MainTex, sampler_MainTex, input.uv + real2( i, -i ) * res ).rgb;
                col.rgb += SAMPLE_TEXTURE2D( _MainTex, sampler_MainTex, input.uv + real2( -i, i ) * res ).rgb;
                col.rgb += SAMPLE_TEXTURE2D( _MainTex, sampler_MainTex, input.uv + real2( -i, -i ) * res ).rgb;
                col.rgb /= 5.0f;
                
                return col;
            }
            ENDHLSL
        }
    }
}