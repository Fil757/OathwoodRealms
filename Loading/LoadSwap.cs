using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoadSwap : MonoBehaviour
{
    public GameObject Player_1;
    public GameObject LoadSwapBallPrefab;

    public void SwapLoadFigureToPlayer1(int swap_amount)
    {
        GameObject turnmng_obj = GameObject.Find("TurnManager");
        TurnManager turnmng_script = turnmng_obj.GetComponent<TurnManager>();
        GameObject current_fig = turnmng_script.current_figure_P1;

        Display_Figure current_fig_display = current_fig.GetComponent<Display_Figure>();
        Display_Figure player_1_display = Player_1.GetComponent<Display_Figure>();

        if (current_fig_display.FIGURE_LOAD < swap_amount)
        {
            Messagebox.current.ShowMessageBox();
            return;
        }

        LoadToFigure.current.LoadingToFigure(current_fig, -1 * swap_amount);
        //LoadToFigure.current.LoadingToFigure(Player_1, swap_amount);
        LoadBarController.current.PayLoad(swap_amount, current_fig_display.LoadBar);

        StartCoroutine(FireBurst(
        LoadSwapBallPrefab,                  // dein Prefab
        current_fig.transform.position,      // Start
        Player_1.transform.position,         // Ziel
        950f,                                // Geschwindigkeit
        swap_amount,                         // Anzahl
        0.1f,                                // Abstand
        LeanTweenType.linear                 // Typ
        ));


    }

    public void SwapLoadPlayer1ToFigure(int swap_amount)
    {
        GameObject turnmng_obj = GameObject.Find("TurnManager");
        TurnManager turnmng_script = turnmng_obj.GetComponent<TurnManager>();
        GameObject current_fig = turnmng_script.current_figure_P1;

        Display_Figure current_fig_display = current_fig.GetComponent<Display_Figure>();
        Display_Figure player_1_display = Player_1.GetComponent<Display_Figure>();

        if (player_1_display.FIGURE_LOAD < swap_amount)
        {
            Messagebox.current.ShowMessageBox();
            return;
        }

        LoadToFigure.current.LoadingToFigure(current_fig, swap_amount);
        //LoadToFigure.current.LoadingToFigure(Player_1, -1 * swap_amount);
        //LoadBarController.current.PayLoad(swap_amount, Player_1.transform.Find("Health_Load/LoadBar").GetComponent<Image>());

        StartCoroutine(FireBurst(
        LoadSwapBallPrefab,
        Player_1.transform.position,               
        current_fig.transform.position,         
        950f,                               
        swap_amount,                        
        0.1f,                              
        LeanTweenType.linear               
        ));
    }

    public static IEnumerator FireBurst(GameObject prefab, Vector3 startPos, Vector3 targetPos, float speed,
    int count = 10, float interval = 0.1f,
    LeanTweenType easeType = LeanTweenType.linear)
    {
        for (int i = 0; i < count; i++)
        {
            MovePrefab(prefab, startPos, targetPos, speed, easeType);
            yield return new WaitForSeconds(interval);
        }
    }

    public static void MovePrefab(GameObject prefab, Vector3 startPos, Vector3 targetPos, float speed,
    LeanTweenType easeType = LeanTweenType.linear, System.Action<GameObject> onComplete = null)
    {
        if (prefab == null) return;

        // Prefab instanzieren
        GameObject obj = Object.Instantiate(prefab);

        // Startposition setzen
        obj.transform.position = startPos;

        // Distanz
        float distance = Vector3.Distance(startPos, targetPos);

        // Dauer = Distanz / Speed → Speed ist jetzt „units per second“
        if (speed <= 0f) speed = 1f;
        float duration = distance / speed;

        // LeanTween-Animation
        LeanTween.move(obj, targetPos, duration)
            .setEase(easeType)
            .setOnComplete(() =>
            {
                onComplete?.Invoke(obj);
                Object.Destroy(obj);
            });
    }
}
