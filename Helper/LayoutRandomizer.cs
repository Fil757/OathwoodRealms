using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(HorizontalOrVerticalLayoutGroup))]
public class LayoutRandomizer : MonoBehaviour
{
    [Range(0f, 100f)] public float offsetRange = 10f;
    [Range(0f, 10f)] public float delay = 0.05f;
    public bool randomizeRotation = false;
    [Range(0f, 15f)] public float rotationRange = 5f;

    private void Start()
    {
        StartCoroutine(ApplyRandomOffset());
    }

    private IEnumerator ApplyRandomOffset()
    {
        // kurz warten, bis LayoutGroup fertig angeordnet hat
        yield return new WaitForEndOfFrame();

        foreach (RectTransform child in transform)
        {
            if (!child.gameObject.activeSelf) continue;

            Vector3 pos = child.localPosition;
            pos.x += Random.Range(-offsetRange, offsetRange);
            pos.y += Random.Range(-offsetRange, offsetRange);
            pos.z += Random.Range(-offsetRange, offsetRange);
            child.localPosition = pos;

            if (randomizeRotation)
            {
                child.localRotation = Quaternion.Euler(0, 0, Random.Range(-rotationRange, rotationRange));
            }

            yield return new WaitForSeconds(delay);
        }
    }
}

