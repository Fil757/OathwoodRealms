using UnityEngine;

public class DisableMeshesOnStart : MonoBehaviour
{
    [Header("Zielobjekte (6 Stück)")]
    public GameObject[] targetObjects = new GameObject[6];

    private void Start()
    {
        foreach (var obj in targetObjects)
        {
            if (obj == null) continue;

            MeshFilter mf = obj.GetComponent<MeshFilter>();
            if (mf != null)
            {
                mf.mesh = null;
                Debug.Log($"Mesh von '{obj.name}' wurde auf None gesetzt.");
            }
            else
            {
                Debug.LogWarning($"Kein MeshFilter auf '{obj.name}' gefunden.");
            }
        }
    }
}

