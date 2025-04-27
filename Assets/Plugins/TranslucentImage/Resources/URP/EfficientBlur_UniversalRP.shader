Shader "Hidden/EfficientBlur_UniversalRP"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend Off

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            //HLSLcc is not used by default on gles
            #pragma prefer_hlslcc gles
            //SRP don't support dx9
            #pragma exclude_renderers d3d11_9x

            #pragma vertex VertBlur
            #pragma fragment FragBlur
            #pragma multi_compile_local BACKGROUND_FILL_NONE BACKGROUND_FILL_COLOR
            // #pragma enable_d3d11_debug_symbols

            #include "interop_urp.hlsl"

            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_MainTex);

            #include "../Shaders/EfficientBlur.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
