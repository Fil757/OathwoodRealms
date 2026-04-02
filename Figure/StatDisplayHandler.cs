using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class StatDisplayHandler : MonoBehaviour
{
    [System.Serializable]
    public class StatLayout
    {
        public GameObject targetObject;

        [Header("Lokale Position (x, y, z)")]
        public Vector3 localPosition;

        [Header("Lokale Rotation (Euler)")]
        public Vector3 localRotation;

        [Header("Lokale Skalierung")]
        public Vector3 localScale = Vector3.one;
    }

    [System.Serializable]
    public class NameOffsetEntry
    {
        [Header("Figurenname exakt wie im Hierarchy/Prefab (inkl. (Clone), falls vorhanden)")]
        public string figureName;

        [Header("Offsets je Seite")]
        public Vector3 offsetP1;
        public Vector3 offsetP2;
    }

    [System.Serializable]
    public class OffsetBlock
    {
        [Header("Zielobjekt, das verschoben werden soll")]
        public GameObject target;

        [Header("Eigene Offset-Tabelle für dieses Ziel")]
        public NameOffsetEntry[] table;
    }

    [Header("Individuelle Layouts")]
    public StatLayout[] layouts;

    [Header("Offsets: Stats Anzeige")]
    public OffsetBlock stats;

    [Header("Offsets: Health/Load Anzeige")]
    public OffsetBlock healthLoad;

    private void Awake()
    {
        Transform parent = transform.parent;

        // P2: Layout anwenden
        if (parent != null && parent.name == "P2_Figures")
        {
            ApplyLayout();
        }

        // Danach: Offsets je Block anwenden
        ApplyOffsetBlock(stats);
        ApplyOffsetBlock(healthLoad);
    }

    public void ApplyLayout()
    {
        if (layouts == null || layouts.Length == 0) return;

        foreach (var layout in layouts)
        {
            if (layout.targetObject == null) continue;

            RectTransform rt = layout.targetObject.GetComponent<RectTransform>();
            if (rt == null) continue;

            rt.localPosition = layout.localPosition;
            rt.localRotation = Quaternion.Euler(layout.localRotation);
            rt.localScale = layout.localScale;
        }
    }

    private void ApplyOffsetBlock(OffsetBlock block)
    {
        if (block == null || block.target == null) return;

        RectTransform rt = block.target.GetComponent<RectTransform>();
        if (rt == null) return;

        bool isP2 = IsP2Side();
        string figure = gameObject.name;

        Vector3 offset = GetOffsetFromTable(block.table, figure, isP2);
        if (offset == Vector3.zero) return;

        rt.localPosition += offset;
    }

    private bool IsP2Side()
    {
        Transform parent = transform.parent;
        return parent != null && parent.name == "P2_Figures";
    }

    private Vector3 GetOffsetFromTable(NameOffsetEntry[] table, string figureName, bool isP2)
    {
        if (table == null || table.Length == 0) return Vector3.zero;

        foreach (var entry in table)
        {
            if (entry == null) continue;
            if (!string.Equals(entry.figureName, figureName)) continue;

            return isP2 ? entry.offsetP2 : entry.offsetP1;
        }

        return Vector3.zero;
    }
}
