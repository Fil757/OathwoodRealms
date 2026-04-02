using UnityEngine;

public static class ObjectShaker
{
    /// <summary>
    /// Schneller, hochfrequenter Shake (lokale Position).
    /// </summary>
    public static void ShakeObject(GameObject obj, float intensitaet, float duration)
    {
        if (obj == null) return;

        Vector3 originalPos = obj.transform.localPosition;
        int shakeCount = Mathf.Max(4, Mathf.RoundToInt(duration * 25)); 
        // 25 Hz ~ sehr hoher Shake, passt für kurze intensive Effekte

        // Mehrfach nacheinander kleine Random-Moves
        LeanTween.value(0f, 1f, duration).setOnUpdate((float t) =>
        {
            // pro Frame ein neues Random-Ziel
            Vector3 offset = Random.insideUnitSphere * intensitaet;
            obj.transform.localPosition = originalPos + offset;
        })
        .setOnComplete(() =>
        {
            // zuverlässig zurücksetzen
            LeanTween.moveLocal(obj, originalPos, 0.05f);
        });
    }
}

