#ifndef RK_PLAYER_LIGHT_SHADOW_INCLUDED
#define RK_PLAYER_LIGHT_SHADOW_INCLUDED
float4 _RKPlayerShadowBody; // stable ellipse center.xy and radii.zw
float4 _RKPlayerShadowSelf; // actual body, independent of shadow shaping
float4 _RKPlayerShadowShape; // strength, reach, softness, reserved
float4 _RKPlayerShadowSources[8];
int _RKPlayerShadowSourceCount;

half RKPlayerLightTransmission(float2 light, float2 receiver)
{
    if (_RKPlayerShadowSourceCount <= 0) return 1;
    bool selected = false;
    [loop] for (int i = 0; i < _RKPlayerShadowSourceCount; i++)
        if (dot(light - _RKPlayerShadowSources[i].xy, light - _RKPlayerShadowSources[i].xy) < 0.0001) selected = true;
    if (!selected) return 1;
    float2 a = (light - _RKPlayerShadowBody.xy) / _RKPlayerShadowBody.zw;
    float2 b = (receiver - _RKPlayerShadowBody.xy) / _RKPlayerShadowBody.zw;
    // Preserve the body and avoid an enormous cone when it overlaps the source.
    float2 self = (receiver - _RKPlayerShadowSelf.xy) / _RKPlayerShadowSelf.zw;
    float2 sourceToSelf = (light - _RKPlayerShadowSelf.xy) / _RKPlayerShadowSelf.zw;
    // The old 15% padding also protected platform pixels below the feet.
    // Stop above the soles, and keep this separate from the adjustable caster.
    if (length(sourceToSelf) <= 1.15 || (abs(self.x) <= 1.05 && self.y > -0.95 && self.y <= 1.05)) return 1;
    float2 ray = b - a;
    float t = -dot(a, ray) / max(0.0001, dot(ray, ray));
    if (t <= 0 || t >= 1) return 1;
    float soft = _RKPlayerShadowShape.z;
    float edge = 1 - smoothstep(max(0.05, 1 - soft * 1.2), 1 + max(0.001, soft), length(a + ray * t));
    float reach = _RKPlayerShadowShape.y;
    float fade = 1 - smoothstep(reach * 0.45, reach, distance(receiver, _RKPlayerShadowBody.xy));
    float nearLight = smoothstep(1.15, 2, length(sourceToSelf));
    return 1 - _RKPlayerShadowShape.x * edge * fade * nearLight;
}
#endif
