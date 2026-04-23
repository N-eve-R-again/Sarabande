using Sarabande.Actors;
using Sarabande.Levels;
using Sarabande.Listeners;
using Sarabande.Obstacles;
using Sarabande.Triggerables;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static UnityEditor.Progress;

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

        if (Application.isPlaying) return;
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

        EditorGUILayout.Space();
        DrawDisplayLayerMasks();

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        if (editor.hotCopyCreated == true && editor.dataCopy != null)
        {

            DrawToolBar();
        }

        serializedObject.ApplyModifiedProperties();
        editor.UpdateFlags();
    }
    void DrawDisplayLayerMasks()
    {
        EditorGUILayout.LabelField("Display in Scene View", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.BeginHorizontal();

        // Bouton All
        bool allActive = editor.displayFilter == DisplayFilter.Everything;
        GUI.backgroundColor = allActive ? Color.cyan : Color.gray;

        if (GUILayout.Button("All", GUILayout.Height(20)))
        {
            editor.displayFilter = allActive ? DisplayFilter.None : DisplayFilter.Everything;
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = Color.white;

        DisplayFilterButton("O", "Obstacles", DisplayFilter.Obstacles);
        DisplayFilterButton("L", "Listeners", DisplayFilter.Listeners);
        DisplayFilterButton("T", "Triggerables", DisplayFilter.Triggerables);
        DisplayFilterButton("T.L", "Trigger Links", DisplayFilter.TriggerLinks);


        EditorGUILayout.EndHorizontal();

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
        boxStyle.padding = new RectOffset(2, 2, 2, 2);

        EditorGUILayout.BeginVertical(windowStyle);

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
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        switch (editor.currentTool)
        {
            case EditorToolType.Edit: SelectToolInspector();
                break;
            case EditorToolType.Erase: EraseInspector(); 
                break;
            case EditorToolType.Misc: MiscInspector(); break;
            case EditorToolType.Place:
                DrawPlaceToolInspector(); break;
                //case LevelEditor.ToolType.Remove: RemoveTool(e, gridPos); break;
        }

        EditorGUILayout.Space();

    }
    private void EraseInspector()
    {
        GUIStyle windowStyle = new GUIStyle(GUI.skin.window);
        windowStyle.padding = new RectOffset(20, 20, 10, 10);

        EditorGUILayout.BeginVertical(windowStyle);
        EditorGUILayout.LabelField("Erase Tool - Click on a Object to Erase it - Can't remove Player or Exit ", EditorStyles.helpBox);
        
        EditorGUILayout.EndVertical();
    }
    private void MiscInspector()
    {
        GUI.backgroundColor = Color.gray * 1.75f;
        SerializedProperty dataCopyProp = serializedObject.FindProperty("dataCopy");
        SerializedObject dataCopySO = new SerializedObject(dataCopyProp.objectReferenceValue);

        GUIStyle windowStyle = new GUIStyle(GUI.skin.window);
        windowStyle.padding = new RectOffset(20, 20, 10, 10);

        EditorGUILayout.BeginVertical(windowStyle);


        EditorGUILayout.LabelField("Level Settings:", EditorStyles.boldLabel);
        DrawSeparator(false, false);

        SerializedProperty widthLevel = dataCopySO.FindProperty("width");
        SerializedProperty heightlevel = dataCopySO.FindProperty("height");
        SerializedProperty discosSequences = dataCopySO.FindProperty("discoSequencesConfigs");
        SerializedProperty exit = dataCopySO.FindProperty("exit");

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(10, 5, 5, 10);

        EditorGUILayout.BeginVertical(boxStyle);

        EditorGUILayout.PropertyField(widthLevel);
        EditorGUILayout.PropertyField(heightlevel);

        EditorGUILayout.EndVertical();
        DrawSeparator(false, false);

        EditorGUILayout.BeginVertical(boxStyle);
        exit.isExpanded = true;
        EditorGUILayout.PropertyField(exit);

        EditorGUILayout.EndVertical();


        DrawSeparator(false, false);

        EditorGUILayout.BeginVertical(boxStyle);

        if (discosSequences.arraySize > 0)
        {
            EditorGUILayout.PropertyField(discosSequences.GetArrayElementAtIndex(0));
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.white;
        dataCopySO.ApplyModifiedProperties();
        editor.Updatebounds();
        editor.UpdateLinks();
        editor.UpdateFlags();


        EditorGUILayout.EndVertical();
    }
    private void DrawPlaceToolInspector()
    {
        GUIStyle windowStyle = new GUIStyle(GUI.skin.window);
        windowStyle.padding = new RectOffset(20, 20, 10, 20);
        GUI.backgroundColor = Color.gray * 1.75f;
        EditorGUILayout.BeginVertical(windowStyle);

        EditorGUILayout.BeginHorizontal();

        for (int i = 0; i < 4; i++)
        {
            bool isSelected = (int)editor.placeObjectType == i;

            GUI.backgroundColor = isSelected ? Color.green : Color.gray;

            if (GUILayout.Button(((PlaceObjectType)i).ToString(), GUILayout.Height(25)))
            {
                Undo.RecordObject(editor, "Object Place Type Change");
                editor.placeObjectType = (PlaceObjectType)i;
                editor.selectedPlaceTypeIndex = 0;
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        DrawSeparator();

        switch (editor.placeObjectType)
        {
            case PlaceObjectType.Obstacle:
                ObstaclePlaceTool();
                break;
            case PlaceObjectType.Listener:
                ListenerPlaceTool();
                break;
            case PlaceObjectType.Triggerable:
                TriggerablePlaceTool();
                break;
            case PlaceObjectType.Actor:
                EditorGUILayout.LabelField("Actor", EditorStyles.boldLabel);
                break;
        }

        EditorGUILayout.EndVertical();
        GUI.backgroundColor =Color.white;
        return;

    }

    private void DrawPlaceBrushes(List<string> names)
    {
        EditorGUILayout.BeginHorizontal();


        for (int i = 0; i < names.Count; i++)
        {
            bool isSelected = editor.selectedPlaceTypeIndex == i;
            GUI.backgroundColor = isSelected ? Color.green : Color.gray;

            string nametype = names[i];
            string label = nametype;

            if (GUILayout.Button(label, GUILayout.Height(30)))
            {
                editor.selectedPlaceTypeIndex = i;
            }

            GUI.backgroundColor = Color.white;
            
        }
        EditorGUILayout.EndHorizontal();
    }

    private void ListenerPlaceTool()
    {
        editor.GetListenerBrushes();
        DrawPlaceBrushes(editor.listenerDummiesNames);
        DrawSeparator(false, false);

        SerializedProperty listprop = serializedObject.FindProperty("listenerDummies");
        SerializedProperty currentBrush = listprop.GetArrayElementAtIndex(editor.selectedPlaceTypeIndex);

        currentBrush.isExpanded = true;

        switch (editor.listenerDummies[editor.selectedPlaceTypeIndex])
        {
            case TriggerObjectConfig:
                InspectTriggerObject(currentBrush, false); 
                break;
            case FakeWallData:
                InspectFakeWall(currentBrush, false); 
                break;
            default:
                EditorGUILayout.PropertyField(currentBrush, true);
                break;
        }

    }

    private void TriggerablePlaceTool()
    {
        editor.GetTriggerableBrushes();
        DrawPlaceBrushes(editor.triggerableDummiesNames);
        DrawSeparator(false, false);

        SerializedProperty listprop = serializedObject.FindProperty("triggerableDummies");
        SerializedProperty currentBrush = listprop.GetArrayElementAtIndex(editor.selectedPlaceTypeIndex);
        currentBrush.isExpanded = true;

        EditorGUILayout.PropertyField(currentBrush, true);


    }

    private void ObstaclePlaceTool()
    {
        SerializedProperty currentBrush = serializedObject.FindProperty("obstacleDummy");

        currentBrush.isExpanded = true;
        InspectObstacle(currentBrush, false);

    }

    private void InstantMoveButton()
    {
        bool isSelected = editor.movingSubToolActivated;

        string label = isSelected ? "Waiting..." : "  Move   ";
        string tooltip = isSelected ? "Waiting for User to Click on a Cell in SceneView" : "Move Selected Object To Cursor";

        GUIContent content = new GUIContent(label, tooltip);
        GUI.backgroundColor = isSelected ? Color.blue : Color.white;
        if (GUILayout.Button(content, GUILayout.Height(17)))
        {
            Debug.Log("Toolsub");
            editor.movingSubToolActivated = !editor.movingSubToolActivated;
        }

        GUI.backgroundColor = Color.white;
    }

    private void SelectToolInspector()
    {
        GUI.backgroundColor = Color.gray * 1.75f;
        GUIStyle windowStyle = new GUIStyle(GUI.skin.window);
        windowStyle.padding = new RectOffset(20, 20, 10, 20);

        EditorGUILayout.BeginVertical(windowStyle);

        if (editor.objectIsSelected)
        {
            if (editor.selectedObjectType == SelectedObjectType.Hero)
            {
                SerializedProperty dataCopyProp = serializedObject.FindProperty("dataCopy");
                if (dataCopyProp != null && dataCopyProp.objectReferenceValue != null)
                {
                    SerializedObject dataCopySO = new SerializedObject(dataCopyProp.objectReferenceValue);

                    SerializedProperty itemProp = dataCopySO.FindProperty("hero");


                    if (itemProp != null)
                    {
                        EditorGUILayout.PropertyField(itemProp, GUIContent.none, true);

                        Undo.RecordObject(editor.dataCopy, "Inspect Obj");
                        dataCopySO.ApplyModifiedProperties();

                        editor.UpdateLinks();
                        editor.UpdateFlags();
                    }
                }
                EditorGUILayout.EndVertical();
                return;
            }

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

                        switch (editor.selectedObjectType)
                        {
                            case SelectedObjectType.Obstacle:
                                InspectObstacle(itemProp); break;
                            case SelectedObjectType.Listener: 
                                InspectListener(itemProp); break;
                            case SelectedObjectType.Triggerable: 
                                InspectTriggerable(itemProp); break;
                        }

                        //EditorGUILayout.EndVertical();
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
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndVertical();
    }
    private void DrawCellField(SerializedProperty item)
    {


        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(10, 5, 5, 10);

        EditorGUILayout.BeginVertical(boxStyle);
        SerializedProperty cell = item.FindPropertyRelative("cell");


        EditorGUILayout.LabelField($"Cell:", EditorStyles.label);

        EditorGUILayout.BeginHorizontal();

        DrawDisabledField(cell);

        if (editor.currentTool == EditorToolType.Edit)
        {
            InstantMoveButton();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawTitle(string title)
    {
        EditorGUILayout.LabelField($"Trigger Object", EditorStyles.boldLabel);
        DrawSeparator(false, true);
    }

    private void InspectTriggerable(SerializedProperty item)
    {

        item.isExpanded = true;
        EditorGUILayout.PropertyField(item, GUIContent.none, true);

    }

    private void InspectListener(SerializedProperty item)
    {
        item.isExpanded = true;
        switch (editor.dataCopy.listeners[editor.selectedObjectIndex])
        {
            case FakeWallData:
                InspectFakeWall(item, true);
                break;
            case TriggerObjectConfig:
                InspectTriggerObject(item);
                break;
            case MessageConfig:
                InspectMessage(item);
                break;
            default:
                EditorGUILayout.LabelField($"Selected {editor.dataCopy.listeners[editor.selectedObjectIndex].GetType().Name} :", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(item, GUIContent.none, true);
                break;
        }

    }
    private void InspectObstacle(SerializedProperty item, bool title = true)
    {
        if (title) DrawTitle($"Obstacle");

        DrawCellField(item);

        SerializedProperty type = item.FindPropertyRelative("type");
        SerializedProperty dir = item.FindPropertyRelative("thinWallDirection");

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(type, GUIContent.none);

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
        EditorGUILayout.LabelField($"Selected Message Collectible :", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(item, GUIContent.none, true);
    }
    private void InspectArrowTrap(SerializedProperty item)
    {
        EditorGUILayout.PropertyField(item, GUIContent.none);
    }
    private void InspectGate(SerializedProperty item)
    {
        EditorGUILayout.PropertyField(item, GUIContent.none);
    }

    private void InspectFakeWall(SerializedProperty item, bool title = false)
    {
        if (title) DrawTitle($"Fake Wall");

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(10, 5, 5, 10);

        DrawCellField(item);

    }

    private void InspectTriggerObject(SerializedProperty item, bool title = true)
    {
        if (title) DrawTitle($"Trigger Object");

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(10, 5,5, 10);
        
        SerializedProperty type = item.FindPropertyRelative("type");
        SerializedProperty triggerkeys = item.FindPropertyRelative("triggerKeys");
        SerializedProperty rearmType = item.FindPropertyRelative("rearmType");
        SerializedProperty timerR = item.FindPropertyRelative("timeToRearm");
        SerializedProperty attached = item.FindPropertyRelative("attachedTo");


        DrawCellField(item);
        //EditorGUILayout.PropertyField(cell, GUIContent.none);


        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(triggerkeys);

        EditorGUILayout.BeginVertical(boxStyle);

        EditorGUILayout.LabelField($"Type:", EditorStyles.boldLabel);

        int currentType = type.intValue;
        string[] typeLabels = { "Invisible", "Pad", "Lever"};
        int newType = GUILayout.Toolbar(currentType, typeLabels);

        if (newType != currentType)
        {
            type.intValue = newType; // ou dir.enumValueIndex = newDir
        }

        if (type.intValue == (int)TriggerObjectConfig.Type.Lever)
        {
            EditorGUILayout.LabelField($"Attached to side:", EditorStyles.miniLabel);
            string[] dirLabels = { "↑", "→", "↓", "←" };
            int currentDir = attached.intValue; // ou dir.enumValueIndex si c'est un enum

            int newDir = GUILayout.Toolbar(currentDir, dirLabels, GUILayout.Width(120));
            if (newDir != currentDir) attached.intValue = newDir; // ou dir.enumValueIndex = newDir
        }


        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();


        EditorGUILayout.BeginVertical(boxStyle);

        EditorGUILayout.LabelField($"Rearm Type:", EditorStyles.boldLabel);

        int currentRearmType = rearmType.intValue;
        string[] RearmTypeLabels = { "One Shot", "CallBack", "Timer", "Instant"};
        string[] RearmTypeToolTips = { "Only activates once", "Rearms only after a callback from a triggerable", "Automaticaly rearms itself after some time", "Instantaneous Reload" };


        int newRearmType = GUILayout.Toolbar(currentRearmType, RearmTypeLabels);

        if (newRearmType != currentRearmType)
        {
            rearmType.intValue = newRearmType; // ou dir.enumValueIndex = newDir
        }
        EditorGUILayout.LabelField(RearmTypeToolTips[currentRearmType], EditorStyles.helpBox);
        if (rearmType.intValue == (int)RearmType.Timer)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Time to rearm (seconds):", EditorStyles.miniLabel);
            EditorGUILayout.PropertyField(timerR, GUIContent.none);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }


}
