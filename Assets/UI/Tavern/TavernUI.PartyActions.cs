using UnityEngine;

public partial class TavernUI
{
    private void OnHireClicked(CharacterDataSO ch)
    {
        if (ch == null || string.IsNullOrEmpty(ch.ID))
        {
            Debug.LogWarning("[Tavern] Hire: character without ID was skipped.");
            return;
        }

        if (CharacterPartyManager.Instance == null)
        {
            Debug.LogWarning("[Tavern] Hire: CharacterPartyManager was not found.");
            var saveMgr = FindObjectOfType<GameSaveManager>();
            if (saveMgr != null)
                saveMgr.SaveGame();
            return;
        }

        if (_characterDB == null || _itemDatabase == null)
        {
            Debug.LogWarning("[Tavern] Hire: Character DB or Item DB is missing.");
            return;
        }

        string instanceId = CharacterPartyManager.Instance.AddCharacterToParty(ch.ID);
        if (!CharacterPartyManager.Instance.SwapToCharacter(instanceId, _characterDB, _itemDatabase))
        {
            Debug.LogWarning($"[Tavern] Hire: failed to swap to new instance of '{ch.ID}'.");
            return;
        }

        TryGrantStarterWeapon(checkStash: false, preferEquip: true);

        RerollHireChoices();
        CompleteRequiredCharacterSelection();
        FindObjectOfType<GameSaveManager>()?.SaveGame();
        Close();
    }

    private void OnSwapToHostelClicked(CharacterDataSO ch, string characterInstanceId)
    {
        if (CharacterPartyManager.Instance == null || string.IsNullOrEmpty(characterInstanceId))
            return;

        if (!CharacterPartyManager.Instance.SwapToCharacter(characterInstanceId, _characterDB, _itemDatabase))
            return;

        CompleteRequiredCharacterSelection();
        FindObjectOfType<GameSaveManager>()?.SaveGame();
        Close();
    }

    private void OnDeleteHostelCharacterConfirmed(CharacterDataSO ch, string characterInstanceId)
    {
        if (ch == null || string.IsNullOrEmpty(characterInstanceId))
            return;

        if (CharacterPartyManager.Instance == null)
        {
            Debug.LogWarning("[Tavern] Delete: CharacterPartyManager not found.");
            return;
        }

        if (!CharacterPartyManager.Instance.RemoveCharacterFromParty(characterInstanceId))
        {
            Debug.LogWarning($"[Tavern] Delete: failed to remove hostel hero instance '{characterInstanceId}'.");
            return;
        }

        var saveMgr = FindObjectOfType<GameSaveManager>();
        if (saveMgr != null)
            saveMgr.SaveGame();

        RefreshHostelList();
    }
}
