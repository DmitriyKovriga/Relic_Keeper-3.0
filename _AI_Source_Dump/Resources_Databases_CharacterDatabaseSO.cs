using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[CreateAssetMenu(menuName = "RPG/Character Database")]
public class CharacterDatabaseSO : ScriptableObject
{
    [SerializeField] private List<CharacterDataSO> _characters;
    private Dictionary<string, CharacterDataSO> _lookup;

    public void Init()
    {
        _lookup = _characters.ToDictionary(x => x.ID, x => x);
    }

    public CharacterDataSO GetCharacterByID(string id)
    {
        if (_lookup == null) Init();
        return _lookup.GetValueOrDefault(id);
    }
}