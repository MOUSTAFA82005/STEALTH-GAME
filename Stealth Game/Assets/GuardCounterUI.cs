using TMPro;
using UnityEngine;

public class GuardCounterUI : MonoBehaviour
{
    public static GuardCounterUI Instance;

    public TMP_Text killsText;
    public TMP_Text remainingText;

    private int totalGuards;
    private int killedGuards;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        GuardAI[] guards = FindObjectsByType<GuardAI>(FindObjectsSortMode.None);
        totalGuards = guards.Length;
        killedGuards = 0;

        UpdateUI();
    }

    public void RegisterKill()
    {
        killedGuards++;
        UpdateUI();
    }

    void UpdateUI()
    {
        int remaining = totalGuards - killedGuards;

        if (killsText != null)
            killsText.text = "Kills: " + killedGuards;

        if (remainingText != null)
            remainingText.text = "Remaining: " + remaining;
    }

    public int GetRemainingGuards()
    {
        return totalGuards - killedGuards;
    }

    public int GetKilledGuards()
    {
        return killedGuards;
    }
}