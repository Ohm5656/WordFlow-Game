Shader "WordFlow/CloudFog"
{
    Properties
    {
        // UGUI assigns the graphic texture to _MainTex on every RawImage. Declaring it here silences
        // the per-frame "doesn't have a texture property '_MainTex'" error (which trips Error Pause
        // and auto-pauses play mode). The fragment ignores it — the clouds are procedural.
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _CloudColor ("Cloud Color", Color) = (0.62, 0.64, 0.68, 1)
        _Density ("Density", Range(0, 1)) = 0.55

        _CloudScale ("Cloud Scale (cells across)", Float) = 3.5
        _Coverage ("Coverage Inside Band", Range(0, 1)) = 0.55

        _MaxReach ("Max Reach", Range(0, 0.5)) = 0.26
        _CoreFrac ("Solid Core Fraction", Range(0, 1)) = 0.4
        _EdgeSoftness ("Front Softness", Range(0.01, 0.5)) = 0.15
        _Fluff ("Edge Fluff", Range(0, 1)) = 0.35
        _Softness ("Edge Softness", Range(0.01, 0.5)) = 0.18

        _DriftSpeed ("Drift Speed", Float) = 0.02
        _BoilSpeed ("Boil Speed", Float) = 0.08

        _FadeIn ("Opacity Fade-In (progress)", Range(0.02, 1)) = 0.45
        _PulseSwell ("Beat Pulse Swell", Range(0, 0.2)) = 0.04
        _PulseReach ("Beat Pulse Reach", Range(0, 0.1)) = 0.03

        _Progress ("Progress (driven)", Range(0, 1)) = 0
        _FogTime ("Fog Time (driven)", Float) = 0
        _Pulse ("Beat Pulse (driven)", Range(0, 1)) = 0
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

            float4 _CloudColor;
            float _Density;

            float _CloudScale;
            float _Coverage;
            float _Fluff;
            float _Softness;

            float _MaxReach;
            float _CoreFrac;
            float _EdgeSoftness;

            float _DriftSpeed;
            float _BoilSpeed;

            float _FadeIn;
            float _PulseSwell;
            float _PulseReach;

            float _Progress;
            float _FogTime;
            float _Pulse;


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


            float fbm(float2 p)
            {
                float sum = 0.0;
                float amp = 0.5;

                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    sum += noise(p) * amp;

                    p = p * 2.02 + float2(17.3, 9.1);
                    amp *= 0.5;
                }

                return sum / 0.9375;
            }


            // One round puff per cell, its centre and radius hashed from the cell id. Sampled over
            // the 3x3 neighbourhood and unioned with max(), so a puff that straddles a cell border
            // still bleeds into its neighbours — that soft union is what makes the puffs clump into
            // clusters instead of sitting in a visible grid.
            float PuffField(float2 p, float swell)
            {
                float2 cell = floor(p);
                float2 f = p - cell;

                float best = 0.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 o = float2(x, y);
                        float2 id = cell + o;

                        float2 jitter = float2(
                            random(id),
                            random(id + 31.7)
                        );

                        float2 center = o + 0.15 + jitter * 0.7;

                        float radius =
                            0.30
                            + random(id + 71.3) * 0.22
                            + swell;

                        float d = length(f - center);

                        float puff = 1.0 - smoothstep(
                            radius - _Softness,
                            radius,
                            d
                        );

                        best = max(best, puff);
                    }
                }

                return best;
            }


            fixed4 frag(v2f i) : SV_Target
            {
                float t = _FogTime;
                float boil = t * _BoilSpeed;

                // The overlay is a 16:9 rect but uv is 0..1 on both axes, so sample x in a stretched
                // space — otherwise every puff comes out as a squashed ellipse.
                float2 uv = float2(i.uv.x * 1.7777, i.uv.y);

                float2 p = uv * _CloudScale + float2(t * _DriftSpeed, 0.0);

                // Push the sample point around with fbm before evaluating the puffs, so the puff
                // edges break into cauliflower lumps. Without this they read as soap bubbles.
                float2 warp = float2(
                    fbm(p * 1.7 + float2(0.0, boil)),
                    fbm(p * 1.7 + float2(5.2, 1.3) - float2(boil * 0.6, 0.0))
                ) - 0.5;

                // Each countdown beep drives _Pulse to 1, swelling every puff — the clouds breathe
                // on the same beat the purple smoke lurches on.
                float puffs = PuffField(
                    p + warp * _Fluff * 2.0,
                    _Pulse * _PulseSwell
                );

                // Distance to the nearest screen edge, measured in unstretched uv — the same ruler
                // EdgeFog uses, so the two layers advance in step instead of drifting apart.
                float d = min(
                    min(i.uv.x, 1.0 - i.uv.x),
                    min(i.uv.y, 1.0 - i.uv.y)
                );

                // Same reach maths as the purple layer: the band grows in from all four edges as the
                // clock runs out and lurches on each countdown beep. _MaxReach < 0.5, so the front
                // never reaches the centre and the book stays readable.
                float reach = _Progress * _MaxReach + _Pulse * _PulseReach;
                float core = reach * _CoreFrac;

                float front = 1.0 - smoothstep(
                    core,
                    reach + _EdgeSoftness,
                    d
                );

                // The front feeds the puff coverage THRESHOLD rather than multiplying the final
                // alpha. Deep in the band every puff clears the threshold; at the leading edge only
                // the biggest ones do. That is what dissolves the advancing front into separate
                // clumps and stragglers — multiplying the alpha instead would fade a full-screen
                // cloud field behind a smooth rectangular window, which is the bug this fixes.
                float threshold = lerp(1.0, 1.0 - _Coverage, front);

                float clouds = smoothstep(
                    threshold,
                    threshold + _Softness,
                    puffs
                );

                clouds = saturate(clouds) * _Density;

                // Materialise out of nothing instead of switching on at full weight.
                clouds *= smoothstep(0.0, _FadeIn, _Progress);

                return float4(
                    _CloudColor.rgb,
                    clouds * _CloudColor.a
                );
            }

            ENDCG
        }
    }
}
