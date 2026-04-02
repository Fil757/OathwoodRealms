using UnityEngine;

public class PhysicsRayCastDebugger : MonoBehaviour
{
    public Camera cam;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (cam == null) cam = Camera.main;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                Debug.Log("Physics Hit: " + hit.collider.name + " | Layer: " + hit.collider.gameObject.layer);
            }
        }
    }
}