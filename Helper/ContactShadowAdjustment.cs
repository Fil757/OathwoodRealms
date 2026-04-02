using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ContactShadowAdjustment : MonoBehaviour
{
    [Serializable]
    public class ShadowOverride
    {
        [Tooltip("Exakter Name des Ziel-Objekts (Ancestor), z.B. \"F0013\"")]
        public string AncestorName;

        [Tooltip("Breite/Höhe des Contact-Shadow RectTransforms")]
        public Vector2 Size = new Vector2(200f, 60f);

        [Tooltip("Optional: Wenn true, wird zusätzlich anchoredPosition gesetzt.")]
        public bool OverrideAnchoredPosition = false;

        public Vector2 AnchoredPosition = Vector2.zero;
    }

    [Header("References")]
    [Tooltip("Das UI-Image des Kontaktschattens (oder leer lassen -> nimmt Image auf diesem Objekt).")]
    public Image contactShadowImage;

    [Header("Ancestor Detection")]
    [Tooltip("Wie viele Eltern hoch wird geschaut. 2 = Grandparent, 3 = Great-Grandparent, ...")]
    [Min(1)] public int ancestorSteps = 2;

    [Header("Defaults (Fallback, wenn keine Regel matcht)")]
    public bool captureDefaultFromCurrent = true;
    public Vector2 defaultSize = new Vector2(80f, 80f);
    public Vector2 defaultAnchoredPosition = Vector2.zero;

    [Header("Overrides")]
    public List<ShadowOverride> overrides = new List<ShadowOverride>();

    [Header("Robustness")]
    [Tooltip("Versucht in den ersten Frames mehrfach anzuwenden (hilft bei Reparenting nach OnEnable).")]
    [Range(0, 30)] public int startupRetries = 5;

    [Tooltip("Wenn true, wird die Größe in LateUpdate immer wieder gesetzt (falls andere Scripts/Layout es überschreiben).")]
    public bool lockAppliedSize = false;

    // intern
    private RectTransform _rt;
    private Vector2 _capturedDefaultSize;
    private Vector2 _capturedDefaultAnchoredPos;
    private bool _hasCapturedDefaults;

    private string _lastAncestorName;
    private ShadowOverride _lastMatch; // zuletzt gefundener Override (oder null)
    private bool _hasAppliedAtLeastOnce;

    private void Awake()
    {
        if (contactShadowImage == null)
            contactShadowImage = GetComponent<Image>();

        _rt = contactShadowImage != null ? contactShadowImage.rectTransform : GetComponent<RectTransform>();

        CaptureDefaultsIfNeeded();
    }

    private void OnEnable()
    {
        CaptureDefaultsIfNeeded();
        _hasAppliedAtLeastOnce = false;
        _lastAncestorName = null;
        _lastMatch = null;

        // Sofort versuchen + dann ein paar Frames retry (für Reparent/Setup)
        ApplyForCurrentAncestor(force: true);
        if (startupRetries > 0) StartCoroutine(StartupRetryRoutine());
    }

    private System.Collections.IEnumerator StartupRetryRoutine()
    {
        // ein paar Frames warten und jeweils neu anwenden
        for (int i = 0; i < startupRetries; i++)
        {
            yield return null; // nächster Frame
            ApplyForCurrentAncestor(force: true);
        }
    }

    private void LateUpdate()
    {
        if (!lockAppliedSize) return;

        // Wenn bereits einmal angewendet wurde, halten wir die Werte stabil
        if (_hasAppliedAtLeastOnce)
        {
            ApplyLastResultAgain();
        }
    }

    public void RefreshNow()
    {
        CaptureDefaultsIfNeeded();
        ApplyForCurrentAncestor(force: true);
    }

    private void CaptureDefaultsIfNeeded()
    {
        if (_rt == null) return;
        if (_hasCapturedDefaults) return;

        if (captureDefaultFromCurrent)
        {
            _capturedDefaultSize = _rt.sizeDelta;
            _capturedDefaultAnchoredPos = _rt.anchoredPosition;
        }
        else
        {
            _capturedDefaultSize = defaultSize;
            _capturedDefaultAnchoredPos = defaultAnchoredPosition;
        }

        _hasCapturedDefaults = true;
    }

    private void ApplyForCurrentAncestor(bool force)
    {
        if (_rt == null) return;

        string ancestorName = GetAncestorName(transform, ancestorSteps);

        if (!force && ancestorName == _lastAncestorName) return;
        _lastAncestorName = ancestorName;

        // Match suchen
        ShadowOverride match = null;
        if (!string.IsNullOrEmpty(ancestorName))
        {
            for (int i = 0; i < overrides.Count; i++)
            {
                var o = overrides[i];
                if (o == null) continue;

                if (string.Equals(o.AncestorName, ancestorName, StringComparison.Ordinal))
                {
                    match = o;
                    break;
                }
            }
        }

        _lastMatch = match;

        if (match == null)
        {
            ApplyDefaults();
        }
        else
        {
            ApplySize(match.Size);
            if (match.OverrideAnchoredPosition) _rt.anchoredPosition = match.AnchoredPosition;
            else _rt.anchoredPosition = _capturedDefaultAnchoredPos;
        }

        _hasAppliedAtLeastOnce = true;
    }

    private void ApplyLastResultAgain()
    {
        if (_rt == null) return;

        if (_lastMatch == null)
        {
            ApplyDefaults();
        }
        else
        {
            ApplySize(_lastMatch.Size);
            if (_lastMatch.OverrideAnchoredPosition) _rt.anchoredPosition = _lastMatch.AnchoredPosition;
            else _rt.anchoredPosition = _capturedDefaultAnchoredPos;
        }
    }

    private void ApplyDefaults()
    {
        ApplySize(_capturedDefaultSize);
        _rt.anchoredPosition = _capturedDefaultAnchoredPos;
    }

    private void ApplySize(Vector2 size)
    {
        // SetSizeWithCurrentAnchors ist oft robuster als nur sizeDelta.
        _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
    }

    private static string GetAncestorName(Transform t, int stepsUp)
    {
        if (t == null) return null;

        Transform cur = t;
        for (int i = 0; i < stepsUp; i++)
        {
            if (cur.parent == null) return null;
            cur = cur.parent;
        }
        return cur != null ? cur.name : null;
    }
}
