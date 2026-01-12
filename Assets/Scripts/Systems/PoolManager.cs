using UnityEngine;
using System.Collections.Generic;

public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }

    // Словарь: ID Префаба (int) -> Очередь выключенных объектов
    private Dictionary<int, Queue<GameObject>> _poolDictionary = new Dictionary<int, Queue<GameObject>>();
    
    // Папка для порядка в иерархии
    private Transform _poolContainer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Создаем контейнер, куда будем складывать выключенные объекты
        GameObject container = new GameObject("--- POOL ---");
        container.transform.SetParent(transform);
        _poolContainer = container.transform;
    }

    /// <summary>
    /// Главный метод: Дай мне объект! (Аналог Instantiate)
    /// </summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null)
        {
            Debug.LogError("[PoolManager] Попытка заспавнить null префаб!");
            return null;
        }

        // Получаем уникальный ID префаба (это быстрее, чем сравнивать имена)
        int key = prefab.GetInstanceID();

        // Если такого пула еще нет — создаем
        if (!_poolDictionary.ContainsKey(key))
        {
            _poolDictionary.Add(key, new Queue<GameObject>());
        }

        GameObject objToSpawn;

        // 1. Если в пуле есть готовые объекты — берем
        if (_poolDictionary[key].Count > 0)
        {
            objToSpawn = _poolDictionary[key].Dequeue();
        }
        // 2. Если пустой — создаем новый
        else
        {
            objToSpawn = Instantiate(prefab);
            // Вешаем метку, чтобы знать, чей это объект
            var pooledComponent = objToSpawn.AddComponent<PooledObject>();
            pooledComponent.PrefabID = key;
        }

        // Настройка трансформа
        objToSpawn.transform.SetParent(parent); // null = корень сцены
        objToSpawn.transform.position = position;
        objToSpawn.transform.rotation = rotation;
        
        // Активация (важно делать это в конце, чтобы OnEnable сработал на настроенном объекте)
        objToSpawn.SetActive(true);

        return objToSpawn;
    }

    /// <summary>
    /// Главный метод: Забери объект обратно! (Аналог Destroy)
    /// </summary>
    public void ReturnToPool(GameObject obj)
    {
        // Проверяем нашу метку
        var pooledObj = obj.GetComponent<PooledObject>();
        
        // Если метки нет, значит объект создан не через PoolManager (или метку удалили)
        if (pooledObj == null)
        {
            Debug.LogWarning($"[PoolManager] Объект '{obj.name}' не из пула (нет PooledObject). Удаляю через Destroy.");
            Destroy(obj);
            return;
        }

        // Выключаем
        obj.SetActive(false);
        
        // Прячем в контейнер для порядка
        obj.transform.SetParent(_poolContainer);

        // Возвращаем в нужную очередь
        if (_poolDictionary.TryGetValue(pooledObj.PrefabID, out var queue))
        {
            queue.Enqueue(obj);
        }
        else
        {
            // Ситуация почти невозможная, но на всякий случай создадим очередь
            var newQueue = new Queue<GameObject>();
            newQueue.Enqueue(obj);
            _poolDictionary.Add(pooledObj.PrefabID, newQueue);
        }
    }
}

// Маленький вспомогательный класс-метка.
// Висит на каждом объекте, который прошел через пул.
public class PooledObject : MonoBehaviour
{
    public int PrefabID;
}