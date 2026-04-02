using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoadPaySystem : MonoBehaviour
{
    public static LoadPaySystem current;

    void Awake()
    { current = this; }

    public void PayLoad(GameObject payingFigure, int cost)
    {
        // --- Guards ---
        if (!payingFigure)
        {
            Debug.LogWarning("[AttackController] PayLoad: payingFigure == null.");
            return;
        }

        var display = payingFigure.GetComponent<Display_Figure>();
        if (!display)
        {
            Debug.LogWarning($"[AttackController] PayLoad: Display_Figure fehlt auf '{payingFigure.name}'.");
            return;
        }

        // --- Logische Begrenzung ---
        cost = Mathf.Max(0, cost); // negative Kosten verhindern
        int newLoad = Mathf.Max(0, display.FIGURE_LOAD - cost);

        // --- Wert anwenden ---
        display.FIGURE_LOAD = newLoad;
    }

    private Image FindImageOnChildPath(Transform root, string relativePath)
    {
        if (!root) return null;
        Transform t = root.Find(relativePath);
        if (!t) return null;
        return t.GetComponent<Image>();
    }

}
