#ifndef RELIC_HIT_FLASH_INCLUDED
#define RELIC_HIT_FLASH_INCLUDED

// Preserve the lit/graded pixel contrast and hue instead of flattening the sprite to white.
// Alpha/coverage, normals, status channels and the material itself stay with their original owners.
half3 RKHitFlash(half3 rgb, half amount, half3 color)
{
    return rgb + (.15h + .85h * saturate(rgb)) * saturate(color) * clamp(amount, 0.0h, .5h);
}

#endif
