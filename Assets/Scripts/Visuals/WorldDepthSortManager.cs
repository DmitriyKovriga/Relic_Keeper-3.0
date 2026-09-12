using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Visuals
{
    [DefaultExecutionOrder(1000)]
    public sealed class WorldDepthSortManager : MonoBehaviour
    {
        private static WorldDepthSortManager s_instance;
        private static readonly List<WorldDepthSort> s_dynamicSorters = new List<WorldDepthSort>(128);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (s_instance != null)
                return;

            var host = new GameObject(nameof(WorldDepthSortManager));
            s_instance = host.AddComponent<WorldDepthSortManager>();
            DontDestroyOnLoad(host);
        }

        internal static void Register(WorldDepthSort sorter)
        {
            if (sorter == null || !sorter.RequiresDynamicUpdates)
                return;

            if (!s_dynamicSorters.Contains(sorter))
                s_dynamicSorters.Add(sorter);
        }

        internal static void Unregister(WorldDepthSort sorter)
        {
            if (sorter == null)
                return;

            s_dynamicSorters.Remove(sorter);
        }

        private void LateUpdate()
        {
            for (int i = s_dynamicSorters.Count - 1; i >= 0; i--)
            {
                WorldDepthSort sorter = s_dynamicSorters[i];
                if (sorter == null || !sorter.isActiveAndEnabled)
                {
                    s_dynamicSorters.RemoveAt(i);
                    continue;
                }

                sorter.ApplySort();
            }
        }
    }
}
