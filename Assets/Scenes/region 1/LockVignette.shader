Shader "WordFlow/LockVignette"
{
    Properties
    {
        // UGUI assigns the graphic texture to _MainTex on every RawImage. Declaring it silences the
        // per-frame "doesn't have a texture property '_MainTex'" error (which trips Error Pause).
        // The fragment ignores it — the vignette is procedural.
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        // Deep blood red, not a bright signal red. Multiply drives everything toward this colour, so
        // a bright tint would wash the screen out instead of darkening it. The green and blue floors
        // (0.12 / 0.15) stop the purple smoke from losing its blue entirely.
        _Color ("Alert Tint", Color) = (0.55, 0.12, 0.15, 1)

        _Intensity ("Intensity (driven)", Range(0, 1)) = 0
        // Kept for material compatibility. The fixed red hold deliberately ignores beat pulses.
        _Pulse ("Pulse (compatibility)", Range(0, 1)) = 0

        _EdgeStart ("Vignette Inner Edge", Range(0, 1.5)) = 0.15
        _EdgeEnd ("Vignette Outer Edge", Range(0, 1.5)) = 1.1
        _CoreGlow ("Centre Tint", Range(0, 1)) = 0.3

        // Ceiling on the darkening, so the book and stones never fall into unreadable shadow.
        _Strength ("Max Strength", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        // MULTIPLY, not additive. Additive brightens — and on this bright scene (cream book, pale
        // sky) adding red gave candy pink, which reads as sweet, not dangerous. Multiply darkens and
        // reddens instead: result = dst * tint.
        //
        // It keeps the guarantee additive gave us: multiply SCALES every pixel, it never covers one,
        // so the smoke's contrast against the background survives proportionally and the smoke stays
        // fully visible. It just reads as smoke in a blood-red room now. Do not change this line.
        Blend DstColor Zero
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

            float4 _Color;
            float _Intensity;
            float _Pulse;
            float _EdgeStart;
            float _EdgeEnd;
            float _CoreGlow;
            float _Strength;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 0 at the centre, 1 at the edge midpoints, ~1.41 in the corners — the alert closes
                // in hardest from the edges, while the book in the middle stays readable.
                float d = length((i.uv - 0.5) * 2.0);

                float vignette = smoothstep(_EdgeStart, _EdgeEnd, d);

                // _CoreGlow keeps a wash of red even dead centre, so the whole screen reads "you took
                // a hit" rather than "there is a red frame around the screen".
                float shape = saturate(vignette + _CoreGlow);

                // Fixed red hold: beep pulses still drive the smoke, but never change this overlay.
                // Only the lock routine's fade envelope changes _Intensity.
                float level = _Intensity;

                // lerp from white (multiply by 1 = untouched) toward the tint. _Strength caps how far
                // it can ever go, so nothing is ever crushed to black.
                float3 tint = lerp(float3(1, 1, 1), _Color.rgb, saturate(shape * level) * _Strength);

                return float4(tint, 1.0);
            }

            ENDCG
        }
    }
}
