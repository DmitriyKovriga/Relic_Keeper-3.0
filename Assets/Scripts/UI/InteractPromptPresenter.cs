using Scripts.Dungeon;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Scripts.UI
{
    /// <summary>
    /// Pooled world-space "[Key] prompt" under the current interactable.
    /// One host for the lifetime of this component; show/hide via SetActive only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractPromptPresenter : MonoBehaviour
    {
        private const string HostName = "InteractPrompt";
        private const string MeshChildName = "PromptMesh";

        [Tooltip("World-space offset from the bottom of the interactable visual bounds.")]
        [SerializeField] private Vector3 _belowBoundsPadding = new Vector3(0f, 0.05f, 0f);
        [SerializeField, Min(0.1f)] private float _fontSize = 3.5f;
        [SerializeField] private Color _color = new Color(1f, 0.95f, 0.8f, 0.95f);
        [SerializeField] private string _sortingLayer = "VFX";
        [SerializeField] private int _sortingOrder = 1100;

        private Transform _host;
        private TextMeshPro _label;
        private Transform _followTarget;
        private string _lastRendered;
        private bool _visible;

        public void Show(IInteractable interactable)
        {
            if (interactable is not Component component || component == null || !interactable.CanInteract())
            {
                Hide();
                return;
            }

            string prompt = interactable.GetPrompt();
            if (string.IsNullOrWhiteSpace(prompt))
            {
                Hide();
                return;
            }

            if (!EnsureHost())
            {
                Hide();
                return;
            }

            _followTarget = component.transform;

            string key = ResolveInteractKeyLabel();
            string text = string.IsNullOrEmpty(key) ? prompt : $"[{key}] {prompt}";
            if (_label != null && text != _lastRendered)
            {
                _label.text = text;
                _lastRendered = text;
                _label.ForceMeshUpdate();
            }

            ApplyBelowTargetPosition();

            if (!_visible && _host != null)
            {
                _host.gameObject.SetActive(true);
                _visible = true;
            }
        }

        public void Hide()
        {
            _followTarget = null;
            _lastRendered = null;
            if (!_visible)
                return;

            if (_host != null)
                _host.gameObject.SetActive(false);
            _visible = false;
        }

        private void LateUpdate()
        {
            if (!_visible || _host == null || _followTarget == null)
                return;
            ApplyBelowTargetPosition();
        }

        private void OnDisable() => Hide();

        private void OnDestroy()
        {
            if (_host == null)
                return;

            if (Application.isPlaying)
                Destroy(_host.gameObject);
            else
                DestroyImmediate(_host.gameObject);

            _host = null;
            _label = null;
            _visible = false;
        }

        private bool EnsureHost()
        {
            // Unity fake-null: destroyed objects compare equal to null.
            if (_host != null && _label != null)
                return true;

            _host = null;
            _label = null;

            var go = new GameObject(HostName);
            // Must NOT parent to the player: facing flip uses negative scale.x and mirrors TMP.
            go.transform.SetParent(null, true);
            go.transform.position = transform.position;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            _host = go.transform;

            var meshObject = new GameObject(MeshChildName);
            meshObject.transform.SetParent(_host, false);
            meshObject.transform.localPosition = Vector3.zero;
            meshObject.transform.localRotation = Quaternion.identity;
            meshObject.transform.localScale = Vector3.one;

            _label = meshObject.AddComponent<TextMeshPro>();
            _label.raycastTarget = false;
            _label.richText = true;
            // Top of the text sits on the pivot; glyphs grow downward (under the object).
            _label.alignment = TextAlignmentOptions.Top;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.overflowMode = TextOverflowModes.Overflow;
            _label.fontSize = _fontSize;
            _label.color = _color;
            _label.extraPadding = true;

            var font = UIFontResolver.ResolveTMPFontAsset(_label.font);
            if (font != null)
                _label.font = font;

            var rect = _label.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.localPosition = Vector3.zero;
            rect.sizeDelta = new Vector2(10f, 2f);

            var renderer = _label.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (TryGetSortingLayerId(_sortingLayer, out int sortingLayerId))
                    renderer.sortingLayerID = sortingLayerId;
                renderer.sortingOrder = _sortingOrder;
            }

            go.SetActive(false);
            _visible = false;
            _lastRendered = null;
            return _host != null && _label != null;
        }

        private void ApplyBelowTargetPosition()
        {
            if (_host == null || _followTarget == null)
                return;

            Vector3 worldPos;
            if (TryGetVisualBounds(_followTarget, out Bounds bounds))
            {
                worldPos = new Vector3(bounds.center.x, bounds.min.y, _followTarget.position.z);
                worldPos += _belowBoundsPadding;
            }
            else
            {
                // Fallback: clearly under the root (titles sit around +1.2 local).
                worldPos = _followTarget.position + new Vector3(0f, -0.9f, 0f);
            }

            _host.SetPositionAndRotation(worldPos, Quaternion.identity);
            // Counteract any inherited/leftover flip (negative scale mirrors text).
            Vector3 scale = _host.localScale;
            if (scale.x < 0f || scale.y < 0f || scale.z < 0f ||
                !Mathf.Approximately(scale.x, 1f) ||
                !Mathf.Approximately(scale.y, 1f) ||
                !Mathf.Approximately(scale.z, 1f))
            {
                _host.localScale = Vector3.one;
            }
        }

        private static bool TryGetVisualBounds(Transform target, out Bounds bounds)
        {
            bounds = default;
            bool found = false;

            // Prefer colliders on this object only (ignore huge child triggers if any).
            Collider2D[] ownColliders = target.GetComponents<Collider2D>();
            for (int i = 0; i < ownColliders.Length; i++)
            {
                Collider2D col = ownColliders[i];
                if (col == null || !col.enabled)
                    continue;
                if (!found)
                {
                    bounds = col.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(col.bounds);
                }
            }

            if (found)
                return true;

            Collider2D[] childColliders = target.GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < childColliders.Length; i++)
            {
                Collider2D col = childColliders[i];
                if (col == null || !col.enabled)
                    continue;
                if (!found)
                {
                    bounds = col.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(col.bounds);
                }
            }

            if (found)
                return true;

            SpriteRenderer[] sprites = target.GetComponentsInChildren<SpriteRenderer>();
            for (int i = 0; i < sprites.Length; i++)
            {
                SpriteRenderer sprite = sprites[i];
                if (sprite == null || !sprite.enabled || sprite.sprite == null)
                    continue;
                if (!found)
                {
                    bounds = sprite.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(sprite.bounds);
                }
            }

            return found;
        }

        private static string ResolveInteractKeyLabel()
        {
            InputAction action = null;
            try
            {
                GameInput input = InputManager.InputActions;
                if (input != null)
                    action = input.Player.Interact;
            }
            catch
            {
                action = null;
            }

            if (action == null)
                return "E";

            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite || binding.isPartOfComposite)
                    continue;

                string path = binding.effectivePath;
                if (string.IsNullOrEmpty(path))
                    continue;

                if (path.IndexOf("Keyboard", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    path.IndexOf("Gamepad", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                string human = InputControlPath.ToHumanReadableString(
                    path,
                    InputControlPath.HumanReadableStringOptions.OmitDevice);
                if (!string.IsNullOrWhiteSpace(human))
                    return Beautify(human);
            }

            string fallback = action.GetBindingDisplayString(
                0,
                InputBinding.DisplayStringOptions.DontUseShortDisplayNames |
                InputBinding.DisplayStringOptions.IgnoreBindingOverrides);
            if (!string.IsNullOrEmpty(fallback))
            {
                fallback = fallback.Replace("Hold ", string.Empty).Replace("hold ", string.Empty).Trim();
                if (fallback.Length > 0)
                    return Beautify(fallback);
            }

            return "E";
        }

        private static string Beautify(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            value = value.Trim();
            value = value.Replace("Left Shift", "LShift")
                .Replace("Right Shift", "RShift")
                .Replace("Left Ctrl", "LCtrl")
                .Replace("Right Ctrl", "RCtrl")
                .Replace("Left Alt", "LAlt")
                .Replace("Right Alt", "RAlt")
                .Replace("Control", "Ctrl");
            return value;
        }

        private static bool TryGetSortingLayerId(string layerName, out int id)
        {
            id = 0;
            if (string.IsNullOrEmpty(layerName))
                return false;

            foreach (var layer in SortingLayer.layers)
            {
                if (!string.Equals(layer.name, layerName, System.StringComparison.Ordinal))
                    continue;
                id = layer.id;
                return true;
            }

            return false;
        }
    }
}