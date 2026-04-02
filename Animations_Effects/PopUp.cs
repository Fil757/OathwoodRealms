using UnityEngine;
using TMPro;

public class PopUp : MonoBehaviour
{
    #region Inspector (gemäß Skizze)

    public static PopUp current;

    [Header("Prefab")]
    public GameObject Prefab;

    [Header("Opacity")]
    public float opacity_speed_in = 8f;
    public float full_opacity_hold_time = 0.25f;
    public float opacity_speed_out = 6f;

    [Header("Flight (lokaler Offset relativ zu popUpRoot)")]
    public float flight_height_x = 0f;
    public float flight_height_y = 80f;
    public float flight_height_z = 0f;
    public float flight_speed = 280f; // Einheiten pro Sek.

    [Header("Scale (World Scale)")]
    public float scalefactor = 1.1f;
    public float scaletime = 0.12f;

    [Header("World-Space Canvas")]
    public Camera worldSpaceCamera; // optional – wenn leer, wird Camera.main verwendet

    #endregion

    void Awake()
    {
        current = this;
    }

    #region Public API (gemäß Skizze)

    public void CreatePopUp(
        string popUpType,
        int popUpValue,
        GameObject popUpRoot,
        Vector3 popUpOffsetPos,
        Vector3 popUpOffsetRot,
        string popUpLayer,
        bool figureLayering = true)
    {
        // --- 1) Welt-Start-Transform aus Root + lokalen Offsets berechnen
        Transform rootT = popUpRoot.transform;

        Vector3 worldStartPos = rootT.TransformPoint(popUpOffsetPos);
        Quaternion worldStartRot = rootT.rotation * Quaternion.Euler(popUpOffsetRot);

        // --- 2) Instanz erzeugen (WELT-Position/Rotation, Parent optional dranhängen)
        var inst = Instantiate(Prefab, worldStartPos, worldStartRot, rootT);


        // --- 3) World-Space-Canvas konfigurieren (entscheidend für „echtes“ Layering via Kamera)
        if (figureLayering)
        {
            var cv = inst.GetComponentInChildren<Canvas>(true);
            if (!cv) cv = inst.AddComponent<Canvas>();
            cv.renderMode = RenderMode.WorldSpace;
            cv.worldCamera = worldSpaceCamera ? worldSpaceCamera : Camera.main;
            cv.overrideSorting = true;                // eigene Zeichenebene
            cv.sortingLayerName = "UI";               // optional (rein für Reihenfolge vs. andere Canvases)
            cv.sortingOrder = 0;
        }

        // --- 4) GameObject-Layer rekursiv setzen (Kamera-Culling entscheidet Sichtbarkeit)
        int layer = LayerMask.NameToLayer(popUpLayer);
        SetLayerRecursively(inst, layer);

        // --- 5) Farbe nach Typ + Textwert
        var tmp = inst.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            Color c = Color.white;
            if (popUpType == "Damage") c = new Color(1f, 0.2f, 0.2f, 1f);
            if (popUpType == "Healing") c = new Color(0.2f, 1f, 0.2f, 1f);
            if (popUpType == "Loading") c = new Color(0.38f, 0.93f, 0.92f, 1f); // hellblau/türkis
            tmp.color = c;
            tmp.text = popUpValue.ToString();
        }

        // --- 6) Animation (World Space)
        PopUpAnimation(inst, rootT);
    }

    #endregion

    #region Private Animation (gemäß Skizze, World-Space)

    private void PopUpAnimation(GameObject popObj, Transform rootT)
    {
        // Start (Welt)
        Vector3 startWorld = popObj.transform.position;

        // Flugziel = Start + lokaler Flug-Offset relativ zu rootT → in Welt umrechnen
        Vector3 localFlight = new Vector3(flight_height_x, flight_height_y, flight_height_z);
        Vector3 endWorld = startWorld + rootT.TransformVector(localFlight);

        // Dauer aus Strecke / Geschwindigkeit
        float dist = Vector3.Distance(startWorld, endWorld);
        float flightDuration = (flight_speed <= 0f) ? 0f : dist / flight_speed;

        // Scale (World Scale)
        LeanTween.scale(popObj, Vector3.one * scalefactor, Mathf.Max(0f, scaletime))
                 .setEase(LeanTweenType.easeOutQuad);

        // Move (Weltkoordinaten)
        LeanTween.move(popObj, endWorld, Mathf.Max(0f, flightDuration))
                 .setEase(LeanTweenType.easeOutQuad);

        // Opacity (TMP a) – In → Hold → Out
        var tmp = popObj.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            var c = tmp.color; c.a = 0f; tmp.color = c;

            LeanTween.value(popObj, 0f, 1f, 1f / Mathf.Max(0.0001f, opacity_speed_in))
                .setOnUpdate((float a) => { var cc = tmp.color; cc.a = a; tmp.color = cc; })
                .setOnComplete(() =>
                {
                    LeanTween.delayedCall(popObj, Mathf.Max(0f, full_opacity_hold_time), () =>
                    {
                        LeanTween.value(popObj, 1f, 0f, 1f / Mathf.Max(0.0001f, opacity_speed_out))
                            .setOnUpdate((float a2) => { var cc2 = tmp.color; cc2.a = a2; tmp.color = cc2; })
                            .setOnComplete(() =>
                            {
                                Destroy(popObj);
                            });
                    });
                });
        }
        else
        {
            // Fallback-Lebenszeit
            LeanTween.delayedCall(popObj, Mathf.Max(flightDuration, 0.4f) + full_opacity_hold_time, () =>
            {
                Destroy(popObj);
            });
        }
    }

    #endregion

    #region Helpers

    private void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        for (int i = 0; i < go.transform.childCount; i++)
            SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
    }

    #endregion

    public void FigurePopUp(string type, int value, GameObject root)
    {

        // --- Parent bestimmen ---
        string parentName = root.transform.parent ? root.transform.parent.name : string.Empty;
        bool isP1 = parentName == "P1_Figures";
        string selfLayer = isP1 ? "P1-GUI-Layer" : "P2-GUI-Layer";
        string oppLayer = isP1 ? "P2-GUI-Layer" : "P1-GUI-Layer";

        // --- Feste PopUp-Offsets und Rotationen ---
        Vector3 selfPosOffset = new Vector3(0f, -30f, -50f);
        Vector3 selfRotOffset = new Vector3(-90f, 0f, 0f);

        Vector3 oppPosOffset = new Vector3(0f, 30f, -50f);
        Vector3 oppRotOffset = new Vector3(90f, 0f, -180f);

        // --- PopUps erzeugen ---
        CreatePopUp(
            type,
            value,
            root,
            selfPosOffset,
            selfRotOffset,
            selfLayer
        );

        CreatePopUp(
            type,
            value,
            root,
            oppPosOffset,
            oppRotOffset,
            oppLayer
        );
    }
}
