using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.AI.Navigation.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Excludes decorative foliage from navigation without changing structural geometry.</summary>
public static class NavMeshBakePreparation
{
    [MenuItem("Tools/LastNight/Navigation/Preview Decorative Foliage")]
    private static void PreviewMenu()
    {
        var candidates = FindCandidates();
        Debug.Log($"Decorative foliage pending navigation exclusion: {candidates.Count}");
        Selection.objects = candidates.ToArray();
    }

    [MenuItem("Tools/LastNight/Navigation/Exclude Decorative Foliage And Bake")]
    private static void ApplyMenu()
    {
        int changed = ApplyExclusions();
        var surfaces = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        NavMeshAssetManager.instance.StartBakingSurfaces(surfaces);
        Debug.Log($"Excluded {changed} decorative foliage meshes. Baking {surfaces.Length} navigation surfaces.");
    }

    public static List<GameObject> FindCandidates()
    {
        var result = new List<GameObject>();
        foreach (var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            if (filter.gameObject.scene != SceneManager.GetActiveScene() || filter.sharedMesh == null) continue;
            string meshName = filter.sharedMesh.name;
            // This asset pack separates leaves from trunks. Only these known decorations are eligible.
            if (!meshName.StartsWith("vines", StringComparison.OrdinalIgnoreCase) &&
                !meshName.StartsWith("leaves", StringComparison.OrdinalIgnoreCase)) continue;
            if (!filter.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled) continue;
            bool hasSolidCollider = false;
            foreach (var collider in filter.GetComponents<Collider>())
                hasSolidCollider |= collider.enabled && !collider.isTrigger;
            if (hasSolidCollider) continue;
            // Preserve existing hand-authored modifier rules.
            if (filter.TryGetComponent<NavMeshModifier>(out _)) continue;
            result.Add(filter.gameObject);
        }
        return result;
    }

    public static int ApplyExclusions()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Prepare navigation in Edit Mode.");
        var candidates = FindCandidates();
        if (candidates.Count == 0) return 0;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Exclude decorative foliage from NavMesh");
        foreach (var candidate in candidates)
        {
            var modifier = Undo.AddComponent<NavMeshModifier>(candidate);
            Undo.RecordObject(modifier, "Exclude decorative foliage");
            modifier.ignoreFromBuild = true;
            modifier.applyToChildren = false;
            EditorUtility.SetDirty(modifier);
            if (PrefabUtility.IsPartOfPrefabInstance(candidate))
                PrefabUtility.RecordPrefabInstancePropertyModifications(modifier);
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return candidates.Count;
    }
}
