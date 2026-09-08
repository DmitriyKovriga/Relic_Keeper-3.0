using UnityEngine;

namespace Scripts.Dungeon
{
    public enum PortalType
    {
        EnterDungeon,
        NextRoom,
        ReturnToHub
    }

    /// <summary>
    /// Портал для перехода в следующую комнату или в город. Активируется по нажатию Interact.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class DungeonPortal : MonoBehaviour, IInteractable
    {
        [Header("Config")]
        [SerializeField] private PortalType _portalType = PortalType.NextRoom;
        [SerializeField] private string _interactPrompt = "Выйти";
        [SerializeField] private bool _isActive = true;
        [Header("Visuals")]
        [SerializeField] private bool _autoFixSpriteOrder = true;
        [SerializeField] private bool _autoFixSpriteLayer = true;
        [SerializeField] private string _preferredSortingLayer = "Foreground";
        [SerializeField] private int _minOrderInLayer = 10;
        [SerializeField] private bool _forceWorldZ = true;
        [SerializeField] private float _targetWorldZ = 0f;
        [SerializeField, Min(0f)] private float _portalAnimationSpeed = 1f;
        [Header("Enter Dungeon")]
        [SerializeField] private DungeonDataSO _targetDungeon;
        [Header("World Label")]
        [Tooltip("Подпись над порталом. Работает только для порталов входа в данж.")]
        [SerializeField] private bool _showWorldLabel = true;
        [Tooltip("Если пусто — берётся ключ из DungeonDataSO")]
        [SerializeField] private string _labelLocalizationKey;
        [SerializeField] private string _labelFallback;
        [Tooltip("Если у портала ещё нет ребёнка WorldLabel, он создастся в этой локальной точке. Дальше двигайте именно WorldLabel.")]
        [SerializeField] private Vector3 _defaultLabelLocalPosition = new Vector3(0f, 1.4f, 0f);

        public PortalType Type => _portalType;
        public DungeonDataSO TargetDungeon => _targetDungeon;
        public bool IsActive
        {
            get => _isActive;
            set => _isActive = value;
        }

        public string GetPrompt() => _interactPrompt;
        public bool CanInteract() => _isActive;

        private void Awake()
        {
            ApplyAnimationSpeed();
            TrySetupWorldLabel();

            if (_forceWorldZ)
            {
                var pos = transform.position;
                if (!Mathf.Approximately(pos.z, _targetWorldZ))
                {
                    transform.position = new Vector3(pos.x, pos.y, _targetWorldZ);
                }
            }

            if (!_autoFixSpriteOrder) return;
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (_autoFixSpriteLayer)
                {
                    int preferredLayerId = SortingLayer.NameToID(_preferredSortingLayer);
                    if (preferredLayerId != 0 || _preferredSortingLayer.Equals("Default"))
                    {
                        sr.sortingLayerID = preferredLayerId;
                    }
                }

                if (sr.sortingOrder < _minOrderInLayer)
                {
                    sr.sortingOrder = _minOrderInLayer;
                }

                // Safety: avoid accidental fully transparent sprite.
                var c = sr.color;
                if (c.a < 1f)
                {
                    c.a = 1f;
                    sr.color = c;
                }
            }
        }

        private void OnValidate()
        {
            ApplyAnimationSpeed();
        }

        private void TrySetupWorldLabel()
        {
            if (!_showWorldLabel || _portalType != PortalType.EnterDungeon)
                return;

            string key = _labelLocalizationKey;
            if (string.IsNullOrEmpty(key) && _targetDungeon != null)
                key = _targetDungeon.NameLocalizationKey;

            string fallback = _labelFallback;
            if (string.IsNullOrEmpty(fallback) && _targetDungeon != null)
                fallback = _targetDungeon.DisplayName;

            if (string.IsNullOrEmpty(key) && string.IsNullOrEmpty(fallback))
                return;

            Scripts.UI.WorldLocalizedLabel.Create(transform, key, fallback, _defaultLabelLocalPosition);
        }

        private void ApplyAnimationSpeed()
        {
            var animator = GetComponent<Animator>();
            if (animator != null)
                animator.speed = Mathf.Max(0f, _portalAnimationSpeed);
        }

        public void Interact()
        {
            if (!_isActive) return;
            DungeonController.OnPortalUsed(this);
        }

        /// <summary>Включить/выключить портал программно (например после смерти босса).</summary>
        public void SetActive(bool active)
        {
            _isActive = active;
            gameObject.SetActive(active); // Портал появляется в мире при активации
        }
    }
}
