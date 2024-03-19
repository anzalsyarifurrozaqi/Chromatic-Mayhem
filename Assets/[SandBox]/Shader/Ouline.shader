Shader "Hidden/Outline"
{

    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}        
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline" }

        Pass{
            
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl" 

            TEXTURE2D(_CameraColorTexture);
            SAMPLER(sampler_CameraColorTexture);
            float4 _CameraColorTexture_TexelSize;

            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            TEXTURE2D(_CameraDepthNormalsTexture);
            SAMPLER(sampler_CameraDepthNormalsTexture);

            float _OutlineThickness;
            float _DepthSensitivity;
            float _NormalsSensitivity;
            float _ColorSensitivity;

            float3 DecodeNormal(float4 enc)
            {
                float kSclae = 1.777;
                float3 nn = enc.xyz * float3(2 * kSclae, 2 * kSclae, 0) + float3(-kSclae, -kSclae, 1);
                float g = 2.0 / dot(nn.xyz, nn.xyz);
                float3 n;
                n.xy = g * nn.xy;
                n.z = g-1;
                return n;
            }

            struct Attribute
            {
                float4 vertex   : POSITION;                
				float2 uv       : TEXCOORD0;
            };

            struct Varrying
            {
                float4 vertex           : SV_POSITION;
                float2 uv               : TEXCOORD0;                
            };

            Varrying vert (Attribute IN)
            {                
                Varrying OUT;
                
                OUT.vertex = TransformObjectToHClip(IN.vertex.xyz);
                OUT.uv = IN.uv;

                return OUT;
            }

            float4 frag (Varrying IN) : SV_Target
            {
                float2 UV = IN.uv;

                float halfScaleFloor = floor(_OutlineThickness * 0.5);
                float halfScaleCeil = ceil(_OutlineThickness * 0.5);
                float2 Texel = (1.0) / float2(_CameraColorTexture_TexelSize.z, _CameraColorTexture_TexelSize.w);

                float2 uvSamples[4];
                float depthSamples[4];
                float3 normalSamples[4], colorSamples[4];

                uvSamples[0] = UV - float2(Texel.x, Texel.y) * halfScaleFloor;
                uvSamples[1] = UV + float2(Texel.x, Texel.y) * halfScaleCeil;
                uvSamples[2] = UV + float2(Texel.x * halfScaleCeil, -Texel.y * halfScaleFloor);
                uvSamples[3] = UV + float2(-Texel.x * halfScaleFloor, Texel.y * halfScaleCeil);

                for (int i = 0; i < 4; ++i)
                {
                    depthSamples[i] = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, uvSamples[i]).r;
                    normalSamples[i] = DecodeNormal(SAMPLE_TEXTURE2D(_CameraDepthNormalsTexture, sampler_CameraDepthNormalsTexture, uvSamples[i]));
                    colorSamples[i] = SAMPLE_TEXTURE2D(_CameraColorTexture, sampler_CameraColorTexture, uvSamples[i]);
                }

                // Depth
                float depthFiniteDifferent0 = depthSamples[1] - depthSamples[0];
                float depthFiniteDifferent1 = depthSamples[3] - depthSamples[2];
                float edgeDepth = sqrt(pow(depthFiniteDifferent0, 2) + pow(depthFiniteDifferent1, 2)) * 100;
                float depthThreshold = (1/_DepthSensitivity) * depthSamples[0];
                edgeDepth = edgeDepth > depthThreshold ? 1 : 0;

                // Normals
                float3 normalFiniteDifference0 = normalSamples[1] - normalSamples[0];
                float3 normalFiniteDifference1 = normalSamples[3] - normalSamples[2];
                float edgeNormal = sqrt(dot(normalFiniteDifference0, normalFiniteDifference0) + dot(normalFiniteDifference1, normalFiniteDifference1));
                edgeNormal = edgeNormal > (1/_NormalsSensitivity) ? 1 : 0;

                // Color
                float3 colorFiniteDifference0 = colorSamples[1] - colorSamples[0];
                float3 colorFiniteDifference1 = colorSamples[3] - colorSamples[2];
                float edgeColor = sqrt(dot(colorFiniteDifference0, colorFiniteDifference0) + dot(colorFiniteDifference1, colorFiniteDifference1));
                edgeColor = edgeColor > (1/_ColorSensitivity) ? 1 : 0;

                float edge = max(edgeDepth, max(edgeNormal, edgeColor));

                float4 original = SAMPLE_TEXTURE2D(_CameraColorTexture, sampler_CameraColorTexture, uvSamples[0]);
                float4 oulineColor = float4(0,0,0,1);

                return ((1 - edge) * original) + (edge * lerp(original, oulineColor, oulineColor.a));
            }
            ENDHLSL
        }
    }
}
