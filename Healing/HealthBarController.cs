using UnityEngine;
using UnityEngine.UI;

public class HealthBarController : MonoBehaviour
{
    public static HealthBarController current;

    private void Awake()
    {
        current = this;
    }

    public void ChangeHealthBar(float amount, Image healthBar, float currentHealth, float maxHealth)
    {
        if (healthBar != null)
        {
            var currentHealth_new = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
            healthBar.fillAmount = Mathf.Clamp01(currentHealth_new / maxHealth);
        }
    }
}

