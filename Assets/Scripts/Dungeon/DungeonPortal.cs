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
        [Header("Enter Dungeon")]
        [SerializeField] private DungeonDataSO _targetDungeon;

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
