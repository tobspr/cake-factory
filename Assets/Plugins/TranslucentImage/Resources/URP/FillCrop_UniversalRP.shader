Shader "Hidden/FillCrop_UniversalRP"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            //HLSLcc is not used by default on gles
            #pragma prefer_hlslcc gles
            //SRP don't support dx9
            #pragma exclude_renderers d3d11_9x

            #pragma vertex vert
            #pragma fragment frag

            #include "./interop_urp.hlsl"
            #include "../Shaders/fullscreen.hlsl"
            #include "../Shaders/common.hlsl"

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(FullScreenVertexInput v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float4 posCS;
                float2 screenUV;
                GetFullScreenVertexData(v, posCS, screenUV);

                o.vertex = half4(posCS.xy, 0.0, 1.0);
                o.uv = screenUV;
                return o;
            }

            TEXTURE2D_X(_MainTex);
            float4 _CropRegion;

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i)
                float2 uv = CropUV(i.uv, _CropRegion);
                return all(uv == saturate(uv)) ? SAMPLE_SCREEN_TEX(_MainTex, uv) : half4(0, 0, 0, 1);
            }
            ENDHLSL
        }
    }
}
