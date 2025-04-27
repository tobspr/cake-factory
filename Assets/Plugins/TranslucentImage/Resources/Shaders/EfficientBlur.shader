Shader "Hidden/EfficientBlur"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex VertBlur
            #pragma fragment FragBlur
            #pragma multi_compile_local BACKGROUND_FILL_NONE BACKGROUND_FILL_COLOR
            // #pragma enable_d3d11_debug_symbols

            #include "interop_birp.cginc"

            UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);

            #include "EfficientBlur.hlsl"
            ENDCG
        }
    }

    FallBack Off
}
