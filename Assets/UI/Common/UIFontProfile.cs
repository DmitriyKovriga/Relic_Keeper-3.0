using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "UIFontProfile", menuName = "Relic Keeper/UI/Font Profile")]
public class UIFontProfile : ScriptableObject
{
    [Header("Основные шрифты")]
    public Font uiToolkitFont;
    public TMP_FontAsset tmpFontAsset;

    [Header("Превью")]
    [TextArea(1, 3)]
    public string previewEnglish = "The quick brown fox jumps over 13 lazy dogs.";
    [TextArea(1, 3)]
    public string previewRussian = "Съешь ещё этих мягких французских булок, да выпей чаю.";
}
