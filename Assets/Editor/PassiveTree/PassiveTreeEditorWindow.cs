using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements; // Важно для Toolbar и InspectorElement
using Scripts.Skills.PassiveTree;
using System.Linq;

namespace Scripts.Editor.PassiveTree
{
    public class PassiveTreeEditorWindow : EditorWindow
    {
        private PassiveSkillTreeGraphView _graphView;
        private PassiveSkillTreeSO _currentTree;
        private VisualElement _inspectorContainer; // Контейнер для правой панели

        [MenuItem("Tools/Passive Tree Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<PassiveTreeEditorWindow>();
            window.titleContent = new GUIContent("Passive Tree");
        }

        [UnityEditor.Callbacks.OnOpenAsset(1)]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            if (Selection.activeObject is PassiveSkillTreeSO tree)
            {
                OpenWindow();
                var window = GetWindow<PassiveTreeEditorWindow>();
                window.LoadTree(tree);
                return true;
            }
            return false;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;

            // 1. Toolbar (Верхняя панель)
            var toolbar = new Toolbar();
            var saveBtn = new ToolbarButton(() => 
            { 
                AssetDatabase.SaveAssets(); 
                Debug.Log("Tree Saved!");
            }) { text = "Save Asset" };
            toolbar.Add(saveBtn);
            root.Add(toolbar);

            // 2. Split View (Разделитель экрана)
            var splitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
            root.Add(splitView);
            // Растягиваем на все окно под тулбаром
            splitView.style.flexGrow = 1; 

            // 3. Левая панель (Граф)
            _graphView = new PassiveSkillTreeGraphView
            {
                style = { flexGrow = 1 }
            };
            // Подписываемся на изменение выбора внутри графа
            _graphView.OnNodeSelected = OnNodeSelectionChanged;
            
            splitView.Add(_graphView);

            // 4. Правая панель (Инспектор)
            _inspectorContainer = new ScrollView(ScrollViewMode.Vertical);
            _inspectorContainer.style.paddingLeft = 10;
            _inspectorContainer.style.paddingRight = 10;
            _inspectorContainer.style.paddingTop = 10;
            
            // Добавляем заголовок "Settings"
            var label = new Label("Node Settings") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 10 } };
            _inspectorContainer.Add(label);

            splitView.Add(_inspectorContainer);

            // Загрузка если уже есть
            if (_currentTree != null) LoadTree(_currentTree);
        }

        private void LoadTree(PassiveSkillTreeSO tree)
        {
            _currentTree = tree;
            if (_graphView != null)
            {
                _graphView.PopulateView(_currentTree);
            }
        }

        // Этот метод вызывается из GraphView (нужно добавить вызов туда!)
        private void OnNodeSelectionChanged(PassiveSkillTreeNode node)
        {
            _inspectorContainer.Clear();
            
            var titleLabel = new Label("Node Settings") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 10 } };
            _inspectorContainer.Add(titleLabel);

            if (node == null) return;

            // 1. Создаем SerializedObject для всего дерева
            var so = new SerializedObject(_currentTree);
            var nodesProp = so.FindProperty("Nodes");

            // 2. Ищем индекс нашего нода в списке
            int index = _currentTree.Nodes.IndexOf(node.Data);
            if (index < 0) return;

            // 3. Получаем свойство конкретного элемента
            var nodeProp = nodesProp.GetArrayElementAtIndex(index);

            // --- FIX: Используем PropertyField вместо InspectorElement ---
            var propertyField = new PropertyField(nodeProp);
            
            // Важно: Привязываем (Bind) поле к сериализованному объекту, чтобы данные отображались
            propertyField.Bind(so);

            // 4. Подписываемся на изменения
            // TrackPropertyValue - это метод расширения UnityEditor.UIElements
            propertyField.TrackPropertyValue(nodeProp, (prop) => 
            {
                // Применяем изменения (хотя Bind делает это автоматически, Apply гарантирует запись)
                so.ApplyModifiedProperties(); 
                
                // Обновляем визуал нода на графе (цвет, заголовок)
                node.RefreshVisuals();
            });

            _inspectorContainer.Add(propertyField);
        }
    }
}