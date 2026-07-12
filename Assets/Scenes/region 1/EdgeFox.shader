Shader "WordFlow/EdgeFog"
{
    Properties
    {
        // UGUI (RawImage/Image) always assigns the graphic texture to _MainTex; declaring it here
        // silences the per-frame "doesn't have a texture property '_MainTex'" console error (which
        // also trips Error Pause). The fragment ignores it — the fog is procedural.
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _FogColor ("Fog Color", Color) = (0.52, 0.55, 0.63, 1)
        _Progress ("Fog Progress", Range(0, 1)) = 0

        _MaxReach ("Max Reach", Range(0, 0.5)) = 0.34
        _CoreFrac ("Solid Core Fraction", Range(0, 1)) = 0.35
        _Softness ("Front Softness", Range(0.01, 0.5)) = 0.22

        _NoiseScale ("Noise Scale", Float) = 2.5
        _NoiseStrength ("Front Wobble", Range(0, 0.4)) = 0.18
        _WarpAmount ("Curl / Warp", Range(0, 1)) = 0.4
        _WispStrength ("Wisp Strength", Range(0, 1)) = 0.6

        _Density ("Density", Range(0, 1)) = 1

        _FogTime ("Fog Time", Float) = 0
        _Speed ("Inward Drift Speed", Float) = 0.05
        _BoilSpeed ("Boil Speed", Float) = 0.12

        _Pulse ("Beat Pulse (driven)", Range(0, 1)) = 0
        _PulseReach ("Beat Pulse Reach", Range(0, 0.1)) = 0.03

        _FadeIn ("Opacity Fade-In (progress)", Range(0.02, 1)) = 0.45
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
            float _CoreFrac;
            float _Softness;

            float _NoiseScale;
            float _NoiseStrength;
            float _WarpAmount;
            float _WispStrength;
            float _Density;

            float _FogTime;
            float _Speed;
            float _BoilSpeed;

            float _Pulse;
            float _PulseReach;

            float _FadeIn;


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


            // Fractal brownian motion: 4 octaves of value noise. This is what turns the old flat
            // blobs into cloud-like billows. Result is normalised back to roughly 0..1.
            float fbm(float2 p)
            {
                float sum = 0.0;
                float amp = 0.5;

                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    sum += noise(p) * amp;

                    // 2.02 (not 2.0) + an offset keeps the octaves from lining up on a grid.
                    p = p * 2.02 + float2(17.3, 9.1);
                    amp *= 0.5;
                }

                return sum / 0.9375;
            }


            // Domain-warped fbm: sample the noise field at coordinates that are themselves pushed
            // around by another noise field. This is what makes the smoke curl instead of slide.
            float SmokeField(float2 p, float boil)
            {
                float2 w = float2(
                    fbm(p + float2(0.0, boil)),
                    fbm(p + float2(5.2, 1.3) + float2(boil * 0.8, 0.0))
                );

                return fbm(p + (w - 0.5) * (_WarpAmount * 4.0));
            }


            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                float t = _FogTime;
                float boil = t * _BoilSpeed;

                // The whole noise field drifts toward the centre of the screen, so every edge's
                // smoke reads as advancing inward rather than scrolling sideways.
                float2 c = uv - 0.5;
                float2 inward = -c / max(length(c), 0.001);
                float2 flow = inward * (t * _Speed);

                float2 p = uv * _NoiseScale + flow;

                // Distance to the nearest screen edge. min() over the 4 sides is the same closing
                // rectangular frame the old max()-of-4-sides produced, for a quarter of the cost.
                float d = min(
                    min(uv.x, 1.0 - uv.x),
                    min(uv.y, 1.0 - uv.y)
                );

                // Each countdown beep drives _Pulse to 1, so the whole smoke front lurches inward
                // and settles back. Audio and visual landing on the same beat is what sells the
                // countdown — without it the fog is just a filter that happens to be growing.
                float reach = _Progress * _MaxReach + _Pulse * _PulseReach;
                float core = reach * _CoreFrac;

                // Wobble the front, but fade the wobble out to nothing at the screen edge so the
                // core never develops holes.
                float n = SmokeField(p, boil);
                float edgeGuard = saturate(d / max(reach, 0.001));
                float dd = d + (n - 0.5) * _NoiseStrength * edgeGuard;

                // Solid up to the core, then one long soft gradient out past the reach. This wide
                // tail is what the reference fog has and the old hard smoothstep band did not.
                float fog = 1.0 - smoothstep(
                    core,
                    reach + _Softness,
                    dd
                );

                // Wisps: high-frequency detail that only eats the leading edge. coreMask -> 1 deep
                // inside the smoke, so the core stays opaque.
                float detail = fbm(
                    uv * _NoiseScale * 3.0
                    + flow * 1.6
                    + float2(boil * 0.4, -boil * 0.25)
                );

                float wisp = lerp(1.0 - _WispStrength, 1.0, detail);
                float coreMask = smoothstep(0.75, 1.0, fog);

                fog *= lerp(wisp, 1.0, coreMask);

                fog = saturate(fog) * _Density;

                // The band's soft tail (_Softness) is a constant that is always added to reach, so
                // the instant _Progress leaves 0 the screen edge would already sit at full density
                // — the smoke would "switch on" and then merely widen. Ramping opacity in over the
                // first _FadeIn of progress makes it materialise out of nothing instead: a faint
                // haze that thickens, which is what a threat creeping in actually looks like.
                fog *= smoothstep(0.0, _FadeIn, _Progress);

                // One colour for the whole countdown — the heavy storm-gray that used to only appear
                // in the last few seconds. A progress-driven tint was tried and cut: the shift had
                // to be crammed into the final ~3s to read as "panic", which made the last frames
                // look like a different smoke rather than the same smoke, thicker. Pressure comes
                // from reach, density and the beat pulse — not from a hue change.
                return float4(
                    _FogColor.rgb,
                    fog * _FogColor.a
                );
            }

            ENDCG
        }
    }
}
