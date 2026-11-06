Shader "UI/SolarFlareStroke"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SolarDeepColor ("Solar Deep Plasma (Deep Amber)", Color) = (0.85, 0.35, 0.08, 1.0)
        _SolarFlareColor ("Solar Flare (Vivid Gold)", Color) = (1.5, 0.85, 0.22, 1.0)
        _SolarCoreColor ("Solar Core (White-Hot Burst)", Color) = (2.2, 2.0, 1.6, 1.0)
        _Speed ("Plasma Flow Speed", Float) = 2.0
        _PulseSpeed ("Flare Burst Pulse Rate", Float) = 3.0
        _Intensity ("Glow Intensity", Float) = 1.35
        _TurbulenceScale ("Turbulence Scale", Float) = 0.035

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _SolarDeepColor;
            fixed4 _SolarFlareColor;
            fixed4 _SolarCoreColor;
            float _Speed;
            float _PulseSpeed;
            float _Intensity;
            float _TurbulenceScale;
            float _UnscaledTime;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            // Organic multi-octave solar plasma turbulence function
            float SolarTurbulence(float2 p, float t)
            {
                float2 p1 = p * 0.040 + float2(t * 0.7, sin(t * 0.5) * 0.3);
                float2 p2 = p * 0.085 - float2(t * 1.1, cos(t * 0.6) * 0.4);
                float2 p3 = p * 0.170 + float2(sin(p.y * 0.06 + t * 1.4), cos(p.x * 0.06 - t * 1.2));

                float w1 = sin(p1.x + sin(p1.y * 1.2));
                float w2 = sin(p2.y * 1.3 + cos(p2.x));
                float w3 = sin(p3.x * 1.4 + p3.y * 1.4);

                float combined = (w1 + w2 * 0.6 + w3 * 0.3) / 1.9;
                return saturate(combined * 0.5 + 0.5);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 texColor = tex2D(_MainTex, IN.texcoord);
                
                // Discard empty transparent pixels - prevents any square border artifacts
                clip(texColor.a - 0.003);

                float t = (_UnscaledTime > 0.0001 ? _UnscaledTime : _Time.y) * _Speed;

                // Continuous world-space plasma flow across the stroke
                float plasma = SolarTurbulence(IN.worldPosition.xy * _TurbulenceScale, t);

                // Travelling coronal flare pulses along the outline
                float coronalLoop = sin(IN.worldPosition.x * 0.018 - t * 2.2 + plasma * 2.5) * 0.5 + 0.5;
                coronalLoop = pow(coronalLoop, 3.5);

                // Solar breathing eruption pulse
                float eruption = 0.88 + 0.28 * sin(t * _PulseSpeed + IN.worldPosition.x * 0.012);

                // Plasma heat map: Deep Amber -> Vivid Gold -> White-Hot Core
                float heat = saturate(plasma * 0.7 + coronalLoop * 0.55);
                half3 flareGrad = lerp(_SolarDeepColor.rgb, _SolarFlareColor.rgb, heat);
                flareGrad = lerp(flareGrad, _SolarCoreColor.rgb, pow(heat, 3.0));

                // Modulate the authentic stroke's own soft gradient with solar fire
                half3 finalRgb = texColor.rgb * flareGrad * (_Intensity * eruption);

                // Natural soft alpha preserved from btnStroke.png
                half finalAlpha = texColor.a * IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                finalAlpha *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                return half4(finalRgb, finalAlpha);
            }
            ENDCG
        }
    }
}
