using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Enemies
{
    public class EnemyJumpLink : MonoBehaviour
    {
        private static readonly List<EnemyJumpLink> ActiveLinks = new List<EnemyJumpLink>();

        [SerializeField] private Transform _exitPoint;
        [SerializeField] private float _entryRadius = 0.35f;
        [SerializeField] private float _maxUseDistance = 8f;
        [SerializeField] private bool _bidirectional;

        public Transform ExitPoint => _exitPoint != null ? _exitPoint : transform;
        public float EntryRadius => Mathf.Max(0.05f, _entryRadius);
        public float MaxUseDistance => Mathf.Max(0.5f, _maxUseDistance);
        public bool Bidirectional => _bidirectional;

        private void OnEnable()
        {
            if (!ActiveLinks.Contains(this))
                ActiveLinks.Add(this);
        }

        private void OnDisable()
        {
            ActiveLinks.Remove(this);
        }

        public static EnemyJumpLink FindBest(Vector2 enemyPosition, Vector2 targetPosition, float maxSearchDistance)
        {
            EnemyJumpLink best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < ActiveLinks.Count; i++)
            {
                EnemyJumpLink link = ActiveLinks[i];
                if (link == null)
                    continue;

                float distanceToEntry = Vector2.Distance(enemyPosition, link.transform.position);
                if (distanceToEntry > Mathf.Min(link.MaxUseDistance, maxSearchDistance))
                    continue;

                Vector2 exitPosition = link.ExitPoint.position;
                float distanceExitToTarget = Vector2.Distance(exitPosition, targetPosition);
                float score = distanceToEntry + distanceExitToTarget * 0.85f;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = link;
                }

                if (!link.Bidirectional)
                    continue;

                float reverseDistanceToEntry = Vector2.Distance(enemyPosition, exitPosition);
                if (reverseDistanceToEntry > Mathf.Min(link.MaxUseDistance, maxSearchDistance))
                    continue;

                float reverseExitToTarget = Vector2.Distance((Vector2)link.transform.position, targetPosition);
                float reverseScore = reverseDistanceToEntry + reverseExitToTarget * 0.85f;
                if (reverseScore < bestScore)
                {
                    bestScore = reverseScore;
                    best = link;
                }
            }

            return best;
        }

        public Vector2 GetEntryFor(Vector2 enemyPosition)
        {
            if (!_bidirectional)
                return transform.position;

            float distanceToForward = Vector2.Distance(enemyPosition, transform.position);
            float distanceToReverse = Vector2.Distance(enemyPosition, ExitPoint.position);
            return distanceToReverse < distanceToForward ? (Vector2)ExitPoint.position : (Vector2)transform.position;
        }

        public Vector2 GetExitFor(Vector2 enemyPosition)
        {
            if (!_bidirectional)
                return ExitPoint.position;

            float distanceToForward = Vector2.Distance(enemyPosition, transform.position);
            float distanceToReverse = Vector2.Distance(enemyPosition, ExitPoint.position);
            return distanceToReverse < distanceToForward ? (Vector2)transform.position : (Vector2)ExitPoint.position;
        }

        private void OnDrawGizmos()
        {
            Vector3 entry = transform.position;
            Vector3 exit = ExitPoint.position;

            Gizmos.color = new Color(0.35f, 0.9f, 0.95f, 0.9f);
            Gizmos.DrawWireSphere(entry, EntryRadius);
            Gizmos.DrawWireSphere(exit, EntryRadius);
            Gizmos.DrawLine(entry, exit);
        }
    }
}
