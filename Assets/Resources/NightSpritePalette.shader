Shader "UI/NightSpritePalette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ChromaKeyStrength ("Green Matte Cleanup", Range(0, 1)) = 0
        _MatteAlphaFloor ("Matte Alpha Floor", Range(0, 1)) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
        // Source PNGs contain straight alpha. This blend prevents low-alpha matte pixels from
        // contributing their full RGB colour as a visible rectangle.
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _AlphaTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            fixed _ChromaKeyStrength;
            fixed _MatteAlphaFloor;
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

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // Some quest sprites use Unity's ETC1 alpha-split import path. The alpha lives in
                // _AlphaTex instead of the colour texture; sampling it here preserves the original
                // cutout rather than rendering the transparent rectangle as image data.
                color.a *= tex2D(_AlphaTex, IN.texcoord).r;

                // Several imported actor frames carry a teal/green removal matte in their RGB data.
                // The normal UI ETC material hides it through its original alpha path; when using a
                // custom night material we remove that matte explicitly, without affecting assets
                // that do not need it (_ChromaKeyStrength is zero for those sprites).
                fixed greenDominance = color.g - max(color.r, color.b);
                fixed greenMatte = smoothstep(0.06, 0.17, greenDominance) * smoothstep(0.18, 0.55, color.g);
                color.a *= 1.0 - greenMatte * _ChromaKeyStrength;

                // The same generated frames have patterned remnants in other colours too
                // (brown and near-black), but all of those pixels stay below the opaque actor
                // silhouette. Trim that residual semi-transparent matte only for the dedicated
                // actor material; ordinary scene artwork keeps this at zero.
                fixed alphaMatte = smoothstep(_MatteAlphaFloor, 1.0, color.a);
                color.a *= lerp(1.0, alphaMatte, _ChromaKeyStrength);

                // Keep the original silhouette and alpha. Darken with cool moonlight while retaining
                // some warm pixels so magic, fire and gold still read as intentional light sources.
                fixed luminance = dot(color.rgb, fixed3(0.2126, 0.7152, 0.0722));
                fixed warmth = saturate((color.r - color.b) * 1.7);
                fixed3 moonlit = color.rgb * fixed3(0.38, 0.54, 0.86);
                fixed3 warmLight = color.rgb * fixed3(0.92, 0.62, 0.30);
                moonlit = lerp(moonlit, warmLight, warmth * (0.30 + luminance * 0.45));
                color.rgb = moonlit;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
