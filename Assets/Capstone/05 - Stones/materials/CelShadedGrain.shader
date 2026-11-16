Shader "Custom/CelShadedGrain"
{
    Properties
    {
        [Header(Base)]
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(Cel Shading)]
        _ShadowColor ("Shadow Colour", Color) = (0.35, 0.35, 0.5, 1)
        _AmbientInfluence ("Ambient Light Influence", Range(0, 1)) = 0.5
        [IntRange] _Bands ("Tone Bands", Range(2, 8)) = 3
        _Smoothness ("Band Edge Softness", Range(0.005, 0.5)) = 0.02

        [Header(Grain)]
        _GrainAmount ("Grain Amount (negative = darker)", Range(-1, 1)) = 0.15
        _GrainScale ("Grain Scale (higher = finer)", Range(1, 50000)) = 60
        _GrainStretch ("Grain Stretch XYZ", Vector) = (1, 1, 1, 0)
        _GrainClampMin ("Grain Clamp Min", Range(0, 1)) = 0.25
        _GrainClampMax ("Grain Clamp Max", Range(0, 1)) = 0.75
        _GrainBalance ("Grain Balance (0 = spots only, 1 = light + dark)", Range(0, 1)) = 1
        _GrainSoftness ("Grain Softness (low = blocky)", Range(0.01, 0.5)) = 0.5
        _BandJitter ("Band Edge Jitter", Range(0, 0.5)) = 0.05
        _GrainSpeed ("Grain Boil Speed (0 = still)", Range(0, 30)) = 0
        [Toggle] _WorldSpace ("Grain In World Space", Float) = 0

        [Header(Other)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadowColor;
            half _AmbientInfluence;
            half _Bands;
            half _Smoothness;
            half _GrainAmount;
            float _GrainScale;
            float4 _GrainStretch;
            half _GrainClampMin;
            half _GrainClampMax;
            half _GrainBalance;
            half _GrainSoftness;
            half _BandJitter;
            half _GrainSpeed;
            half _WorldSpace;
        CBUFFER_END
        ENDHLSL

        // ---------------------------------------------------------------
        // Main pass: lighting, shadows and grain
        // ---------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS);

                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = nrm.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            // ---------------- Noise ----------------

            // Cheap 3D hash: turns a position into a pseudo-random 0-1 number
            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            // 3D value noise. "soft" controls the blending between grid cells:
            // 0.5 = smooth blobs, low values = hard blocky grain
            float ValueNoise(float3 p, float soft)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 w = smoothstep(0.5 - soft, 0.5 + soft, f);

                float n000 = Hash13(i);
                float n100 = Hash13(i + float3(1, 0, 0));
                float n010 = Hash13(i + float3(0, 1, 0));
                float n110 = Hash13(i + float3(1, 1, 0));
                float n001 = Hash13(i + float3(0, 0, 1));
                float n101 = Hash13(i + float3(1, 0, 1));
                float n011 = Hash13(i + float3(0, 1, 1));
                float n111 = Hash13(i + float3(1, 1, 1));

                float x00 = lerp(n000, n100, w.x);
                float x10 = lerp(n010, n110, w.x);
                float x01 = lerp(n001, n101, w.x);
                float x11 = lerp(n011, n111, w.x);

                return lerp(lerp(x00, x10, w.y), lerp(x01, x11, w.y), w.z);
            }

            // The finished grain signal for this pixel.
            // Balance 0: runs 0 to 1 (spots on a neutral background).
            // Balance 1: runs -1 to 1 (lighter and darker patches).
            half GrainNoise(float3 positionOS, float3 positionWS)
            {
                // Stuck to the object, or fixed in the world (toggle)
                float3 p = lerp(positionOS, positionWS, _WorldSpace);
                p *= _GrainScale * _GrainStretch.xyz;

                // "Boil": the pattern re-randomises in steps, like hand-drawn animation
                float t = floor(_Time.y * _GrainSpeed);
                p += t * float3(17.1, 31.7, 53.3);

                float n = ValueNoise(p, _GrainSoftness);

                // Clamp window: below Min becomes 0, above Max becomes 1,
                // and everything in between is stretched to fill 0 to 1
                float range = max(_GrainClampMax - _GrainClampMin, 0.001);
                float m = saturate((n - _GrainClampMin) / range);

                return (half)lerp(m, m * 2.0 - 1.0, _GrainBalance);
            }

            // ---------------- Cel lighting ----------------

            // Turns a smooth 0-1 light value into a few flat tones
            half CelRamp(half v)
            {
                half levels = _Bands - 1.0h;
                half scaled = saturate(v) * levels;
                half band = floor(scaled);
                half f = frac(scaled);
                half edge = smoothstep(0.5h - _Smoothness, 0.5h + _Smoothness, f);
                return saturate((band + edge) / levels);
            }

            // One light's contribution. The jitter nudges the value before it is
            // snapped to a tone, which roughens the edges between bands.
            half3 CelLight(Light light, half3 normalWS, half bandJitter)
            {
                half ndl = saturate(dot(normalWS, light.direction));
                half atten = saturate(light.distanceAttenuation) * light.shadowAttenuation;
                half v = ndl * atten;
                // No jitter where there is no light at all, so noise can't invent light
                v = (v > 0.001h) ? saturate(v + bandJitter) : 0.0h;
                return light.color * CelRamp(v);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 normalWS = normalize(input.normalWS);

                // One grain value per pixel, used for both grain effects
                half grain = GrainNoise(input.positionOS, input.positionWS);
                half bandJitter = grain * _BandJitter;

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                half4 shadowMask = half4(1, 1, 1, 1);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.shadowCoord = shadowCoord;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                // Main directional light (sun)
                half3 lighting = 0;
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, shadowMask);
                lighting += CelLight(mainLight, normalWS, bandJitter);

                // Point lights, spot lights and extra directional lights
                // Neutral ambient occlusion value (newer URP versions require it)
                AmbientOcclusionFactor aoFactor = (AmbientOcclusionFactor)0;
                aoFactor.indirectAmbientOcclusion = 1.0h;
                aoFactor.directAmbientOcclusion = 1.0h;

            #if defined(_ADDITIONAL_LIGHTS) || USE_FORWARD_PLUS
                uint pixelLightCount = GetAdditionalLightsCount();

                #if USE_FORWARD_PLUS
                for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
                {
                    FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
                    Light dirLight = GetAdditionalLight(dirIndex, inputData, shadowMask, aoFactor);
                    lighting += CelLight(dirLight, normalWS, bandJitter);
                }
                #endif

                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
                    lighting += CelLight(light, normalWS, bandJitter);
                LIGHT_LOOP_END
            #endif

                // Flat shadow colour plus a little scene ambient light
                half3 ambient = _ShadowColor.rgb + SampleSH(normalWS) * _AmbientInfluence;

                half3 color = albedo.rgb * (ambient + lighting);

                // Grain: brightens (or darkens, if Amount is negative) the final colour
                color *= max(0.0h, 1.0h + grain * _GrainAmount);

                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        // Lets this object cast shadows onto others
        // ---------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                output.positionCS = positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        // Depth pass: needed for effects that read scene depth
        // ---------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        // Depth + normals pass: needed by outline effects that read normals
        // ---------------------------------------------------------------
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalWS = normalize(input.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remapped = octNormalWS * 0.5 + 0.5;
                return half4(PackFloat2To888(remapped), 0.0);
            #else
                return half4(normalWS, 0.0);
            #endif
            }
            ENDHLSL
        }
    }
}