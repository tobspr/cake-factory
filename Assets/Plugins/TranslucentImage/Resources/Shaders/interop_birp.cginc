#include <UnityCG.cginc>

// This need to be a macro since the type of tex can change :(
#define SAMPLE_SCREEN_TEX(tex, uv) UNITY_SAMPLE_SCREENSPACE_TEXTURE(tex, UnityStereoTransformScreenSpaceTex(uv))

// This piecewise approximation has a precision better than 0.5 / 255 in gamma space over the [0..255] range
// i.e. abs(l2g_exact(g2l_approx(value)) - value) < 0.5 / 255
// It is much more precise than GammaToLinearSpace but remains relatively cheap
half3 UIGammaToLinearShim(half3 value)
{
    half3 low = 0.0849710 * value - 0.000163029;
    half3 high = value * (value * (value * 0.265885 + 0.736584) - 0.00980184) + 0.00319697;

    // We should be 0.5 away from any actual gamma value stored in an 8 bit channel
    const half3 split = (half3)0.0725490; // Equals 18.5 / 255
    return (value < split) ? low : high;
}
