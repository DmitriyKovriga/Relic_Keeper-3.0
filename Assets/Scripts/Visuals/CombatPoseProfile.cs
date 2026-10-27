using UnityEngine;

namespace Scripts.Visuals
{
    [CreateAssetMenu(menuName = "RPG/Visuals/Combat Pose Profile")]
    public sealed class CombatPoseProfile : ScriptableObject
    {
        public const string ResourcePath = "Visuals/CombatPoseProfile";
        public bool EnableBodyFollow = true;
        public bool EnableEnemyRecoil = true;
        [Header("Body follow (visual degrees and scale)")]
        [Range(0f, 6f)] public float WindupDegrees = 2f;
        [Range(0f, 6f)] public float StrikeDegrees = 2.5f;
        [Range(.95f, 1f)] public float WindupHeight = .99f;
        [Range(.95f, 1f)] public float StrikeHeight = .985f;
        [Range(1f, 1.8f)] public float HeavyBodyMultiplier = 1.4f;
        [Header("Enemy recoil (source pixels, scaled seconds)")]
        [Range(0f, 2.5f)] public float RecoilPixels = 1.2f;
        [Range(0f, 4f)] public float RecoilDegrees = 2f;
        [Range(0f, .06f)] public float RecoilCompression = .025f;
        [Range(.05f, .3f)] public float RecoilDuration = .16f;
        [Range(0f, .2f)] public float RecoilRetrigger = .05f;
        [Range(0f, 1f)] public float BossRecoilMultiplier = .35f;

        public static CombatPoseProfile LoadDefault() => Resources.Load<CombatPoseProfile>(ResourcePath);
    }

    public readonly struct AttackBodyPose
    {
        public static readonly AttackBodyPose Neutral = new AttackBodyPose(0f, 1f);
        public readonly float Degrees, Height;
        public AttackBodyPose(float degrees, float height) { Degrees = degrees; Height = height; }
    }

    public static class SpriteFootPose
    {
        public static Vector3 RotationCompensation(float footY, Quaternion rotation)
        {
            var foot = new Vector3(0f, footY, 0f);
            return foot - rotation * foot;
        }
    }
}
