using UnityEngine;

public class FloatAndRotate : MonoBehaviour
{
    [Header("Float")]
    public bool enableFloat = true;
    public float floatAmplitude = 0.2f;
    public float floatSpeed = 1f;
    public Vector3 floatAxis = Vector3.up; // z.B. (1,0,0) für X, (0,1,0) für Y, (0,0,1) für Z

    [Header("Rotation")]
    public bool enableRotation = true;
    public Vector3 rotationAxis = Vector3.up; // Achse der Rotation
    public float rotationSpeed = 45f;

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.localPosition;
    }

    void Update()
    {
        if (enableFloat)
        {
            float offset = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
            transform.localPosition = startPos + floatAxis.normalized * offset;
        }

        if (enableRotation)
        {
            transform.Rotate(rotationAxis.normalized, rotationSpeed * Time.deltaTime, Space.Self);
        }
    }
}
