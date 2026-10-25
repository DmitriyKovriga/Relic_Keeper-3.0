#ifndef RELIC_LOCAL_LIGHT_INCLUDED
#define RELIC_LOCAL_LIGHT_INCLUDED
// URP Renderer2D's unmasked additive blend style (slot 1). Keep distant art,
// overlay UI and combat effects independent of this local surface illumination.
#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightVariables.hlsl"
#if USE_SHAPE_LIGHT_TYPE_1 && !defined(COMBINED_SHAPE_LIGHT_PASS)
TEXTURE2D(_ShapeLightTexture1);
SAMPLER(sampler_ShapeLightTexture1);
half2 _ShapeLightBlendFactors1;
#endif
half3 RKLocalIrradiance(float2 lightingUV)
{
#if USE_SHAPE_LIGHT_TYPE_1
    return SAMPLE_TEXTURE2D(_ShapeLightTexture1, sampler_ShapeLightTexture1, lightingUV).rgb
        * _ShapeLightBlendFactors1.y * _HDREmulationScale;
#else
    return 0;
#endif
}
half RKReceivesLocalLight(float group, float effect)
{
    return step(0.5, group) * (1.0 - step(2.5, group)) * (1.0 - step(0.5, effect));
}
half3 RKApplyLocalLight(half3 color, float2 uv, float group, float effect)
{
    // Preserve the sprite's dark outlines and material colors instead of
    // laying the same opaque-looking amber wash over every surface texel.
    return color * (1.0 + 2.0 * RKLocalIrradiance(uv) * RKReceivesLocalLight(group, effect));
}
#endif
