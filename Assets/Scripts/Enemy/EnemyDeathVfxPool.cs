using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Enemies
{
    /// <summary>Per-room pool for all transient death-effect roots. Children stay attached to their root.</summary>
    public sealed class EnemyDeathVfxPool : MonoBehaviour
    {
        private readonly Stack<GameObject> _fragmentPool = new Stack<GameObject>();
        private readonly Stack<GameObject> _decalPool = new Stack<GameObject>();
        private readonly Stack<GameObject> _compositePool = new Stack<GameObject>();
        private Transform _container;

        public GameObject GetFragment(Transform parent, string objectName) => Get(_fragmentPool, parent, objectName);
        public GameObject GetDecal(Transform parent, string objectName) => Get(_decalPool, parent, objectName);
        public GameObject GetComposite(Transform parent, string objectName) => Get(_compositePool, parent, objectName);

        public void ReturnFragment(GameObject value) => Return(_fragmentPool, value);
        public void ReturnDecal(GameObject value) => Return(_decalPool, value);
        public void ReturnComposite(GameObject value) => Return(_compositePool, value);

        private GameObject Get(Stack<GameObject> pool, Transform parent, string objectName)
        {
            GameObject value = pool.Count > 0 ? pool.Pop() : new GameObject(objectName);
            value.name = objectName;
            value.transform.SetParent(parent, false);
            value.SetActive(true);
            return value;
        }

        private void Return(Stack<GameObject> pool, GameObject value)
        {
            if (value == null)
                return;

            value.SetActive(false);
            value.transform.SetParent(Container, false);
            pool.Push(value);
        }

        private Transform Container
        {
            get
            {
                if (_container != null)
                    return _container;

                var host = new GameObject("DeathVfxPool");
                host.transform.SetParent(transform, false);
                _container = host.transform;
                return _container;
            }
        }
    }
}
