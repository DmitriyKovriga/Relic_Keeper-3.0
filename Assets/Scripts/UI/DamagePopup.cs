using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    [SerializeField] private TextMeshPro _textMesh;

    [Header("Animation Settings")]
    [SerializeField] private float _moveSpeedY = 3f;
    [SerializeField] private float _lifeTime = 1f;

    private Color _colPhys = Color.white;
    private Color _colFire = new Color(1f, 0.4f, 0.2f);
    private Color _colCold = new Color(0.2f, 0.8f, 1f);
    private readonly Color _colLight = new Color(1f, 1f, 0.6f);
    private readonly Color _colCrit = new Color(1f, 0.9f, 0.2f);

    private float _timer;
    private Color _textColor;
    private Vector3 _startScale;

    private void OnEnable()
    {
        transform.localScale = Vector3.one;
        _startScale = Vector3.one;
    }

    public void Setup(float damageAmount, bool isCrit, string damageType)
    {
        if (_textMesh == null)
        {
            _textMesh = GetComponent<TextMeshPro>();
            if (_textMesh == null) return;
        }

        var configuredFont = UIFontResolver.ResolveTMPFontAsset(_textMesh.font);
        if (configuredFont != null && _textMesh.font != configuredFont)
            _textMesh.font = configuredFont;

        _textMesh.text = Mathf.RoundToInt(damageAmount).ToString();
        _timer = _lifeTime;
        _textMesh.sortingOrder = 50;

        if (isCrit)
        {
            _textColor = _colCrit;
            _textMesh.fontSize = 8;
            transform.localScale = _startScale * 1.2f;
        }
        else
        {
            _textMesh.fontSize = 5;
            transform.localScale = _startScale;

            switch (damageType)
            {
                case "Fire": _textColor = _colFire; break;
                case "Cold": _textColor = _colCold; break;
                case "Lightning": _textColor = _colLight; break;
                default: _textColor = _colPhys; break;
            }
        }

        _textMesh.color = _textColor;
        _textMesh.alpha = 1f;
    }

    private void Update()
    {
        transform.position += new Vector3(0, _moveSpeedY * Time.deltaTime, 0);

        _timer -= Time.deltaTime;

        if (_timer <= _lifeTime * 0.5f)
        {
            float fadeAlpha = _timer / (_lifeTime * 0.5f);
            _textMesh.alpha = fadeAlpha;
        }

        if (_timer <= 0)
        {
            if (PoolManager.Instance != null)
                PoolManager.Instance.ReturnToPool(gameObject);
            else
                Destroy(gameObject);
        }
    }
}
