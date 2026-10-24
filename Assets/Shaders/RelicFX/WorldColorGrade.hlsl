#ifndef RELIC_WORLD_COLOR_GRADE_INCLUDED
#define RELIC_WORLD_COLOR_GRADE_INCLUDED
// Camera-scoped globals. Role 0 and disabled profiles leave the original RGB untouched.
float _RKColorEnabled, _RKColorEffectsInfluence;
float4 _RKColorParameters[4];
float4 _RKColorTints[4];

half3 RKGradeColor(half3 rgb, float group, float influence, float isEffect)
{
    if (_RKColorEnabled < 0.5 || group < 0.5 || influence <= 0.0) return rgb;
    int index = clamp((int)group, 0, 3);
    float3 settings = _RKColorParameters[index].xyz;
    float3 graded = max(0.0, (rgb - 0.18) * settings.y + 0.18) * settings.x;
    float luminance = dot(graded, float3(0.2126, 0.7152, 0.0722));
    graded = max(0.0, lerp(luminance.xxx, graded, settings.z)) * _RKColorTints[index].rgb;
    return lerp(rgb, graded, saturate(influence) * lerp(1.0, _RKColorEffectsInfluence, saturate(isEffect)));
}
#endif
