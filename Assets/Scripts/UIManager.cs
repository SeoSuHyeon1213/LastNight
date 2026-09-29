using UnityEngine;
using UnityEngine.UI;

// Retains serialized UnityEvent targets and old callers while SurvivalHud owns all UI behavior.
[RequireComponent(typeof(SurvivalHud))]
public class UIManager : MonoBehaviour {
    private static UIManager cached;
    public static UIManager instance {
        get {
            if (cached == null) cached = FindAnyObjectByType<UIManager>();
            return cached;
        }
    }
    [HideInInspector] public Text ammoText;
    [HideInInspector] public Text scoreText;
    [HideInInspector] public Text waveText;
    [HideInInspector] public GameObject gameoverUI;
    private SurvivalHud hud;
    private SurvivalHud Hud {
        get {
            if (hud == null) hud = GetComponent<SurvivalHud>();
            if (hud == null) hud = gameObject.AddComponent<SurvivalHud>();
            hud.ConfigureLegacy(ammoText, scoreText, waveText, gameoverUI);
            return hud;
        }
    }
    private void Awake() { if (cached == null) cached = this; var owner = Hud; }
    private void OnDestroy() { if (cached == this) cached = null; }
    public void UpdateAmmoText(int magazine, int reserve, bool infiniteAmmo = false) => Hud.UpdateAmmoText(magazine, reserve, infiniteAmmo);
    public void UpdateScoreText(int score) => Hud.UpdateScoreText(score);
    public void UpdateWaveText(int wave, int count) => Hud.UpdateWaveText(wave, count);
    public void SetActiveGameoverUI(bool active) => Hud.SetActiveGameoverUI(active);
    public void GameRestart() => Hud.GameRestart();
}
