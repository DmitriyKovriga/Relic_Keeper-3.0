using UnityEngine;
using UnityEngine.InputSystem;
using System.IO;
using Scripts.Inventory;


public class GameSaveManager : MonoBehaviour
{
    [Header("Core Dependencies")]
    [SerializeField] private PlayerStats _playerStats;
    [SerializeField] private CharacterDatabaseSO _characterDB;
    [SerializeField] private CharacterDataSO _defaultCharacter;

    [Header("Inventory Dependencies")]
    [SerializeField] private ItemDatabaseSO _itemDatabase;

    // Используем Path.Combine для надежности путей на разных ОС
    private string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");

    private System.Collections.IEnumerator Start()
    {
        // 1. Инициализация баз данных
        if (_characterDB != null) _characterDB.Init();
        if (_itemDatabase != null) _itemDatabase.Init();

        // 2. ВАЖНО: Ждем 1 кадр.
        // Это дает время InventoryManager'у и UI проинициализироваться (выполнить свои Awake/Start).
        yield return null; 

        // 3. Теперь загружаем
        if (File.Exists(SavePath))
        {
            LoadGame();
        }
        else
        {
            StartNewGame();
        }
    }

    private void Update()
    {
        // Dev Tools
        if (Keyboard.current == null) return;

        if (Keyboard.current.kKey.wasPressedThisFrame) SaveGame(); // F5/K - Save
        if (Keyboard.current.lKey.wasPressedThisFrame) LoadGame(); // F9/L - Load
        if (Keyboard.current.deleteKey.wasPressedThisFrame) DeleteSave();

        if (Keyboard.current.f12Key.wasPressedThisFrame)
        {
            string path = Application.persistentDataPath;
            Application.OpenURL(path); 
            Debug.Log($"[System] Opening Save Folder: {path}");
        }
    }

    public void SaveGame()
    {
        Debug.Log("[System] Saving Game...");

        if (_playerStats == null) 
        {
            Debug.LogError("[System] Error: PlayerStats ref is missing!"); 
            return;
        }

        // Собираем данные из новых модулей
        var data = new GameSaveData
        {
            // ID класса
            CharacterClassID = _playerStats.CurrentClassID,
            
            // Состояние ресурсов
            CurrentHealth = _playerStats.Health.Current,
            CurrentMana = _playerStats.Mana.Current,
            
            // Прогресс (из LevelingSystem)
            CurrentLevel = _playerStats.Leveling.Level,
            CurrentXP = _playerStats.Leveling.CurrentXP,
            RequiredXP = _playerStats.Leveling.RequiredXP,

            Inventory = InventoryManager.Instance != null ? InventoryManager.Instance.GetSaveData() : new InventorySaveData()
        };

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"[System] Game Saved. Level: {data.CurrentLevel}, XP: {data.CurrentXP}");
    }

    public void LoadGame()
    {
        if (!File.Exists(SavePath)) return;

        try 
        {
            string json = File.ReadAllText(SavePath);
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);

            CharacterDataSO characterData = _characterDB.GetCharacterByID(data.CharacterClassID);
            
            if (characterData != null)
            {
                // 1. Инит статов
                _playerStats.Initialize(characterData);
                _playerStats.ApplyLoadedState(data);

                // 2. Инит инвентаря (AI ADDED)
                if (InventoryManager.Instance != null && _itemDatabase != null)
                {
                    InventoryManager.Instance.LoadState(data.Inventory, _itemDatabase);
                }
                
                Debug.Log($"[System] Game Loaded.");
            }
            else 
            {
                StartNewGame();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[System] Load Error: {e.Message} \n {e.StackTrace}");
            StartNewGame();
        }
    }

    public void DeleteSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log("[System] Save Deleted.");
            StartNewGame();
        }
    }

    private void StartNewGame()
    {
        if (_defaultCharacter != null)
        {
            _playerStats.Initialize(_defaultCharacter);
            Debug.Log("[System] Started New Game (Default Character).");
        }
        else
        {
            Debug.LogError("[System] Default Character Data is missing!");
        }
    }
}