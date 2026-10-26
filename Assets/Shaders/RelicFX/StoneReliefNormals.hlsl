#ifndef RK_STONE_RELIEF_NORMALS_INCLUDED
#define RK_STONE_RELIEF_NORMALS_INCLUDED
// Preserve the established illumination of flat and unlit artwork. URP's
// normal buffer clears to (0.5,0.5,0.5), which is not a usable surface normal.
// Only tilted normals change the light; ambient color remains independent.
half RKStoneNormalResponseFromDirection(half3 normal, half3 direction)
{
    if (dot(normal, normal) < 0.01) return 1;
    normal = normalize(normal);
    half3 plane = -GetViewForwardDir();
    // Also absorb the small XY error from the 8-bit normals render target.
    if (dot(normal, plane) > 0.99995) return 1;
    return clamp(max(0, dot(direction, normal)) / max(0.001, dot(direction, plane)), 0, 1.6);
}
half RKStoneNormalResponse(float2 screenUV, float3 world, float3 light, float lightHeight)
{
#if USE_NORMAL_MAP
    half3 normal = UnpackNormalRGBNoScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, screenUV));
    half3 plane = -GetViewForwardDir();
    float3 projected = light - (dot(light - world, plane) - lightHeight) * plane;
    half3 direction = normalize(projected - world);
    return RKStoneNormalResponseFromDirection(normal, direction);
#else
    return 1;
#endif
}
#endif
