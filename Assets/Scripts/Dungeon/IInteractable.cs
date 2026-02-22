namespace Scripts.Dungeon
{
    /// <summary>
    /// Объект, с которым можно взаимодействовать по нажатию Interact.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Текст подсказки ("Выйти", "В город").</summary>
        string GetPrompt();

        /// <summary>Вызывается при нажатии Interact.</summary>
        void Interact();

        /// <summary>Доступен ли для взаимодействия (например портал после босса).</summary>
        bool CanInteract();
    }
}
