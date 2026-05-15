using TMPro; // TextMeshPro UI
using UnityEngine; // Unity framework

public class KillCounter : MonoBehaviour // Tracks kill count
{
    public static KillCounter Instance; // Singleton instance

    [Header("UI")] // UI section
    public TMP_Text killsText;      // "Kills: X" // Kills display
    public TMP_Text remainingText;  // "Remaining: X" // Remaining display

    private int totalGuards;   // Set on start // Total guard count
    private int killedGuards;  // Incremented per kill // Killed count

    void Awake() // Singleton initialization
    {
        // singleton // Singleton setup
        if (Instance == null) Instance = this; // Set instance
        else Destroy(gameObject); // Destroy duplicate
    }

    void Start() // Initialize on start
    {
        // count // Count guards
        totalGuards  = FindObjectsByType<GuardAI>(FindObjectsSortMode.None).Length; // Find all guards
        killedGuards = 0; // Start at zero
        UpdateUI(); // Display initial state
    }

    public void AddKill() // Increment kill counter
    {
        killedGuards++; // Increment kills
        UpdateUI(); // Update display

        // win check // Check for victory
        if (killedGuards >= totalGuards && GameUIManager.Instance != null) // All dead
            GameUIManager.Instance.ShowWin(); // Show win screen
    }

    public int GetRemainingGuards() => totalGuards - killedGuards; // Get remaining guards
    public int GetKilledGuards()    => killedGuards; // Get killed count

    void UpdateUI() // Update UI text
    {
        if (killsText     != null) killsText.text     = "Kills: "     + killedGuards; // Update kills
        if (remainingText != null) remainingText.text = "Remaining: " + (totalGuards - killedGuards); // Update remaining
    }
}
