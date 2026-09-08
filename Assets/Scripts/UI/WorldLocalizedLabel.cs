using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Scripts.UI
{
    /// <summary>
    /// Локализованная подпись в мире. Вешается на дочерний объект WorldLabel:
    /// его Transform — это позиция надписи, код её никогда не перезаписывает.
    /// Сам текст рисуется на вложенном меше, чтобы не превращать WorldLabel в RectTransform.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Relic Keeper/World Localized Label")]
    public class WorldLocalizedLabel : MonoBehaviour
    {
        public const string DefaultChildName = "WorldLabel";
        private const string MeshChildName = "LabelMesh";

        [Header("Text")]
        [SerializeField] private string _localizationTable = "MenuLabels";
        [SerializeField] private string _localizationKey;
        [SerializeField] private string _fallbackText = string.Empty;

        [Header("Appearance")]
        [SerializeField, Min(0.1f)] private float _fontSize = 4f;
        [SerializeField] private Color _color = new Color(0.95f, 0.88f, 0.68f);

        [Header("Sorting")]
        [SerializeField] private string _sortingLayer = "VFX";
        [SerializeField] private int _sortingOrder = 1000;

        private TextMeshPro _text;

        public static WorldLocalizedLabel Create(Transform parent, string localizationKey, string fallbackText, Vector3 localPosition)
        {
            if (parent == null)
                return null;

            var existing = parent.GetComponentInChildren<WorldLocalizedLabel>(true);
            if (existing != null)
            {
                existing.Configure(localizationKey, fallbackText);
                return existing;
            }

            Transform child = parent.Find(DefaultChildName);
            GameObject host;
            if (child != null)
            {
                host = child.gameObject;
            }
            else
            {
                host = new GameObject(DefaultChildName);
                host.transform.SetParent(parent, false);
                host.transform.localPosition = localPosition;
                host.transform.localRotation = Quaternion.identity;
                host.transform.localScale = Vector3.one;
            }

            var label = host.GetComponent<WorldLocalizedLabel>();
            if (label == null)
                label = host.AddComponent<WorldLocalizedLabel>();

            label.Configure(localizationKey, fallbackText);
            return label;
        }

        public void Configure(string localizationKey, string fallbackText)
        {
            _localizationKey = localizationKey;
            _fallbackText = fallbackText;
            EnsureText();
            Refresh();
        }

        public string CurrentText => _text != null ? _text.text : _fallbackText;

        private void OnEnable()
        {
            EnsureText();
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            Refresh();
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }

        private void OnLocaleChanged(Locale locale) => Refresh();

        private void EnsureText()
        {
            DisableLeftoverHostText();

            Transform meshTransform = transform.Find(MeshChildName);
            GameObject meshObject;
            if (meshTransform != null)
            {
                meshObject = meshTransform.gameObject;
            }
            else
            {
                meshObject = new GameObject(MeshChildName);
                meshObject.transform.SetParent(transform, false);
                meshObject.transform.localPosition = Vector3.zero;
                meshObject.transform.localRotation = Quaternion.identity;
                meshObject.transform.localScale = Vector3.one;
                meshObject.hideFlags = HideFlags.DontSave;
            }

            _text = meshObject.GetComponent<TextMeshPro>();
            if (_text == null)
                _text = meshObject.AddComponent<TextMeshPro>();

            _text.raycastTarget = false;
            _text.alignment = TextAlignmentOptions.Bottom;
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            _text.overflowMode = TextOverflowModes.Overflow;
            _text.extraPadding = true;

            var font = UIFontResolver.ResolveTMPFontAsset(_text.font);
            if (font != null)
                _text.font = font;

            var rect = _text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.localPosition = Vector3.zero;
            rect.sizeDelta = new Vector2(8f, 2f);

            ApplyStyle();
        }

        private void DisableLeftoverHostText()
        {
            var leftover = GetComponent<TextMeshPro>();
            if (leftover != null)
                leftover.enabled = false;
        }

        private void ApplyStyle()
        {
            if (_text == null)
                return;

            _text.fontSize = _fontSize;
            _text.color = _color;

            var meshRenderer = _text.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                return;

            if (TryGetSortingLayerId(_sortingLayer, out int sortingLayerId))
                meshRenderer.sortingLayerID = sortingLayerId;
            meshRenderer.sortingOrder = _sortingOrder;
        }

        private static bool TryGetSortingLayerId(string layerName, out int id)
        {
            id = 0;
            if (string.IsNullOrEmpty(layerName))
                return false;

            foreach (var layer in SortingLayer.layers)
            {
                if (!string.Equals(layer.name, layerName, StringComparison.Ordinal))
                    continue;
                id = layer.id;
                return true;
            }

            return false;
        }

        private void Refresh()
        {
            EnsureText();
            if (_text == null)
                return;

            ApplyStyle();
            _text.text = string.IsNullOrEmpty(_fallbackText) ? _localizationKey : _fallbackText;
            _text.ForceMeshUpdate();

            if (string.IsNullOrEmpty(_localizationKey) || string.IsNullOrEmpty(_localizationTable))
                return;

            var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(_localizationTable, _localizationKey);
            if (operation.IsDone)
            {
                ApplyLocalized(operation.Result);
                return;
            }

            operation.Completed += _ => ApplyLocalized(operation.Result);
        }

        private void ApplyLocalized(string value)
        {
            if (_text == null || string.IsNullOrEmpty(value))
                return;
            if (value.IndexOf("translation found", StringComparison.OrdinalIgnoreCase) >= 0)
                return;
            _text.text = value;
            _text.ForceMeshUpdate();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!isActiveAndEnabled)
                return;
            Refresh();
        }

        private void OnDrawGizmos()
        {
            Bounds bounds = GetTextBounds();
            Gizmos.color = _color;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }

        private Bounds GetTextBounds()
        {
            if (_text != null)
            {
                _text.ForceMeshUpdate();
                var meshBounds = _text.textBounds;
                if (meshBounds.size.sqrMagnitude > 0.0001f)
                {
                    Vector3 worldCenter = _text.transform.TransformPoint(meshBounds.center);
                    Vector3 worldSize = Vector3.Scale(meshBounds.size, _text.transform.lossyScale);
                    return new Bounds(worldCenter, worldSize);
                }
            }

            return new Bounds(transform.position + Vector3.up * 0.25f, new Vector3(1.2f, 0.5f, 0.1f));
        }
#endif
    }
}
