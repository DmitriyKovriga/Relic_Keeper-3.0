using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[CreateAssetMenu(menuName = "RPG/Character Database")]
public class CharacterDatabaseSO : ScriptableObject
{
    [SerializeField] private List<CharacterDataSO> _characters = new List<CharacterDataSO>();
    private Dictionary<string, CharacterDataSO> _lookup;

    public IReadOnlyList<CharacterDataSO> AllCharacters => _characters ??= new List<CharacterDataSO>();

    public void Init()
    {
        if (_characters == null) _characters = new List<CharacterDataSO>();
        _lookup = _characters.Where(c => c != null && !string.IsNullOrEmpty(c.ID))
            .ToDictionary(x => x.ID, x => x);
    }

    public CharacterDataSO GetCharacterByID(string id)
    {
        if (_lookup == null) Init();
        return _lookup != null ? _lookup.GetValueOrDefault(id) : null;
    }

    public bool ContainsCharacter(string id)
    {
        if (_lookup == null) Init();
        return _lookup != null && _lookup.ContainsKey(id);
    }

#if UNITY_EDITOR
    public void AddCharacter(CharacterDataSO character)
    {
        if (character == null || string.IsNullOrEmpty(character.ID)) return;
        if (_characters == null) _characters = new List<CharacterDataSO>();
        if (!_characters.Contains(character))
        {
            _characters.Add(character);
            _lookup = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }

    public void RemoveCharacter(CharacterDataSO character)
    {
        if (_characters != null && _characters.Remove(character))
        {
            _lookup = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}