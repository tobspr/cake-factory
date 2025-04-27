struct FullScreenVertexInput
{
    #if !SHADER_API_GLES
    uint vertexID : SV_VertexID;
    #else
    float4 position : POSITION;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct FullScreenVertexOutput
{
    float2 uv : TEXCOORD0;
    float4 vertex : SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO
};


float2 VertexToUV(float2 vertex)
{
    float2 texcoord = (vertex + 1.0) * 0.5; // triangle vert to uv
    #if UNITY_UV_STARTS_AT_TOP
    texcoord = texcoord * float2(1.0, -1.0) + float2(0.0, 1.0);
    #endif
    return texcoord;
}


// BiRP do not have these methods, so copy here and namespaced to avoid collision in URP

// Generates a triangle in homogeneous clip space, s.t.
// v0 = (-1, -1, 1), v1 = (3, -1, 1), v2 = (-1, 3, 1).
float2 tai_GetFullScreenTriangleTexCoord(uint vertexID)
{
    #if UNITY_UV_STARTS_AT_TOP
    return float2((vertexID << 1) & 2, 1.0 - (vertexID & 2));
    #else
    return float2((vertexID << 1) & 2, vertexID & 2);
    #endif
}

float4 tai_GetFullScreenTriangleVertexPosition(uint vertexID, float z)
{
    // note: the triangle vertex position coordinates are x2 so the returned UV coordinates are in range -1, 1 on the screen.
    float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
    float4 pos = float4(uv * 2.0 - 1.0, z, 1.0);
    return pos;
}

void GetFullScreenVertexData(FullScreenVertexInput v, out float4 posCS, out float2 screenUV)
{
    #if !SHADER_API_GLES
    posCS = tai_GetFullScreenTriangleVertexPosition(v.vertexID, UNITY_NEAR_CLIP_VALUE);
    screenUV = tai_GetFullScreenTriangleTexCoord(v.vertexID);
    #else
    posCS = v.position;
    screenUV = VertexToUV(v.position.xy);
    #endif
}
