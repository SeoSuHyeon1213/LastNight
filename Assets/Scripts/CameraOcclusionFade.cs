using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Attach to the real rendering camera, not a Cinemachine virtual camera.
[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(10000)]
public sealed class CameraOcclusionFade : MonoBehaviour {
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.3f, 0f);
    [Tooltip("Only roof/wall layers that are allowed to fade. Colliders are required.")]
    [SerializeField] private LayerMask obstructionLayers;
    [SerializeField, Range(0f, 1f)] private float fadedOpacity = 0.2f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float probeRadius = 0.2f;
    [SerializeField, Min(0.02f)] private float scanInterval = 0.05f;
    [SerializeField, Min(0f)] private float restoreDelay = 0.15f;

    private sealed class Entry {
        public Renderer renderer;
        public Material[] originals;
        public Material[] copies;
        public Color[] colors;
        public float opacity = 1f;
        public float lastSeen;
    }
    private readonly Dictionary<Renderer, Entry> entries = new Dictionary<Renderer, Entry>();
    private readonly HashSet<Renderer> unsupported = new HashSet<Renderer>();
    private readonly List<Renderer> finished = new List<Renderer>();
    private Camera renderingCamera;
    private float nextScan;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    private void Start() {
        renderingCamera = GetComponent<Camera>();
        if (target == null) {
            PlayerHealth player = FindAnyObjectByType<PlayerHealth>();
            if (player != null) target = player.transform;
        }
        if (target == null || !target.gameObject.scene.IsValid() || obstructionLayers.value == 0) {
            Debug.LogError("CameraOcclusionFade: 씬의 Target과 지붕/벽용 Obstruction Layers를 지정하세요.", this);
            enabled = false;
        }
    }

    private void LateUpdate() {
        if (target == null || renderingCamera == null || !renderingCamera.isActiveAndEnabled) {
            RestoreAll();
            return;
        }
        if (Time.time >= nextScan) {
            nextScan = Time.time + Mathf.Max(0.02f, scanInterval);
            Vector3 end = target.position + targetOffset;
            Vector3 start = transform.position;
            Vector3 direction = end - start;
            // Endpoint overlaps cover cases where the camera starts inside a roof collider.
            foreach (Collider collider in Physics.OverlapSphere(start, probeRadius, obstructionLayers, QueryTriggerInteraction.Ignore))
                Mark(collider);
            if (direction.sqrMagnitude > 0.0001f)
                foreach (RaycastHit hit in Physics.SphereCastAll(start, probeRadius, direction.normalized,
                    direction.magnitude, obstructionLayers, QueryTriggerInteraction.Ignore)) Mark(hit.collider);
        }
        finished.Clear();
        foreach (KeyValuePair<Renderer, Entry> pair in entries) {
            Entry entry = pair.Value;
            if (entry.renderer == null) { finished.Add(pair.Key); continue; }
            bool blocked = Time.time - entry.lastSeen <= scanInterval + restoreDelay;
            entry.opacity = Mathf.MoveTowards(entry.opacity, blocked ? fadedOpacity : 1f,
                Time.deltaTime / Mathf.Max(0.01f, fadeDuration));
            for (int i = 0; i < entry.copies.Length; i++) {
                Color color = entry.colors[i];
                color.a *= entry.opacity;
                entry.copies[i].SetColor(BaseColor, color);
            }
            if (!blocked && entry.opacity >= 1f) finished.Add(pair.Key);
        }
        foreach (Renderer renderer in finished) {
            Release(entries[renderer]);
            entries.Remove(renderer);
        }
    }

    private void Mark(Collider collider) {
        if (collider.transform.IsChildOf(target) || collider.GetComponentInParent<LivingEntity>() != null) return;
        Renderer renderer = collider.GetComponent<Renderer>();
        if (renderer == null) renderer = collider.GetComponentInParent<Renderer>();
        if (renderer == null || !renderer.enabled || unsupported.Contains(renderer)) return;
        if (!entries.TryGetValue(renderer, out Entry entry)) {
            Material[] materials = renderer.sharedMaterials;
            if (materials.Length == 0) return;
            foreach (Material material in materials) {
                string shaderName = material != null ? material.shader.name : "";
                if (material == null || !material.HasProperty(BaseColor) ||
                    (shaderName != "Universal Render Pipeline/Lit" &&
                     shaderName != "Universal Render Pipeline/Simple Lit" &&
                     shaderName != "Universal Render Pipeline/Unlit")) {
                    unsupported.Add(renderer);
                    Debug.LogWarning("CameraOcclusionFade: URP Lit/Simple Lit/Unlit 재질만 지원합니다. " + renderer.name, renderer);
                    return;
                }
            }
            entry = new Entry {
                renderer = renderer, originals = materials,
                copies = new Material[materials.Length], colors = new Color[materials.Length]
            };
            for (int i = 0; i < materials.Length; i++) {
                Material copy = new Material(materials[i]);
                copy.name = materials[i].name + " (Occlusion Fade)";
                entry.copies[i] = copy;
                entry.colors[i] = copy.GetColor(BaseColor);
                copy.SetFloat("_Surface", 1f);
                copy.SetFloat("_Blend", 0f);
                copy.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                copy.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                copy.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                copy.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                copy.SetFloat("_ZWrite", 0f);
                copy.SetOverrideTag("RenderType", "Transparent");
                copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                copy.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                copy.DisableKeyword("_ALPHAMODULATE_ON");
                copy.SetShaderPassEnabled("DepthOnly", false);
                copy.renderQueue = (int)RenderQueue.Transparent;
            }
            renderer.sharedMaterials = entry.copies;
            entries.Add(renderer, entry);
        }
        entry.lastSeen = Time.time;
    }

    private void Release(Entry entry) {
        if (entry.renderer != null) entry.renderer.sharedMaterials = entry.originals;
        foreach (Material copy in entry.copies) if (copy != null) Destroy(copy);
    }
    private void RestoreAll() {
        foreach (Entry entry in entries.Values) Release(entry);
        entries.Clear();
    }
    private void OnDisable() { RestoreAll(); }
    private void OnDestroy() { RestoreAll(); }
}
