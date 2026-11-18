// Toon Water for Unity 6 URP (VR single-pass instanced safe)
// Based on https://roystan.net/articles/toon-water/
// Needs: URP asset > Depth Texture enabled.
Shader "Custom/ToonWater"
{
    Properties
    {
        // 1. Depth based colour
        _DepthGradientShallow("Depth Gradient Shallow", Color) = (0.325, 0.807, 0.971, 0.725)
        _DepthGradientDeep("Depth Gradient Deep", Color) = (0.086, 0.407, 1, 0.749)
        _DepthMaxDistance("Depth Maximum Distance", Float) = 1

        // 2. Waves / foam
        _SurfaceNoise("Surface Noise", 2D) = "white" {}
        _SurfaceNoiseCutoff("Surface Noise Cutoff", Range(0, 1)) = 0.777
        _SurfaceNoiseScroll("Surface Noise Scroll Amount", Vector) = (0.03, 0.03, 0, 0)
        _SurfaceDistortion("Surface Distortion", 2D) = "white" {}
        _SurfaceDistortionAmount("Surface Distortion Amount", Range(0, 1)) = 0.27

        // 3. Consistent size foam
        _FoamColor("Foam Color", Color) = (1, 1, 1, 1)
        _FoamMaxDistance("Foam Maximum Distance", Float) = 0.4
        _FoamMinDistance("Foam Minimum Distance", Float) = 0.04
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ToonWater"
            Tags { "LightMode" = "UniversalForward" }

            // 4. Transparency
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // 6. Anti-aliasing width for the foam edge
            #define SMOOTHSTEP_AA 0.01

            TEXTURE2D(_SurfaceNoise);      SAMPLER(sampler_SurfaceNoise);
            TEXTURE2D(_SurfaceDistortion); SAMPLER(sampler_SurfaceDistortion);

            CBUFFER_START(UnityPerMaterial)
                float4 _DepthGradientShallow;
                float4 _DepthGradientDeep;
                float  _DepthMaxDistance;

                float4 _SurfaceNoise_ST;
                float  _SurfaceNoiseCutoff;
                float4 _SurfaceNoiseScroll;
                float4 _SurfaceDistortion_ST;
                float  _SurfaceDistortionAmount;

                float4 _FoamColor;
                float  _FoamMaxDistance;
                float  _FoamMinDistance;
            CBUFFER_END

            // 5. Normal alpha blend of foam over water colour (keeps foam colour true)
            float4 alphaBlend(float4 top, float4 bottom)
            {
                float3 color = (top.rgb * top.a) + (bottom.rgb * (1 - top.a));
                float alpha = top.a + bottom.a * (1 - top.a);
                return float4(color, alpha);
            }

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
                float4 screenPos  : TEXCOORD0;
                float2 noiseUV    : TEXCOORD1;
                float2 distortUV  : TEXCOORD2;
                float3 normalWS   : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                ZERO_INITIALIZE(Varyings, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.screenPos  = p.positionNDC;          // .w = depth of the water surface
                o.noiseUV    = TRANSFORM_TEX(v.uv, _SurfaceNoise);
                o.distortUV  = TRANSFORM_TEX(v.uv, _SurfaceDistortion);
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                // --- 1. Depth difference between water surface and whatever is behind it
                float rawDepth = SampleSceneDepth(screenUV);
                float existingDepthLinear = LinearEyeDepth(rawDepth, _ZBufferParams);
                float depthDifference = existingDepthLinear - i.screenPos.w;

                float waterDepthDifference01 = saturate(depthDifference / _DepthMaxDistance);
                float4 waterColor = lerp(_DepthGradientShallow, _DepthGradientDeep, waterDepthDifference01);

                // --- 3. Angle of the surface under the water (rebuilt from the depth buffer,
                //        so no extra normals camera is needed)
                float3 posWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                float3 n = cross(ddy(posWS), ddx(posWS));
                n *= rsqrt(max(dot(n, n), 1e-12));
                float normalDot = abs(dot(n, normalize(i.normalWS)));

                float foamDistance = lerp(_FoamMaxDistance, _FoamMinDistance, normalDot);
                float foamDepthDifference01 = saturate(depthDifference / foamDistance);
                float surfaceNoiseCutoff = foamDepthDifference01 * _SurfaceNoiseCutoff;

                // --- 2. Waves: scrolling noise, pulled around by a distortion texture
                float2 distortSample =
                    (SAMPLE_TEXTURE2D(_SurfaceDistortion, sampler_SurfaceDistortion, i.distortUV).xy * 2 - 1)
                    * _SurfaceDistortionAmount;

                float2 noiseUV = i.noiseUV + _Time.y * _SurfaceNoiseScroll.xy + distortSample;
                float surfaceNoiseSample = SAMPLE_TEXTURE2D(_SurfaceNoise, sampler_SurfaceNoise, noiseUV).r;

                float surfaceNoise = smoothstep(surfaceNoiseCutoff - SMOOTHSTEP_AA,
                                                surfaceNoiseCutoff + SMOOTHSTEP_AA,
                                                surfaceNoiseSample);

                float4 surfaceNoiseColor = _FoamColor;
                surfaceNoiseColor.a *= surfaceNoise;

                return alphaBlend(surfaceNoiseColor, waterColor);
            }
            ENDHLSL
        }
    }
}
