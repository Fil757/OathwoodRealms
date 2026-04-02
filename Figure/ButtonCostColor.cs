using UnityEngine;
using UnityEngine.UI;

public class ButtonCostColor : MonoBehaviour
{
    [Header("Icon Images")]
    public Image iconTarget;
    public Image iconAttack;
    public Image iconDefend;
    public Image iconSpecial;

    [Header("Figure Source (GameObject mit Display_Figure)")]
    public GameObject figureObject;

    [Header("Colors")]
    [Tooltip("Normale Icon-Farbe, wenn Aktion erlaubt ist.")]
    public Color enabledColor = Color.white;

    [Tooltip("Icon-Farbe, wenn Aktion NICHT erlaubt ist (z.B. zu wenig Load).")]
    public Color disabledColor = new Color(0.35f, 0.15f, 0.15f, 1f); // dunkel/rot

    private Display_Figure _df;

    private void Awake()
    {
        CacheDisplayFigure();
    }

    private void OnEnable()
    {
        CacheDisplayFigure();
        ApplyColors(); // sofort korrekt beim Aktivieren
    }

    private void Update()
    {
        ApplyColors();
    }

    private void CacheDisplayFigure()
    {
        if (figureObject == null)
        {
            _df = null;
            return;
        }

        _df = figureObject.GetComponent<Display_Figure>();
    }

    private void ApplyColors()
    {
        if (_df == null)
        {
            // Wenn keine Quelle vorhanden ist, lieber alles "enabled" lassen,
            // damit du nicht aus Versehen UI permanent dunkel machst.
            SetIcon(iconTarget, true);
            SetIcon(iconAttack, true);
            SetIcon(iconDefend, true);
            SetIcon(iconSpecial, true);
            return;
        }

        int load = _df.FIGURE_LOAD;

        bool canDoNormal = load >= _df.FIGURE_COST;       // Attack/Defend/Target
        bool canDoSpecial = load >= _df.FIGURE_COST_SPC;  // Special

        SetIcon(iconTarget, canDoNormal);
        SetIcon(iconAttack, canDoNormal);
        SetIcon(iconDefend, canDoNormal);
        SetIcon(iconSpecial, canDoSpecial);
    }

    private void SetIcon(Image img, bool enabled)
    {
        if (img == null) return;
        img.color = enabled ? enabledColor : disabledColor;
    }
}
