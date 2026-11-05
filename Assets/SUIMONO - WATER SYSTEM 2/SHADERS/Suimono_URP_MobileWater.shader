Shader "Suimono2/URP_MobileWater"
{
    Properties
    {
        [Header(Water Colors and Transparency)]
        _ShallowColor ("Shallow Color", Color) = (0.22, 0.62, 0.68, 0.75)
        _DeepColor ("Deep Color", Color) = (0.05, 0.22, 0.38, 0.92)
        _SpecColor ("Specular Color", Color) = (1.0, 0.98, 0.90, 1.0)
        _Smoothness ("Smoothness / Sparkle", Range(0.1, 1.0)) = 0.94
        _FresnelPower ("Fresnel Power", Range(0.5, 6.0)) = 3.0

        [Header(Suimono Waves Normal Maps)]
        [NoScaleOffset] _NormalTexA ("Wave Normal A (Calm)", 2D) = "bump" {}
        _NormalScaleA ("Wave Scale A", Float) = 0.05
        _WaveSpeedA ("Wave Speed A (XY)", Vector) = (0.022, 0.014, 0, 0)

        [NoScaleOffset] _NormalTexB ("Wave Normal B (Roll)", 2D) = "bump" {}
        _NormalScaleB ("Wave Scale B", Float) = 0.11
        _WaveSpeedB ("Wave Speed B (XY)", Vector) = (-0.016, 0.024, 0, 0)

        _NormalStrength ("Wave Normal Strength", Range(0.0, 2.0)) = 0.85

        [Header(Sky and Cubemap Reflections)]
        [NoScaleOffset] _CubeTex ("Reflection Cubemap", Cube) = "" {}
        _ReflectionStrength ("Reflection Strength", Range(0.0, 1.0)) = 0.6

        [Header(Foam and Surface Shimmer)]
        [NoScaleOffset] _FoamTex ("Foam Texture", 2D) = "black" {}
        _FoamColor ("Foam Color", Color) = (0.92, 0.97, 1.0, 0.5)
        _FoamScale ("Foam Scale", Float) = 0.035
        _FoamSpeed ("Foam Speed (XY)", Vector) = (0.012, -0.016, 0, 0)
        _FoamIntensity ("Foam Intensity", Range(0.0, 1.0)) = 0.25

        [Header(Subtle Wave Motion)]
        _WaveAmp ("Wave Undulation Height", Range(0.0, 0.1)) = 0.015
        _WaveFreq ("Wave Frequency", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogCoord : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _SpecColor;
                float _Smoothness;
                float _FresnelPower;

                float _NormalScaleA;
                float4 _WaveSpeedA;
                float _NormalScaleB;
                float4 _WaveSpeedB;
                float _NormalStrength;

                float _ReflectionStrength;

                float4 _FoamColor;
                float _FoamScale;
                float4 _FoamSpeed;
                float _FoamIntensity;

                float _WaveAmp;
                float _WaveFreq;
            CBUFFER_END

            // Dynamic player tracking for water clarity around the swimming character
            float4 _GlobalPlayerPos;
            float _GlobalPlayerRadius;

            TEXTURE2D(_NormalTexA);
            SAMPLER(sampler_NormalTexA);
            TEXTURE2D(_NormalTexB);
            SAMPLER(sampler_NormalTexB);
            TEXTURE2D(_FoamTex);
            SAMPLER(sampler_FoamTex);
            TEXTURECUBE(_CubeTex);
            SAMPLER(sampler_CubeTex);

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);

                // Subtle organic undulating motion
                if (_WaveAmp > 0.0001)
                {
                    float wave = sin(_Time.y * _WaveFreq + worldPos.x * 0.35 + worldPos.z * 0.3) * _WaveAmp;
                    worldPos.y += wave;
                }

                output.positionWS = worldPos;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogCoord = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 worldPos = input.positionWS;
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(worldPos);

                // World-space planar UV coordinates (seamless across any mesh, UV-independent)
                float2 uvA = worldPos.xz * _NormalScaleA + _WaveSpeedA.xy * _Time.y;
                float2 uvB = worldPos.xz * _NormalScaleB + _WaveSpeedB.xy * _Time.y;

                // Sample and unpack normal maps
                half4 normSampleA = SAMPLE_TEXTURE2D(_NormalTexA, sampler_NormalTexA, uvA);
                half4 normSampleB = SAMPLE_TEXTURE2D(_NormalTexB, sampler_NormalTexB, uvB);

                half3 nA = UnpackNormal(normSampleA);
                half3 nB = UnpackNormal(normSampleB);

                // Blend wave normals
                half3 nBlended = normalize(half3(nA.xy + nB.xy, nA.z * nB.z));
                nBlended.xy *= _NormalStrength;
                nBlended = normalize(nBlended);

                // Construct robust TBN frame from mesh normal
                half3 meshNormal = normalize(input.normalWS);
                half3 tangent = abs(meshNormal.y) > 0.99 ? half3(1, 0, 0) : normalize(cross(half3(0, 1, 0), meshNormal));
                half3 bitangent = cross(meshNormal, tangent);

                // Perturbed normal in world space
                half3 perturbedNormalWS = normalize(tangent * nBlended.x + bitangent * nBlended.y + meshNormal * nBlended.z);

                // Fresnel effect
                half NdotV = saturate(dot(perturbedNormalWS, viewDirWS));
                half fresnel = pow(1.0 - NdotV, _FresnelPower);

                // Base water color gradient from deep to shallow/fresnel
                half4 waterCol = lerp(_DeepColor, _ShallowColor, 1.0 - (fresnel * 0.45));
                waterCol.a = lerp(_ShallowColor.a, _DeepColor.a, 1.0 - (fresnel * 0.3));

                // Environmental Scene Lighting (Day vs. Night adaptation)
                // At night, moonlight and ambient light naturally darken the water rather than leaving it glowing opaque cyan
                Light mainLight = GetMainLight();
                half3 ambientLight = SampleSH(perturbedNormalWS);
                half NdotL = saturate(dot(perturbedNormalWS, mainLight.direction));
                half3 directLight = mainLight.color * NdotL;
                half3 sceneIllum = max(ambientLight + directLight * 0.75, half3(0.08, 0.10, 0.14));
                half envLuminance = saturate(dot(sceneIllum, half3(0.299, 0.587, 0.114)) * 1.25 + 0.05);

                // Modulate base water colors by scene lighting so the water dims harmoniously with nightfall
                waterCol.rgb *= sceneIllum;

                // Reflections: sample cubemap + environment probe, scaling brightness with environment illumination
                half3 reflDir = reflect(-viewDirWS, perturbedNormalWS);
                half3 envRefl = GlossyEnvironmentReflection(reflDir, 1.0 - _Smoothness, 1.0);
                half3 cubeRefl = SAMPLE_TEXTURECUBE(_CubeTex, sampler_CubeTex, reflDir).rgb * envLuminance;
                half3 finalRefl = max(cubeRefl, envRefl * 0.8) * envLuminance;
                waterCol.rgb = lerp(waterCol.rgb, finalRefl, fresnel * _ReflectionStrength);

                // Main light specular sparkle
                half3 halfDir = normalize(mainLight.direction + viewDirWS);
                half NdotH = saturate(dot(perturbedNormalWS, halfDir));
                half specPower = exp2(10.0 * _Smoothness + 1.0);
                half specular = pow(NdotH, specPower) * _Smoothness;
                half3 specLight = specular * mainLight.color * _SpecColor.rgb;
                waterCol.rgb += specLight;

                // Foam / ripple texture overlay lit by scene lighting
                float2 foamUV = worldPos.xz * _FoamScale + _FoamSpeed.xy * _Time.y;
                half foamSample = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, foamUV).r;
                half foamFactor = saturate(foamSample * _FoamIntensity * _FoamColor.a);
                half3 litFoam = _FoamColor.rgb * sceneIllum;
                waterCol.rgb = lerp(waterCol.rgb, litFoam, foamFactor);

                // Player proximity clarity clearance:
                // When swimming or wading in water, smoothly soften water opacity directly around the character
                // so the player is always clearly visible beneath the surface, especially during nighttime!
                float distToPlayer = distance(worldPos.xz, _GlobalPlayerPos.xz);
                float playerRadius = _GlobalPlayerRadius > 0.1 ? _GlobalPlayerRadius : 2.2;
                float playerProximity = smoothstep(playerRadius * 0.25, playerRadius * 1.35, distToPlayer);
                waterCol.a = lerp(waterCol.a * 0.35, waterCol.a, playerProximity);

                // Apply fog
                waterCol.rgb = MixFog(waterCol.rgb, input.fogCoord);

                return waterCol;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
