using UnityEngine;

public class DeactivateTrapP2Button : MonoBehaviour
{
    public GameObject ButtonToDeactivate;

    void Awake()
    {
        if (gameObject.transform.parent.name == "Traps_P2"){ButtonToDeactivate.SetActive(false);}
    }
}
