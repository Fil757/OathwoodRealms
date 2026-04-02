using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpecialMoveButton : MonoBehaviour
{
    [SerializeField] Button btnA, btnB;
    [SerializeField] TMP_Text txtA, txtB;

    SpecialMoveController controller;
    Display_Figure current;
    SpecialMove moveA, moveB;

    public void Show(Display_Figure display, SpecialMove a, SpecialMove b, SpecialMoveController ctrl)
    {
        current    = display;
        controller = ctrl;
        moveA      = a;
        moveB      = b;

        SetupButton(btnA, txtA, moveA);
        SetupButton(btnB, txtB, moveB);

        gameObject.SetActive((moveA != null) || (moveB != null));
    }

    public void Hide() => gameObject.SetActive(false);

    void SetupButton(Button btn, TMP_Text txt, SpecialMove move)
    {
        if (move == null) { btn.gameObject.SetActive(false); return; }

        btn.gameObject.SetActive(true);
        if (txt) txt.text = $"{move.SPECIALMOVE_TITLE}  [{move.SPECIALMOVE_LOAD_COST}%]";

        // Interactable nach aktuellem Load der Figur
        bool canAfford = current != null && current.FIGURE_LOAD >= move.SPECIALMOVE_LOAD_COST;
        btn.interactable = canAfford;

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            //controller.TryExecute(current, move);
            //Hide();
        });
    }

    // Optional public, falls du im laufenden Zug die Panel-States refreshen willst
    public void RefreshStates()
    {
        SetupButton(btnA, txtA, moveA);
        SetupButton(btnB, txtB, moveB);
    }
}


