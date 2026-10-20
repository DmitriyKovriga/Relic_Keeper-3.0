# Lucky Shot

Primary bow skill, zero cooldown and mana cost. Uses the existing bow draw/release timing and arrow sprite.

Authored defaults: 100% weapon damage, 18 m/s base speed, four-second lifetime. One environment bounce plus one per whole positive Projectile Chain; no enemy-to-enemy chaining. After a solid surface contact, aim at the nearest unhit reachable enemy within 12 m on the open side of the surface. With no eligible target, use physical reflection. Wall targeting excludes blocked, dead, previously hit and owner targets. Triggers do not count as surfaces. After the first bounce, hits deal 75% more damage; the bonus does not stack with further bounces. Fork and pierce retain their usual behavior and hit history. Forked children inherit the remaining wall budget and empowerment.

## PixelLab sources

- Icon: job `57e521b1-ae30-4d51-ab63-8bc774fbd0c6`, gallery `34149a26-c49b-5ead-83d2-2cf7b483e4fa`, 32×32.
- Ricochet spark: [PixelLab effect](https://pixellab.ai/create-vfx?effect=f8543f84-9fe4-41c6-af8f-d5d8507cba39), export `b98b1b33-5a0f-49e0-bf3b-29ae101ed4d0`. Seven 28×26 transparent frames. Original anchor (8, 13) from the top left. Original JSON included.
- Unity plays the spark once at 20 fps, oriented away from the contacted surface. The arrow turns gold when empowered. Both use the existing attack-VFX opacity setting.

Balance and visuals are editable in the SpawnProjectile step of Recipe_LuckyShot.asset. Ordinary projectile skills opt out of environment ricochets by default.
