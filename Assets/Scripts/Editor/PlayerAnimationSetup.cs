using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// FBX 기본 클립의 실제 참조와 반복 설정은 Unity Importer를 통해 저장한다.
[InitializeOnLoad]
internal static class PlayerAnimationSetup {
    private const string ControllerPath = "Assets/Animations/ShooterAnimator.controller";
    private static readonly string[] MovementPaths = {
        "Assets/Animations/Ch03_nonPBR@Rifle Side Step.fbx",
        "Assets/Animations/Ch03_nonPBR@Run Left.fbx",
        "Assets/Animations/Ch03_nonPBR@Run Right.fbx"
    };

    static PlayerAnimationSetup() {
        EditorApplication.delayCall += Apply;
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Apply;
        };
    }

    [MenuItem("Tools/Last Night/Repair Player Movement Animations")]
    private static void Apply() {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) {
            EditorApplication.delayCall += Apply;
            return;
        }
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) return;
        foreach (string path in MovementPaths) EnsureLoop(path);

        bool changed = false;
        foreach (BlendTree tree in AssetDatabase.LoadAllAssetsAtPath(ControllerPath).OfType<BlendTree>()) {
            if (tree.name != "Movement Tree") continue;
            ChildMotion[] children = tree.children;
            for (int i = 0; i < children.Length; i++) {
                Vector2 position = children[i].position;
                if (Mathf.Abs(position.y) > 0.001f || Mathf.Abs(position.x) < 0.001f) continue;
                int pathIndex = Mathf.Abs(position.x) < 0.75f ? 0 : (position.x < 0f ? 1 : 2);
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(MovementPaths[pathIndex])
                    .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip == null) {
                    Debug.LogError("옆걸음 클립을 찾을 수 없습니다: " + MovementPaths[pathIndex]);
                    continue;
                }
                if (children[i].motion == clip) continue;
                children[i].motion = clip;
                changed = true;
            }
            if (changed) {
                tree.children = children;
                EditorUtility.SetDirty(tree);
            }
        }
        // 권총 상체 포즈도 이동/정지 상태에서 지속되도록 반복한다.
        EnsureLoop(AssetDatabase.GUIDToAssetPath("4726b8ea69bdff34f9b0abb596abbc25"));
        EnsureLoop(AssetDatabase.GUIDToAssetPath("af3bdf98006a8d142991af16360bc950"));
        if (changed) AssetDatabase.SaveAssets();
    }

    private static void EnsureLoop(string path) {
        if (string.IsNullOrEmpty(path)) return;
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return;
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips.Length == 0) clips = importer.defaultClipAnimations;
        bool changed = false;
        foreach (ModelImporterClipAnimation clip in clips) {
            if (clip.loopTime) continue;
            clip.loopTime = true;
            changed = true;
        }
        if (!changed) return;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }
}
