using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class ForceCanvasCamera : MonoBehaviour
{
    [Header("Soll-Kamera für diesen Canvas")]
    public Camera targetCamera;

    private Canvas _canvas;

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
        ApplyCamera();
    }

    private void OnEnable()
    {
        ApplyCamera();
    }

    private void Update()
    {
        // Falls die Kamera zur Laufzeit verloren geht -> erneut setzen
        if (_canvas.worldCamera == null && targetCamera != null)
        {
            ApplyCamera();
        }
    }

    private void ApplyCamera()
    {
        if (targetCamera == null)
        {
            Debug.LogWarning($"[ForceCanvasCamera] Keine Kamera zugewiesen auf {name}", this);
            return;
        }

        _canvas.worldCamera = targetCamera;
    }
}

