using System;
using UnityEditor;
using UnityEditorInternal.VR;
using UnityEngine;

public static class CustomStylesGUI
{
    public static void DrawSeparator(bool spaceBefore = false, bool spaceAfter = false)
    {
        if (spaceBefore) EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        if (spaceAfter) EditorGUILayout.Space();
    }

    public static void DrawTitle(string title, bool separator = true)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        if (!separator)
        {
            EditorGUILayout.Space();
            return;
        }
        DrawSeparator(false, true);
    }

    public static void DrawHelpBox(string text)
    {
        EditorGUILayout.LabelField(text, EditorStyles.helpBox);
    }

    public static void DrawLargeLabel(string text)
    {
        EditorGUILayout.LabelField(text, EditorStyles.largeLabel);
    }

    public static void ResetBGColor()
    {
        GUI.backgroundColor = Color.white;
    }
    public static void ResetContentColor()
    {
        GUI.contentColor = Color.white;
    }


}

public class StyleLibrary
{
    GUIStyle ClassicBox = GetClassicBox();

    public static GUIStyle GetClassicBox()
    {
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(10, 10, 10, 10);
        return boxStyle;
    }
}

public static class CustomButtonsGUI
{
    public static void DrawDisabledButton(string _buttonLabel)
    {
        GUI.enabled = false;
        GUILayout.Button(_buttonLabel);
        GUI.enabled = true;
    }

    public static void DrawCardinalDirection(SerializedProperty dir, string title = "")
    {
        if (title != "")
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniLabel);
        }

        string[] dirLabels = { "↑", "→", "↓", "←" };
        int currentDir = dir.intValue; // ou dir.enumValueIndex si c'est un enum

        int newDir = GUILayout.Toolbar(currentDir, dirLabels, GUILayout.Width(120));

        if (newDir != currentDir)
        {
            dir.intValue = newDir; // ou dir.enumValueIndex = newDir
        }
    }

}

public static class CustomFieldsGUI
{
    public static void DrawDisabledField(SerializedProperty property)
    {
        GUI.enabled = false;
        EditorGUILayout.PropertyField(property, GUIContent.none);

        GUI.enabled = true;
    }

    public static void DrawCellField(SerializedProperty item, LevelEditor editor)
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
            InstantMoveButton(editor);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    internal static void InstantMoveButton(LevelEditor editor)
    {
        bool isSelected = editor.movingSubToolActivated;

        string label = isSelected ? "Waiting..." : "  Move   ";
        string tooltip = isSelected ? "Waiting for User to Click on a Cell in SceneView" : "Move Selected Object To Cursor";

        GUIContent content = new GUIContent(label, tooltip);
        GUI.backgroundColor = isSelected ? Color.blue : Color.white;
        if (GUILayout.Button(content, GUILayout.Height(17))) editor.movingSubToolActivated = !editor.movingSubToolActivated;

        CustomStylesGUI.ResetBGColor();
    }

    public static void DrawConditionnalField(bool condition, SerializedProperty property, string title = "")
    {
        if (!condition) return;

        EditorGUILayout.BeginHorizontal();

        if(title != "") EditorGUILayout.LabelField(title, EditorStyles.miniLabel);
        EditorGUILayout.PropertyField(property, GUIContent.none);

        EditorGUILayout.EndHorizontal();
    }
}

