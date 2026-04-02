using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpecialButtonLauncher : MonoBehaviour
{
    [SerializeField] SpecialMoveButton specialPanel;
    [SerializeField] SpecialMoveController controller;
    [SerializeField] TurnManager turnManager;

    public void OnClickOpenSpecialPanel()
    {
        var activeGO = turnManager.current_figure_P1 != null
            ? turnManager.current_figure_P1
            : turnManager.current_figure_P2;
        if (!activeGO) return;

        var disp  = activeGO.GetComponent<Display_Figure>();
        var moveA = disp.GetSpecialA();   // kommt aus Display_Figure (siehe vorige Nachricht)
        var moveB = disp.GetSpecialB();

        specialPanel.Show(disp, moveA, moveB, controller);
    }
}
