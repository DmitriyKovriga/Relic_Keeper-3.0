using UnityEngine;
using UnityEngine.InputSystem;
using System.IO;

public class GameSaveManager : MonoBehaviour
{
    [SerializeField] private PlayerStats _playerStats;
    [SerializeField] private CharacterDatabaseSO _characterDB;
    [SerializeField] private CharacterDataSO _defaultCharacter;

    private string SavePath => Application.persistentDataPath + "/savegame.json";

    private void Start()
    {
        _characterDB.Init(); // Если нужно инициализировать базу
        
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
        if (Keyboard.current.kKey.wasPressedThisFrame) SaveGame(); // F5 - Save
        if (Keyboard.current.lKey.wasPressedThisFrame) LoadGame(); // F9 - Load
        if (Keyboard.current.deleteKey.wasPressedThisFrame) DeleteSave();

        if (Keyboard.current.f12Key.wasPressedThisFrame)
        {
            string path = Application.persistentDataPath;
            // Этот метод работает и на Windows, и на Mac
            Application.OpenURL(path); 
            Debug.Log($"[System] Opening Save Folder: {path}");
        }
    }

    public void SaveGame()
    {
        Debug.Log("[Debug] Пытаюсь сохранить..."); // <--- Добавь это

    if (_playerStats == null) 
    {
        Debug.LogError("[Debug] ОШИБКА: Ты забыл привязать PlayerStats в инспекторе!"); 
        return;
    }


        var data = new GameSaveData
        {
            // 1. Сохраняем ID класса
            CharacterClassID = _playerStats.CurrentClassID,
            
            // 2. Сохраняем состояние
            CurrentHealth = _playerStats.CurrentHealth,
            CurrentMana = _playerStats.CurrentMana,
            
            // 3. Сохраняем прогресс (Уровень и Опыт)
            CurrentLevel = _playerStats.Level,
            CurrentXP = _playerStats.CurrentXP,
            RequiredXP = _playerStats.RequiredXP
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
                // Сначала инициализируем базовые статы класса
                _playerStats.Initialize(characterData);
                
                // Потом накатываем сверху сохраненные данные (Уровень, ХП, Опыт)
                _playerStats.ApplyLoadedState(data);
                
                Debug.Log($"[System] Game Loaded: {characterData.DisplayName}, Lvl {data.CurrentLevel}");
            }
            else 
            {
                Debug.LogWarning($"[System] Class '{data.CharacterClassID}' not found. Starting New Game.");
                StartNewGame();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[System] Load Error: {e.Message}");
            // В случае ошибки сейва лучше начать новую игру, чем крашить
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
        _playerStats.Initialize(_defaultCharacter);
    }
}