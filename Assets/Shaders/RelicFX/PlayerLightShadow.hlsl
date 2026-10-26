#ifndef RK_PLAYER_LIGHT_SHADOW_INCLUDED
#define RK_PLAYER_LIGHT_SHADOW_INCLUDED
float4 _RKPlayerShadowBody; // stable ellipse center.xy and radii.zw
float4 _RKPlayerShadowSelf; // actual body, independent of shadow shaping
float4 _RKPlayerShadowShape; // strength, reach, softness, crossing fade
float4 _RKPlayerShadowSources[8];
float4 _RKPlayerShadowGroundSources[8]; // length, blocked fraction, direction, root radius
float4 _RKPlayerShadowGroundPlane; // anchor.xy, supporting normal.zw
float4 _RKPlayerShadowGroundSpan; // supported tangent min/max, thickness
int _RKPlayerShadowSourceCount;

float RKPlayerGroundOcclusion(float2 receiver, float4 source)
{
    if (source.x <= 0.001 || source.y <= 0) return 0;
    float2 normal = _RKPlayerShadowGroundPlane.zw;
    float2 tangent = float2(normal.y, -normal.x);
    float2 offset = receiver - _RKPlayerShadowGroundPlane.xy;
    float across = dot(offset, tangent);
    if (across < _RKPlayerShadowGroundSpan.x || across > _RKPlayerShadowGroundSpan.y) return 0;
    float depth = -dot(offset, normal);
    // Only the shallow top of the supporting floor, never open space above it.
    if (depth < 0 || depth > _RKPlayerShadowGroundSpan.z) return 0;
    float along = across * source.z;
    float progress = saturate(along / source.x);
    float thickness = _RKPlayerShadowGroundSpan.z;
    float halfWidth = thickness * lerp(0.22, 0.5, progress);
    float center = thickness * 0.25;
    float penumbra = 1 - smoothstep(halfWidth * 0.3, halfWidth, abs(depth - center));
    float root = smoothstep(-source.w, 0, along);
    float tip = 1 - smoothstep(0, source.x, max(0, along));
    return source.y * root * tip * penumbra;
}

float RKPlayerBodyOcclusion(float2 light, float2 receiver)
{
    float2 a = (light - _RKPlayerShadowBody.xy) / _RKPlayerShadowBody.zw;
    float2 b = (receiver - _RKPlayerShadowBody.xy) / _RKPlayerShadowBody.zw;
    // Preserve the body and avoid an enormous cone when it overlaps the source.
    float2 self = (receiver - _RKPlayerShadowSelf.xy) / _RKPlayerShadowSelf.zw;
    float2 sourceToSelf = (light - _RKPlayerShadowSelf.xy) / _RKPlayerShadowSelf.zw;
    // The old 15% padding also protected platform pixels below the feet.
    // Stop above the soles, and keep this separate from the adjustable caster.
    if (length(sourceToSelf) <= 1.15 || (abs(self.x) <= 1.05 && self.y > -0.95 && self.y <= 1.05)) return 0;
    float2 ray = b - a;
    float t = -dot(a, ray) / max(0.0001, dot(ray, ray));
    if (t <= 0 || t >= 1) return 0;
    float soft = _RKPlayerShadowShape.z;
    float edge = 1 - smoothstep(max(0.05, 1 - soft * 1.2), 1 + max(0.001, soft), length(a + ray * t));
    float reach = _RKPlayerShadowShape.y;
    float fade = 1 - smoothstep(reach * 0.45, reach, distance(receiver, _RKPlayerShadowBody.xy));
    float nearLight = smoothstep(1.15, 2, length(sourceToSelf));
    float core = max(0.06, _RKPlayerShadowSelf.z * 0.6);
    float crossing = smoothstep(core, max(core + 0.05, _RKPlayerShadowShape.w), abs(light.x - _RKPlayerShadowSelf.x));
    return _RKPlayerShadowShape.x * edge * fade * nearLight * crossing;
}

half RKPlayerLightTransmission(float2 light, float2 receiver)
{
    [loop] for (int i = 0; i < _RKPlayerShadowSourceCount; i++)
    {
        if (dot(light - _RKPlayerShadowSources[i].xy, light - _RKPlayerShadowSources[i].xy) >= 0.0001) continue;
        // Union, rather than two multiplied shadows. Each source loses only its
        // own contribution; ambient and an opposing torch remain available.
        return 1 - max(RKPlayerBodyOcclusion(light, receiver), RKPlayerGroundOcclusion(receiver, _RKPlayerShadowGroundSources[i]));
    }
    return 1;
}
#endif
