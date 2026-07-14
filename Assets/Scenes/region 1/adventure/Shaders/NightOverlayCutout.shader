// Stylised night overlay for reference_forest. A full-screen sprite tinted by its vertex
// colour (NightLighting sets darkColor + alpha) that cuts soft circular openings around up to
// eight light sources. NightLighting.cs feeds per-light data via a MaterialPropertyBlock:
//   _LightData0.._LightData7 = (centerX, centerY, innerRadius, outerRadius)  [world units]
//   _MinimumDarkness          = darkness kept at the brightest point (so holes aren't full daylight)
// An unused light slot has outerRadius (w) == 0 and is ignored.
// Slots 0-3: the fixture lights (fire_camp + lamps). Slots 4-7: runtime lights (hero + night quest
// pools), driven by NightLighting.SetDynamicLight. Also reused, on a UGUI RawImage with an
// instanced material, as the CutScene_bear/CutScene_ga night spotlight-follow overlay
// (NightTintOverlay) — only slot 0 is used there.
Shader "NSC/NightOverlayCutout"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _MinimumDarkness ("Minimum Darkness In Light", Range(0,1)) = 0.58
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv    : TEXCOORD0;
                float2 world : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _LightData0;
            float4 _LightData1;
            float4 _LightData2;
            float4 _LightData3;
            float4 _LightData4;
            float4 _LightData5;
            float4 _LightData6;
            float4 _LightData7;
            float  _MinimumDarkness;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // 1.0 = full darkness here, lower = a hole. Unused slot (w<=0) keeps full darkness.
            float DarknessFromLight(float4 data, float2 worldPos)
            {
                if (data.w <= 0.0001) return 1.0;
                float d = distance(worldPos, data.xy);
                float t = smoothstep(data.z, data.w, d); // 0 inside inner radius, 1 past outer radius
                return lerp(_MinimumDarkness, 1.0, t);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                float dark = 1.0;
                dark = min(dark, DarknessFromLight(_LightData0, i.world));
                dark = min(dark, DarknessFromLight(_LightData1, i.world));
                dark = min(dark, DarknessFromLight(_LightData2, i.world));
                dark = min(dark, DarknessFromLight(_LightData3, i.world));
                dark = min(dark, DarknessFromLight(_LightData4, i.world));
                dark = min(dark, DarknessFromLight(_LightData5, i.world));
                dark = min(dark, DarknessFromLight(_LightData6, i.world));
                dark = min(dark, DarknessFromLight(_LightData7, i.world));
                col.a *= dark;
                return col;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
