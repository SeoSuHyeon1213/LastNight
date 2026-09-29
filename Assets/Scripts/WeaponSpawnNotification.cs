using UnityEngine;
using UnityEngine.UI;

// Canvas의 Text를 연결하고 RectTransform으로 알림 위치를 배치한다.
public sealed class WeaponSpawnNotification : MonoBehaviour {
    public Text messageText;
    [SerializeField, Min(0.1f)] private float displayDuration = 4f;
    private float hideAt;
    private bool visible;

    private void Awake() {
        if (messageText == null) messageText = GetComponent<Text>();
        if (messageText == null) {
            Debug.LogError("WeaponSpawnNotification: Message Text를 연결하세요.", this);
            enabled = false;
            return;
        }
        messageText.enabled = false;
    }

    public void Show(int wave, string weaponName) {
        if (!isActiveAndEnabled || messageText == null) return;
        messageText.text = $"WAVE {wave} - WEAPON SPAWNED: {weaponName}";
        messageText.enabled = true;
        hideAt = Time.time + displayDuration;
        visible = true;
    }

    private void Update() {
        if (!visible || Time.time < hideAt) return;
        Hide();
    }

    private void OnDisable() {
        Hide();
    }

    private void Hide() {
        if (messageText != null) messageText.enabled = false;
        visible = false;
    }
}
