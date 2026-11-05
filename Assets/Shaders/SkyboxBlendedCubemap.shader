Shader "Custom/SkyboxBlendedCubemap" {
    Properties {
        _Tint ("Tint Color", Color) = (1, 1, 1, 1)
        [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
        _Rotation ("Rotation", Range(0, 360)) = 0
        [NoScaleOffset] _TexA ("Cubemap A", Cube) = "grey" {}
        [NoScaleOffset] _TexB ("Cubemap B", Cube) = "grey" {}
        _Blend ("Blend (0 = A, 1 = B)", Range(0, 1)) = 0.0
    }

    SubShader {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            samplerCUBE _TexA;
            half4 _TexA_HDR;
            samplerCUBE _TexB;
            half4 _TexB_HDR;

            half4 _Tint;
            half _Exposure;
            float _Rotation;
            float _Blend;

            float3 RotateAroundYInDegrees (float3 vertex, float degrees)
            {
                float alpha = degrees * UNITY_PI / 180.0;
                float sina, cosa;
                sincos(alpha, sina, cosa);
                float2x2 m = float2x2(cosa, -sina, sina, cosa);
                return float3(mul(m, vertex.xz), vertex.y).xzy;
            }

            struct appdata_t {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float3 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 rotated = RotateAroundYInDegrees(v.vertex.xyz, _Rotation);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = rotated;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half4 rawA = texCUBE(_TexA, i.texcoord);
                half4 rawB = texCUBE(_TexB, i.texcoord);

                half3 colA = DecodeHDR(rawA, _TexA_HDR);
                half3 colB = DecodeHDR(rawB, _TexB_HDR);

                // Smooth cinematic crossfade between the two skyboxes
                half3 col = lerp(colA, colB, saturate(_Blend));
                col *= _Tint.rgb;
                col *= _Exposure;

                return half4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
