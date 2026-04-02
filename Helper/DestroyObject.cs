using UnityEngine;

public class DestroyObject : MonoBehaviour
{

    public void DestroyTarget(GameObject target)
    {
        if (target == null)
        {
            Debug.LogWarning("Kein Zielobjekt zugewiesen.");
            return;
        }

        Destroy(target);
    }
}
