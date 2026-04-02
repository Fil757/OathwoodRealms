using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(HorizontalOrVerticalLayoutGroup))]
public class DynamicLayoutSpacing : MonoBehaviour
{
    public enum LayoutType
    {
        Auto,
        Vertical,
        Horizontal
    }

    [Header("Layout-Typ-Auswahl")]
    [Tooltip("Auto: Script erkennt selbst, ob Vertical- oder HorizontalLayoutGroup vorhanden ist.")]
    public LayoutType layoutType = LayoutType.Auto;

    [Header("Referenzen (optional)")]
    [SerializeField]
    private VerticalLayoutGroup verticalLayoutGroup;

    [SerializeField]
    private HorizontalLayoutGroup horizontalLayoutGroup;

    [Header("Spacing-Einstellungen")]
    [Tooltip("Standard-Spacing, z.B. für 0 oder 1 Element.")]
    public float defaultSpacing = 0f;

    [Tooltip("Spacing, wenn genau 2 aktive Elemente vorhanden sind.")]
    public float spacingFor2 = 20f;

    [Tooltip("Spacing, wenn genau 3 aktive Elemente vorhanden sind.")]
    public float spacingFor3 = 10f;

    // interner Cache
    private int _lastActiveChildCount = -1;

    // gemeinsame Basis-Klasse für Vertical/Horizontal
    private HorizontalOrVerticalLayoutGroup _layoutGroup;

    private void Awake()
    {
        ResolveLayoutGroup();
        UpdateSpacing(); // initial
    }

    private void Update()
    {
        UpdateSpacing();
    }

    /// <summary>
    /// Ermittelt anhand der Einstellung (Auto/Vertical/Horizontal),
    /// welche LayoutGroup verwendet werden soll.
    /// </summary>
    private void ResolveLayoutGroup()
    {
        // Falls Referenzen im Inspector gelassen wurden, nichts erzwingen
        if (layoutType == LayoutType.Vertical)
        {
            if (!verticalLayoutGroup)
                verticalLayoutGroup = GetComponent<VerticalLayoutGroup>();

            _layoutGroup = verticalLayoutGroup;
        }
        else if (layoutType == LayoutType.Horizontal)
        {
            if (!horizontalLayoutGroup)
                horizontalLayoutGroup = GetComponent<HorizontalLayoutGroup>();

            _layoutGroup = horizontalLayoutGroup;
        }
        else // Auto
        {
            // Versuche zuerst Vertical
            if (!verticalLayoutGroup)
                verticalLayoutGroup = GetComponent<VerticalLayoutGroup>();

            if (verticalLayoutGroup)
            {
                _layoutGroup = verticalLayoutGroup;
                layoutType = LayoutType.Vertical; // zur Info im Inspector
                return;
            }

            // Sonst Horizontal
            if (!horizontalLayoutGroup)
                horizontalLayoutGroup = GetComponent<HorizontalLayoutGroup>();

            if (horizontalLayoutGroup)
            {
                _layoutGroup = horizontalLayoutGroup;
                layoutType = LayoutType.Horizontal; // zur Info im Inspector
                return;
            }

            Debug.LogWarning($"[DynamicLayoutSpacing] Keine passende LayoutGroup gefunden auf {gameObject.name}.");
        }

        if (_layoutGroup == null)
        {
            // Fallback, falls irgendwas schief ging:
            _layoutGroup = GetComponent<HorizontalOrVerticalLayoutGroup>();
        }
    }

    private void UpdateSpacing()
    {
        if (_layoutGroup == null)
            return;

        int activeChildCount = GetActiveChildCount();

        if (activeChildCount == _lastActiveChildCount)
            return;

        _lastActiveChildCount = activeChildCount;

        switch (activeChildCount)
        {
            case 2:
                _layoutGroup.spacing = spacingFor2;
                break;

            case 3:
                _layoutGroup.spacing = spacingFor3;
                break;

            default: // 0, 1 oder mehr als 3
                _layoutGroup.spacing = defaultSpacing;
                break;
        }
    }

    /// <summary>
    /// Zählt nur aktive Kinder (SetActive(true)).
    /// </summary>
    private int GetActiveChildCount()
    {
        int count = 0;

        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child.gameObject.activeSelf)
                count++;
        }

        // Sicherheit, wie von dir gewünscht maximal 3
        if (count > 3)
            count = 3;

        return count;
    }
}
