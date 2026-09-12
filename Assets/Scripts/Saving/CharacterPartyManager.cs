using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Scripts.Inventory;
using Scripts.Saving;
using Scripts.Skills;
using Scripts.Skills.PassiveTree;

public class CharacterPartyManager : MonoBehaviour
{
    public static CharacterPartyManager Instance { get; private set; }

    public event Action<string> OnActiveCharacterChanged;

    private readonly Dictionary<string, CharacterSaveData> _partyCharacters = new Dictionary<string, CharacterSaveData>();
    private string _activeCharacterID;

    private PlayerStats _playerStats;
    private PassiveTreeManager _passiveTreeManager;

    public string ActiveCharacterID => _activeCharacterID;
    public bool HasActiveCharacter => !string.IsNullOrEmpty(_activeCharacterID) && _partyCharacters.ContainsKey(_activeCharacterID);
    public IReadOnlyList<string> PartyCharacterIDs => _partyCharacters.Keys.ToList();
    public IReadOnlyList<string> HostelCharacterIDs => _partyCharacters.Keys.Where(id => id != _activeCharacterID).ToList();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        _playerStats = FindObjectOfType<PlayerStats>();
        if (_playerStats != null)
            _passiveTreeManager = _playerStats.GetComponent<PassiveTreeManager>();
    }

    public bool HasCharacter(string characterInstanceId) =>
        !string.IsNullOrEmpty(characterInstanceId) && _partyCharacters.ContainsKey(characterInstanceId);

    public CharacterSaveData GetCharacterData(string characterInstanceId)
    {
        if (string.IsNullOrEmpty(characterInstanceId))
            return null;

        return _partyCharacters.TryGetValue(characterInstanceId, out var data) ? data : null;
    }

    public void LoadFromSave(GameSaveData data, CharacterDatabaseSO characterDB, ItemDatabaseSO itemDB)
    {
        _partyCharacters.Clear();
        _activeCharacterID = null;

        if (data.SaveVersion >= 2 && data.Characters != null && data.Characters.Count > 0)
        {
            foreach (var ch in data.Characters)
            {
                if (string.IsNullOrEmpty(ch.CharacterClassID))
                    continue;

                if (string.IsNullOrEmpty(ch.CharacterInstanceID))
                    ch.CharacterInstanceID = Guid.NewGuid().ToString("N");

                _partyCharacters[ch.CharacterInstanceID] = ch;
            }

            bool savedActiveCharacterExists = !string.IsNullOrEmpty(data.ActiveCharacterID) &&
                                              _partyCharacters.ContainsKey(data.ActiveCharacterID);
            if (savedActiveCharacterExists)
                _activeCharacterID = data.ActiveCharacterID;
            else if (!string.IsNullOrEmpty(data.ActiveCharacterID) || data.SaveVersion < 4)
                _activeCharacterID = _partyCharacters.Keys.FirstOrDefault();
            else
                _activeCharacterID = null;
        }
        else if (!string.IsNullOrEmpty(data.CharacterClassID))
        {
            var legacy = new CharacterSaveData(data.CharacterClassID);
            legacy.CurrentHealth = data.CurrentHealth;
            legacy.CurrentMana = data.CurrentMana;
            legacy.CurrentLevel = data.CurrentLevel;
            legacy.CurrentXP = data.CurrentXP;
            legacy.RequiredXP = data.RequiredXP;
            legacy.SkillPoints = data.SkillPoints;
            legacy.Inventory = data.Inventory ?? new InventorySaveData();
            legacy.AllocatedPassiveNodes = data.AllocatedPassiveNodes ?? new List<string>();

            _partyCharacters[legacy.CharacterInstanceID] = legacy;
            _activeCharacterID = legacy.CharacterInstanceID;
        }

        if (data.SaveVersion < 4 && string.IsNullOrEmpty(_activeCharacterID) && _partyCharacters.Count > 0)
            _activeCharacterID = _partyCharacters.Keys.First();
    }

    public void WriteToSave(GameSaveData data)
    {
        data.ActiveCharacterID = _activeCharacterID;
        data.Characters.Clear();
        foreach (var kv in _partyCharacters)
            data.Characters.Add(kv.Value);
    }

    public void SaveCurrentToParty()
    {
        if (_playerStats == null)
            return;

        if (!HasActiveCharacter)
            return;

        var ch = GetOrCreateCharacterData(_activeCharacterID);
        ch.CurrentHealth = _playerStats.Health.Current;
        ch.CurrentMana = _playerStats.Mana.Current;
        ch.CurrentLevel = _playerStats.Leveling.Level;
        ch.CurrentXP = _playerStats.Leveling.CurrentXP;
        ch.RequiredXP = _playerStats.Leveling.RequiredXP;
        ch.SkillPoints = _playerStats.Leveling.SkillPoints;
        ch.Inventory = InventoryManager.Instance != null ? InventoryManager.Instance.GetSaveData() : new InventorySaveData();
        ch.AllocatedPassiveNodes = _passiveTreeManager != null ? _passiveTreeManager.GetSaveData() : new List<string>();
    }

    public void LoadCharacterIntoGame(CharacterSaveData chData, CharacterDataSO characterData, ItemDatabaseSO itemDB)
    {
        if (chData == null || characterData == null || _playerStats == null)
        {
            if (_playerStats == null)
                Debug.LogWarning("[CharacterPartyManager] LoadCharacterIntoGame: PlayerStats was not found.");
            return;
        }

        _playerStats.Initialize(characterData);

        if (_passiveTreeManager != null)
        {
            _passiveTreeManager.IsPreviewMode = false;
            _passiveTreeManager.SetTreeData(characterData.PassiveTree);
            if (characterData.PassiveTree != null)
                _passiveTreeManager.LoadState(chData.AllocatedPassiveNodes);
        }

        if (InventoryManager.Instance != null && itemDB != null)
            InventoryManager.Instance.LoadState(chData.Inventory ?? new InventorySaveData(), itemDB, applyStatEvents: false);

        _playerStats.ResyncExternalStatModifiers(
            InventoryManager.Instance != null ? InventoryManager.Instance.EquipmentItems : null,
            _passiveTreeManager);

        _playerStats.ApplyLoadedState(chData);

        var skillManager = _playerStats.GetComponent<PlayerSkillManager>();
        if (skillManager != null)
        {
            skillManager.CancelAllSkills();
            skillManager.RefreshAllSkills();
        }
    }

    public string AddCharacterToParty(string characterClassId)
    {
        if (string.IsNullOrEmpty(characterClassId))
            return null;

        var character = new CharacterSaveData(characterClassId);
        _partyCharacters[character.CharacterInstanceID] = character;
        return character.CharacterInstanceID;
    }

    public bool RemoveCharacterFromParty(string characterInstanceId)
    {
        if (string.IsNullOrEmpty(characterInstanceId) || !_partyCharacters.ContainsKey(characterInstanceId))
            return false;

        if (characterInstanceId == _activeCharacterID)
        {
            Debug.LogWarning($"[CharacterPartyManager] RemoveCharacterFromParty: cannot remove active character '{characterInstanceId}'.");
            return false;
        }

        return _partyCharacters.Remove(characterInstanceId);
    }

    public bool RemoveActiveCharacterAfterDeath()
    {
        if (!HasActiveCharacter)
            return false;

        string deadCharacterId = _activeCharacterID;
        _activeCharacterID = null;
        bool removed = _partyCharacters.Remove(deadCharacterId);
        if (removed)
            Debug.Log($"[CharacterPartyManager] Dead character '{deadCharacterId}' was permanently removed.");
        return removed;
    }

    public bool SwapToCharacter(string characterInstanceId, CharacterDatabaseSO characterDB, ItemDatabaseSO itemDB)
    {
        if (string.IsNullOrEmpty(characterInstanceId) || !_partyCharacters.ContainsKey(characterInstanceId))
        {
            Debug.LogWarning($"[CharacterPartyManager] SwapToCharacter: character instance '{characterInstanceId}' is not in party.");
            return false;
        }

        var chData = _partyCharacters[characterInstanceId];
        var characterData = characterDB?.GetCharacterByID(chData.CharacterClassID);
        if (characterData == null)
        {
            Debug.LogWarning($"[CharacterPartyManager] SwapToCharacter: character class '{chData.CharacterClassID}' was not found in Character Database.");
            return false;
        }

        if (_playerStats == null)
        {
            Debug.LogWarning("[CharacterPartyManager] SwapToCharacter: PlayerStats was not found in scene.");
            return false;
        }

        if (HasActiveCharacter)
            SaveCurrentToParty();
        _activeCharacterID = characterInstanceId;
        LoadCharacterIntoGame(chData, characterData, itemDB);
        OnActiveCharacterChanged?.Invoke(characterInstanceId);
        return true;
    }

    private CharacterSaveData GetOrCreateCharacterData(string instanceId)
    {
        if (!_partyCharacters.TryGetValue(instanceId, out var data))
        {
            var classId = _playerStats != null ? _playerStats.CurrentClassID : "Unknown";
            data = new CharacterSaveData(classId, instanceId);
            _partyCharacters[instanceId] = data;
        }

        return data;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
