Shader "WordFlow/EdgeFog"
{
    Properties
    {
        // UGUI (RawImage/Image) always assigns the graphic texture to _MainTex; declaring it here
        // silences the per-frame "doesn't have a texture property '_MainTex'" console error (which
        // also trips Error Pause). The fragment ignores it — the fog is procedural.
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FogColor ("Fog Color", Color) = (0.55, 0.57, 0.62, 1)
        _Progress ("Fog Progress", Range(0, 1)) = 0
        _MaxReach ("Max Reach", Range(0, 0.5)) = 0.38
        _Softness ("Softness", Range(0.001, 0.2)) = 0.08
        _NoiseScale ("Noise Scale", Float) = 5
        _NoiseStrength ("Noise Strength", Range(0, 0.2)) = 0.08
        _Density ("Density", Range(0, 1)) = 0.55
        _FogTime ("Fog Time", Float) = 0
        _Speed ("Speed", Float) = 0.12
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _FogColor;

            float _Progress;
            float _MaxReach;
            float _Softness;

            float _NoiseScale;
            float _NoiseStrength;
            float _Density;

            float _FogTime;
            float _Speed;


            v2f vert(appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                return o;
            }


            float random(float2 p)
            {
                return frac(
                    sin(dot(p, float2(12.9898, 78.233)))
                    * 43758.5453
                );
            }


            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                f = f * f * (3.0 - 2.0 * f);

                float a = random(i);
                float b = random(i + float2(1, 0));
                float c = random(i + float2(0, 1));
                float d = random(i + float2(1, 1));

                return lerp(
                    lerp(a, b, f.x),
                    lerp(c, d, f.x),
                    f.y
                );
            }


            float GetSideFog(
                float distanceFromEdge,
                float noiseValue,
                float reach
            )
            {
                float distortedDistance =
                    distanceFromEdge
                    + (noiseValue - 0.5) * _NoiseStrength;

                return 1.0 - smoothstep(
                    reach - _Softness,
                    reach + _Softness,
                    distortedDistance
                );
            }


            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                float time = _FogTime * _Speed;
                float scale = _NoiseScale;

                float leftNoise = noise(
                    float2(
                        uv.y * scale + time,
                        uv.x * scale + time * 0.4
                    )
                );

                float rightNoise = noise(
                    float2(
                        uv.y * scale - time + 17.0,
                        (1.0 - uv.x) * scale + time * 0.5
                    )
                );

                float bottomNoise = noise(
                    float2(
                        uv.x * scale + time + 31.0,
                        uv.y * scale + time * 0.6
                    )
                );

                float topNoise = noise(
                    float2(
                        uv.x * scale - time + 53.0,
                        (1.0 - uv.y) * scale + time * 0.45
                    )
                );


                float reach = _Progress * _MaxReach;

                float leftFog = GetSideFog(
                    uv.x,
                    leftNoise,
                    reach
                );

                float rightFog = GetSideFog(
                    1.0 - uv.x,
                    rightNoise,
                    reach
                );

                float bottomFog = GetSideFog(
                    uv.y,
                    bottomNoise,
                    reach
                );

                float topFog = GetSideFog(
                    1.0 - uv.y,
                    topNoise,
                    reach
                );


                float fog = max(
                    max(leftFog, rightFog),
                    max(topFog, bottomFog)
                );


                float detail1 = noise(
                    uv * scale * 2.0
                    + float2(time * 0.7, time * 0.25)
                );

                float detail2 = noise(
                    uv * scale * 3.5
                    + float2(-time * 0.3, time * 0.6)
                );

                float detail =
                    detail1 * 0.65
                    + detail2 * 0.35;

                fog *= lerp(0.35, 1.0, detail);

                fog *= _Density;

                fog *= smoothstep(
                    0.0,
                    0.03,
                    _Progress
                );


                return float4(
                    _FogColor.rgb,
                    fog * _FogColor.a
                );
            }

            ENDCG
        }
    }
}