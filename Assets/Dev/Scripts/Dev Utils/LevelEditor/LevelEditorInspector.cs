using Sarabande.Levels;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelEditor))]
public partial class LevelEditorInspector : Editor
{
    public static string versionName = "0.1c";


    private LevelEditor editor;

    void OnEnable()
    {
        editor = (LevelEditor)target;
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    void OnUndoRedo()
    {
        editor.Refresh();
        editor.UpdateFlags();
        SceneView.RepaintAll(); // Rafraîchit la scène
    }

    // Pour l'Inspector UI
    public override void OnInspectorGUI()
    {

        serializedObject.Update();
        GUI.contentColor = Color.white;

        EditorGUILayout.LabelField($"♥ ♥ ♥  GROOVY Level Editor - prototype v{versionName}  ♥ ♥ ♥", EditorStyles.centeredGreyMiniLabel);

        GUIStyle windowStyle = new GUIStyle(GUI.skin.window);
        windowStyle.padding = new RectOffset(10, 10, 10, 10);
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(10, 10, 10, 10);
        SerializedProperty data = serializedObject.FindProperty("levelData");
        SerializedProperty copy = serializedObject.FindProperty("levelDataCopy");


        EditorGUILayout.BeginVertical(windowStyle);
        EditorGUILayout.LabelField("File", EditorStyles.boldLabel);




        if (editor.levelData == null)
        {
            if (editor.hotCopyCreated)
            {
                DrawLinkBrokenWarning();
            }
            else
            {
                DrawNoFile();
            }
        }
        else
        {

            if (!editor.hotCopyCreated)
            {
                DrawImportMessage();
            }
            else
            {
                DrawFileLoaded();
            }
            
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();



        if (editor.hotCopyCreated == true && editor.dataCopy != null)
        {

            DrawToolBar();
        }

        serializedObject.ApplyModifiedProperties();
        editor.UpdateFlags();
    }

    void DisplayFilterButton(string label, string tooltip, DisplayFilter flag)
    {
        Color activateColor = new Color(0.5f, 0.8f, 0.5f);
        Color desactivated = new Color(0.8f, 0.5f, 0.5f);

        GUIContent content = new GUIContent(label, tooltip);

        bool isActive = editor.displayFilter.HasFlag(flag);
        GUI.backgroundColor = isActive ? activateColor : desactivated;

        if (GUILayout.Button(content, GUILayout.Height(20)))
        {
            editor.ChangeDisplayFlag(flag);
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = Color.white;
    }
    private void DrawToolBar()
    {
        GUIStyle windowStyle = new GUIStyle(GUI.skin.window);
        windowStyle.padding = new RectOffset(10, 10, 10, 10);
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(10, 4, 10, 4);

        EditorGUILayout.BeginVertical(windowStyle);

        EditorGUILayout.BeginVertical(boxStyle);

        EditorGUILayout.BeginHorizontal();

        // Bouton All
        bool allActive = editor.displayFilter == DisplayFilter.Everything;
        GUI.backgroundColor = allActive ? Color.cyan : Color.gray;

        if (GUILayout.Button("All", GUILayout.Height(20)))
        {
            editor.displayFilter = allActive ? DisplayFilter.None : DisplayFilter.Everything;
        }

        GUI.backgroundColor = Color.white;

        DisplayFilterButton("O", "Obstacles", DisplayFilter.Obstacles);
        DisplayFilterButton("L", "Listeners", DisplayFilter.Listeners);
        DisplayFilterButton("T", "Triggerables", DisplayFilter.Triggerables);
        DisplayFilterButton("T.L", "Trigger Links", DisplayFilter.TriggerLinks);


        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
        // Tes boutons, toolbar, etc.
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

        // Toolbar avec sélection automatique
        EditorGUILayout.BeginHorizontal();

        Color[] toolColors = {
            new Color(0.5f, 1f, 0.5f),  // Place = vert
            new Color(1f, 0.5f, 0.5f),  // Remove = rouge
            new Color(0.5f, 0.8f, 1f),   // Select = bleu
            new Color(0.5f, 0.5f, 0.5f)   // Select = bleu
        };

        for (int i = 0; i < 4; i++)
        {
            bool isSelected = (int)editor.currentTool == i;

            GUI.backgroundColor = isSelected ? toolColors[i] : Color.white;

            if (GUILayout.Button(((EditorToolType)i).ToString(), GUILayout.Height(30)))
            {
                Undo.RecordObject(editor, "Tool Change");
                editor.currentTool = (EditorToolType)i;
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        switch (editor.currentTool)
        {
            case EditorToolType.Edit: SelectToolInspector(); break;
            case EditorToolType.Misc: MiscInspector(); break;
                //case LevelEditor.ToolType.Remove: RemoveTool(e, gridPos); break;
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();

    }

    private void MiscInspector()
    {

        SerializedProperty dataCopyProp = serializedObject.FindProperty("dataCopy");
        SerializedObject dataCopySO = new SerializedObject(dataCopyProp.objectReferenceValue);

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(10, 4, 10, 4);

        EditorGUILayout.BeginVertical(boxStyle);

        /*EditorGUILayout.LabelField("Level Dimensions:", EditorStyles.boldLabel);
        GUI.contentColor = Color.gray;
        EditorGUILayout.LabelField("X => Level's Width || Y => Level's Height", EditorStyles.miniLabel);*/
        GUI.contentColor = Color.white;

        SerializedProperty widthLevel = dataCopySO.FindProperty("width");
        SerializedProperty heightlevel = dataCopySO.FindProperty("height");
        SerializedProperty discosSequences = dataCopySO.FindProperty("discoSequencesConfigs");

        EditorGUILayout.PropertyField(widthLevel);
        EditorGUILayout.PropertyField(heightlevel);
        EditorGUILayout.PropertyField(discosSequences.GetArrayElementAtIndex(0));

        EditorGUILayout.Space();
        dataCopySO.ApplyModifiedProperties();
        editor.Updatebounds();
        editor.UpdateLinks();


        EditorGUILayout.EndVertical();
    }

    private void SelectToolInspector()
    {

        if (editor.objectIsSelected)
        {

            // Récupère via SerializedProperty
            string basePath = editor.selectedObjectType switch
            {
                SelectedObjectType.Obstacle => "obstacles",
                SelectedObjectType.Listener => "listeners",
                SelectedObjectType.Triggerable => "triggerables",

                _ => null
            };


            if (basePath != null)
            {
                SerializedProperty dataCopyProp = serializedObject.FindProperty("dataCopy");
                if (dataCopyProp != null && dataCopyProp.objectReferenceValue != null)
                {
                    SerializedObject dataCopySO = new SerializedObject(dataCopyProp.objectReferenceValue);

                    SerializedProperty listProp = dataCopySO.FindProperty(basePath);

                    SerializedProperty itemProp = listProp.GetArrayElementAtIndex(editor.selectedObjectIndex);

                    if (itemProp != null)
                    {
                        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
                        boxStyle.padding = new RectOffset(10, 10, 10, 10);
                        EditorGUILayout.BeginVertical(boxStyle);

                        EditorGUILayout.LabelField($"Selected {editor.selectedObjectType.ToString()} :", EditorStyles.boldLabel);

                        switch (editor.selectedObjectType)
                        {
                            case SelectedObjectType.Obstacle:
                        
                                InspectObstacle(itemProp); break;
                            case SelectedObjectType.Listener: InspectListener(itemProp); break;
                            case SelectedObjectType.Triggerable: InspectTriggerable(itemProp); break;
                        }

                        EditorGUILayout.EndVertical();
                    }
                    Undo.RecordObject(editor.dataCopy, "Inspect Obj");
                    dataCopySO.ApplyModifiedProperties();

                    editor.UpdateLinks();
                    editor.UpdateFlags();

                }



            }
        }
        else
        {
            EditorGUILayout.LabelField("No Object Selected - Click on an object to see its properties", EditorStyles.helpBox);
        }
    }
    private void InspectTriggerable(SerializedProperty item)
    {

        item.isExpanded = true;
        EditorGUILayout.PropertyField(item, GUIContent.none, true);

    }

    private void InspectListener(SerializedProperty item)
    {
        item.isExpanded = true;
        EditorGUILayout.PropertyField(item, GUIContent.none,true);
    }
    private void InspectObstacle(SerializedProperty item)
    {

        EditorGUILayout.Space();

        SerializedProperty cell = item.FindPropertyRelative("cell");
        SerializedProperty type = item.FindPropertyRelative("type");
        SerializedProperty dir = item.FindPropertyRelative("thinWallDirection");

        EditorGUILayout.PropertyField(type, GUIContent.none);

        EditorGUILayout.PropertyField(cell, GUIContent.none);

        EditorGUILayout.BeginHorizontal();

        if (type.intValue == (int)ObstacleData.ObstacleType.ThinWall)
        {
            string[] dirLabels = { "↑", "→", "↓", "←" };
            int currentDir = dir.intValue; // ou dir.enumValueIndex si c'est un enum

            int newDir = GUILayout.Toolbar(currentDir, dirLabels, GUILayout.Width(120));

            if (newDir != currentDir)
            {
                dir.intValue = newDir; // ou dir.enumValueIndex = newDir
            }

        }

        EditorGUILayout.EndHorizontal();

    }

    private void InspectMessage(SerializedProperty item)
    {
        EditorGUILayout.PropertyField(item, GUIContent.none);
    }
    private void InspectArrowTrap(SerializedProperty item)
    {
        EditorGUILayout.PropertyField(item, GUIContent.none);
    }
    private void InspectGate(SerializedProperty item)
    {
        EditorGUILayout.PropertyField(item, GUIContent.none);
    }

    private void InspectTriggerObject(SerializedProperty item)
    {
        EditorGUILayout.PropertyField(item, GUIContent.none);
    }
   

}
