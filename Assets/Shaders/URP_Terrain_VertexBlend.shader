Shader "Custom/URP_Terrain_VertexBlend"
{
    Properties
    {
        [Header(Mode Selection)]
        [Toggle(_USE_CHANNEL_WEIGHTS)] _UseChannelWeights("Direct RGB Weights (R=Dirt, G=Grass, B=Rock)", Float) = 1.0

        [Header(Grass Layer)]
        _GrassTex("Grass Albedo", 2D) = "white" {}
        _GrassNorm("Grass Normal", 2D) = "bump" {}
        _GrassTint("Grass Tint", Color) = (0.75, 0.95, 0.5, 1.0)
        _GrassTiling("Grass Tiling", Float) = 30.0
        _GrassSmoothness("Grass Smoothness", Range(0, 1)) = 0.08

        [Header(Rock Layer)]
        _RockTex("Rock Albedo", 2D) = "white" {}
        _RockNorm("Rock Normal", 2D) = "bump" {}
        _RockTint("Rock Tint", Color) = (0.85, 0.85, 0.85, 1.0)
        _RockTiling("Rock Tiling", Float) = 20.0
        _RockSmoothness("Rock Smoothness", Range(0, 1)) = 0.15
        _TriplanarSharpness("Rock Triplanar Sharpness", Range(1, 10)) = 4.0

        [Header(Dirt Layer)]
        _DirtTex("Dirt Albedo", 2D) = "white" {}
        _DirtNorm("Dirt Normal", 2D) = "bump" {}
        _DirtTint("Dirt Tint", Color) = (0.9, 0.82, 0.72, 1.0)
        _DirtTiling("Dirt Tiling", Float) = 25.0
        _DirtSmoothness("Dirt Smoothness", Range(0, 1)) = 0.1

        [Header(Slope and Normal)]
        _SlopeRockThreshold("Slope Rock Angle (Normal Y)", Range(0.1, 0.95)) = 0.65
        _SlopeRockBlend("Slope Rock Blend Softness", Range(0.01, 0.4)) = 0.15
        _NormalScale("Normal Strength", Range(0, 2)) = 1.0
        _BlenderTintStrength("Blender Color Tint", Range(0, 1)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_GrassTex); SAMPLER(sampler_GrassTex);
        TEXTURE2D(_GrassNorm); SAMPLER(sampler_GrassNorm);

        TEXTURE2D(_RockTex); SAMPLER(sampler_RockTex);
        TEXTURE2D(_RockNorm); SAMPLER(sampler_RockNorm);

        TEXTURE2D(_DirtTex); SAMPLER(sampler_DirtTex);
        TEXTURE2D(_DirtNorm); SAMPLER(sampler_DirtNorm);

        CBUFFER_START(UnityPerMaterial)
            float _UseChannelWeights;
            float4 _GrassTint;
            float _GrassTiling;
            float _GrassSmoothness;

            float4 _RockTint;
            float _RockTiling;
            float _RockSmoothness;
            float _TriplanarSharpness;

            float4 _DirtTint;
            float _DirtTiling;
            float _DirtSmoothness;

            float _SlopeRockThreshold;
            float _SlopeRockBlend;
            float _NormalScale;
            float _BlenderTintStrength;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS   : POSITION;
            float3 normalOS     : NORMAL;
            float4 tangentOS    : TANGENT;
            float4 color        : COLOR;
            float2 uv           : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS   : SV_POSITION;
            float3 positionWS   : TEXCOORD0;
            float3 normalWS     : TEXCOORD1;
            float4 color        : COLOR;
            float fogFactor     : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        Varyings VertexPass(Attributes IN)
        {
            Varyings OUT;
            UNITY_SETUP_INSTANCE_ID(IN);
            UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

            VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
            VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

            OUT.positionCS = posInputs.positionCS;
            OUT.positionWS = posInputs.positionWS;
            OUT.normalWS   = normInputs.normalWS;
            OUT.color      = IN.color;
            OUT.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);

            return OUT;
        }

        // Triplanar rock sampling for seamless cliffs
        void SampleRockTriplanar(float3 worldPos, float3 normalWS, out half3 rockCol, out half3 rockNorm)
        {
            float3 blendWeights = pow(abs(normalWS), _TriplanarSharpness);
            float sum = blendWeights.x + blendWeights.y + blendWeights.z;
            blendWeights /= max(sum, 0.0001);

            float scale = _RockTiling * 0.02;
            float2 uvX = worldPos.zy * scale;
            float2 uvY = worldPos.xz * scale;
            float2 uvZ = worldPos.xy * scale;

            half4 cX = SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, uvX);
            half4 cY = SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, uvY);
            half4 cZ = SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, uvZ);
            rockCol = (cX.rgb * blendWeights.x + cY.rgb * blendWeights.y + cZ.rgb * blendWeights.z) * _RockTint.rgb;

            half4 nX = SAMPLE_TEXTURE2D(_RockNorm, sampler_RockNorm, uvX);
            half4 nY = SAMPLE_TEXTURE2D(_RockNorm, sampler_RockNorm, uvY);
            half4 nZ = SAMPLE_TEXTURE2D(_RockNorm, sampler_RockNorm, uvZ);
            half3 normX = UnpackNormalScale(nX, _NormalScale);
            half3 normY = UnpackNormalScale(nY, _NormalScale);
            half3 normZ = UnpackNormalScale(nZ, _NormalScale);
            rockNorm = normalize(normX * blendWeights.x + normY * blendWeights.y + normZ * blendWeights.z);
        }

        ENDHLSL

        // ----------------------------------------------------------------
        // Forward Lit Pass
        // ----------------------------------------------------------------
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex VertexPass
            #pragma fragment FragPass

            #pragma multi_compile_instancing
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fog

            half4 FragPass(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 worldPos = IN.positionWS;
                float3 geomNormalWS = normalize(IN.normalWS);

                // --- Calculate Layer Weights ---
                float3 weights;
                if (_UseChannelWeights > 0.5)
                {
                    // Direct channel weights: G = Grass, B = Rock, R = Dirt
                    weights = float3(IN.color.r, IN.color.g, IN.color.b);
                }
                else
                {
                    // Heuristic decoding of raw Blender vertex colors
                    float g = IN.color.g;
                    float r = IN.color.r;
                    float b = IN.color.b;
                    float greenExc = max(0.0, g - max(r, b));
                    float redExc = max(0.0, r - g);
                    float gray = 1.0 - saturate(abs(r - g) * 5.0 + abs(g - b) * 5.0);

                    weights.g = saturate(greenExc * 5.0); // grass
                    weights.r = saturate(redExc * 5.0);   // dirt
                    weights.b = saturate(gray * 3.0);     // rock
                }

                // Slope blending: steep cliffs become rock
                float slopeRock = 1.0 - smoothstep(_SlopeRockThreshold - _SlopeRockBlend, _SlopeRockThreshold + _SlopeRockBlend, geomNormalWS.y);
                weights.b = max(weights.b, slopeRock);

                // Normalize weights
                float totalW = weights.x + weights.y + weights.z;
                if (totalW > 0.001) weights /= totalW;
                else weights = float3(0.0, 1.0, 0.0); // fallback grass

                // --- Texture Sampling ---
                // Grass
                float2 uvGrass = worldPos.xz * (_GrassTiling * 0.02);
                half4 grassAlbedo = SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, uvGrass) * _GrassTint;
                half4 grassNormSample = SAMPLE_TEXTURE2D(_GrassNorm, sampler_GrassNorm, uvGrass);
                half3 grassNormal = UnpackNormalScale(grassNormSample, _NormalScale);

                // Dirt
                float2 uvDirt = worldPos.xz * (_DirtTiling * 0.02);
                half4 dirtAlbedo = SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, uvDirt) * _DirtTint;
                half4 dirtNormSample = SAMPLE_TEXTURE2D(_DirtNorm, sampler_DirtNorm, uvDirt);
                half3 dirtNormal = UnpackNormalScale(dirtNormSample, _NormalScale);

                // Rock (Triplanar)
                half3 rockAlbedo;
                half3 rockNormal;
                SampleRockTriplanar(worldPos, geomNormalWS, rockAlbedo, rockNormal);

                // --- Blend Textures ---
                half3 finalAlbedo = dirtAlbedo.rgb * weights.x +
                                   grassAlbedo.rgb * weights.y +
                                   rockAlbedo.rgb  * weights.z;

                half3 tangentNormal = dirtNormal * weights.x +
                                      grassNormal * weights.y +
                                      rockNormal  * weights.z;

                float finalSmoothness = _DirtSmoothness * weights.x +
                                        _GrassSmoothness * weights.y +
                                        _RockSmoothness  * weights.z;

                // Optional blend of vertex color tint
                if (_BlenderTintStrength > 0.0)
                {
                    finalAlbedo = lerp(finalAlbedo, finalAlbedo * IN.color.rgb * 1.5, _BlenderTintStrength);
                }

                // Blend normals into world space
                float3 groundPerturb = float3(dirtNormal.x * weights.x + grassNormal.x * weights.y,
                                              0.0,
                                              dirtNormal.y * weights.x + grassNormal.y * weights.y) * 0.6;
                float3 normalWS = normalize(geomNormalWS + groundPerturb + rockNormal * (weights.z * 0.5));

                // --- URP Lighting ---
                float4 shadowCoord = TransformWorldToShadowCoord(worldPos);
                Light mainLight = GetMainLight(shadowCoord);

                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float shadow = lerp(0.4, 1.0, mainLight.shadowAttenuation);
                half3 direct = mainLight.color.rgb * (NdotL * 0.6 + 0.4) * shadow;
                half3 ambient = SampleSH(normalWS) * 0.4 + half3(0.25, 0.25, 0.25);

                // Additional Lights
                half3 additionalLighting = half3(0, 0, 0);
                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                {
                    Light addLight = GetAdditionalLight(lightIndex, worldPos);
                    float addNdotL = saturate(dot(normalWS, addLight.direction));
                    additionalLighting += addLight.color * (addNdotL * addLight.distanceAttenuation * addLight.shadowAttenuation);
                }
                #endif

                half3 finalColor = finalAlbedo * (direct + ambient + additionalLighting);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // ----------------------------------------------------------------
        // Shadow Caster Pass
        // ----------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct ShadowAttributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 _LightDirection;

            ShadowVaryings ShadowPassVertex(ShadowAttributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 ShadowPassFragment(ShadowVaryings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }

        // ----------------------------------------------------------------
        // DepthOnly Pass
        // ----------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthPassVertex
            #pragma fragment DepthPassFragment

            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes
            {
                float4 positionOS   : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            DepthVaryings DepthPassVertex(DepthAttributes input)
            {
                DepthVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthPassFragment(DepthVaryings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }

        // ----------------------------------------------------------------
        // DepthNormals Pass
        // ----------------------------------------------------------------
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthNormalsAttributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthNormalsVaryings
            {
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            DepthNormalsVaryings DepthNormalsVertex(DepthNormalsAttributes input)
            {
                DepthNormalsVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFragment(DepthNormalsVaryings input) : SV_TARGET
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
