using System.Collections.Generic;
using Scripts.Dungeon;
using Scripts.StatusEffects;
using UnityEngine;

namespace Scripts.Enemies
{
    /// <summary>
    /// Тренировочный манекен в хабе: каждая смерть повышает его уровень и воскрешает его.
    /// Манекен не уничтожается никогда — на максимальном уровне он просто воскресает снова.
    /// </summary>
    [RequireComponent(typeof(EnemyEntity))]
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(EnemyStats))]
    public class DummyEvolution : MonoBehaviour
    {
        private static readonly List<DummyEvolution> Instances = new List<DummyEvolution>();

        [SerializeField] private int _maxLevel = 30;

        // Ссылка на данные, чтобы перезагружать их при левелапе
        [SerializeField] private EnemyDataSO _dummyData;

        [Tooltip("Отключать манекен, пока игрок в подземелье, чтобы его не добивали висящие эффекты")]
        [SerializeField] private bool _hideWhileInDungeon = true;

        private EnemyEntity _entity;
        private EnemyHealth _health;
        private EnemyStats _stats;

        private void Awake()
        {
            _entity = GetComponent<EnemyEntity>();
            _health = GetComponent<EnemyHealth>();
            _stats = GetComponent<EnemyStats>();
            PreventPermanentDeath();

            if (!Instances.Contains(this))
                Instances.Add(this);
        }

        private void Start()
        {
            PreventPermanentDeath();
            _health.OnDeath += HandleDeath;
            if (!DungeonController.IsHubActive)
                ApplyHubVisibility(false);
        }

        private void OnDestroy()
        {
            Instances.Remove(this);
            if (_health != null)
                _health.OnDeath -= HandleDeath;
        }

        /// <summary>
        /// Вызывать с живого объекта (DungeonController), а не через C# event на самом манекене:
        /// после SetActive(false) инстанс всё ещё в списке, и его можно снова включить.
        /// </summary>
        public static void RefreshHubVisibility(bool hubActive)
        {
            for (int i = Instances.Count - 1; i >= 0; i--)
            {
                var dummy = Instances[i];
                if (dummy == null)
                {
                    Instances.RemoveAt(i);
                    continue;
                }

                dummy.ApplyHubVisibility(hubActive);
            }
        }

        private void ApplyHubVisibility(bool hubActive)
        {
            if (!_hideWhileInDungeon)
                return;

            if (!hubActive)
                GetComponent<StatusEffectController>()?.ResetAll();

            if (gameObject.activeSelf != hubActive)
                gameObject.SetActive(hubActive);

            if (hubActive)
                Restore();
        }

        private void HandleDeath(EnemyHealth hp)
        {
            int currentLevel = _stats.Level;

            if (currentLevel < _maxLevel)
            {
                int nextLevel = currentLevel + 1;
                Debug.Log($"<color=yellow>[Dummy] EVOLUTION! Level {currentLevel} -> {nextLevel}</color>");

                if (_dummyData != null)
                    _entity.Setup(_dummyData, nextLevel);
            }
            else
            {
                Debug.Log("<color=red>[Dummy] MAX LEVEL REACHED. Staying at max level.</color>");
            }

            PreventPermanentDeath();
            _health.Resurrect();
        }

        private void Restore()
        {
            PreventPermanentDeath();
            GetComponent<StatusEffectController>()?.ResetAll();

            if (_dummyData != null && _entity != null)
                _entity.Setup(_dummyData, Mathf.Max(1, _stats != null ? _stats.Level : 1));

            _health.Resurrect();
        }

        private void PreventPermanentDeath()
        {
            if (_health != null)
                _health.DestroyOnDeath = false;
        }
    }
}
