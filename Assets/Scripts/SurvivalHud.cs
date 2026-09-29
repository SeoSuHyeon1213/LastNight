using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class SurvivalHud : MonoBehaviour {
    [SerializeField] private PlayerHealth player;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Font hudFont;
    [SerializeField, Min(1f)] private float radarRange = 25f;
    [SerializeField, Min(0.05f)] private float scanInterval = 0.25f;
    [SerializeField, Range(0f, 1f)] private float lowHealthRatio = 0.25f;
    [SerializeField] private WeaponIcon[] weaponIcons;
    [System.Serializable] private struct WeaponIcon { public GunData weaponData; public Sprite icon; }
    private const float RadarRadius = 86f;
    private static readonly Color Panel = new Color(0.07f, 0.08f, 0.07f, 0.85f);
    private static readonly Color Ink = new Color(0.9f, 0.88f, 0.74f);
    private static readonly Color Olive = new Color(0.53f, 0.62f, 0.32f);
    private static readonly Color Danger = new Color(0.88f, 0.22f, 0.16f);
    private readonly List<Image> markers = new List<Image>();
    private readonly List<Zombie> targets = new List<Zombie>();
    private readonly List<Graphic> hiddenLegacy = new List<Graphic>();
    private readonly Vector3[] directions = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
    private readonly RectTransform[] cardinals = new RectTransform[4];
    private PlayerShooter shooter;
    [Header("Existing UI / migrated from UIManager")]
    [SerializeField] private Text legacyAmmoText;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text waveText;
    [SerializeField] private GameObject gameoverUI;

    public void ConfigureLegacy(Text ammo, Text score, Text wave, GameObject gameover) {
        if (legacyAmmoText == null) legacyAmmoText = ammo;
        if (scoreText == null) scoreText = score;
        if (waveText == null) waveText = wave;
        if (gameoverUI == null) gameoverUI = gameover;
    }
    private void Awake() {
        UIManager adapter = GetComponent<UIManager>();
        if (adapter != null) ConfigureLegacy(adapter.ammoText, adapter.scoreText, adapter.waveText, adapter.gameoverUI);
    }
    public void UpdateAmmoText(int magazine, int reserve, bool infinite = false) {
        if (legacyAmmoText != null) legacyAmmoText.text = magazine + "/" + (infinite ? "INF" : reserve.ToString());
    }
    public void UpdateScoreText(int score) {
        if (scoreText != null) scoreText.text = "Score : " + score;
    }
    public void UpdateWaveText(int wave, int remaining) {
        if (waveText != null) waveText.text = "Wave : " + wave + "\nEnemy Left : " + remaining;
    }
    public void SetActiveGameoverUI(bool active) {
        if (gameoverUI != null) gameoverUI.SetActive(active);
        else Debug.LogError("SurvivalHud: Gameover UI 참조가 필요합니다.", this);
    }
    public void GameRestart() {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    private GameObject root;
    private RectTransform radar;
    private Image healthFill, weaponImage;
    private Text healthText, weaponText, ammoText;
    private CanvasGroup visibility;
    private float nextScan;
    private Texture2D radarTexture;
    private Sprite radarSprite;
    private int lastHp = -1, lastMaxHp = -1, lastAmmo = -1, lastReserve = -1;
    private float lastRatio = -1f;
    private Gun lastWeapon;
    private bool displayInitialized, lastInfinite, lastReloading;
    public bool OwnsHudBranch(Transform node) => root != null &&
        (node == root.transform || node.IsChildOf(root.transform));

    private void Start() {
        if (player == null) player = FindAnyObjectByType<PlayerHealth>();
        if (player == null) {
            Debug.LogError("SurvivalHud: PlayerHealth를 찾을 수 없습니다.", this);
            enabled = false;
            return;
        }
        shooter = player.GetComponent<PlayerShooter>();

        if (viewCamera == null) viewCamera = Camera.main;
        if (hudFont == null) hudFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        root = new GameObject("Survival HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        // A nested canvas does not automatically stretch its RectTransform to the screen.
        RectTransform hudRect = (RectTransform)root.transform;
        hudRect.anchorMin = Vector2.zero;
        hudRect.anchorMax = Vector2.one;
        hudRect.offsetMin = hudRect.offsetMax = Vector2.zero;
        hudRect.localScale = Vector3.one;
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        visibility = root.GetComponent<CanvasGroup>();
        visibility.interactable = visibility.blocksRaycasts = false;
        BuildRadar();
        Image panel = Box("Health", root.transform, new Vector2(0.5f, 0), new Vector2(0, 78), new Vector2(430, 100), Panel);
        healthText = Label("Health Value", panel.transform, new Vector2(0, 20), new Vector2(400, 40), 28);
        Image track = Box("Track", panel.transform, Vector2.one * 0.5f, new Vector2(0, -20), new Vector2(390, 20), Color.black);
        healthFill = Box("Fill", track.transform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(390, 20), Olive);
        healthFill.rectTransform.pivot = new Vector2(0, 0.5f);
        panel = Box("Weapon", root.transform, new Vector2(1, 0), new Vector2(-225, 98), new Vector2(390, 140), Panel);
        weaponText = Label("Weapon Name", panel.transform, new Vector2(0, 42), new Vector2(370, 30), 23);
        ammoText = Label("Ammo", panel.transform, new Vector2(50, -15), new Vector2(265, 70), 30);
        weaponImage = Box("Weapon Icon", panel.transform, Vector2.one * 0.5f, new Vector2(-135, -15), new Vector2(95, 60), Ink);
        weaponImage.preserveAspect = true;
        HideLegacy();
    }

    private RectTransform MakeRect(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size) {
        RectTransform rect = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
    private Image Box(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color) {
        Image image = MakeRect(label, parent, anchor, position, size).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
    private Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize) {
        Text text = MakeRect(name, parent, Vector2.one * 0.5f, position, size).gameObject.AddComponent<Text>();
        text.font = hudFont;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Ink;
        text.raycastTarget = false;
        return text;
    }
    private void BuildRadar() {
        radar = MakeRect("Radar", root.transform, Vector2.zero, new Vector2(155, 155), new Vector2(240, 240));
        // Point filtering preserves the deliberately stepped ring at larger HUD sizes.
        const int size = 64;
        radarTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        radarTexture.filterMode = FilterMode.Point;
        radarTexture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
            float radius = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
            pixels[y * size + x] = radius > 31f ? Color.clear : radius > 29f || Mathf.Abs(radius - 16f) < 0.5f ? Olive : Panel;
        }
        radarTexture.SetPixels(pixels);
        radarTexture.Apply();
        radarSprite = Sprite.Create(radarTexture, new Rect(0, 0, size, size), Vector2.one * 0.5f);
        Box("Disc", radar, Vector2.one * 0.5f, Vector2.zero, new Vector2(240, 240), Color.white).sprite = radarSprite;
        string[] names = { "N", "E", "S", "W" };
        for (int i = 0; i < 4; i++) {
            Text text = Label(names[i], radar, Vector2.zero, new Vector2(26, 26), 19);
            text.text = names[i];
            cardinals[i] = text.rectTransform;
        }
        Image playerMarker = Box("Player", radar, Vector2.one * 0.5f, Vector2.zero, new Vector2(9, 9), Ink);
        playerMarker.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        Box("Forward", radar, Vector2.one * 0.5f, new Vector2(0, 11), new Vector2(3, 9), Ink);
    }
    private void Update() {
        if (root == null || player == null) return;

        float max = Mathf.Max(0, player.startingHealth);
        float hp = Mathf.Clamp(player.health, 0, max);
        float ratio = max > 0 ? hp / max : 0;
        int currentHp = Mathf.CeilToInt(hp), maximumHp = Mathf.CeilToInt(max);
        if (currentHp != lastHp || maximumHp != lastMaxHp) {
            healthText.text = $"HP  {currentHp} / {maximumHp}";
            lastHp = currentHp; lastMaxHp = maximumHp;
        }
        if (ratio != lastRatio) {
            healthFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 390f * ratio);
            lastRatio = ratio;
        }
        healthFill.color = ratio <= lowHealthRatio ? Danger : Olive;
        Gun active = shooter != null ? shooter.gun : null;
        if (!displayInitialized || active != lastWeapon) {
            weaponText.text = active == null ? "NO WEAPON" : active.gunData != null ? active.gunData.displayName : active.name;
            Sprite icon = null;
            if (active != null && weaponIcons != null) foreach (WeaponIcon entry in weaponIcons)
                if (entry.weaponData == active.gunData) { icon = entry.icon; break; }
            weaponImage.sprite = icon;
            weaponImage.enabled = icon != null;
        }
        int ammo = active != null ? active.magAmmo : -1;
        int reserve = active != null ? active.ammoRemain : -1;
        bool infinite = active != null && active.HasInfiniteAmmo;
        bool reloading = active != null && active.state == Gun.State.Reloading;
        if (!displayInitialized || active != lastWeapon || ammo != lastAmmo || reserve != lastReserve ||
            infinite != lastInfinite || reloading != lastReloading) {
            ammoText.text = active == null ? "-- / --" : $"{ammo} / {(infinite ? "INF" : reserve.ToString())}" +
                (reloading ? "\nRELOADING" : "");
            lastAmmo = ammo; lastReserve = reserve; lastInfinite = infinite; lastReloading = reloading;
        }
        lastWeapon = active;
        displayInitialized = true;
        if (Time.time >= nextScan) {
            nextScan = Time.time + Mathf.Max(0.05f, scanInterval);
            targets.Clear();
            foreach (Zombie zombie in FindObjectsByType<Zombie>(FindObjectsSortMode.None))
                if (!zombie.dead) targets.Add(zombie);
        }
        UpdateRadar();
    }
    private void UpdateRadar() {
        float yaw = viewCamera != null ? viewCamera.transform.eulerAngles.y : player.transform.eulerAngles.y;
        Quaternion toView = Quaternion.Euler(0, -yaw, 0);
        for (int i = 0; i < 4; i++) {
            Vector3 direction = toView * directions[i];
            cardinals[i].anchoredPosition = new Vector2(direction.x, direction.z) * 102;
        }
        int used = 0;
        foreach (Zombie zombie in targets) {
            if (zombie == null || zombie.dead || !zombie.isActiveAndEnabled) continue;
            Vector3 offset = zombie.transform.position - player.transform.position;
            offset.y = 0;
            if (offset.sqrMagnitude > radarRange * radarRange) continue;
            if (used == markers.Count) markers.Add(Box("Zombie", radar, Vector2.one * 0.5f, Vector2.zero, new Vector2(7, 7), Danger));
            Vector3 relative = toView * offset;
            Image marker = markers[used++];
            marker.gameObject.SetActive(true);
            marker.rectTransform.anchoredPosition = new Vector2(relative.x, relative.z) * (RadarRadius / Mathf.Max(1, radarRange));
        }
        for (int i = used; i < markers.Count; i++) markers[i].gameObject.SetActive(false);
    }
    private void HideLegacy() {
        if (legacyAmmoText != null) HideGraphic(legacyAmmoText);
        if (player != null && player.healthSlider != null)
            foreach (Graphic graphic in player.healthSlider.GetComponentsInChildren<Graphic>(true)) HideGraphic(graphic);
    }
    private void HideGraphic(Graphic graphic) {
        if (!graphic.enabled) return;
        hiddenLegacy.Add(graphic);
        graphic.enabled = false;
    }
    private void OnEnable() { if (root != null) { root.SetActive(true); HideLegacy(); } }
    private void OnDisable() {
        RestoreOtherUi();
        if (root != null) root.SetActive(false);
        foreach (Graphic graphic in hiddenLegacy) if (graphic != null) graphic.enabled = true;
        hiddenLegacy.Clear();
    }
    private void OnDestroy() {
        if (root != null) Destroy(root);
        if (radarSprite != null) Destroy(radarSprite);
        if (radarTexture != null) Destroy(radarTexture);
    }
    private GameObject exclusivePanel;
    private readonly Dictionary<CanvasGroup, GroupState> hiddenUi = new Dictionary<CanvasGroup, GroupState>();

    private struct GroupState {
        public float alpha;
        public bool interactable;
        public bool blocksRaycasts;
        public bool ignoreParentGroups;
    }

    private void LateUpdate() {
        GameSessionManager session = GameSessionManager.instance;
        GameObject panel = gameoverUI != null && gameoverUI.activeInHierarchy
            ? gameoverUI : session != null ? session.ActiveMenuPanel : null;
        if (visibility != null) visibility.alpha = panel != null ? 0f : 1f;
        if (exclusivePanel == panel) return;
        RestoreOtherUi();
        exclusivePanel = panel;
        if (panel == null) return;

        // 전환할 때만 검색하며, 동적으로 생성된 Survival HUD Canvas도 포함한다.
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            if (canvas.isRootCanvas) HideOutsidePanel(canvas.transform, panel.transform);
        }
    }

    private void HideOutsidePanel(Transform node, Transform panel) {
        if (node == panel || node.IsChildOf(panel)) return;
        if (!panel.IsChildOf(node)) {
            HideUiBranch(node);
            return;
        }
        // 패널의 조상은 유지하고 형제 UI만 숨겨 패널의 버튼 입력을 보존한다.
        for (int i = 0; i < node.childCount; i++)
            HideOutsidePanel(node.GetChild(i), panel);
    }

    private void HideUiBranch(Transform node) {
        SurvivalHud hud = node.GetComponentInParent<SurvivalHud>();
        if (hud != null && hud.OwnsHudBranch(node)) return;
        // The HUD owns its combat display; menus and other UI retain existing handling.
        if (node.GetComponentInChildren<SurvivalHud>(true) != null) {
            for (int i = 0; i < node.childCount; i++) HideUiBranch(node.GetChild(i));
            return;
        }
        CanvasGroup group = node.GetComponent<CanvasGroup>();
        if (group == null) group = node.gameObject.AddComponent<CanvasGroup>();
        foreach (CanvasGroup childGroup in node.GetComponentsInChildren<CanvasGroup>(true)) {
            if (!hiddenUi.ContainsKey(childGroup)) {
                hiddenUi.Add(childGroup, new GroupState {
                    alpha = childGroup.alpha,
                    interactable = childGroup.interactable,
                    blocksRaycasts = childGroup.blocksRaycasts,
                    ignoreParentGroups = childGroup.ignoreParentGroups
                });
            }
            childGroup.alpha = 0f;
            childGroup.interactable = false;
            childGroup.blocksRaycasts = false;
            childGroup.ignoreParentGroups = false;
        }
    }

    private void RestoreOtherUi() {
        foreach (KeyValuePair<CanvasGroup, GroupState> entry in hiddenUi) {
            if (entry.Key == null) continue;
            entry.Key.alpha = entry.Value.alpha;
            entry.Key.interactable = entry.Value.interactable;
            entry.Key.blocksRaycasts = entry.Value.blocksRaycasts;
            entry.Key.ignoreParentGroups = entry.Value.ignoreParentGroups;
        }
        hiddenUi.Clear();
        exclusivePanel = null;
    }

}
