using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponGripProfile))]
public sealed class WeaponGripProfileEditor : Editor {
    private Animator poseSource;
    public override void OnInspectorGUI() {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("손가락은 캐릭터마다 축이 다릅니다. 원하는 자세를 만든 Humanoid Animator에서 로컬 회전을 저장하세요. 배열이 비어 있으면 원래 애니메이션을 유지합니다.", MessageType.Info);
        poseSource = (Animator)EditorGUILayout.ObjectField("Finger Pose Source", poseSource, typeof(Animator), true);
        using (new EditorGUI.DisabledScope(poseSource == null || !poseSource.isHuman)) {
            if (GUILayout.Button("Capture Finger Pose")) {
                WeaponGripProfile profile = (WeaponGripProfile)target;
                Undo.RecordObject(profile, "Capture Weapon Finger Pose");
                profile.CaptureFingerPose(poseSource);
                EditorUtility.SetDirty(profile);
            }
        }
    }
}
