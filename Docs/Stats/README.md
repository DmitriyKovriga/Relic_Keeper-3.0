# Stats System Docs

Этот набор документов описывает текущую production-модель статов в `Relic Keeper`.

Файлы:

- `Stats_System_Architecture.md`
  - архитектура stat metadata
  - различие между `Final Scalar`, `Combat Scalar`, `Context Modifier`
  - роль `DamageContext`
- `Affixes_And_Localization.md`
  - как генерятся аффиксы
  - как работают `Flat / Increase / More`
  - как устроена автолокализация
- `PassiveTree_Stats.md`
  - как статы живут в нодах пассивного дерева
  - как они форматируются и поддерживаются
- `Gameplay_Stat_Semantics.md`
  - как в будущем должны работать игровые статы
  - конкретные правила для урона, крита, регена, скоростей и контекстных модификаторов
- `Upgrade_And_Repair.md`
  - как использовать production upgrade
  - что именно он чинит
  - какие ограничения и проверки остаются

Базовый принцип системы:

1. Мы не меняем существующие `StatType` ID без крайней необходимости.
2. Смысл статов хранится в metadata (`StatsDatabaseSO`), а не в одних только именах enum.
3. UI обязан форматировать статы через metadata, особенно процентные итоговые scalar-статы.
4. Context-модификаторы не являются “итоговыми числами персонажа” и должны применяться через `DamageContext`.
