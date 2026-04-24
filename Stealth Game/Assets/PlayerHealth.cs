using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;

    [Header("UI")]
    public Slider healthSlider;
    public Image healthFillImage;

    [Header("Colors")]
    public Color normalHealthColor = Color.green;
    public Color damageHealthColor = Color.red;

    [Header("Smooth Settings")]
    public float flashToDamageDuration = 0.08f;
    public float flashBackDuration = 0.25f;
    public float sliderSmoothSpeed = 8f;

    private Coroutine flashCoroutine;
    private float displayedHealth;

    void Start()
    {
        currentHealth = maxHealth;
        displayedHealth = currentHealth;

        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        if (healthFillImage != null)
        {
            healthFillImage.color = normalHealthColor;
        }
    }

    void Update()
    {
        if (healthSlider != null)
        {
            displayedHealth = Mathf.Lerp(
                displayedHealth,
                currentHealth,
                sliderSmoothSpeed * Time.deltaTime
            );

            if (Mathf.Abs(displayedHealth - currentHealth) < 0.05f)
                displayedHealth = currentHealth;

            healthSlider.value = displayedHealth;
        }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;

        if (currentHealth < 0)
            currentHealth = 0;

        if (healthFillImage != null)
        {
            if (flashCoroutine != null)
                StopCoroutine(flashCoroutine);

            flashCoroutine = StartCoroutine(SmoothFlashHealthBar());
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        if (GameUIManager.Instance != null)
            GameUIManager.Instance.ShowGameOver();
    }

    IEnumerator SmoothFlashHealthBar()
    {
        float time = 0f;

        while (time < flashToDamageDuration)
        {
            time += Time.unscaledDeltaTime;
            float t = time / flashToDamageDuration;
            healthFillImage.color = Color.Lerp(normalHealthColor, damageHealthColor, t);
            yield return null;
        }

        healthFillImage.color = damageHealthColor;

        time = 0f;

        while (time < flashBackDuration)
        {
            time += Time.unscaledDeltaTime;
            float t = time / flashBackDuration;
            healthFillImage.color = Color.Lerp(damageHealthColor, normalHealthColor, t);
            yield return null;
        }

        healthFillImage.color = normalHealthColor;
    }
}