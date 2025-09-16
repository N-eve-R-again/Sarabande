#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Linq;

public static class ExportProjectAndSceneHierarchy
{
    // === A. Arborescence des dossiers/fichiers du projet (Assets/) ===
    [MenuItem("Tools/Export/Text/Project Folder Tree…")]
    public static void ExportProjectFolderTree()
    {
        // point de départ : Assets/
        var rootPath = Application.dataPath;
        var savePath = EditorUtility.SaveFilePanel(
            "Exporter l'arborescence des dossiers/fichiers (Assets/)",
            "", "ProjectFolderTree.txt", "txt");
        if (string.IsNullOrEmpty(savePath)) return;

        var sb = new StringBuilder();
        sb.AppendLine("Assets/");
        WriteDir(rootPath, 1, sb);

        File.WriteAllText(savePath, sb.ToString(), new UTF8Encoding(false));
        EditorUtility.RevealInFinder(savePath);
        Debug.Log($"Arborescence du projet exportée vers: {savePath}");
    }

    private static void WriteDir(string dir, int depth, StringBuilder sb)
    {
        string indent = new string(' ', depth * 2);

        // Dossiers
        foreach (var d in Directory.EnumerateDirectories(dir).OrderBy(p => p))
        {
            var name = Path.GetFileName(d);
            sb.AppendLine($"{indent}- {name}/");
            WriteDir(d, depth + 1, sb);
        }

        // Fichiers (on ignore les .meta)
        foreach (var f in Directory.EnumerateFiles(dir).OrderBy(p => p))
        {
            if (f.EndsWith(".meta")) continue;
            var name = Path.GetFileName(f);
            sb.AppendLine($"{indent}- {name}");
        }
    }

    // === B. Hiérarchie des GameObjects de la scène active ===
    [MenuItem("Tools/Export/Text/Active Scene Hierarchy…")]
    public static void ExportActiveSceneHierarchy()
    {
        var savePath = EditorUtility.SaveFilePanel(
            "Exporter la hiérarchie de la scène active",
            "", "ActiveSceneHierarchy.txt", "txt");
        if (string.IsNullOrEmpty(savePath)) return;

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();

        var sb = new StringBuilder();
        sb.AppendLine($"Scene: {scene.name}");
        foreach (var go in roots.OrderBy(g => g.name))
            WriteGO(go.transform, 0, sb);

        File.WriteAllText(savePath, sb.ToString(), new UTF8Encoding(false));
        EditorUtility.RevealInFinder(savePath);
        Debug.Log($"Hiérarchie de scène exportée vers: {savePath}");
    }

    private static void WriteGO(Transform t, int depth, StringBuilder sb)
    {
        string indent = new string(' ', depth * 2);
        var go = t.gameObject;

        // statut actif/inactif + liste rapide des composants (hors Transform)
        var components = go.GetComponents<Component>()
                           .Where(c => c != null && !(c is Transform))
                           .Select(c => c.GetType().Name)
                           .ToArray();
        string comps = components.Length > 0 ? $" [{string.Join(", ", components)}]" : "";
        string active = go.activeInHierarchy ? "" : " (inactive)";

        sb.AppendLine($"{indent}- {go.name}{active}{comps}");

        for (int i = 0; i < t.childCount; i++)
            WriteGO(t.GetChild(i), depth + 1, sb);
    }
}
#endif
