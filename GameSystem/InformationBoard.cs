using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class InformationBoard : MonoBehaviour
{
    // --- UI Referenzen ---
    public TextMeshProUGUI nameText;
    public Image portrait;

    public TextMeshProUGUI healthText;
    public TextMeshProUGUI atkText;
    public TextMeshProUGUI defText;

    public TextMeshProUGUI cast_costText;
    public TextMeshProUGUI std_costText;
    public TextMeshProUGUI spell_costText;

    public TextMeshProUGUI spell_description;
    public TextMeshProUGUI castspell_description;
    public TextMeshProUGUI fieldeffect_descrpition;

    // --- Daten, die gesetzt werden sollen ---
    // Name + Descriptions = string
    public string nameString;
    public string spell_descriptionString;
    public string castspell_descriptionString;
    public string fieldeffect_descriptionString;

    // Int-Werte
    public int health;
    public int atk;
    public int def;

    public int cast_cost;
    public int std_cost;
    public int spell_cost;

    // Portrait → Sprite
    public Sprite portraitSprite;

    // --- Fade-In Settings ---
    [Header("Fade-In")]
    [Tooltip("Dauer des Fade-In von 0 auf 1 Alpha.")]
    public float fadeDuration = 0.35f;

    private TextMeshProUGUI[] _allTexts;
    private Image[] _allImages;

    private void Awake()
    {
        // Alle Texte & Images im Board sammeln
        _allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
        _allImages = GetComponentsInChildren<Image>(true);

        // Beim Erzeugen ALLES unsichtbar machen (Alpha 0)
        SetAllAlpha(0f);
    }

    private void Update()
    {
        // Strings
        if (nameText != null) nameText.text = nameString;
        if (spell_description != null) spell_description.text = spell_descriptionString;
        if (castspell_description != null) castspell_description.text = castspell_descriptionString;
        if (fieldeffect_descrpition != null) fieldeffect_descrpition.text = fieldeffect_descriptionString;

        // Zahlen
        if (healthText != null) healthText.text = health.ToString();
        if (atkText != null) atkText.text = atk.ToString();
        if (defText != null) defText.text = def.ToString();

        if (cast_costText != null) cast_costText.text = cast_cost.ToString();
        if (std_costText != null) std_costText.text = std_cost.ToString();
        if (spell_costText != null) spell_costText.text = spell_cost.ToString();

        // Bild
        if (portrait != null) portrait.sprite = portraitSprite;
    }

    // --------- Öffentlicher Einstieg für den Fade-In nach Datentransfer ---------
    public void PlayFadeIn()
    {
        StopAllCoroutines();
        StartCoroutine(FadeInRoutine());
    }

    // --------- Hilfs-Routinen ---------
    private IEnumerator FadeInRoutine()
    {
        float elapsed = 0f;

        // Start bei Alpha 0 sicherstellen
        SetAllAlpha(0f);

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);

            SetAllAlpha(t);

            yield return null;
        }

        // Sicherheit: auf exakt 1 setzen
        SetAllAlpha(1f);
    }

    private void SetAllAlpha(float alpha)
    {
        if (_allTexts != null)
        {
            foreach (var txt in _allTexts)
            {
                if (txt == null) continue;
                Color c = txt.color;
                c.a = alpha;
                txt.color = c;
            }
        }

        if (_allImages != null)
        {
            foreach (var img in _allImages)
            {
                if (img == null) continue;
                Color c = img.color;
                c.a = alpha;
                img.color = c;
            }
        }
    }
}
