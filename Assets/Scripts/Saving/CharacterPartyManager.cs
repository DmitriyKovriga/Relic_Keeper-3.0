using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Scripts.Inventory;
using Scripts.Saving;
using Scripts.Skills.PassiveTree;

/// <summary>
/// Управляет партией персонажей: активный, хостел. Связывает персонажа с инвентарём и деревом.
/// Склад общий для всех.
/// </summary>
public class CharacterPartyManager : MonoBehaviour
{
    public static CharacterPartyManager Instance { get; private set; }

    public event Action<string> OnActiveCharacterChanged;

    private Dictionary<string, CharacterSaveData> _partyCharacters = new Dictionary<string, CharacterSaveData>();
    private string _activeCharacterID;

    private PlayerStats _playerStats;
    private PassiveTreeManager _passiveTreeManager;

    public string ActiveCharacterID => _activeCharacterID;
    public IReadOnlyList<string> PartyCharacterIDs => _partyCharacters.Keys.ToList();

    public IReadOnlyList<string> HostelCharacterIDs =>
        _partyCharacters.Keys.Where(id => id != _activeCharacterID).ToList();

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

    /// <summary>Есть ли персонаж в партии (активный или хостел).</summary>
    public bool HasCharacter(string characterId) => _partyCharacters.ContainsKey(characterId);

    /// <summary>Получить данные персонажа из партии.</summary>
    public CharacterSaveData GetCharacterData(string characterId) =>
        _partyCharacters.TryGetValue(characterId, out var d) ? d : null;

    /// <summary>Загрузить состояние партии из сейва.</summary>
    public void LoadFromSave(GameSaveData data, CharacterDatabaseSO characterDB, ItemDatabaseSO itemDB)
    {
        _partyCharacters.Clear();
        _activeCharacterID = null;

        if (data.SaveVersion >= 2 && data.Characters != null && data.Characters.Count > 0)
        {
            foreach (var ch in data.Characters)
            {
                if (string.IsNullOrEmpty(ch.CharacterClassID)) continue;
                _partyCharacters[ch.CharacterClassID] = ch;
            }
            _activeCharacterID = !string.IsNullOrEmpty(data.ActiveCharacterID) && _partyCharacters.ContainsKey(data.ActiveCharacterID)
                ? data.ActiveCharacterID
                : data.Characters[0].CharacterClassID;
        }
        else if (!string.IsNullOrEmpty(data.CharacterClassID))
        {
            var legacy = new CharacterSaveData
            {
                CharacterClassID = data.CharacterClassID,
                CurrentHealth = data.CurrentHealth,
                CurrentMana = data.CurrentMana,
                CurrentLevel = data.CurrentLevel,
                CurrentXP = data.CurrentXP,
                RequiredXP = data.RequiredXP,
                SkillPoints = data.SkillPoints,
                Inventory = data.Inventory ?? new InventorySaveData(),
                AllocatedPassiveNodes = data.AllocatedPassiveNodes ?? new List<string>()
            };
            _partyCharacters[legacy.CharacterClassID] = legacy;
            _activeCharacterID = legacy.CharacterClassID;
        }

        if (string.IsNullOrEmpty(_activeCharacterID) && _partyCharacters.Count > 0)
            _activeCharacterID = _partyCharacters.Keys.First();
    }

    /// <summary>Собрать текущее состояние в сейв.</summary>
    public void WriteToSave(GameSaveData data)
    {
        data.ActiveCharacterID = _activeCharacterID;
        data.Characters.Clear();
        foreach (var kv in _partyCharacters)
            data.Characters.Add(kv.Value);
    }

    /// <summary>Сохранить текущего активного персонажа в партию (из PlayerStats, Inventory, Tree).</summary>
    public void SaveCurrentToParty()
    {
        if (string.IsNullOrEmpty(_activeCharacterID) || _playerStats == null) return;

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

    /// <summary>Загрузить персонажа из партии в PlayerStats, Inventory, Tree.</summary>
    public void LoadCharacterIntoGame(CharacterSaveData chData, CharacterDataSO characterData, ItemDatabaseSO itemDB)
    {
        if (chData == null || characterData == null || _playerStats == null)
        {
            if (_playerStats == null) Debug.LogWarning("[CharacterPartyManager] LoadCharacterIntoGame: PlayerStats не найден.");
            return;
        }

        _playerStats.Initialize(characterData);
        _playerStats.ApplyLoadedState(chData);
        // При свапе персонаж появляется с полным HP/маной (отдых в хостеле)
        _playerStats.Health.RestoreFull();
        _playerStats.Mana.RestoreFull();

        if (InventoryManager.Instance != null && itemDB != null)
            InventoryManager.Instance.LoadState(chData.Inventory ?? new InventorySaveData(), itemDB);

        if (_passiveTreeManager != null)
        {
            if (characterData.PassiveTree != null)
            {
                _passiveTreeManager.SetTreeData(characterData.PassiveTree);
                if (chData.AllocatedPassiveNodes != null)
                    _passiveTreeManager.LoadState(chData.AllocatedPassiveNodes);
            }
        }
    }

    /// <summary>Добавить нового персонажа в партию (при найме).</summary>
    public void AddCharacterToParty(string characterId)
    {
        if (string.IsNullOrEmpty(characterId) || _partyCharacters.ContainsKey(characterId)) return;
        _partyCharacters[characterId] = new CharacterSaveData(characterId);
    }

    /// <summary>Сменить активного персонажа. Текущий уходит в хостел, новый становится активным.</summary>
    public bool SwapToCharacter(string newCharacterId, CharacterDatabaseSO characterDB, ItemDatabaseSO itemDB)
    {
        if (string.IsNullOrEmpty(newCharacterId) || !_partyCharacters.ContainsKey(newCharacterId))
        {
            Debug.LogWarning($"[CharacterPartyManager] SwapToCharacter: персонаж '{newCharacterId}' не в партии.");
            return false;
        }
        var characterData = characterDB?.GetCharacterByID(newCharacterId);
        if (characterData == null)
        {
            Debug.LogWarning($"[CharacterPartyManager] SwapToCharacter: персонаж '{newCharacterId}' не найден в Character Database.");
            return false;
        }
        if (_playerStats == null)
        {
            Debug.LogWarning("[CharacterPartyManager] SwapToCharacter: PlayerStats не найден. Добавьте PlayerStats в сцену.");
            return false;
        }

        SaveCurrentToParty();
        _activeCharacterID = newCharacterId;
        var chData = _partyCharacters[newCharacterId];
        LoadCharacterIntoGame(chData, characterData, itemDB);
        OnActiveCharacterChanged?.Invoke(newCharacterId);
        return true;
    }

    private CharacterSaveData GetOrCreateCharacterData(string id)
    {
        if (!_partyCharacters.TryGetValue(id, out var d))
        {
            d = new CharacterSaveData(id);
            _partyCharacters[id] = d;
        }
        return d;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
