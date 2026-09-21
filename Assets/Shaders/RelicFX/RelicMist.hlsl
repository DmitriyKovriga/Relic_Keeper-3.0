#ifndef RELIC_MIST_INCLUDED
#define RELIC_MIST_INCLUDED
float RelicHash(float2 p)
{
    float3 q = frac(float3(p.x, p.y, p.x) * 0.1031);
    q += dot(q, q.yzx + 33.33);
    return frac((q.x + q.y) * q.z);
}
float RelicNoise(float2 p)
{
    float2 cell = floor(p), t = frac(p);
    t = t * t * (3.0 - 2.0 * t);
    return lerp(lerp(RelicHash(cell), RelicHash(cell + float2(1, 0)), t.x),
        lerp(RelicHash(cell + float2(0, 1)), RelicHash(cell + 1), t.x), t.y);
}
float RelicWisps(float2 world, float2 drift, float scale, float quality)
{
    float2 p = world * scale;
    float broad = RelicNoise(p * float2(0.5, 1.7) + drift);
    if (quality < 0.5) return smoothstep(0.27, 0.76, broad);
    float ribbon = RelicNoise(p * float2(0.85, 3.8) - drift * 0.73 + float2(9, 17) + broad * 0.65);
    float density = broad * 0.6 + ribbon * 0.4;
    if (quality > 1.5) density += (RelicNoise(p * float2(2.0, 6.5) + drift * 1.4) - 0.5) * 0.13;
    return smoothstep(0.3, 0.7, density);
}
#endif
