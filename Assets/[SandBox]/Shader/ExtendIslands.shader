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

            struct Attribute
            {
                float4 vertex       : POSITION;
                float3 normal       : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varrying
            {
                float4 vertex       : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 worldPos     : TEXCOORD1;
                float3 worldNormal  : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float _OffsetUV;
            sampler2D _UVIslands;

            float3 _LightDirection;

            float4 GetShadowCasterPositionCS(float3 positionWS, float3 normalWS) {
                float3 lightDirectionWS = _LightDirection;

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            #define TEX(uv) tex2D(_MainTex, uv).r

            Varrying vert (Attribute IN)
            {
                Varrying OUT;
                OUT.vertex = TransformObjectToHClip(IN.vertex);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.worldPos = TransformObjectToWorld(IN.vertex);
                OUT.worldNormal = TransformObjectToWorldNormal(IN.normal);
                return OUT;
            }

            half4 frag (Varrying IN) : SV_Target
            {
                float2 offsets[8] = {float2(-_OffsetUV, 0), float2(_OffsetUV, 0), float2(0, _OffsetUV), float2(0, -_OffsetUV), float2(-_OffsetUV, _OffsetUV), float2(_OffsetUV, _OffsetUV), float2(_OffsetUV, -_OffsetUV), float2(-_OffsetUV, -_OffsetUV)};
				float2 uv = IN.uv;
				float4 color = tex2D(_MainTex, uv);
				float4 island = tex2D(_UVIslands, uv);

                if(island.z < 1){
                    float4 extendedColor = color;
                    for	(int i = 0; i < offsets.Length; i++){
                        float2 currentUV = uv + offsets[i] * _MainTex_TexelSize.xy;
                        float4 offsettedColor = tex2D(_MainTex, currentUV);
                        extendedColor = max(offsettedColor, extendedColor);
                    }
                    color = extendedColor;
                }
				return color;
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