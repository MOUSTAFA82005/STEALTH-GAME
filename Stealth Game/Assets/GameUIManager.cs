using UnityEngine; // Unity framework
using UnityEngine.SceneManagement; // Scene management

public class GameUIManager : MonoBehaviour // UI manager for game end states
{
    public static GameUIManager Instance; // Singleton instance

    [Header("Panels")] // Panels section
    public GameObject winPanel;       // Shown on win // Win panel reference
    public GameObject gameOverPanel;  // Shown on death // Game over panel reference

    [Header("Scene Names")] // Scene names section
    public string mainMenuSceneName = "MainMenu"; // Target scene // Main menu name

    private bool gameEnded; // Prevents double-trigger // End flag

    void Awake() // Singleton initialization
    {
        // singleton // Singleton setup
        if (Instance == null) Instance = this; // Set instance
        else Destroy(gameObject); // Destroy duplicate
    }

    void Start() // Initialize on start
    {
        // hide all panels, ensure time runs // Initial setup
        if (winPanel     != null) winPanel.SetActive(false); // Hide win panel
        if (gameOverPanel != null) gameOverPanel.SetActive(false); // Hide game over panel
        Time.timeScale = 1f; // Resume time
    }

    public void ShowWin() // Show win panel
    {
        if (gameEnded) return; // Already ended
        gameEnded = true; // Mark as ended
        if (winPanel != null) winPanel.SetActive(true); // Show panel
        Time.timeScale = 0f; // Freeze // Pause game
    }

    public void ShowGameOver() // Show game over panel
    {
        if (gameEnded) return; // Already ended
        gameEnded = true; // Mark as ended
        if (gameOverPanel != null) gameOverPanel.SetActive(true); // Show panel
        Time.timeScale = 0f; // Freeze // Pause game
    }

    public void PlayAgain() // Restart current scene
    {
        Time.timeScale = 1f; // Resume time
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); // Reload scene
    }

    public void GoToMainMenu() // Return to main menu
    {
        Time.timeScale = 1f; // Resume time
        SceneManager.LoadScene(mainMenuSceneName); // Load main menu
    }
}
