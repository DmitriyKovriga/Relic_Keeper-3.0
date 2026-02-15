using UnityEngine;
using UnityEngine.InputSystem;
using System.IO;
using Scripts.Inventory;
using Scripts.Saving;
using Scripts.Skills.PassiveTree;

public class GameSaveManager : MonoBehaviour
{
    public const int CurrentSaveVersion = 2;

    [Header("Core Dependencies")]
    [SerializeField] private PlayerStats _playerStats;
    [SerializeField] private CharacterDatabaseSO _characterDB;
    [SerializeField] private CharacterDataSO _defaultCharacter;

    [Header("Inventory Dependencies")]
    [SerializeField] private ItemDatabaseSO _itemDatabase;

    [Header("New Game (optional)")]
    [Tooltip("Если задан, при новой игре откроется окно найма вместо дефолтного персонажа")]
    [SerializeField] private TavernUI _tavernUIForNewGame;

    private PassiveTreeManager _passiveTreeManager;
    private CharacterPartyManager _partyManager;

    private string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");

    private System.Collections.IEnumerator Start()
    {
        if (_playerStats != null)
            _passiveTreeManager = _playerStats.GetComponent<PassiveTreeManager>();
        _partyManager = FindObjectOfType<CharacterPartyManager>();

        if (_characterDB != null) _characterDB.Init();
        if (_itemDatabase != null) _itemDatabase.Init();

        yield return null;

        if (File.Exists(SavePath))
            LoadGame();
        else if (_tavernUIForNewGame != null)
            _tavernUIForNewGame.Open(forNewGame: true);
        else
            StartNewGame();
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

        var data = new GameSaveData { SaveVersion = CurrentSaveVersion };
        data.Stash = StashManager.Instance != null ? StashManager.Instance.GetSaveData() : new StashSaveData();

        if (_partyManager != null)
        {
            _partyManager.SaveCurrentToParty();
            _partyManager.WriteToSave(data);
        }
        else
        {
            data.ActiveCharacterID = _playerStats.CurrentClassID;
            data.Characters.Add(new CharacterSaveData
            {
                CharacterClassID = _playerStats.CurrentClassID,
                CurrentHealth = _playerStats.Health.Current,
                CurrentMana = _playerStats.Mana.Current,
                CurrentLevel = _playerStats.Leveling.Level,
                CurrentXP = _playerStats.Leveling.CurrentXP,
                RequiredXP = _playerStats.Leveling.RequiredXP,
                SkillPoints = _playerStats.Leveling.SkillPoints,
                Inventory = InventoryManager.Instance != null ? InventoryManager.Instance.GetSaveData() : new InventorySaveData(),
                AllocatedPassiveNodes = _passiveTreeManager != null ? _passiveTreeManager.GetSaveData() : new System.Collections.Generic.List<string>()
            });
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"[System] Game Saved.");
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

            string activeId = !string.IsNullOrEmpty(data.ActiveCharacterID) ? data.ActiveCharacterID : data.CharacterClassID;
            CharacterDataSO characterData = _characterDB?.GetCharacterByID(activeId);

            if (_partyManager != null)
            {
                _partyManager.LoadFromSave(data, _characterDB, _itemDatabase);
                activeId = _partyManager.ActiveCharacterID;
                characterData = _characterDB?.GetCharacterByID(activeId);
            }

            if (characterData != null)
            {
                if (_partyManager != null)
                {
                    var chData = _partyManager.GetCharacterData(activeId);
                    _partyManager.LoadCharacterIntoGame(chData, characterData, _itemDatabase);
                }
                else
                {
                    _playerStats.Initialize(characterData);
                    _playerStats.ApplyLoadedState(data);
                    if (InventoryManager.Instance != null && _itemDatabase != null)
                        InventoryManager.Instance.LoadState(data.Inventory ?? new InventorySaveData(), _itemDatabase);
                    if (_passiveTreeManager != null && data.AllocatedPassiveNodes != null)
                        _passiveTreeManager.LoadState(data.AllocatedPassiveNodes);
                }

                if (StashManager.Instance != null && _itemDatabase != null)
                    StashManager.Instance.LoadState(data.Stash ?? new StashSaveData(), _itemDatabase);

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
        if (data.SaveVersion == 1)
        {
            data.ActiveCharacterID = data.CharacterClassID;
            if (data.Characters == null) data.Characters = new System.Collections.Generic.List<CharacterSaveData>();
            if (data.Characters.Count == 0 && !string.IsNullOrEmpty(data.CharacterClassID))
            {
                data.Characters.Add(new CharacterSaveData
                {
                    CharacterClassID = data.CharacterClassID,
                    CurrentHealth = data.CurrentHealth,
                    CurrentMana = data.CurrentMana,
                    CurrentLevel = data.CurrentLevel,
                    CurrentXP = data.CurrentXP,
                    RequiredXP = data.RequiredXP,
                    SkillPoints = data.SkillPoints,
                    Inventory = data.Inventory ?? new InventorySaveData(),
                    AllocatedPassiveNodes = data.AllocatedPassiveNodes ?? new System.Collections.Generic.List<string>()
                });
            }
            data.SaveVersion = 2;
            Debug.Log("[System] Save migrated: 1 -> 2 (per-character save).");
        }
    }

    private void StartNewGame()
    {
        if (_defaultCharacter != null)
        {
            if (_partyManager != null)
            {
                _partyManager.AddCharacterToParty(_defaultCharacter.ID);
                _partyManager.SwapToCharacter(_defaultCharacter.ID, _characterDB, _itemDatabase);
            }
            else
            {
                _playerStats.Initialize(_defaultCharacter);
            }
            Debug.Log("[System] Started New Game (Default Character).");
        }
        else
        {
            Debug.LogError("[System] Default Character Data is missing!");
        }
    }

    /// <summary>Вызвать для показа окна найма при новой игре (вместо StartNewGame с дефолтом).</summary>
    public void RequestHireWindowForNewGame()
    {
        if (File.Exists(SavePath)) return;
        // Окно найма вызовется из TavernUI; GameSaveManager не запускает игру до выбора героя
        Debug.Log("[System] New game - waiting for hire selection.");
    }
}