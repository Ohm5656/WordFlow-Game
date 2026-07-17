Shader "WordFlow/LockVignette"
{
    Properties
    {
        // UGUI assigns the graphic texture to _MainTex on every RawImage. Declaring it silences the
        // per-frame "doesn't have a texture property '_MainTex'" error (which trips Error Pause).
        // The fragment ignores it — the vignette is procedural.
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        // Warm red, used only as a translucent light around the edge of the screen.
        _Color ("Edge Light", Color) = (0.9, 0.08, 0.06, 1)

        _Intensity ("Intensity (driven)", Range(0, 1)) = 0
        // Kept for material compatibility. The fixed red hold deliberately ignores beat pulses.
        _Pulse ("Pulse (compatibility)", Range(0, 1)) = 0

        _EdgeStart ("Vignette Inner Edge", Range(0, 1.5)) = 0.72
        _EdgeEnd ("Vignette Outer Edge", Range(0, 1.5)) = 1.18
        _CoreGlow ("Centre Tint (compatibility)", Range(0, 1)) = 0

        // Ceiling on the edge-light opacity, keeping the puzzle readable.
        _Strength ("Max Strength", Range(0, 1)) = 0.42
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        // Alpha blending carries only an edge cue, so the book remains its authored colour.
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

                // Only the routine's fade envelope changes _Intensity. The centre deliberately
                // remains transparent, so this is a red edge-light rather than a full-screen wash.
                float alpha = saturate(vignette * _Intensity) * _Strength;
                return float4(_Color.rgb, alpha);
            }

            ENDCG
        }
    }
}
