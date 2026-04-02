using UnityEngine;
using System.Collections.Generic;

public class ChildActivityManager : MonoBehaviour
{
    [System.Serializable]
    public class ChildToggleEntry
    {
        [Tooltip("Objekt, dessen Kinder geprüft werden.")]
        public GameObject targetParent;

        [Tooltip("Objekt, das an/aus geschaltet wird, wenn der Parent Kinder aktiv hat.")]
        public GameObject targetVisual;
    }

    [Tooltip("Liste von Parent/Visual-Paaren, die überwacht werden sollen.")]
    public List<ChildToggleEntry> entries = new List<ChildToggleEntry>();

    void Update()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.targetParent == null || entry.targetVisual == null)
                continue;

            bool hasActiveChild = false;

            Transform parentTransform = entry.targetParent.transform;
            int childCount = parentTransform.childCount;

            for (int c = 0; c < childCount; c++)
            {
                var childGO = parentTransform.GetChild(c).gameObject;

                // activeSelf: nur direkt gesetzter Active-State
                // activeInHierarchy wäre inkl. Eltern
                if (childGO.activeSelf)
                {
                    hasActiveChild = true;
                    break;
                }
            }

            // Visual toggeln
            entry.targetVisual.SetActive(hasActiveChild);
        }
    }
}
