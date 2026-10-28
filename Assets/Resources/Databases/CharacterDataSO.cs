using UnityEngine;
using System.Collections.Generic;
using Scripts.Stats;
using Scripts.Skills.PassiveTree;
using System;

[CreateAssetMenu(menuName = "RPG/Character Data")]
public class CharacterDataSO : ScriptableObject
{
    [field: SerializeField] public string ID { get; private set; }
    
    [Header("Localization")]
    [Tooltip("Ключ в MenuLabels для имени. Например: character.warrior.name")]
    [SerializeField] private string _nameKey;
    [Tooltip("Ключ в MenuLabels для описания. Например: character.warrior.description")]
    [SerializeField] private string _descriptionKey;
    
    [Header("Fallback (если локализация не задана)")]
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("<DisplayName>k__BackingField")] private string _displayNameFallback;
    [TextArea, UnityEngine.Serialization.FormerlySerializedAs("Description")] [SerializeField] private string _descriptionFallback;

    [Header("Visual")]
    [Tooltip("Портрет/спрайт персонажа для UI найма и хостела")]
    [SerializeField] private Sprite _portrait;

    [Header("Movement Animations")]
    [Tooltip("Looped movement animation. Frames can be assigned directly, or loaded from Resources path.")]
    [SerializeField] private CharacterSpriteAnimation _runAnimation = new CharacterSpriteAnimation();
    [Tooltip("Played once when jump starts.")]
    [SerializeField] private CharacterSpriteAnimation _jumpAnimation = new CharacterSpriteAnimation { Loop = false };
    [Tooltip("Played when vertical velocity changes from upward to downward, then held/looped while falling.")]
    [SerializeField] private CharacterSpriteAnimation _fallAnimation = new CharacterSpriteAnimation { Loop = false };

    [SerializeField] private CharacterSpriteAnimation _landingAnimation = new CharacterSpriteAnimation { Loop = false };
    [SerializeField] private CharacterSpriteAnimation _neutralDodgeAnimation = new CharacterSpriteAnimation { Loop = false };
    [SerializeField] private CharacterSpriteAnimation _spinAnimation = new CharacterSpriteAnimation { Loop = false };

    [Header("Starting Stats Configuration")]
    [Tooltip("Добавь сюда только те статы, которые отличаются от стандартных.")]
    [SerializeField] private List<StatConfig> _startingStats;

    [Header("Passive Tree")]
    [Tooltip("Дерево пассивных навыков этого персонажа")]
    [SerializeField] private PassiveSkillTreeSO _passiveTree;

    /// <summary>Ключ локализации имени. Генерируется из ID: character.{id}.name</summary>
    public string NameKey => !string.IsNullOrEmpty(_nameKey) ? _nameKey : $"character.{ID ?? name}.name";
    /// <summary>Ключ локализации описания. Генерируется из ID: character.{id}.description</summary>
    public string DescriptionKey => !string.IsNullOrEmpty(_descriptionKey) ? _descriptionKey : $"character.{ID ?? name}.description";
    /// <summary>Имя для отображения (fallback, когда локализация не используется).</summary>
    public string DisplayName => !string.IsNullOrEmpty(_displayNameFallback) ? _displayNameFallback : (ID ?? "Unknown");
    /// <summary>Описание для отображения (fallback, когда локализация не загружена).</summary>
    public string DescriptionFallback => _descriptionFallback ?? "";
    public Sprite Portrait => _portrait;
    public CharacterSpriteAnimation RunAnimation => _runAnimation;
    public CharacterSpriteAnimation JumpAnimation => _jumpAnimation;
    public CharacterSpriteAnimation FallAnimation => _fallAnimation;
    public CharacterSpriteAnimation LandingAnimation => _landingAnimation;
    public CharacterSpriteAnimation NeutralDodgeAnimation => _neutralDodgeAnimation;
    public CharacterSpriteAnimation SpinAnimation => _spinAnimation;
    public PassiveSkillTreeSO PassiveTree => _passiveTree;

    public List<StatConfig> StartingStats => _startingStats ??= new List<StatConfig>();

    [System.Serializable]
    public struct StatConfig
    {
        public StatType Type;
        public float Value;
    }

    [Serializable]
    public class CharacterSpriteAnimation
    {
        [Tooltip("Optional zero-based planted-foot frames. Surface dust still requires travel and its usual interval.")]
        public int[] FootContactFrames = Array.Empty<int>();
        [Tooltip("Optional direct frames. If empty, Sprite Sheet Resources Path is used.")]
        public Sprite[] Frames;
        [Tooltip("Path inside Resources without extension. Example: Heroes/WarriorResources/WarriorWalk-Sheet")]
        public string SpriteSheetResourcesPath;
        [Min(0.01f)] public float FrameDuration = 0.08f;
        public bool Loop = true;
        [Tooltip("Jump pose to restart from when another jump begins in the air. Zero preserves the ground-start sequence.")]
        [Min(0)] public int AirborneRestartFrame;
        [Tooltip("Optional vertical weapon presentation offsets per animation frame, in world units. Used while running.")]
        public float[] WeaponVerticalOffsets;
        [Tooltip("Optional horizontal weapon presentation offsets matching authored grip poses.")]
        public float[] WeaponHorizontalOffsets;
        [Tooltip("Presentation width while this animation plays. Does not resize colliders or weapon sockets.")]
        [Range(0.8f, 1.2f)] public float HorizontalScale = 1f;
        [Tooltip("Optional per-frame width/height poses, interpolated with feet anchored. Empty preserves legacy presentation.")]
        public Vector2[] PresentationScales;

        public bool HasAnySource =>
            (Frames != null && Frames.Length > 0) || !string.IsNullOrWhiteSpace(SpriteSheetResourcesPath);
    }

#if UNITY_EDITOR
    public void SetNameKey(string value) => _nameKey = value;
    public void SetDescriptionKey(string value) => _descriptionKey = value;
    public void SetDisplayNameFallback(string value) => _displayNameFallback = value;
    public void SetDescriptionFallback(string value) => _descriptionFallback = value;
    public void SetPortrait(Sprite value) => _portrait = value;
    public void SetPassiveTree(PassiveSkillTreeSO value) => _passiveTree = value;
    public void SetMovementAnimationResourcePaths(string runPath, string jumpPath, string fallPath)
    {
        _runAnimation ??= new CharacterSpriteAnimation();
        _jumpAnimation ??= new CharacterSpriteAnimation { Loop = false };
        _fallAnimation ??= new CharacterSpriteAnimation();

        _runAnimation.SpriteSheetResourcesPath = runPath;
        _runAnimation.Loop = true;
        _jumpAnimation.SpriteSheetResourcesPath = jumpPath;
        _jumpAnimation.Loop = false;
        _fallAnimation.SpriteSheetResourcesPath = fallPath;
        _fallAnimation.Loop = false;
    }
#endif
}
