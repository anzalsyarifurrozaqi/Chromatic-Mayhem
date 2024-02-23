Shader "TNTC/ExtendIslands"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _UVIslands ("Texture UVIsalnds", 2D) = "white" {}
        _OffsetUV ("UVOffset", float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"        
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"     

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float _OffsetUV;
            sampler2D _UVIslands;

            #define TEX(uv) tex2D(_MainTex, uv).r

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {                
                float2 uv = i.uv;
                float3 color = float3(0, 0, 0);

                float3 noise = tex2D(_MainTex, i.worldPos.xy).rgb;
                float gray = noise.x;

                float3 unit =  float3(3.0 / _ScreenParams.xy, 0);
                float3 normal = normalize(float3(
                    TEX(uv + unit.xy) - TEX(uv - unit.xz),
                    TEX(uv - unit.zy) - TEX(uv + unit.zy),
                    gray*gray
                ));

                float3 dir = normalize(float3(0,1,2));
                float specular = pow(dot(normal, dir)*0.5 + 0.5, 20);
                color += float3(0.5, 0.5, 0.5) * specular;

                float4 background = 1;
                color = lerp(background, clamp(color, 0.0, 1.0), smoothstep(0.2, 0.5, noise.x));

                return float4(color, 1);
            }
            ENDHLSL
        }
    }
}

// float2 uv = i.uv;
// float4 color = tex2D(_MainTex, uv);

// float3 noise = tex2D(_MainTex, uv).rgb;
// float gray = noise.x;

// float3 unit =  float3(3.0 / _ScreenParams.xy, 0);
// float3 normal = normalize(float3(
//     TEX(uv + unit.xy) - TEX(uv - unit.xz),
//     TEX(uv - unit.zy) - TEX(uv + unit.zy),
//     gray*gray
// ));

// float3 dir = normalize(float3(0,1,2.0));
// float specular = pow(dot(normal, dir)*0.5+0.5, 20.0);
// color.rgb += float3(0.5, 0.5, 0.5) * specular;

// float4 background = tex2D(_MainTex, uv);
// color = lerp(background, clamp(color, 0.0, 1.0), smoothstep(0.2, 0.5, noise.x));

// return color;