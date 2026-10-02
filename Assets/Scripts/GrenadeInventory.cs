using UnityEngine;

[DisallowMultipleComponent]
public sealed class GrenadeInventory : MonoBehaviour {
    [SerializeField, Min(1)] private int capacity = 5;
    public int Count { get; private set; }
    public int Capacity => capacity;

    public int Add(int amount) {
        int accepted = Mathf.Clamp(amount, 0, Mathf.Max(0, capacity - Count));
        Count += accepted;
        return accepted;
    }

    public bool TrySpendOne() {
        if (Count < 1) return false;
        Count--;
        return true;
    }

    private void OnValidate() { capacity = Mathf.Max(1, capacity); }
}
