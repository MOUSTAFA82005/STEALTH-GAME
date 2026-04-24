using TMPro;
using UnityEngine;

public class KillCounter : MonoBehaviour
{
    public static KillCounter Instance;

    [Header("UI")]
    public TMP_Text killsText;
    public TMP_Text remainingText;

    private int totalGuards;
    private int killedGuards;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        GuardAI[] guards = FindObjectsByType<GuardAI>(FindObjectsSortMode.None);
        totalGuards = guards.Length;
        killedGuards = 0;
        UpdateUI();
    }

    public void AddKill()
    {
        killedGuards++;
        UpdateUI();

        if (killedGuards >= totalGuards)
        {
            if (GameUIManager.Instance != null)
                GameUIManager.Instance.ShowWin();
        }
    }

    void UpdateUI()
    {
        int remaining = totalGuards - killedGuards;

        if (killsText != null)
            killsText.text = "Kills: " + killedGuards;

        if (remainingText != null)
            remainingText.text = "Remaining: " + remaining;
    }
}