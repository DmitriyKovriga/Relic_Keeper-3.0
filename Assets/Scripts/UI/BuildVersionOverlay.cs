using UnityEngine;

namespace Scripts.UI
{
    /// <summary>Shows the Player Settings version in the lower-left corner of player builds.</summary>
    public sealed class BuildVersionOverlay : MonoBehaviour
    {
        private GUIStyle _labelStyle;
        private GUIStyle _shadowStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateForPlayerBuild()
        {
            if (Application.isEditor || FindFirstObjectByType<BuildVersionOverlay>() != null)
                return;

            var host = new GameObject(nameof(BuildVersionOverlay));
            DontDestroyOnLoad(host);
            host.AddComponent<BuildVersionOverlay>();
        }

        private void OnGUI()
        {
            EnsureStyles();
            string text = $"Version {Application.version}";
            float y = Screen.height - 42f;
            GUI.Label(new Rect(9f, y + 1f, 260f, 24f), text, _shadowStyle);
            GUI.Label(new Rect(8f, y, 260f, 24f), text, _labelStyle);
        }

        private void EnsureStyles()
        {
            if (_labelStyle != null)
                return;

            int fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 45f), 11, 18);
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f, 0.9f) }
            };
            _shadowStyle = new GUIStyle(_labelStyle)
            {
                normal = { textColor = new Color(0f, 0f, 0f, 0.8f) }
            };
        }
    }
}
