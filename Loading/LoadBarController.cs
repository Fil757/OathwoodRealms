using UnityEngine;
using UnityEngine.UI;

public class LoadBarController : MonoBehaviour
{
    public static LoadBarController current;

    private void Awake()
    {
        current = this;
    }

    public void PayLoad(float amount, Image LoadBar)
    {
        //LoadBar.fillAmount -= Mathf.Clamp01(amount/100);
    }

    public void GetLoad(float amount, Image LoadBar)
    {
        //LoadBar.fillAmount += Mathf.Clamp01(amount/100);
    }
}


