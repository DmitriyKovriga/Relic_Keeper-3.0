using Scripts.Dungeon;
using Scripts.UI;
using UnityEngine;

namespace Scripts.Hub
{
    public enum HubService
    {
        Tavern,
        Stash
    }

    /// <summary>
    /// NPC в хабе, который по Interact открывает трактир или склад. Сам создаёт триггер
    /// взаимодействия и локализованную подпись над головой, так что достаточно повесить
    /// компонент на объект NPC и выбрать сервис.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Relic Keeper/Hub Service NPC")]
    public class HubServiceNpc : MonoBehaviour, IInteractable
    {
        public const string TavernLabelKey = "hub.label.tavern";
        public const string StashLabelKey = "hub.label.stash";

        [Header("Service")]
        [SerializeField] private HubService _service = HubService.Tavern;

        [Header("Interaction Area")]
        [Tooltip("Создать триггерный коллайдер, если на объекте его ещё нет")]
        [SerializeField] private bool _createTriggerCollider = true;
        [SerializeField] private Vector2 _triggerSize = new Vector2(2.5f, 3f);
        [SerializeField] private Vector2 _triggerOffset = new Vector2(0f, 1f);

        [Header("Label")]
        [SerializeField] private bool _showLabel = true;
        [Tooltip("Если у NPC ещё нет ребёнка WorldLabel, он создастся над головой. Дальше двигайте именно WorldLabel.")]
        [SerializeField] private Vector3 _defaultLabelLocalPosition = new Vector3(0f, 1.2f, 0f);

        [Header("Optional Explicit References")]
        [Tooltip("Если пусто — окно будет найдено в сцене автоматически")]
        [SerializeField] private TavernUI _tavernUI;
        [SerializeField] private StashPanelToggle _stashPanel;

        private WorldLocalizedLabel _label;

        private void Awake()
        {
            EnsureTriggerCollider();
            EnsureLabel();
        }

        public string GetPrompt()
        {
            if (_label != null && !string.IsNullOrEmpty(_label.CurrentText))
                return _label.CurrentText;
            return _service == HubService.Tavern ? "Tavern" : "Stash";
        }

        public bool CanInteract()
        {
            return _service == HubService.Tavern
                ? ResolveTavern() != null
                : ResolveStash() != null;
        }

        public void Interact()
        {
            switch (_service)
            {
                case HubService.Tavern:
                    OpenTavern();
                    break;
                case HubService.Stash:
                    OpenStash();
                    break;
            }
        }

        private void OpenTavern()
        {
            var tavern = ResolveTavern();
            if (tavern == null)
            {
                Debug.LogWarning($"[HubServiceNpc] '{name}': TavernUI was not found in the scene.");
                return;
            }

            tavern.Open(forNewGame: false);
        }

        private void OpenStash()
        {
            var stash = ResolveStash();
            if (stash == null)
            {
                Debug.LogWarning($"[HubServiceNpc] '{name}': StashPanelToggle was not found in the scene.");
                return;
            }

            stash.OpenStash();
        }

        private TavernUI ResolveTavern()
        {
            if (_service != HubService.Tavern)
                return null;
            if (_tavernUI == null)
                _tavernUI = FindFirstObjectByType<TavernUI>(FindObjectsInactive.Include);
            return _tavernUI;
        }

        private StashPanelToggle ResolveStash()
        {
            if (_service != HubService.Stash)
                return null;
            if (_stashPanel == null)
                _stashPanel = FindFirstObjectByType<StashPanelToggle>(FindObjectsInactive.Include);
            return _stashPanel;
        }

        private void EnsureTriggerCollider()
        {
            if (!_createTriggerCollider || GetComponent<Collider2D>() != null)
                return;

            var box = gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = _triggerSize;
            box.offset = _triggerOffset;
        }

        private void EnsureLabel()
        {
            if (!_showLabel)
                return;

            string key = _service == HubService.Tavern ? TavernLabelKey : StashLabelKey;
            string fallback = _service == HubService.Tavern ? "Tavern" : "Stash";
            _label = WorldLocalizedLabel.Create(transform, key, fallback, _defaultLabelLocalPosition);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.35f);
            Gizmos.DrawWireCube(transform.position + (Vector3)_triggerOffset, _triggerSize);
        }
#endif
    }
}
