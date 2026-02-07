using UnityEngine;
using UnityEngine.InputSystem;
using System.IO;
using Scripts.Inventory;
using Scripts.Saving;
using Scripts.Skills.PassiveTree;

public class GameSaveManager : MonoBehaviour
{
    public const int CurrentSaveVersion = 1;

    [Header("Core Dependencies")]
    [SerializeField] private PlayerStats _playerStats;
    [SerializeField] private CharacterDatabaseSO _characterDB;
    [SerializeField] private CharacterDataSO _defaultCharacter;

    [Header("Inventory Dependencies")]
    [SerializeField] private ItemDatabaseSO _itemDatabase;

    // AI ADDED: Ссылка на менеджер дерева (найдем автоматически или назначь вручную)
    private PassiveTreeManager _passiveTreeManager;

    private string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");

    private System.Collections.IEnumerator Start()
    {
        // Ищем менеджер дерева на игроке
        if (_playerStats != null)
        {
            _passiveTreeManager = _playerStats.GetComponent<PassiveTreeManager>();
        }

        // 1. Инициализация баз данных
        if (_characterDB != null) _characterDB.Init();
        if (_itemDatabase != null) _itemDatabase.Init();

        // 2. Ждем 1 кадр
        yield return null; 

        // 3. Загружаем
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
        if (Keyboard.current == null) return;

        if (Keyboard.current.kKey.wasPressedThisFrame) SaveGame(); 
        if (Keyboard.current.lKey.wasPressedThisFrame) LoadGame(); 
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

        if (_playerStats == null) return;

        var data = new GameSaveData
        {
            SaveVersion = CurrentSaveVersion,
            CharacterClassID = _playerStats.CurrentClassID,
            CurrentHealth = _playerStats.Health.Current,
            CurrentMana = _playerStats.Mana.Current,
            
            CurrentLevel = _playerStats.Leveling.Level,
            CurrentXP = _playerStats.Leveling.CurrentXP,
            RequiredXP = _playerStats.Leveling.RequiredXP,
            
            // AI ADDED: Сохраняем очки навыков
            SkillPoints = _playerStats.Leveling.SkillPoints,

            Inventory = InventoryManager.Instance != null ? InventoryManager.Instance.GetSaveData() : new InventorySaveData(),
            Stash = StashManager.Instance != null ? StashManager.Instance.GetSaveData() : new StashSaveData(),

            AllocatedPassiveNodes = _passiveTreeManager != null ? _passiveTreeManager.GetSaveData() : new System.Collections.Generic.List<string>()
        };

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"[System] Game Saved. Level: {data.CurrentLevel}, SP: {data.SkillPoints}");
    }

    public void LoadGame()
    {
        if (!File.Exists(SavePath)) return;

        try 
        {
            string json = File.ReadAllText(SavePath);
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);

            if (data.SaveVersion < CurrentSaveVersion)
                MigrateSaveData(data);

            CharacterDataSO characterData = _characterDB.GetCharacterByID(data.CharacterClassID);
            
            if (characterData != null)
            {
                // 1. Инит статов и левелинга (включая SkillPoints)
                _playerStats.Initialize(characterData);
                _playerStats.ApplyLoadedState(data);

                // 2. Инит инвентаря
                if (InventoryManager.Instance != null && _itemDatabase != null)
                {
                    InventoryManager.Instance.LoadState(data.Inventory ?? new InventorySaveData(), _itemDatabase);
                }

                // 3. Склад
                if (StashManager.Instance != null && _itemDatabase != null)
                {
                    StashManager.Instance.LoadState(data.Stash ?? new StashSaveData(), _itemDatabase);
                }

                if (_passiveTreeManager != null && data.AllocatedPassiveNodes != null)
                {
                    _passiveTreeManager.LoadState(data.AllocatedPassiveNodes);
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

    private void MigrateSaveData(GameSaveData data)
    {
        if (data.SaveVersion >= CurrentSaveVersion) return;
        if (data.SaveVersion == 0)
        {
            data.SaveVersion = 1;
            Debug.Log("[System] Save migrated: 0 -> 1 (SaveVersion added).");
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