Shader "Jbel/Waterfall"
{
    Properties
    {
        [NoScaleOffset] _FoamTex ("Foam Texture", 2D) = "white" {}
        _Color ("Water Color", Color) = (0.72, 0.88, 0.98, 0.9)
        _Speed ("Fall Speed", Float) = 1.7
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-5" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : TEXCOORD1; float fogCoord : TEXCOORD2; };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Speed;
            CBUFFER_END
            TEXTURE2D(_FoamTex); SAMPLER(sampler_FoamTex);

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = input.uv;          // x = metres across, y = metres down the fall
                o.color = input.color;    // a = edge fade
                o.fogCoord = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _Speed;
                half n1 = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, float2(i.uv.x * 0.21, i.uv.y * 0.09 - t * 0.21)).r;
                half n2 = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, float2(i.uv.x * 0.37 + 0.31, i.uv.y * 0.16 - t * 0.33)).r;
                half streak = saturate(n1 * 0.75 + n2 * 0.65);
                Light mainLight = GetMainLight();
                half3 illum = max(SampleSH(half3(0, 1, 0)) + mainLight.color * 0.7, half3(0.10, 0.12, 0.16));
                half3 col = lerp(_Color.rgb * 0.72, half3(1, 1, 1), smoothstep(0.3, 0.85, streak)) * min(illum, half3(1.15, 1.15, 1.15));
                half a = _Color.a * saturate(0.5 + streak * 0.7) * i.color.a;
                col = MixFog(col, i.fogCoord);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
