Shader "UI/TranslucentImage"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}

        _Vibrancy("Vibrancy", Range(-1, 2)) = 1.25
        _Brightness("Brightness", Range(-1, 1)) = 0
        _Flatten("Flatten", Range(0, 1)) = 0.1

        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255

        _ColorMask("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"= "Transparent"
            "IgnoreProjector"= "True"
            "RenderType"= "Transparent"
            "PreviewType"= "Plane"
            "CanUseSpriteAtlas"= "True"
        }

        Stencil
        {
            Ref[_Stencil]
            Comp[_StencilComp]
            Pass[_StencilOp]
            ReadMask[_StencilReadMask]
            WriteMask[_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest[unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask[_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // #pragma enable_d3d11_debug_symbols

            #include "UnityUI.cginc"
            // UI shaders still use birp texture convention
            #include "interop_birp.cginc"
            #include "common.hlsl"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata
            {
                float4 vertex : POSITION;
                half4  color : COLOR;
                float2 texcoord : TEXCOORD0;
                half2  extraData : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                half4  color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                half2  extraData : TEXCOORD3;
                float4 mask : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4    _TextureSampleAdd;
            float4    _ClipRect;
            float4    _MainTex_ST;
            float     _UIMaskSoftnessX;
            float     _UIMaskSoftnessY;
            int       _UIVertexColorAlwaysGammaSpace;
            UNITY_DECLARE_SCREENSPACE_TEXTURE(_BlurTex);
            uniform half _Vibrancy;
            uniform half _Flatten;
            uniform half _Brightness;
            float4       _CropRegion; //xMin, yMin, xMax, yMax

            v2f vert(appdata IN)
            {
                v2f OUT;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                {
                    IN.color.rgb = UIGammaToLinearShim(IN.color.rgb);
                }
                OUT.color = IN.color;

                OUT.texcoord = IN.texcoord;
                OUT.screenPos = ComputeNonStereoScreenPos(OUT.vertex);
                #if UNITY_VERSION >= 202120 && UNITY_UV_STARTS_AT_TOP
                if(_ProjectionParams.x > 0 && unity_MatrixVP[1][1] < 0)
                    OUT.screenPos.y = OUT.screenPos.w - OUT.screenPos.y;
                #endif
                OUT.extraData = IN.extraData;

                float2 pixelSize = OUT.vertex.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                OUT.mask = float4(IN.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                //Round up the alpha color coming from the interpolator (to 1.0/256.0 steps)
                //The incoming alpha could have numerical instability, which makes it very sensible to
                //HDR color transparency blend, when it blends with the world's texture.
                const half alphaPrecision = half(0xff);
                const half invAlphaPrecision = half(1.0 / alphaPrecision);
                IN.color.a = round(IN.color.a * alphaPrecision) * invAlphaPrecision;

                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                //Overlay
                half4 foregroundColor = tex2D(_MainTex, IN.texcoord.xy) + _TextureSampleAdd;

                half2 screenPos = IN.screenPos.xy / IN.screenPos.w;
                half2 blurTexcoord = CropUV(screenPos, _CropRegion);

                half3 backgroundColor = SAMPLE_SCREEN_TEX(_BlurTex, blurTexcoord).rgb;

                //saturate help keep color in range
                //Exclusion blend
                backgroundColor = saturate(backgroundColor + (.5 - backgroundColor) * _Flatten);

                //Vibrancy
                backgroundColor = saturate(lerp(LinearRgbToLuminance(backgroundColor), backgroundColor, _Vibrancy));

                //Brightness
                backgroundColor = saturate(backgroundColor + _Brightness);


                //Alpha blend with backgroundColor
                half4 color = half4(
                    lerp(backgroundColor, foregroundColor.rgb, IN.extraData[0]),
                    foregroundColor.a
                );
                color *= IN.color;


                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                color.a *= m.x * m.y;
                #endif


                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                color.rgb += noise(screenPos.xy * _ScreenParams.xy) * 1. / 255.;
                color.rgb *= color.a;

                return color;
            }
            ENDCG
        }
    }

    CustomEditor "LeTai.Asset.TranslucentImage.Editor.TranslucentImageShaderGUI"
}
