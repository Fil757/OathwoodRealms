using UnityEngine;
using UnityEngine.UI;

public class FigureButtons : MonoBehaviour
{
    // --- Static: immer nur eine aktive Instanz ---
    public static FigureButtons Current;

    [Header("Buttonfeld Root (dieses Objekt wird aktiv/inaktiv gesetzt)")]
    public GameObject buttonsRoot;              // NICHT das Script-Objekt, sondern das UI-Buttonfeld selbst

    [Header("Referenzen")]
    public RectTransform buttonGroup;           // Container der bewegt wird
    public Image[] fadeImages;                  // Alle Images die eingefadet werden sollen

    [Header("Positionen (lokal / anchoredPosition)")]
    public Vector2 startPositionA;              // Ausgangsposition (tiefer)
    public Vector2 endPositionB;                // Basis-Endposition (Fallback)

    [Header("Layout-basierte End-Positionen (überschreiben endPositionB)")]
    [Tooltip("Wenn aktiv, wird die Endposition abhängig davon gewählt, ob der Parent-Slot allein / zu zweit / zu dritt ist und an welcher Stelle.")]
    public bool useLayoutBasedEndPosition = true;

    [Tooltip("Parent-Slot (z.B. Figure_proto_v1) ist alleine in seinem Parent.")]
    public Vector2 endPos_Alone;

    [Tooltip("Parent-Slot ist 1. von 2 Kindern im Layout-Parent (Index 0 von 2).")]
    public Vector2 endPos_2_First;

    [Tooltip("Parent-Slot ist 2. von 2 Kindern im Layout-Parent (Index 1 von 2).")]
    public Vector2 endPos_2_Second;

    [Tooltip("Parent-Slot ist 1. von 3 Kindern im Layout-Parent (Index 0 von 3).")]
    public Vector2 endPos_3_First;

    [Tooltip("Parent-Slot ist 2. von 3 Kindern im Layout-Parent (Index 1 von 3).")]
    public Vector2 endPos_3_Second;

    [Tooltip("Parent-Slot ist 3. von 3 Kindern im Layout-Parent (Index 2 von 3).")]
    public Vector2 endPos_3_Third;

    [Header("Animationseinstellungen (Move/Fade)")]
    public float moveDuration = 0.25f;
    public float fadeDuration = 0.2f;
    public LeanTweenType moveEase = LeanTweenType.easeOutQuad;
    public LeanTweenType fadeEase = LeanTweenType.easeOutQuad;

    [Header("Scale Animation")]
    public Vector3 startScale = Vector3.zero;
    public Vector3 endScale = new Vector3(0.25f, 0.3f, 0.3f);
    public float scaleDuration = 0.25f;
    public LeanTweenType scaleEase = LeanTweenType.easeOutQuad;

    [Header("Auto-Hide")]
    public bool autoHide = true;
    public float autoHideDelay = 3.5f; // Sekunden bis zum automatischen Ausblenden

    [Header("Klickbereich für 'Outside Click to Hide'")]
    public bool hideOnClickOutside = true;
    public RectTransform clickArea;            // Wenn leer -> buttonGroup
    public float paddingLeft = 50f;
    public float paddingRight = 50f;
    public float paddingTop = 100f;
    public float paddingBottom = 50f;
    public Camera uiCamera;                    // Wird in Awake automatisch als "UI-Game-Camera" gesucht

    private LTDescr _moveTween;
    private LTDescr[] _fadeTweens;
    private LTDescr _autoHideTween;
    private LTDescr _scaleTween;

    private void Awake()
    {
        // UI-Kamera automatisch suchen, falls nicht gesetzt
        if (uiCamera == null)
        {
            foreach (var cam in Camera.allCameras)
            {
                if (cam != null && cam.name == "UI-Game-Camera")
                {
                    uiCamera = cam;
                    break;
                }
            }
        }

        if (clickArea == null)
            clickArea = buttonGroup;

        ResetVisualState();

        // Buttons initial ausblenden
        if (buttonsRoot != null)
            buttonsRoot.SetActive(false);
    }

    /// <summary>
    /// Bringt Position, Scale und Alpha in einen definierten Ausgangszustand.
    /// </summary>
    private void ResetVisualState()
    {
        if (buttonGroup != null)
        {
            buttonGroup.anchoredPosition = startPositionA;
            buttonGroup.localScale       = startScale;
        }

        if (fadeImages != null)
        {
            foreach (var img in fadeImages)
            {
                if (img != null)
                {
                    var c = img.color;
                    img.color = new Color(c.r, c.g, c.b, 0.5f); // Grundzustand: halbtransparent
                }
            }
        }
    }

