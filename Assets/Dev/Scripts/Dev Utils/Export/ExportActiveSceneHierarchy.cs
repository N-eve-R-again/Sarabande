// Assets/Editor/ExportActiveSceneHierarchy.cs
// Unity Editor script — export the active scene hierarchy with all components & serialized settings to a single .txt

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

public static class ExportActiveSceneHierarchy
{
    [MenuItem("Tools/Export/Export Active Scene Hierarchy to .txt")]
    public static void ExportHierarchyToTxt()
    {
        try
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                EditorUtility.DisplayDialog("Export Scene Hierarchy", "Aucune scène active chargée.", "OK");
                return;
            }

            // Choix du fichier de sortie
            string defaultName = $"Scene_{scene.name}_Hierarchy_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
            string savePath = EditorUtility.SaveFilePanel(
                "Save hierarchy export as .txt",
                Application.dataPath,
                defaultName,
                "txt"
            );
            if (string.IsNullOrEmpty(savePath))
                return;

            // Récup racines + tri par ordre hiérarchique d'Unity
            var roots = scene.GetRootGameObjects();
            Array.Sort(roots, (a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

            var sb = new StringBuilder(1024 * 1024);
            sb.AppendLine("==== Unity Active Scene Hierarchy Export ====");
            sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Scene: {scene.name} ({scene.path})");
            sb.AppendLine(new string('=', 100));
            sb.AppendLine();

            long totalNodes = CountNodes(roots);
            long processed = 0;

            foreach (var root in roots)
            {
                DumpGameObjectRecursive(root, sb, 0, ref processed, totalNodes);
            }

            EditorUtility.ClearProgressBar();
            File.WriteAllText(savePath, sb.ToString(), new UTF8Encoding(false));

            EditorUtility.RevealInFinder(savePath);
            EditorUtility.DisplayDialog("Export Scene Hierarchy", $"Export terminé.\nFichier :\n{savePath}", "OK");
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError("[ExportActiveSceneHierarchy] " + ex);
            EditorUtility.DisplayDialog("Export Scene Hierarchy - Erreur", ex.Message, "OK");
        }
    }

    private static long CountNodes(GameObject[] roots)
    {
        long c = 0;
        foreach (var r in roots)
            c += CountChildren(r.transform);
        return c;
    }
    private static long CountChildren(Transform t)
    {
        long c = 1;
        for (int i = 0; i < t.childCount; i++)
            c += CountChildren(t.GetChild(i));
        return c;
    }

    private static void DumpGameObjectRecursive(GameObject go, StringBuilder sb, int indent, ref long processed, long total)
    {
        processed++;
        if (total > 0)
            EditorUtility.DisplayProgressBar("Exporting Scene Hierarchy", go.name, (float)processed / total);

        string ind = new string(' ', indent * 2);
        string path = GetHierarchyPath(go.transform);

        sb.AppendLine(ind + new string('-', Mathf.Clamp(80 - indent * 2, 10, 80)));
        sb.AppendLine($"{ind}GO: {path}");
        sb.AppendLine($"{ind}ActiveSelf: {go.activeSelf}, ActiveInHierarchy: {go.activeInHierarchy}");
        sb.AppendLine($"{ind}Layer: {LayerMask.LayerToName(go.layer)} ({go.layer}), Tag: {go.tag}");
        sb.AppendLine($"{ind}Static: {go.isStatic}");
        sb.AppendLine($"{ind}HideFlags: {go.hideFlags}");

        // Transform (raccourci lisible)
        var tr = go.transform;
        sb.AppendLine($"{ind}Transform:");
        sb.AppendLine($"{ind}  LocalPosition: {tr.localPosition}");
        sb.AppendLine($"{ind}  LocalRotation: {tr.localRotation.eulerAngles} (Euler)");
        sb.AppendLine($"{ind}  LocalScale: {tr.localScale}");

        // Tous les components
        var comps = go.GetComponents<Component>();
        if (comps != null && comps.Length > 0)
        {
            sb.AppendLine($"{ind}Components ({comps.Length}):");
            for (int i = 0; i < comps.Length; i++)
            {
                var comp = comps[i];
                DumpComponent(comp, sb, indent + 1);
            }
        }
        else
        {
            sb.AppendLine($"{ind}Components (0)");
        }

        // Enfants (triés par SiblingIndex)
        for (int i = 0; i < tr.childCount; i++)
        {
            var child = tr.GetChild(i).gameObject;
            DumpGameObjectRecursive(child, sb, indent + 1, ref processed, total);
        }
    }

