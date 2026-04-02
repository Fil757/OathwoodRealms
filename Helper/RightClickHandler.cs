using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class RightClickHandler : MonoBehaviour, IPointerClickHandler
{
    [Header("Pump-Effekt Einstellungen")]
    public float pumpScaleMultiplier = 1.08f;        // wie stark es anwächst
    public float pumpDuration = 0.12f;              // wie schnell hoch / runter

    private bool isPumping = false;
    private Vector3 originalScale;

    public bool isCard;
    public bool isField;
    public bool isTrap;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            OnRightClick();
        }
    }

    private void OnRightClick()
    {
        if(isCard)
        {
            InformationBoardHandler.current.Spawn_CardInfoComplete(gameObject);
        }

        if(isField)
        {
            InformationBoardHandler.current.Spawn_CardInfoComplete(gameObject.transform.parent.gameObject);
        }

        if(isTrap)
        {
            InformationBoardHandler.current.Transfer_FieldInfo_FromTrap(GetFieldCard(GetTrapIndex()));
        }

        StartCoroutine(PumpEffect());
    }

    private IEnumerator PumpEffect()
    {
        if (isPumping) yield break;
        isPumping = true;

        // Ziel des Pump-Effekts bestimmen
        Transform target = transform;
        if ((isField && transform.parent != null) || (isTrap && transform.parent != null))
            target = transform.parent;   // Pump-Effekt auf dem Parent

        Vector3 baseScale = target.localScale;
        Vector3 pumpedScale = baseScale * pumpScaleMultiplier;

        float t = 0f;
        while (t < pumpDuration)
        {
            t += Time.deltaTime;
            target.localScale = Vector3.Lerp(baseScale, pumpedScale, t / pumpDuration);
            yield return null;
        }

        t = 0f;
        while (t < pumpDuration)
        {
            t += Time.deltaTime;
            target.localScale = Vector3.Lerp(pumpedScale, baseScale, t / pumpDuration);
            yield return null;
        }

        target.localScale = baseScale;
        isPumping = false;
    }

    private int GetTrapIndex()
    {
        int trap_index = gameObject.transform.parent.gameObject.transform.GetSiblingIndex();
        return trap_index;
    }

    private FieldCard GetFieldCard(int fieldcard_index)
    {
        GameObject FieldControl_obj = GameObject.Find("FieldCardController");
        var field_contr = FieldControl_obj.GetComponent<FieldCardController>();
        FieldCard searched_Fieldcard = field_contr.ActiveTrapCard[fieldcard_index];

        return searched_Fieldcard;
    }

}
