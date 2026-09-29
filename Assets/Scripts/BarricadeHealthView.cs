//using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BarricadeHealthView : MonoBehaviour {
    public Barricade barricade;
    public Slider healthBar;
    //public TMP_Text label;
    //private Camera viewCamera;
    private BarricadeBuilder builder;
    private void Start() {
        //viewCamera = Camera.main;
        builder = FindAnyObjectByType<BarricadeBuilder>();
    }
    private void LateUpdate() {
        if (barricade == null) return;
        //if (viewCamera != null) transform.rotation = viewCamera.transform.rotation;
        if (healthBar != null) {
            healthBar.maxValue = Mathf.Max(1f, barricade.MaxHealth);
            healthBar.value = barricade.health;
        }
        // if (label != null) {
        //     label.text = $"BARRICADE {barricade.health:0} / {barricade.MaxHealth:0}";
        //     if (builder != null && builder.Target == barricade) {
        //         if (!barricade.CanInstall)
        //             label.text += $"\nFull | Wood {builder.Materials}";
        //         else if (builder.Materials < 1)
        //             label.text += "\nNeed Wood";
        //         else if (builder.IsInstalling)
        //             label.text += $"\nInstalling {builder.Progress:P0} | Wood {builder.Materials}";
        //         else
        //             label.text += $"\nPress {builder.InstallKeyLabel}: Install | Wood {builder.Materials}";
        //     }
        // }
    }
}
