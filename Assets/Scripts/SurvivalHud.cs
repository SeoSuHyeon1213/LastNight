using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class SurvivalHud : MonoBehaviour {
    [SerializeField] private PlayerHealth player;
    [SerializeField] private Camera viewCamera;
    [SerializeField, Min(1f)] private float radarRange = 25f;
    [SerializeField, Min(0.05f)] private float scanInterval = 0.25f;
    [SerializeField, Range(0f, 1f)] private float lowHealthRatio = 0.25f;
    private int grenadeCount = -1;
    private int lastGrenadeCount = -2;
    private const float RadarRadius = 86f;
    private static readonly Color Ink = new Color(0.9f, 0.88f, 0.74f);
    private static readonly Color Olive = new Color(0.53f, 0.62f, 0.32f);
    private static readonly Color Danger = new Color(0.88f, 0.22f, 0.16f);
    [SerializeField] private Image[] radarMarkers;
    private readonly List<Zombie> targets = new List<Zombie>();
    private readonly List<Graphic> hiddenLegacy = new List<Graphic>();
    private readonly Vector3[] directions = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
    [SerializeField] private RectTransform[] cardinals;
    private PlayerShooter shooter;
    private PlankInventory plankInventory;
    private GrenadeInventory grenadeInventory;
    private BarricadeBuilder barricadeBuilder;
    [SerializeField] private Text materialsText;
    private int lastMaterials = -2;
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
        if (scoreText != null) scoreText.text = "SCORE : " + score;
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
    [Header("Editor HUD")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup visibility;
    [SerializeField] private RectTransform radar;
    [SerializeField] private Image healthFill;
    [SerializeField] private Text healthText;
    [SerializeField] private Text weaponText;
    [SerializeField] private Text ammoText;
    [SerializeField] private Text grenadeCountText;
    [Header("Weapon Slots")]
    [SerializeField] private Image primaryWeaponImage;
    [SerializeField] private Image secondaryWeaponImage;
    [SerializeField, Min(1f)] private float equippedWeaponScale = 1.15f;
    [SerializeField, Range(0f, 1f)] private float inactiveWeaponAlpha = 0.4f;
    [Header("Reload Indicator")]
    [SerializeField] private Text reloadText;
    [SerializeField, Min(0.05f)] private float reloadDotInterval = 0.35f;
    private static readonly string[] ReloadMessages = { "Reloading.", "Reloading..", "Reloading..." };
    private Gun reloadingWeapon;
    private float reloadStartedAt;
    private int lastReloadDot = -1;
    [Header("Barricade Interaction")]
    [SerializeField] private GameObject barricadePromptRoot;
    [SerializeField] private Text barricadePromptText;
    private Vector3 primaryBaseScale, secondaryBaseScale;
    private Color primaryBaseColor, secondaryBaseColor;
    private float nextScan;
    private int lastHp = -1, lastMaxHp = -1, lastAmmo = -1, lastReserve = -1;
    private float lastRatio = -1f;
    private Gun lastWeapon;
    private bool displayInitialized, lastInfinite, lastReloading;
    public bool OwnsHudBranch(Transform node) => root != null &&
        (node == root.transform || node.IsChildOf(root.transform));

    private void Start() {
        if (player == null || root == null || visibility == null || radar == null ||
            healthFill == null || healthText == null || weaponText == null || ammoText == null ||
            materialsText == null || grenadeCountText == null || primaryWeaponImage == null ||
            secondaryWeaponImage == null || reloadText == null || cardinals == null || cardinals.Length != 4 ||
            System.Array.Exists(cardinals, value => value == null) || radarMarkers == null ||
            radarMarkers.Length == 0 || System.Array.Exists(radarMarkers, value => value == null)) {
            Debug.LogError("SurvivalHud: Player 및 Editor HUD/Weapon Slots/Reload Indicator/레이더 참조를 Inspector에서 연결하세요.", this);
            enabled = false;
            return;
        }
        shooter = player.GetComponent<PlayerShooter>();
        plankInventory = player.GetComponent<PlankInventory>();
        grenadeInventory = player.GetComponent<GrenadeInventory>();
        barricadeBuilder = player.GetComponent<BarricadeBuilder>();
        ClearReloadIndicator();
        if (barricadePromptRoot != null) barricadePromptRoot.SetActive(false);
        primaryBaseScale = primaryWeaponImage.rectTransform.localScale;
        secondaryBaseScale = secondaryWeaponImage.rectTransform.localScale;
        primaryBaseColor = primaryWeaponImage.color;
        secondaryBaseColor = secondaryWeaponImage.color;
        root.SetActive(true);
        foreach (Image marker in radarMarkers) marker.gameObject.SetActive(false);
        if (grenadeInventory != null) SetGrenadeCount(grenadeInventory.Count);
        UpdateMaterials();
        HideLegacy();
    }

    private void UpdateWeaponSlots() {
        Gun primary = shooter != null ? shooter.PrimaryGun : null;
        Gun secondary = shooter != null ? shooter.SecondaryGun : null;
        Gun equipped = shooter != null ? shooter.gun : null;
        UpdateWeaponSlot(primaryWeaponImage, primary, primary != null && primary == equipped,
            primaryBaseScale, primaryBaseColor);
        UpdateWeaponSlot(secondaryWeaponImage, secondary, secondary != null && secondary == equipped,
            secondaryBaseScale, secondaryBaseColor);
    }

    private void UpdateWeaponSlot(Image image, Gun weapon, bool equipped, Vector3 baseScale, Color baseColor) {
        Sprite icon = weapon != null && weapon.gunData != null ? weapon.gunData.HudIcon : null;
        image.sprite = icon;
        image.enabled = icon != null;
        image.rectTransform.localScale = baseScale * (equipped ? equippedWeaponScale : 1f);
        baseColor.a *= equipped ? 1f : inactiveWeaponAlpha;
        image.color = baseColor;
    }

    private void Update() {
        if (root == null || player == null) return;
        UpdateBarricadePrompt();
        UpdateMaterials();
        UpdateWeaponSlots();
        if (grenadeInventory != null) SetGrenadeCount(grenadeInventory.Count);

        float max = Mathf.Max(0, player.startingHealth);
        float hp = Mathf.Clamp(player.health, 0, max);
        float ratio = max > 0 ? hp / max : 0;
        int currentHp = Mathf.CeilToInt(hp), maximumHp = Mathf.CeilToInt(max);
        if (currentHp != lastHp || maximumHp != lastMaxHp) {
            healthText.text = $"{currentHp} / {maximumHp}";
            lastHp = currentHp; lastMaxHp = maximumHp;
        }
        if (ratio != lastRatio) {
            healthFill.fillAmount = ratio;
            lastRatio = ratio;
        }
        healthFill.color = ratio <= lowHealthRatio ? Danger : Olive;
        Gun active = shooter != null ? shooter.gun : null;
        if (!displayInitialized || active != lastWeapon) {
            string weaponName = active == null ? "NO WEAPON" : active.gunData != null ? active.gunData.displayName : active.name;
            weaponText.text = active == null ? weaponName :
                $"{(shooter.ActiveSlot == 1 ? "PRIMARY" : "SECONDARY")} {weaponName.ToUpperInvariant()} ({shooter.ActiveSlot})";
        }
        int ammo = active != null ? active.magAmmo : -1;
        int reserve = active != null ? active.ammoRemain : -1;
        bool infinite = active != null && active.HasInfiniteAmmo;
        bool reloading = active != null && active.state == Gun.State.Reloading;
        UpdateReloadIndicator(active);
        if (!displayInitialized || active != lastWeapon || ammo != lastAmmo || reserve != lastReserve ||
            infinite != lastInfinite || reloading != lastReloading) {
            ammoText.text = active == null ? "-- / --" : !active.UsesAmmo ? "MELEE" : $"{ammo} / {(infinite ? "INF" : reserve.ToString())}";
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

    private void UpdateReloadIndicator(Gun active) {
        if (reloadText == null) return;
        if (active == null || !active.isActiveAndEnabled || active.state != Gun.State.Reloading ||
            player.dead || (GameManager.instance != null && GameManager.instance.isGameover)) {
            ClearReloadIndicator();
            return;
        }
        if (reloadingWeapon != active) {
            reloadingWeapon = active;
            reloadStartedAt = Time.time;
            lastReloadDot = -1;
        }
        // 총의 재장전과 같은 게임 시간을 사용해 일시정지 중에는 점도 멈춘다.
        int dot = Mathf.FloorToInt(Mathf.Max(0f, Time.time - reloadStartedAt) /
            Mathf.Max(0.05f, reloadDotInterval)) % ReloadMessages.Length;
        reloadText.enabled = true;
        if (lastReloadDot == dot) return;
        reloadText.text = ReloadMessages[dot];
        lastReloadDot = dot;
    }

    private void ClearReloadIndicator() {
        reloadingWeapon = null;
        lastReloadDot = -1;
        if (reloadText == null) return;
        reloadText.enabled = false;
        reloadText.text = string.Empty;
    }

    private void UpdateBarricadePrompt() {
        if (barricadePromptRoot == null || barricadePromptText == null) return;
        bool show = barricadeBuilder != null && barricadeBuilder.CanShowPrompt;
        if (barricadePromptRoot.activeSelf != show) barricadePromptRoot.SetActive(show);
        if (!show) return;
        if (barricadeBuilder.IsInstalling)
            barricadePromptText.text = $"Installing barricade {Mathf.RoundToInt(barricadeBuilder.Progress * 100f)}%";
        else if (!barricadeBuilder.Target.CanInstall)
            barricadePromptText.text = "Barricade full";
        else
            barricadePromptText.text = $"Press {barricadeBuilder.InstallKeyLabel} : Install barricade" +
                (barricadeBuilder.Materials < 1 ? "\nNeed wood" : "");
    }
    // 수류탄 인벤토리가 초기화되거나 획득/사용될 때 실제 보유량을 전달한다.
    public void SetGrenadeCount(int count) {
        grenadeCount = Mathf.Max(0, count);
        RefreshGrenadeCount();
    }

    private void RefreshGrenadeCount() {
        if (grenadeCountText == null || lastGrenadeCount == grenadeCount) return;
        grenadeCountText.text = grenadeCount < 0 ? "--" : $"x{grenadeCount}";
        grenadeCountText.color = grenadeCount == 0 ? Danger : Ink;
        lastGrenadeCount = grenadeCount;
    }

    private void UpdateMaterials() {
        int count = plankInventory != null ? plankInventory.Count : -1;
        if (count == lastMaterials) return;
        materialsText.text = count < 0 ? "--" : count.ToString();
        materialsText.color = count == 0 ? Danger : Ink;
        lastMaterials = count;
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
            if (used == radarMarkers.Length) break;
            Vector3 relative = toView * offset;
            Image marker = radarMarkers[used++];
            marker.gameObject.SetActive(true);
            marker.rectTransform.anchoredPosition = new Vector2(relative.x, relative.z) * (RadarRadius / Mathf.Max(1, radarRange));
        }
        for (int i = used; i < radarMarkers.Length; i++) radarMarkers[i].gameObject.SetActive(false);
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
        ClearReloadIndicator();
        RestoreOtherUi();
        if (root != null) root.SetActive(false);
        foreach (Graphic graphic in hiddenLegacy) if (graphic != null) graphic.enabled = true;
        hiddenLegacy.Clear();
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

        // 메뉴 전환 시에만 다른 Canvas의 표시 상태를 보존하고 숨긴다.
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