    private void Update()
    {
        if (!hideOnClickOutside)
            return;

        // Nur die aktuell aktive Instanz reagiert auf Outside-Klicks
        if (Current != this)
            return;

        if (buttonsRoot == null || !buttonsRoot.activeSelf)
            return; // nichts zu tun, wenn ausgeblendet

        if (!Input.GetMouseButtonDown(0))
            return; // nur auf Mausklick reagieren

        RectTransform targetRect = clickArea != null ? clickArea : buttonGroup;
        if (targetRect == null)
            return;

        Vector2 mousePos = Input.mousePosition;
        Vector2 localPoint;

        // Screen -> Local im Rect
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, mousePos, uiCamera, out localPoint))
            return;

        // Rect um Padding erweitern
        Rect r = targetRect.rect;
        r.xMin -= paddingLeft;
        r.xMax += paddingRight;
        r.yMin -= paddingBottom;
        r.yMax += paddingTop;

        // Wenn Klick AUSSERHALB -> Buttons sofort schließen
        if (!r.Contains(localPoint))
        {
            DeactivateButtons();
        }
    }

    public void ActivateButtons()
    {
        // --- TURN-LOCK: Nur P1 darf FigureButtons öffnen ---
        if (TurnManager.current == null || TurnManager.current.activePlayer == "P2")
            return;

        var lockComp = GetComponentInParent<FigureActionLock>(true);
        if (lockComp != null && lockComp.IsLocked)
            return;

        if (Current != null && Current != this)
        {
            Current.DeactivateButtons();
        }
        Current = this;

        if (buttonsRoot != null)
            buttonsRoot.SetActive(true);

        ResetVisualState();

        Vector2 resolvedEndPos = GetResolvedEndPosition();

        if (buttonGroup != null)
        {
            _moveTween = LeanTween.move(buttonGroup, resolvedEndPos, moveDuration)
                                .setEase(moveEase);

            buttonGroup.localScale = startScale;
            _scaleTween = LeanTween.scale(buttonGroup, endScale, scaleDuration)
                                .setEase(scaleEase);
        }

        StartFadeTweens(0.5f, 1f);

        if (autoHide && buttonsRoot != null)
        {
            _autoHideTween = LeanTween.delayedCall(buttonsRoot, autoHideDelay, () =>
            {
                DeactivateButtons();
            });
        }

        TutorialHintManager.current.Show_EachFigureButton(1f);
    }


    /// <summary>
    /// Ermittelt die Ziel-Endposition abhängig von der Stellung des Parent-Slots
    /// (z.B. Figure_proto_v1) im Layout-Parent.
    /// Annahme:
    ///   - Diesem Skriptträger sein Parent ist der Slot (Model -> Parent = Figure_proto_v1)
    ///   - Der Slot selbst hängt als Child im Layout-Parent (der max. 3 Children hat)
    /// </summary>
    private Vector2 GetResolvedEndPosition()
    {
        if (!useLayoutBasedEndPosition || buttonGroup == null)
            return endPositionB;

        // Slot = Parent des Skriptträgers (z.B. Figure_proto_v1)
        Transform slot = transform.parent;
        if (slot == null)
            return endPositionB;

        // Layout-Parent = Parent des Slots
        Transform layoutRoot = slot.parent;
        if (layoutRoot == null)
            return endPositionB;

        int siblingCount = layoutRoot.childCount;
        int index        = slot.GetSiblingIndex(); // 0-basiert innerhalb des Layout-Parents

        // Debug optional:
        // Debug.Log($"[FigureButtons] layoutRoot={layoutRoot.name}, siblings={siblingCount}, index={index}");

        switch (siblingCount)
        {
            case 1:
                return endPos_Alone;

            case 2:
                if (index == 0) return endPos_2_First;
                if (index == 1) return endPos_2_Second;
                break;

            case 3:
                if (index == 0) return endPos_3_First;
                if (index == 1) return endPos_3_Second;
                if (index == 2) return endPos_3_Third;
                break;
        }

        // Fallback, wenn mehr als 3 Slots oder Index nicht 0..2
        return endPositionB;
    }

    /// <summary>
    /// Deaktiviert die Buttons immer sofort (kein animiertes Ausblenden).
    /// </summary>
    public void DeactivateButtons()
    {
        if (Current == this)
            Current = null;

        //CancelFadeTweens();
        //CancelAutoHideTween();

        //if (_moveTween  != null) LeanTween.cancel(_moveTween.uniqueId);
        //if (_scaleTween != null) LeanTween.cancel(_scaleTween.uniqueId);

        ResetVisualState();

        if (buttonsRoot != null)
            buttonsRoot.SetActive(false);
    }

    private void StartFadeTweens(float fromAlpha, float toAlpha, System.Action onComplete = null)
    {
        if (fadeImages == null || fadeImages.Length == 0)
        {
            onComplete?.Invoke();
            return;
        }

        int nonNullCount = 0;
        for (int i = 0; i < fadeImages.Length; i++)
        {
            if (fadeImages[i] != null)
                nonNullCount++;
        }

        if (nonNullCount == 0)
        {
            onComplete?.Invoke();
            return;
        }

        _fadeTweens = new LTDescr[fadeImages.Length];
        int finished = 0;

        for (int i = 0; i < fadeImages.Length; i++)
        {
            var img = fadeImages[i];
            if (img == null) continue;

            var c = img.color;
            img.color = new Color(c.r, c.g, c.b, fromAlpha);

            _fadeTweens[i] = LeanTween.value(buttonsRoot, fromAlpha, toAlpha, fadeDuration)
                                      .setEase(fadeEase)
                                      .setOnUpdate((float val) =>
                                      {
                                          var col = img.color;
                                          img.color = new Color(col.r, col.g, col.b, val);
                                      })
                                      .setOnComplete(() =>
                                      {
                                          finished++;
                                          if (finished >= nonNullCount)
                                              onComplete?.Invoke();
                                      });
        }
    }

    private void CancelFadeTweens()
    {
        if (_fadeTweens == null) return;

        for (int i = 0; i < _fadeTweens.Length; i++)
        {
            if (_fadeTweens[i] != null)
                LeanTween.cancel(_fadeTweens[i].uniqueId);
        }
    }

    private void CancelAutoHideTween()
    {
        if (_autoHideTween != null)
        {
            LeanTween.cancel(_autoHideTween.uniqueId);
            _autoHideTween = null;
        }
    }
}
