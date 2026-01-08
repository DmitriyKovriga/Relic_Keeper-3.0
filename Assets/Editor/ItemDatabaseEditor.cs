using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Scripts.Items;
using Scripts.Items.Affixes;

[CustomEditor(typeof(ItemDatabaseSO))]
public class ItemDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        ItemDatabaseSO database = (ItemDatabaseSO)target;

        GUILayout.Space(20);
        GUI.backgroundColor = Color.cyan;
        
        // Кнопка делает магию
        if (GUILayout.Button("Auto-Find & Generate IDs", GUILayout.Height(40)))
        {
            RefreshDatabase(database);
        }
        GUI.backgroundColor = Color.white;
        
        GUILayout.Space(10);
        EditorGUILayout.HelpBox($"Items: {database.AllItems?.Count ?? 0} | Affixes: {database.AllAffixes?.Count ?? 0}", MessageType.Info);
    }

    private void RefreshDatabase(ItemDatabaseSO db)
    {
        // 1. Предметы (оставляем как есть, у них ручные ID, но можно тоже автоматизировать)
        db.AllItems = FindAssetsByType<EquipmentItemSO>();
        
        // 2. Аффиксы - ГЕНЕРАЦИЯ ID
        var affixes = FindAssetsByType<ItemAffixSO>();
        
        foreach (var affix in affixes)
        {
            // Получаем путь: "Assets/ScriptableObjects/Affixes/Gloves/MaxLife_T1.asset"
            string path = AssetDatabase.GetAssetPath(affix);
            
            // Превращаем в читаемый ID: "ScriptableObjects/Affixes/Gloves/MaxLife_T1"
            // Убираем расширение и префикс Assets/ для чистоты
            string smartID = path.Replace("Assets/", "").Replace(".asset", "");

            // Если ID изменился (переместили файл) или был пуст - обновляем
            if (affix.UniqueID != smartID)
            {
                affix.UniqueID = smartID;
                EditorUtility.SetDirty(affix); // Помечаем, что файл изменился
            }
        }
        
        db.AllAffixes = affixes;

        // 3. Сохраняем всё
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets(); // Физически записываем новые ID в файлы аффиксов
        
        Debug.Log($"<color=green>[ItemDatabase]</color> Обновлено! Аффиксы получили уникальные ID на основе путей.");
    }

    private List<T> FindAssetsByType<T>() where T : ScriptableObject
    {
        List<T> assets = new List<T>();
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) assets.Add(asset);
        }
        return assets;
    }
}