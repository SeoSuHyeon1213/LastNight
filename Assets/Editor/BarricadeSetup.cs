using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class BarricadeSetup {
    private const string Folder = "Assets/Resources/Barricade";
    private const string SitePath = Folder + "/Barricade Site.prefab";
    private const string PickupPath = Folder + "/Plank Pickup.prefab";

    [InitializeOnLoadMethod]
    private static void QueueAssets() { EditorApplication.delayCall += EnsureAssets; }

    [MenuItem("Tools/Zombie/Create Barricade Assets")]
    public static void EnsureAssets() {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources", "Barricade");
        Material wood = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Wood.mat");
        if (wood == null) {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogError("Barricade: URP Lit shader unavailable."); return; }
            wood = new Material(shader);
            wood.color = new Color(0.32f, 0.16f, 0.065f);
            AssetDatabase.CreateAsset(wood, Folder + "/Wood.mat");
        }
        GameObject plank = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Plank.prefab");
        if (plank == null) {
            GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.name = "Plank";
            model.transform.localScale = new Vector3(2f, 0.22f, 0.12f);
            model.GetComponent<Renderer>().sharedMaterial = wood;
            Object.DestroyImmediate(model.GetComponent<Collider>());
            plank = PrefabUtility.SaveAsPrefabAsset(model, Folder + "/Plank.prefab");
            Object.DestroyImmediate(model);
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath) == null) {
            GameObject pickup = new GameObject("Plank Pickup");
            pickup.AddComponent<PlankPickup>();
            SphereCollider trigger = pickup.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.6f;
            Rigidbody body = pickup.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(plank);
            model.transform.SetParent(pickup.transform, false);
            model.transform.localScale = new Vector3(0.7f, 0.15f, 0.2f);
            PrefabUtility.SaveAsPrefabAsset(pickup, PickupPath);
            Object.DestroyImmediate(pickup);
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SitePath) == null) {
            GameObject site = new GameObject("Barricade Site");
            Barricade barricade = site.AddComponent<Barricade>();
            barricade.plankPrefab = plank;
            BoxCollider blocker = site.GetComponent<BoxCollider>();
            blocker.center = Vector3.up;
            blocker.size = new Vector3(2f, 2f, 0.2f);
            barricade.plankSockets = new Transform[5];
            for (int i = 0; i < 5; i++) {
                GameObject socket = new GameObject("Plank Socket " + (i + 1));
                socket.transform.SetParent(site.transform, false);
                socket.transform.localPosition = new Vector3(0f, 0.35f + i * 0.33f, 0f);
                socket.transform.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? 8f : -8f);
                barricade.plankSockets[i] = socket.transform;
            }
            GameObject ui = new GameObject("Barricade Health", typeof(RectTransform), typeof(Canvas));
            ui.transform.SetParent(site.transform, false);
            ui.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            ui.transform.localScale = Vector3.one * 0.006f;
            ui.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)ui.transform).sizeDelta = new Vector2(360f, 90f);
            BarricadeHealthView view = ui.AddComponent<BarricadeHealthView>();
            view.barricade = barricade;
            GameObject bar = new GameObject("Health Bar", typeof(RectTransform), typeof(Image), typeof(Slider));
            bar.transform.SetParent(ui.transform, false);
            ((RectTransform)bar.transform).sizeDelta = new Vector2(340f, 16f);
            ((RectTransform)bar.transform).anchoredPosition = new Vector2(0f, -30f);
            bar.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f);
            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(bar.transform, false);
            RectTransform rect = (RectTransform)fill.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(0.3f, 0.8f, 0.3f);
            view.healthBar = bar.GetComponent<Slider>();
            view.healthBar.fillRect = rect;
            view.healthBar.interactable = false;
            //GameObject label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            //label.transform.SetParent(ui.transform, false);
            //((RectTransform)label.transform).sizeDelta = new Vector2(360f, 64f);
            //view.label = label.GetComponent<TextMeshProUGUI>();
            //view.label.fontSize = 20f;
            //view.label.alignment = TextAlignmentOptions.Center;
            //view.label.raycastTarget = false;
            PrefabUtility.SaveAsPrefabAsset(site, SitePath);
            Object.DestroyImmediate(site);
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Zombie/Place Barricade At Selected Entrance")]
    public static void PlaceAtSelected() {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EnsureAssets();
        Transform entrance = Selection.activeTransform;
        if (entrance == null || EditorUtility.IsPersistent(entrance)) {
            Debug.LogError("씬에서 입구 위치의 오브젝트를 선택하세요.");
            return;
        }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SitePath);
        if (prefab == null) return;
        GameObject site = (GameObject)PrefabUtility.InstantiatePrefab(prefab, entrance.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(site, "Place Barricade");
        site.transform.SetPositionAndRotation(entrance.position, entrance.rotation);
        Selection.activeGameObject = site;
    }

    [MenuItem("Tools/Zombie/Setup Game PlayerHouse Barricade")]
    public static void SetupGameEntrance() {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EnsureAssets();
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("Game");
        if (!scene.IsValid() || !scene.isLoaded) {
            Debug.LogError("Game 씬을 열고 다시 실행하세요.");
            return;
        }
        Transform entrance = null;
        foreach (GameObject root in scene.GetRootGameObjects()) {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true)) {
                if (candidate.name != "Wall_01_Exterior_Doorway_01") continue;
                Transform parent = candidate.parent;
                while (parent != null && parent.name != "PlayerHouse") parent = parent.parent;
                if (parent == null) continue;
                if (entrance != null) {
                    Debug.LogError("PlayerHouse 안에 같은 이름의 입구가 여러 개입니다. 하나를 선택하고 Place Barricade At Selected Entrance를 사용하세요.");
                    return;
                }
                entrance = candidate;
            }
        }
        if (entrance == null) {
            Debug.LogError("Game 씬의 PlayerHouse/Wall_01_Exterior_Doorway_01을 찾지 못했습니다.");
            return;
        }
        Barricade existing = entrance.GetComponentInChildren<Barricade>(true);
        if (existing != null) { Selection.activeGameObject = existing.gameObject; return; }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SitePath);
        if (prefab == null) return;
        GameObject site = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(site, "Setup PlayerHouse Barricade");
        Vector3 position = entrance.position;
        Renderer[] renderers = entrance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0) {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }
        site.transform.SetPositionAndRotation(position, entrance.rotation);
        site.transform.SetParent(entrance, true);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = site;
        Debug.Log("바리케이드 배치 완료. 문 폭과 BoxCollider/Plank Sockets 위치를 확인하고 Game 씬을 저장하세요.", site);
    }
}
