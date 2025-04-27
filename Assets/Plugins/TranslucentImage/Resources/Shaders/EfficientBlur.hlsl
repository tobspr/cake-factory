#include "./common.hlsl"
#include "./fullscreen.hlsl"

float4 _MainTex_TexelSize;
half   _Radius;
float4 _CropRegion;
half3  _BackgroundColor;

#define BlurVertexInput FullScreenVertexInput

struct BlurVertexOutput
{
    float4 position : SV_POSITION;
    float4 texcoord : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};

#if defined(UNITY_SINGLE_PASS_STEREO)
float4 StereoAdjustedTexelSize(float4 texelSize)
{
    texelSize.x = texelSize.x * 2.0; // texelSize.x = 1/w. For a double-wide texture, the true resolution is given by 2/w.
    texelSize.z = texelSize.z * 0.5; // texelSize.z = w. For a double-wide texture, the true size of the eye texture is given by w/2.
    return texelSize;
}
#else
float4 StereoAdjustedTexelSize(float4 texelSize)
{
    return texelSize;
}
#endif

float4 GetGatherCoords(float2 uv, float4 texelSize, half radius)
{
    half4 offset = half2(-0.5h, 0.5h).xxyy; //-x, -y, x, y
    offset *= StereoAdjustedTexelSize(texelSize).xyxy;
    offset *= radius;
    return uv.xyxy + offset;
}

BlurVertexOutput VertBlur(BlurVertexInput v)
{
    BlurVertexOutput o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    float4 posCS;
    float2 screenUV;
    GetFullScreenVertexData(v, posCS, screenUV);

    o.position = half4(posCS.xy, 0.0, 1.0);

    float2 uvUnCropped = UnCropUV(screenUV, _CropRegion);
    o.texcoord = GetGatherCoords(uvUnCropped, _MainTex_TexelSize, _Radius);

    return o;
}


half4 FragBlur(BlurVertexOutput i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half4 o = SAMPLE_SCREEN_TEX(_MainTex, i.texcoord.xw) * (1.0h / 4.0h);
    o += SAMPLE_SCREEN_TEX(_MainTex, i.texcoord.zw) * (1.0h / 4.0h);
    o += SAMPLE_SCREEN_TEX(_MainTex, i.texcoord.xy) * (1.0h / 4.0h);
    o += SAMPLE_SCREEN_TEX(_MainTex, i.texcoord.zy) * (1.0h / 4.0h);

    o += noise(i.texcoord.xw * _MainTex_TexelSize.zw) * 1. / 255.;

    #if BACKGROUND_FILL_COLOR
    o.rgb = lerp(_BackgroundColor, o.rgb, o.a);
    o.a = 1.0h;
    #endif

    return o;
}


// v2f vert(appdata v)
// {
//     v2f o;
//     UNITY_SETUP_INSTANCE_ID(v);
//     UNITY_INITIALIZE_OUTPUT(v2f, o);
//     UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
//     o.vertex = UnityObjectToClipPos(v.vertex);
//     o.uv = v.uv;
//
//     o.viewDir = mul(unity_CameraInvProjection, o.vertex).xyz;
//     #if UNITY_UV_STARTS_AT_TOP
//     o.viewDir.y = -o.viewDir.y;
//     #endif
//     o.viewDir.z = -o.viewDir.z;
//     o.viewDir = mul(unity_CameraToWorld, o.viewDir.xyzz).xyz;
//     return o;
// }
//
// samplerCUBE _EnvTex;
// float4      _EnvTex_HDR;
//
// half4 frag(v2f i) : SV_Target
// {
//     UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
//     half4 col = SAMPLE_SCREEN_TEX(_MainTex, i.uv);
//     half4 envData = texCUBE(_EnvTex, normalize(i.viewDir));
//     half3 env = DecodeHDR(envData, _EnvTex_HDR);
//     col.rgb *= col.a;
//     col.rgb = col.rgb + env * (1 - col.a);
//     col.a = 1;
//     return col;
// }