    private static void DumpComponent(Component comp, StringBuilder sb, int indent)
    {
        string ind = new string(' ', indent * 2);

        if (comp == null)
        {
            sb.AppendLine($"{ind}- [Missing Script]");
            return;
        }

        var type = comp.GetType();
        sb.AppendLine($"{ind}- {type.Name}");

        // Cas commun : si le comp a "enabled"
        var enabledProp = type.GetProperty("enabled");
        if (enabledProp != null && enabledProp.PropertyType == typeof(bool))
        {
            try
            {
                bool enabled = (bool)enabledProp.GetValue(comp, null);
                sb.AppendLine($"{ind}  enabled: {enabled}");
            }
            catch { /* ignore */ }
        }

        try
        {
            var so = new SerializedObject(comp);
            var it = so.GetIterator();

            // On parcourt toutes les propriétés visibles
            bool enterChildren = true;
            while (it.NextVisible(enterChildren))
            {
                // On évite de répéter "m_Script" (référence à l'asset C#)
                if (it.propertyPath == "m_Script")
                {
                    enterChildren = false;
                    continue;
                }

                string line = $"{ind}  {PrettyPropName(it)}: {SerializedPropertyToString(it)}";
                sb.AppendLine(line);
                enterChildren = false; // NextVisible(false) pour avancer propriété par propriété
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"{ind}  [Error reading serialized properties: {ex.GetType().Name}: {ex.Message}]");
        }
    }

    private static string PrettyPropName(SerializedProperty p)
    {
        // Indentation basée sur la profondeur de la propriété
        return new string(' ', p.depth * 2) + p.displayName;
    }

    private static string SerializedPropertyToString(SerializedProperty p)
    {
        switch (p.propertyType)
        {
            case SerializedPropertyType.Integer: return p.intValue.ToString();
            case SerializedPropertyType.Boolean: return p.boolValue.ToString();
            case SerializedPropertyType.Float: return p.floatValue.ToString("G9");
            case SerializedPropertyType.String: return Quote(p.stringValue);
            case SerializedPropertyType.Color: return p.colorValue.ToString();
            case SerializedPropertyType.ObjectReference:
                return p.objectReferenceValue ? $"{p.objectReferenceValue.name} ({p.objectReferenceValue.GetType().Name})" : "null";
            case SerializedPropertyType.LayerMask: return p.intValue.ToString();
            case SerializedPropertyType.Enum: return $"{p.enumDisplayNames[p.enumValueIndex]} ({p.enumValueIndex})";
            case SerializedPropertyType.Vector2: return p.vector2Value.ToString();
            case SerializedPropertyType.Vector3: return p.vector3Value.ToString();
            case SerializedPropertyType.Vector4: return p.vector4Value.ToString();
            case SerializedPropertyType.Rect: return p.rectValue.ToString();
            case SerializedPropertyType.ArraySize: return p.intValue.ToString();
            case SerializedPropertyType.Character: return ((char)p.intValue).ToString();
            case SerializedPropertyType.AnimationCurve: return $"Curve(keys:{p.animationCurveValue?.length ?? 0})";
            case SerializedPropertyType.Bounds: return p.boundsValue.ToString();
#if UNITY_2020_1_OR_NEWER
            case SerializedPropertyType.Quaternion: return p.quaternionValue.eulerAngles.ToString() + " (Euler)";
            case SerializedPropertyType.ExposedReference:
                return p.exposedReferenceValue ? $"{p.exposedReferenceValue.name} ({p.exposedReferenceValue.GetType().Name})" : "null";
            case SerializedPropertyType.FixedBufferSize: return p.intValue.ToString();
            case SerializedPropertyType.Vector2Int: return p.vector2IntValue.ToString();
            case SerializedPropertyType.Vector3Int: return p.vector3IntValue.ToString();
            case SerializedPropertyType.RectInt: return p.rectIntValue.ToString();
            case SerializedPropertyType.BoundsInt: return p.boundsIntValue.ToString();
#endif
            default:
                // Pour les types "Generic" (noeuds/arrays), on affiche une info courte
                if (p.isArray && p.propertyType == SerializedPropertyType.Generic)
                    return $"Array(size:{p.arraySize})";
                return "[unsupported or generic]";
        }
    }

    private static string GetHierarchyPath(Transform t)
    {
        var stack = new List<string>(8);
        while (t != null)
        {
            stack.Add(t.name);
            t = t.parent;
        }
        stack.Reverse();
        return string.Join("/", stack);
    }

    private static string Quote(string s)
    {
        if (s == null) return "null";
        // Limiter les chaînes très longues pour garder le fichier lisible (ajuste si besoin)
        const int max = 2000;
        if (s.Length > max) s = s.Substring(0, max) + "…";
        return "\"" + s.Replace("\n", "\\n").Replace("\r", "\\r") + "\"";
    }
}
#endif
