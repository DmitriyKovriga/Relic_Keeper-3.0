using UnityEngine;

public partial class TavernUI
{
    private void OnHireClicked(CharacterDataSO ch)
    {
        if (ch == null || string.IsNullOrEmpty(ch.ID))
        {
            Debug.LogWarning("[Tavern] Hire: персонаж без ID, пропуск.");
            return;
        }

        if (CharacterPartyManager.Instance == null)
        {
            Debug.LogWarning("[Tavern] Hire: CharacterPartyManager не найден.");
            var saveMgr = FindObjectOfType<GameSaveManager>();
            if (saveMgr != null) saveMgr.SaveGame();
            return;
        }

        if (_characterDB == null || _itemDatabase == null)
        {
            Debug.LogWarning("[Tavern] Hire: Character DB или Item DB не назначены.");
            return;
        }

        CharacterPartyManager.Instance.AddCharacterToParty(ch.ID);
        if (!CharacterPartyManager.Instance.SwapToCharacter(ch.ID, _characterDB, _itemDatabase))
        {
            Debug.LogWarning($"[Tavern] Hire: SwapToCharacter не удался для {ch.ID}. Проверьте, что персонаж есть в Character Database.");
            return;
        }

        RerollHireChoices();
        Close();
    }

    private void OnSwapToHostelClicked(CharacterDataSO ch)
    {
        if (CharacterPartyManager.Instance == null) return;
        CharacterPartyManager.Instance.SwapToCharacter(ch.ID, _characterDB, _itemDatabase);
        Close();
    }
}
