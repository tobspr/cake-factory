#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#if UNITY_VERSION < 600000
SAMPLER(sampler_LinearClamp);
#endif
#define SAMPLE_SCREEN_TEX(tex, uv) SAMPLE_TEXTURE2D_X(tex, sampler_LinearClamp, UnityStereoTransformScreenSpaceTex(uv))
