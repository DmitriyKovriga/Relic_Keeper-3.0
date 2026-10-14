# Ground dash presentation

PlayerMovementVisual smoothly reduces the drawn silhouette to 96% width / 87% height on grounded movement dashes, then recovers. Leaving the floor or entering a skill cast clears the compression. Invulnerable dodges and air movement do not receive it. Scale and response are serialized visual settings; root transform, collider and movement balance are unchanged.

Feet remain anchored to the collider bottom. Weapon grip follows the same compression relative to the authored five-pixel-forward, four-pixel-above-floor baseline, retaining character animation grip offsets during a ground dash. Weapon artwork itself is not scaled.

GroundingVisualController emits a directional eight-frame pixel cloud behind the heel once on dash entry. Duration 800 ms, opacity 95%, width relative to body 1.45. The cloud stays at its origin and drifts visually backward/upward as it fades. The existing scrape remains, and other dodge effects keep their original assets.

Controlled Play Mode capture: WarriorMovementPreview.CaptureGroundDashRuntimeViews, both facing directions with held axe. GIF/contact sheet in Docs/Art/MovementVfx. Verification: compilation and GroundDashVisualTests (7) plus WarriorMovementAnimationTests (14).

Ground dash afterimages now use a light blue-grey silhouette material (source alpha only), a 300 ms fade and draw behind the visible body. Hidden source renderers are excluded to avoid duplicate ghosts. Air dodge afterimages use the same light material and a separate light tint; legacy dark tint fields remain hidden for serialization compatibility. Shape recovery response is 18 per second; dash physics/timing are unchanged.

The afterimage renderer cache explicitly includes PlayerMovementVisual.DisplayRenderer even when MovementSprite is a sibling outside the Visuals/weapon hierarchy. Refresh it at dodge emission startup because the presentation body can be created after PlayerAttackInput.Awake. The hidden original source still does not emit a duplicate, and ready flashes skip hidden renderers too. GroundDashVisualTests now has nine passing cases, including both ground/air ownership regressions that cache before body creation, start emission through the real cache refresh, and assert body plus weapon snapshots with stable world positions. Controlled preview calls renderer discovery rather than injecting the renderer array.

The cloud now uses eight imported 32x32 frames derived through imagegen from the user reference screenshot. Unchanged generated source: Docs/Art/MovementVfx/GroundDashReferenceSource.png. GroundDashVfxBuilder.ImportReferenceCloud packs native frames, removes isolated export speckles, and aligns contact pixels to the floor. Resource path: VFX/Grounds/GroundDashReference-Sheet. Procedural puff remains a fallback only. Both air and ground silhouette renderers explicitly bind their source sprite alpha texture; this prevents rectangular afterimages.

Playback skips source frames 00 and 01, beginning at the fully formed plume (02). The remaining six frames span 800 ms; the eight-frame source sheet remains intact.

Cloud playback speed increased by 25%: 1000 / 1.25 = 800 ms.
