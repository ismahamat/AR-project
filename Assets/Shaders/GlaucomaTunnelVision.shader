Shader "ARProject/GlaucomaTunnelVision"
{
    Properties
    {
        _FogTint ("Fog Tint", Color) = (0.015, 0.013, 0.01, 1)
        _ClearRadius ("Central Clear Radius", Range(0.05, 0.55)) = 0.22
        _Feather ("Transition Feather", Range(0.02, 0.25)) = 0.09
        _Darkness ("Peripheral Darkness", Range(0, 1)) = 0.96
        _SpotStrength ("Blind Spot Strength", Range(0, 1)) = 0.65
        _NoiseStrength ("Haze Noise", Range(0, 0.35)) = 0.12
        _VeilStrength ("Milky Veil Strength", Range(0, 0.45)) = 0.18
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "GlaucomaTunnelVision"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _FogTint;
            float _ClearRadius;
            float _Feather;
            float _Darkness;
            float _SpotStrength;
            float _NoiseStrength;
            float _VeilStrength;

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash(i);
                float b = Hash(i + float2(1.0, 0.0));
                float c = Hash(i + float2(0.0, 1.0));
                float d = Hash(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            float SoftSpot(float2 uv, float2 center, float radius, float softness)
            {
                float distanceToCenter = distance(uv, center);
                return 1.0 - smoothstep(radius * softness, radius, distanceToCenter);
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 centered = uv - 0.5;
                float radial = length(centered);

                float tunnel = smoothstep(_ClearRadius, _ClearRadius + _Feather, radial);
                float peripheryMask = smoothstep(_ClearRadius * 0.75, 0.62, radial);

                float spots = 0.0;
                spots += SoftSpot(uv, float2(0.17, 0.70), 0.19, 0.28) * 0.9;
                spots += SoftSpot(uv, float2(0.79, 0.64), 0.16, 0.22) * 0.75;
                spots += SoftSpot(uv, float2(0.22, 0.28), 0.14, 0.25) * 0.65;
                spots += SoftSpot(uv, float2(0.69, 0.23), 0.18, 0.30) * 0.7;
                spots += SoftSpot(uv, float2(0.50, 0.87), 0.13, 0.35) * 0.45;
                spots = saturate(spots * peripheryMask);

                float slowNoise = Noise(uv * 18.0 + _Time.yy * 0.025);
                float fineNoise = Noise(uv * 56.0 - _Time.yy * 0.015);
                float haze = (slowNoise * 0.7 + fineNoise * 0.3) * _NoiseStrength * peripheryMask;
                float veilPulse = 0.55 + 0.45 * sin(_Time.y * 0.85 + slowNoise * 2.0);
                float veil = _VeilStrength * veilPulse * smoothstep(_ClearRadius * 0.35, 0.8, radial);

                float alpha = saturate(tunnel * _Darkness + spots * _SpotStrength + haze + veil);
                float brownShift = saturate(radial * 1.7);
                float3 tint = lerp(_FogTint.rgb * 0.55, _FogTint.rgb + veil * 0.22, brownShift);

                return fixed4(tint, alpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
