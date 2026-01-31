using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Skills.PassiveTree;
using UnityEditor.UIElements; // Для InspectorElement

namespace Scripts.Editor.PassiveTree
{
    public class PassiveTreeEditorWindow : EditorWindow
    {
        private PassiveSkillTreeGraphView _graphView;
        private PassiveSkillTreeSO _currentTree;

        [MenuItem("Tools/Passive Tree Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<PassiveTreeEditorWindow>();
            window.titleContent = new GUIContent("Passive Tree");
        }

        // Вызывается при открытии ассета двойным кликом (Удобно!)
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

            // 1. Создаем Toolbar
            var toolbar = new UnityEditor.UIElements.Toolbar();
            
            // Кнопка сохранения (хотя мы сохраняем авто, но пусть будет)
            var saveBtn = new UnityEditor.UIElements.ToolbarButton(() => 
            { 
                AssetDatabase.SaveAssets(); 
                Debug.Log("Tree Saved!");
            }) { text = "Save Data" };
            
            toolbar.Add(saveBtn);
            root.Add(toolbar);

            // 2. Создаем GraphView
            _graphView = new PassiveSkillTreeGraphView
            {
                style = { flexGrow = 1 }
            };
            root.Add(_graphView);
            
            // Если дерево уже выбрано, грузим
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

        // --- МАГИЯ ИНСПЕКТОРА ---
        // Когда мы выделяем что-то в графе, мы хотим видеть это в Инспекторе Unity
        private void OnSelectionChange()
        {
            // Этот метод вызывается Unity, когда меняется выделение в проекте/сцене.
            // Для GraphView нужна своя логика, но пока мы используем "хак":
            // Мы не будем рисовать кастомный инспектор внутри окна.
            // Вместо этого мы будем полагаться на то, что ноды сериализуются внутри SO.
            // Чтобы редактировать свойства нода (Template, UniqueModifiers), 
            // нам нужно будет выбрать сам ассет SO и найти в списке нужный элемент.
            
            // ПРОДВИНУТЫЙ ВАРИАНТ:
            // Чтобы редактировать нод кликом, нам нужно создать CustomEditor для NodeDefinition
            // или рисовать свойства прямо в GraphView (Blackboard).
            // Для старта самый простой способ: 
            // 1. Открываешь GraphView.
            // 2. Создаешь структуру.
            // 3. Чтобы настроить цифры -> идешь в Inspector самого SO-файла.
        }
    }
}