using UnityEngine; // Unity framework
using UnityEngine.UI; // UI components
using System.Collections; // Coroutines

public class PlayerHealth : MonoBehaviour // Manages player health
{
    public int maxHealth    = 100;  // Max HP // Maximum health
    public int currentHealth;       // Current HP // Current health

    [Header("UI")] // UI section
    public Slider healthSlider;      // HP bar // Health slider
    public Image  healthFillImage;   // Bar fill image // Health fill color

    [Header("Colors")] // Colors section
    public Color normalHealthColor = Color.green;  // Idle color // Normal green
    public Color damageHealthColor = Color.red;    // Hit color // Damage red

    [Header("Smooth Settings")] // Smooth settings section
    public float flashToDamageDuration = 0.08f;  // Seconds to flash red // Flash duration
    public float flashBackDuration     = 0.25f;  // Seconds to return green // Return duration
    public float sliderSmoothSpeed     = 8f;     // Bar lerp speed // Smoothing speed

    private Coroutine flashCoroutine;   // Running flash // Active coroutine
    private float     displayedHealth;  // Lerped HP shown on bar // Displayed value

    void Start() // Initialize health
    {
        // init // Initialize
        currentHealth   = maxHealth; // Set current to max
        displayedHealth = currentHealth; // Set displayed to current

        if (healthSlider != null) // Has slider
        {
            healthSlider.maxValue = maxHealth; // Set max value
            healthSlider.value    = currentHealth; // Set current value
        }

        if (healthFillImage != null) // Has fill image
            healthFillImage.color = normalHealthColor; // Set normal color
    }

    void Update() // Update health bar display
    {
        // smooth bar // Smooth animation
        if (healthSlider == null) return; // No slider
        displayedHealth = Mathf.Lerp(displayedHealth, currentHealth, sliderSmoothSpeed * Time.deltaTime); // Interpolate
        if (Mathf.Abs(displayedHealth - currentHealth) < 0.05f) // Close enough
            displayedHealth = currentHealth; // Snap to value
        healthSlider.value = displayedHealth; // Update slider
    }

    public void TakeDamage(int damage) // Take damage
    {
        // reduce // Reduce health
        currentHealth = Mathf.Max(0, currentHealth - damage); // Subtract damage

        // flash // Flash effect
        if (healthFillImage != null) // Has fill image
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine); // Stop existing
            flashCoroutine = StartCoroutine(SmoothFlashHealthBar()); // Start flash
        }

        // death // Death check
        if (currentHealth <= 0) Die(); // Dead
    }

    void Die() // Handle death
    {
        if (GameUIManager.Instance != null) // Has UI manager
            GameUIManager.Instance.ShowGameOver(); // Show game over
    }

    IEnumerator SmoothFlashHealthBar() // Animate health bar flash
    {
        // green → red // Flash red
        float time = 0f; // Elapsed time
        while (time < flashToDamageDuration) // While flashing
        {
            time += Time.unscaledDeltaTime; // Increment time
            healthFillImage.color = Color.Lerp(normalHealthColor, damageHealthColor, time / flashToDamageDuration); // Lerp to red
            yield return null; // Wait frame
        }
        healthFillImage.color = damageHealthColor; // Set to red

        // red → green // Return green
        time = 0f; // Reset time
        while (time < flashBackDuration) // While returning
        {
            time += Time.unscaledDeltaTime; // Increment time
            healthFillImage.color = Color.Lerp(damageHealthColor, normalHealthColor, time / flashBackDuration); // Lerp to green
            yield return null; // Wait frame
        }
        healthFillImage.color = normalHealthColor; // Set to green
    }
}
