using UnityEngine;

namespace Scripts.Skills.Modules
{
    public class SkillVFX : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private GameObject _vfxPrefab;

        [Header("Timing")]
        [Tooltip("Legacy fallback duration at playback speed 1.0")]
        [SerializeField] private float _baseDuration = 0.5f;

        [Header("Positioning")]
        [SerializeField] private Vector2 _offset;

        [Header("Settings")]
        [SerializeField] private bool _attachToParent = false;
        [SerializeField] private bool _invertFacing = false;

        [Header("Visual Corrections")]
        [Tooltip("Mirror sprite horizontally")]
        [SerializeField] private bool _flipSpriteX = false;
        [Tooltip("Mirror sprite vertically")]
        [SerializeField] private bool _flipSpriteY = false;

        public void Play(
            Transform ownerTransform,
            float facingDirection,
            float scaleMultiplier = 1f,
            float attackSpeed = 1f,
            bool fadeOutEnabled = true,
            float fadeOutStartLifePercent = 0.5f,
            float fadeStartAlphaMultiplier = 0.5f)
        {
            float lifetime = _baseDuration / Mathf.Max(0.0001f, attackSpeed);
            PlayForLifetime(ownerTransform, facingDirection, scaleMultiplier, lifetime, fadeOutEnabled, fadeOutStartLifePercent, fadeStartAlphaMultiplier, out _);
        }

        public GameObject PlayForLifetime(
            Transform ownerTransform,
            float facingDirection,
            float scaleMultiplier,
            float lifetime,
            bool fadeOutEnabled,
            float fadeOutStartLifePercent,
            float fadeStartAlphaMultiplier,
            out Vector3 spawnPos)
        {
            spawnPos = ownerTransform != null
                ? ownerTransform.position + new Vector3(_offset.x * facingDirection, _offset.y, 0f)
                : Vector3.zero;

            if (_vfxPrefab == null)
                return null;

            lifetime = Mathf.Max(0.0001f, lifetime);

            GameObject vfx = Instantiate(_vfxPrefab, spawnPos, Quaternion.identity);

            float finalDir = facingDirection * (_invertFacing ? -1f : 1f);
            Vector3 scale = vfx.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * finalDir * scaleMultiplier;
            scale.y = Mathf.Abs(scale.y) * scaleMultiplier;
            vfx.transform.localScale = scale;

            var sr = vfx.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                if (_flipSpriteX) sr.flipX = !sr.flipX;
                if (_flipSpriteY) sr.flipY = !sr.flipY;
            }

            var anim = vfx.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                float clipDuration = GetAnimatorPlaybackDurationAtSpeedOne(anim, lifetime);
                anim.speed = clipDuration / lifetime;
            }

            if (_attachToParent && ownerTransform != null)
                vfx.transform.SetParent(ownerTransform);

            var autoDestroy = AutoDestroyVFX.Ensure(vfx);
            if (autoDestroy != null)
                autoDestroy.Initialize(lifetime, fadeOutEnabled, fadeOutStartLifePercent, fadeStartAlphaMultiplier);

            return vfx;
        }

        public static float GetAnimatorPlaybackDurationAtSpeedOne(Animator animator, float fallbackDuration)
        {
            fallbackDuration = Mathf.Max(0.0001f, fallbackDuration);

            if (animator == null || animator.runtimeAnimatorController == null)
                return fallbackDuration;

            float maxClipLength = 0f;
            var clips = animator.runtimeAnimatorController.animationClips;
            if (clips != null)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    AnimationClip clip = clips[i];
                    if (clip == null)
                        continue;

                    maxClipLength = Mathf.Max(maxClipLength, clip.length);
                }
            }

            return maxClipLength > 0f ? maxClipLength : fallbackDuration;
        }
    }
}
