using UnityEngine;

namespace Scripts.Dungeon
{
    /// <summary>
    /// Вызов EnterDungeon для кнопки UI. Привязать к OnClick.
    /// </summary>
    public class DungeonEntryButton : MonoBehaviour
    {
        [SerializeField] private DungeonDataSO _dungeonData;

        public void EnterDungeon()
        {
            if (_dungeonData == null)
            {
                Debug.LogWarning("[DungeonEntryButton] Dungeon Data не назначен.");
                return;
            }
            if (DungeonController.Instance != null)
                DungeonController.Instance.EnterDungeon(_dungeonData);
            else
                Debug.LogError("[DungeonEntryButton] DungeonController не найден в сцене.");
        }

        public void SetDungeonData(DungeonDataSO data) => _dungeonData = data;
    }
}
