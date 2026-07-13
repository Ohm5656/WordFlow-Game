Shader "WordFlow/LockVignette"
{
    Properties
    {
        // UGUI assigns the graphic texture to _MainTex on every RawImage. Declaring it silences the
        // per-frame "doesn't have a texture property '_MainTex'" error (which trips Error Pause).
        // The fragment ignores it — the vignette is procedural.
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _Color ("Alert Color", Color) = (0.95, 0.15, 0.12, 1)

        _Intensity ("Intensity (driven)", Range(0, 1)) = 0
        _Pulse ("Pulse (driven)", Range(0, 1)) = 0

        _EdgeStart ("Vignette Inner Edge", Range(0, 1.5)) = 0.35
        _EdgeEnd ("Vignette Outer Edge", Range(0, 1.5)) = 1.15
        _CoreGlow ("Centre Glow", Range(0, 1)) = 0.08
        _PulseGain ("Pulse Gain", Range(0, 2)) = 0.75
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        // ADDITIVE, not alpha-over. This is the whole point of the effect: additive can only ADD red
        // light to what is already on screen, so it is mathematically incapable of hiding the smoke
        // underneath. A normal alpha overlay at any useful strength would bury it — the same
        // arithmetic that buried the purple smoke under the grey coat. Do not change this line.
        Blend SrcAlpha One
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
            float _PulseGain;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 0 at the centre, 1 at the edge midpoints, ~1.41 in the corners — so the alert is
                // hottest where a hit would sting and stays clear of the book in the middle.
                float d = length((i.uv - 0.5) * 2.0);

                float vignette = smoothstep(_EdgeStart, _EdgeEnd, d);

                // A little red even dead centre, so the whole screen reads "you took a hit" rather
                // than "there is a red frame around the screen".
                float a = (vignette + _CoreGlow) * _Intensity * (1.0 + _Pulse * _PulseGain);

                return float4(_Color.rgb, saturate(a) * _Color.a);
            }

            ENDCG
        }
    }
}
