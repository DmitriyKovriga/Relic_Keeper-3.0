# Shared spin presentation

`SpinAround` is a duration step used by Sweep and Conning Tower. One normalized step percentage selects the character pose, weapon orbit and trail frame. `Acceleration` (default 0.65, range 0–1) eases the angular phase: slow at entry/exit and fast through the middle. Setting it to zero restores constant angular speed. Attack Speed changes the step's duration, so all three stay synchronized without independent animation clocks. `Reverse` reverses that shared phase; `Turns` requests complete rotations. Radius, vertical centre and normal Area of Effect scaling determine the orbit. `PlaneDepth` defaults to 0.16: the user-requested thin, horizontal side-view sweep, rather than the previous 0.5-depth oval. The weapon path and PixelLab trail share this projection; characters stay upright with their feet anchored. The presentation radius is 1.2 metres (1.4 for the finisher), centered 0.15 metres below the body pivot at hip/weapon height, keeping the handle near the hands; the broader combat hitboxes retain their existing radii of 2.2 and 2.6 metres.

Warrior, Rogue and Mage each have nine PixelLab frames, including side and back poses, using their existing base sprites as references. Imports use Point filtering, 24 PPU and foot registration to the first frame. The body collider and facing transform do not rotate. The actual equipped weapon follows an elliptical orbit, respects its painted sprite tilt, and sorts behind the body on the far half. A temporary grip transform projects shaft length before applying the artwork tilt, preserving blade width rather than shrinking the entire sprite. Grip reach is limited to 0.1–0.35 metres, and shaft length fits the remaining radius so the tip stays on the trail without detaching the handle from the body. The opening and closing 14% of each turn blend from windup into the orbit and back to the resting grip. The original weapon parent, sibling order, pose, scale, renderer flips and depth sorting are restored when the step ends or the skill is cancelled.

Sweep and Conning Tower's first two turns each take 0.256 seconds at Attack Speed 1. Conning Tower's larger final turn takes 0.384 seconds. Sweep retains its on-hit cooldown reduction; Conning Tower retains its 100% / 100% / 350% damage and cancellation behavior.

The revised effects are short ivory/steel blade smears with dark trailing edges. The final strike uses a thicker, larger cut. They use the same percentage as the equipped weapon, with no bloom or full luminous ring. Transparent effect sheets are intentional; the skill icon retains its opaque background.

## PixelLab sources

| Asset | Source |
|---|---|
| Warrior spin | job `c791cc02-5d27-4ad3-a452-a578f6dc7d0e`, gallery `617e0ed3-a818-5d4c-8849-12cc3519ea33` |
| Rogue spin | job `134dfad6-0c92-4527-9eab-4b9f8ffc7021`, gallery `c16c81d3-d7fd-5573-bb12-11837a0a08c8` |
| Mage spin | job `e55452dd-b9bf-4342-b4b2-80538fb8ae3c`, gallery `f60de3c1-7c92-5aa4-8dc4-51712b5d8898` |
| Steel trail | effect `d1e80429-f911-4e0f-b761-770268cd31dc`, step `2adcb11b-ae4a-4cef-b70a-b771aceb436b`, export `7aa3ed7c-378f-4f15-a823-352b256522c2` |
| Heavy trail | effect `118617f5-6cba-43cf-a9f4-3e61f5569426`, step `e3d9e95b-75fb-4d8e-8085-f78e190a3285`, export `27ed0d90-f40a-4d14-842b-c9c830ddd383` |

Original character frame downloads are retained in `Docs/Art/Spin`. `SpinAroundBuilder.Apply` imports all three character sequences, the step and effects, and updates Sweep. `ConningTowerBuilder.Apply` updates the combo recipe and EN/RU labels using those shared effects. Step labels and help text include English and Russian.

## Verification

44 focused EditMode tests pass across SpinAroundTests, ConningTowerTests, NeutralDodgeAnimationTests, SkillAoeScaleTests and SkillDpsPreviewTests. They cover all three character sequences, normalized sampling, body-state ownership, tilted weapon alignment in both facing directions, pose restoration on cancellation, shared timing, radial targeting and combo damage.

Editor camera previews sample all nine poses for all three characters with the real Axe of Settler and imported blade trails. These verify presentation; the automated recipe tests verify combat and cancellation. Preview objects and source helpers are temporary and do not alter a saved scene.
