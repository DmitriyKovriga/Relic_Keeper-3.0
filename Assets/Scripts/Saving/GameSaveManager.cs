using UnityEngine;
using UnityEngine.InputSystem;
using System.IO;
using Scripts.Inventory;
using Scripts.Saving;
using Scripts.Skills.PassiveTree;
using Scripts.Configuration;

public class GameSaveManager : MonoBehaviour
{
    public const int CurrentSaveVersion = 4;

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
    private bool _handlingPlayerDeath;

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
            if (_partyManager.HasActiveCharacter)
                _partyManager.SaveCurrentToParty();
            _partyManager.WriteToSave(data);
        }
        else
        {
            data.ActiveCharacterID = _playerStats.CurrentClassID;
            data.Characters.Add(new CharacterSaveData
            {
                CharacterInstanceID = _playerStats.CurrentClassID,
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

    public bool TryAutoSave(string reason = null)
    {
        if (!PlaytestConfiguration.AutoSaveEnabled)
            return false;

        SaveGame();
        Debug.Log(string.IsNullOrEmpty(reason)
            ? "[System] Autosave completed."
            : $"[System] Autosave completed: {reason}.");
        return true;
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
            CharacterDataSO characterData = null;
            CharacterSaveData activeCharacterSave = null;

            if (_partyManager != null)
            {
                _partyManager.LoadFromSave(data, _characterDB, _itemDatabase);
                activeId = _partyManager.ActiveCharacterID;
                activeCharacterSave = _partyManager.GetCharacterData(activeId);
                characterData = _characterDB?.GetCharacterByID(activeCharacterSave?.CharacterClassID);

                if (!_partyManager.HasActiveCharacter)
                {
                    if (InventoryManager.Instance != null && _itemDatabase != null)
                        InventoryManager.Instance.LoadState(new InventorySaveData(), _itemDatabase);
                    if (StashManager.Instance != null && _itemDatabase != null)
                        StashManager.Instance.LoadState(data.Stash ?? new StashSaveData(), _itemDatabase);

                    _tavernUIForNewGame?.OpenForRequiredCharacterSelection();
                    Debug.Log("[System] Save has no active character. Waiting for a required Tavern selection.");
                    return;
                }
            }
            else
            {
                characterData = _characterDB?.GetCharacterByID(activeId);
                if (characterData == null && data.Characters != null && data.Characters.Count > 0)
                {
                    activeCharacterSave = data.Characters.Find(ch => ch.CharacterInstanceID == activeId)
                        ?? data.Characters[0];
                    characterData = _characterDB?.GetCharacterByID(activeCharacterSave?.CharacterClassID);
                }
            }

            if (characterData != null)
            {
                if (_partyManager != null)
                {
                    _partyManager.LoadCharacterIntoGame(activeCharacterSave, characterData, _itemDatabase);
                }
                else
                {
                    _playerStats.Initialize(characterData);
                    if (_passiveTreeManager != null)
                    {
                        _passiveTreeManager.IsPreviewMode = false;
                        _passiveTreeManager.SetTreeData(characterData.PassiveTree);
                        if (characterData.PassiveTree != null)
                            _passiveTreeManager.LoadState(data.AllocatedPassiveNodes);
                    }

                    if (InventoryManager.Instance != null && _itemDatabase != null)
                        InventoryManager.Instance.LoadState(data.Inventory ?? new InventorySaveData(), _itemDatabase, applyStatEvents: false);

                    _playerStats.ResyncExternalStatModifiers(
                        InventoryManager.Instance != null ? InventoryManager.Instance.EquipmentItems : null,
                        _passiveTreeManager);

                    _playerStats.ApplyLoadedState(data);

                    var skillManager = _playerStats.GetComponent<Scripts.Skills.PlayerSkillManager>();
                    if (skillManager != null)
                    {
                        skillManager.CancelAllSkills();
                        skillManager.RefreshAllSkills();
                    }
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

    public void HandlePlayerDeath()
    {
        if (_handlingPlayerDeath || PlaytestConfiguration.PlayerImmortal)
            return;

        _handlingPlayerDeath = true;
        if (_partyManager == null)
            _partyManager = FindObjectOfType<CharacterPartyManager>();

        if (_partyManager == null || !_partyManager.RemoveActiveCharacterAfterDeath())
        {
            Debug.LogError("[System] Player death could not remove the active character.");
            _handlingPlayerDeath = false;
            return;
        }

        if (InventoryManager.Instance != null && _itemDatabase != null)
            InventoryManager.Instance.LoadState(new InventorySaveData(), _itemDatabase);

        _playerStats?.ResyncExternalStatModifiers(
            InventoryManager.Instance != null ? InventoryManager.Instance.EquipmentItems : null,
            _passiveTreeManager);

        if (Scripts.Dungeon.DungeonController.Instance != null)
            Scripts.Dungeon.DungeonController.Instance.ReturnToHub();

        SaveGame();
        _tavernUIForNewGame?.OpenForRequiredCharacterSelection();
        _handlingPlayerDeath = false;
        Debug.Log("[System] Character died permanently. Returned to Hub and opened Tavern selection.");
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
        if (data.SaveVersion == 2)
        {
            string oldActiveClassId = data.ActiveCharacterID;
            if (data.Characters != null)
            {
                string migratedActiveInstanceId = null;
                foreach (var ch in data.Characters)
                {
                    if (string.IsNullOrEmpty(ch.CharacterInstanceID))
                        ch.CharacterInstanceID = System.Guid.NewGuid().ToString("N");

                    if (migratedActiveInstanceId == null && !string.IsNullOrEmpty(oldActiveClassId) && ch.CharacterClassID == oldActiveClassId)
                        migratedActiveInstanceId = ch.CharacterInstanceID;
                }

                if (!string.IsNullOrEmpty(migratedActiveInstanceId))
                    data.ActiveCharacterID = migratedActiveInstanceId;
                else if (data.Characters.Count > 0)
                    data.ActiveCharacterID = data.Characters[0].CharacterInstanceID;
            }

            data.SaveVersion = 3;
            Debug.Log("[System] Save migrated: 2 -> 3 (character instances support).");
        }
        if (data.SaveVersion == 3)
        {
            data.SaveVersion = 4;
            Debug.Log("[System] Save migrated: 3 -> 4 (required character selection support).");
        }
    }

    private void StartNewGame()
    {
        if (_defaultCharacter != null)
        {
            if (_partyManager != null)
            {
                string instanceId = _partyManager.AddCharacterToParty(_defaultCharacter.ID);
                _partyManager.SwapToCharacter(instanceId, _characterDB, _itemDatabase);
            }
            else
            {
                _playerStats.Initialize(_defaultCharacter);
                _playerStats.Health.RestoreFull();
                _playerStats.Mana.RestoreFull();
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
