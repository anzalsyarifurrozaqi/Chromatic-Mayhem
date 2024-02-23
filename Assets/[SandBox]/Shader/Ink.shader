Shader "TNTC/TexturePainter"{   

    Properties
    {
        _PainterColor ("Painter Color", Color) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline" }

        Cull Off ZWrite Off ZTest Off

        Pass{
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _RGBANoise;
            float4 _RGBANoise_ST;
            
            float3 _PainterPosition;
            float _Radius;
            float _Hardness;
            float _Strength;
            float4 _PainterColor;
            float _PrepareUV;

            struct Attribute
            {
                float4 vertex   : POSITION;
				float2 uv       : TEXCOORD0;
            };

            struct Varrying
            {
                float4 vertex   : SV_POSITION;
                float2 uv       : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
            };

            float3 fbm(float3 pos)
            {
                float3 result = float3(0, 0, 0);
                float amplitude = 0.5;
                for (float index = 0; index < 3; ++index)
                {
                    result += tex2D(_RGBANoise, pos/amplitude) * amplitude;
                    amplitude /= 2;
                }
                return result;
            }

            Varrying vert (Attribute v)
            {
                Varrying o;
				o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.uv = v.uv;
				float4 uv = float4(0, 0, 0, 1);
                uv.xy = float2(1, _ProjectionParams.x) * (v.uv.xy * float2( 2, 2) - float2(1, 1));
				o.vertex = uv; 
                return o;
            }

            half4 frag (Varrying i) : SV_Target
            {
                float3 pos = float3(i.uv, 0) - _PainterPosition * 1.6;
                float paint = fbm(pos * 0.8).x;

                float m = distance(_PainterPosition, i.worldPos);

                float brush = smoothstep(_Radius, 0.0, m);
                paint *= brush;

                paint += smoothstep(0.25, 0.05, m);

                float4 data = tex2D(_MainTex, float2(0,0));
                float2 mousePrevious = data.xy;

                float offset = 0;

                if (distance(i.worldPos, _PainterColor) < 1)
                {
                    float mask = fbm(pos * 0.8 * 0.5).x;
                    float push = smoothstep(0.3, 0.6, mask);
                    push *= mask;
                    float2 dir = normalize(mousePrevious -_PainterPosition + 0.001);
                    float fadeIn = smoothstep(0, 0.5, data.z);
                    float fadeInAndOut = sin(fadeIn*3.1415);
                    offset = 10.0 * push * normalize(_PainterPosition - i.uv) / float2(1024, 1024) * fadeInAndOut;
                    push *= 500 * distance(mousePrevious, _PainterPosition) * fadeIn;
                    offset += push * dir / float2(1024, 1024);
                }

                float4 frame = tex2D(_MainTex, i.uv + offset);
                paint = max(paint, frame.x);

                return clamp(paint, 0.0, 1.0);
            }
            ENDHLSL
        }
    }
}