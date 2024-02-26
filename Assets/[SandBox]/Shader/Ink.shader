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
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _OCCLUSIONMAP

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
                float3 normal   : NORMAL;
				float2 uv       : TEXCOORD0;
            };

            struct Varrying
            {
                float4 vertex           : SV_POSITION;
                float2 uv               : TEXCOORD0;
                float3 localCoord       : TEXCOORD1;
                float3 localNormal      : TEXCOORD2;
                float3 worldPos         : TEXCOORD3;
            };

            float3 fbm(float2 pos)
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

            Varrying vert (Attribute IN)
            {                
                Varrying OUT;
                
                OUT.uv = IN.uv;
                OUT.localCoord = IN.vertex.xyz;
                OUT.localNormal = IN.normal.xyz;
                OUT.worldPos = TransformObjectToWorld(IN.vertex.xyz);

				float4 uv = float4(0, 0, 0, 1);
                uv.xy = (IN.uv.xy * 2 - 1) * float2(1, _ProjectionParams.x);
				OUT.vertex = uv; 

                return OUT;
            }

            half4 frag (Varrying IN) : SV_Target
            {
                if(_PrepareUV > 0 ){
                    return float4(0, 0, 1, 1);
                }  

                float3 bf = normalize(abs(IN.localNormal));
                bf /= dot(bf, (float3)1);

                float2 tx = IN.localCoord.yz * 0.75 + _PainterPosition.yz;
                float2 ty = IN.localCoord.zx * 0.75 + _PainterPosition.zx;
                float2 tz = IN.localCoord.xy * 0.75 + _PainterPosition.xy;

                float cx = fbm(tx).x * bf.x;
                float cy = fbm(ty).x * bf.y;
                float cz = fbm(tz).x * bf.z;
                float paint = (cx + cy + cz);

                float m = distance(_PainterPosition, IN.worldPos);

                float brush = smoothstep(_Radius, 0.0, m);
                paint *= brush;

                paint += smoothstep(_Strength, 0.0, m);

                float push = smoothstep(0.3, 0.5, paint);
                push *= smoothstep(0.4, 1.0, brush);

                float2 resolution = 2.0 * IN.uv - 1.0;
                float2 offset = 10.0 * push * (tx + tx + tz) / resolution;

                float4 frame = tex2D(_MainTex, IN.uv + offset);
                float4 color = lerp(frame, _PainterColor, paint);
                
                return color;
            }
            ENDHLSL
        }
    }
}

// float3 pos = float3(i.uv, 0) - _PainterPosition * 1.6;
// float paint = fbm(pos * 0.8).x;

// float m = distance(_PainterPosition, i.worldPos);

// float brush = smoothstep(_Radius, 0.0, m);
// paint *= brush;

// paint += smoothstep(0.25, 0.05, m);

// float4 data = tex2D(_MainTex, float2(0,0));
// float2 mousePrevious = data.xy;

// float4 frame = tex2D(_MainTex, i.uv);
// paint = max(paint, frame.x);

// return clamp(paint, 0.0, 1.0);