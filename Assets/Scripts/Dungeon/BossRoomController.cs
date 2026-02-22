using UnityEngine;
using Scripts.Enemies;

namespace Scripts.Dungeon
{
    /// <summary>
    /// В босс-комнате следит за смертью босса и активирует портал «В город».
    /// </summary>
    public class BossRoomController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EnemyHealth _bossEntity;
        [SerializeField] private DungeonPortal _portalToHub;

        private void Start()
        {
            if (_bossEntity != null)
                _bossEntity.OnDeath += OnBossDeath;

            if (_portalToHub != null)
                _portalToHub.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_bossEntity != null)
                _bossEntity.OnDeath -= OnBossDeath;
        }

        private void OnBossDeath(EnemyHealth _)
        {
            if (_portalToHub != null)
                _portalToHub.SetActive(true);
        }
    }
}
