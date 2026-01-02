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
        _characterDB.Init();
        
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
        // Управление системой сохранений (Dev Tools)
        if (Keyboard.current.kKey.wasPressedThisFrame) SaveGame();
        if (Keyboard.current.lKey.wasPressedThisFrame) LoadGame();
        if (Keyboard.current.deleteKey.wasPressedThisFrame) DeleteSave();
    }

    public void SaveGame()
    {
        if (_playerStats == null) return;

        var data = new GameSaveData
        {
            CharacterClassID = _playerStats.CurrentClassID,
            CurrentHealth = _playerStats.CurrentHealth
        };

        File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
        Debug.Log($"[System] Game Saved");
    }

    public void LoadGame()
    {
        if (!File.Exists(SavePath)) return;

        try 
        {
            var json = File.ReadAllText(SavePath);
            var data = JsonUtility.FromJson<GameSaveData>(json);

            var characterData = _characterDB.GetCharacterByID(data.CharacterClassID);
            
            if (characterData != null)
            {
                _playerStats.Initialize(characterData);
                _playerStats.ApplyLoadedState(data.CurrentHealth);
                Debug.Log($"[System] Game Loaded: {characterData.DisplayName}");
            }
            else 
            {
                Debug.LogWarning($"[System] Save file corrupted or class missing. Starting new.");
                StartNewGame();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[System] Save Load Error: {e.Message}");
            StartNewGame();
        }
    }

    public void DeleteSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log("[System] Save Deleted. Resetting...");
            StartNewGame();
        }
    }

    private void StartNewGame()
    {
        _playerStats.Initialize(_defaultCharacter);
    }
}