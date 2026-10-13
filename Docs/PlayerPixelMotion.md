# Player and held-weapon movement stability

The gameplay camera previously used art-pixel snapping. The player's displayed body and weapon sockets have different fractional offsets, so independently rounding each SpriteRenderer can change their relative screen positions during movement. Rigidbody interpolation combined with camera Smart Update also permits the camera to sample a different update phase from the displayed player.

HubScene's gameplay camera now keeps Pixel Perfect Camera's 24 PPU, 480 x 270 reference and point filtering, but uses Grid Snapping None. Rendering stays at the output resolution instead of forcing movement into whole art-pixel increments. Cinemachine follow and blend evaluation both run in LateUpdate, matching the interpolated player transform. Main-menu and UI settings are unchanged.

PlayerMovementVisual automatically installs PlayerRenderAnchor and binds the MovementSprite body plus the Visuals subtree containing held weapons, hand pivots and nocked arrows. At camera render time, they receive one shared pixel correction based on the logical player root and the camera's output pixel spacing. End-camera rendering restores their exact local positions. Physics, colliders, skill sockets and projectile launch origins therefore retain their ordinary positions during gameplay updates. Disable also restores an interrupted render. Cameras without an enabled Pixel Perfect Camera do not apply the correction.

Tools > Visuals > Apply Smooth Pixel Camera Motion applies the camera settings to the open HubScene and saves it. It preserves any other scene edits.

Validation:

- PlayerRenderAnchorTests: five cases covering shared body/weapon correction, facing, unchanged root/collider, exact restoration, repeated rendering and non-pixel-camera handling.
- PlayerMovementTests: all 36 cases passed.
- PlayerMotionRenderCheck.CaptureMovementComparison: GPU-rendered horizontal and vertical fractional movement at 960 x 540. Measured body-to-weapon position varied by two screen pixels on both axes before the change and zero after it. Frames and a comparison GIF are under `%TEMP%/codex-player-motion`.
- PlayerMotionRenderCheck.CaptureCurrentPlayer: captured the actual gameplay camera and player in Play Mode and verified that body, held-weapon subtree, logical root and collider positions were restored exactly. No Unity console errors were reported.

At native 480 x 270, point-filtered art still moves in screen-pixel steps. The correction removes relative wobble; it does not remove intentional sprite-animation changes or turn native pixel art into continuous subpixel imagery.
