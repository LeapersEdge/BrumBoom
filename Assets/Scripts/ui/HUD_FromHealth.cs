using Fusion;
using TMPro;
using UnityEngine;

public class HUD_FromHealth : MonoBehaviour
{
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text livesText;

    private Health targetHealth;

    void Update()
    {
        if (targetHealth == null)
        {
            targetHealth = FindLocalPlayerHealth();
            if (targetHealth == null) return;
        }

        // Ovdje koristi ono što već imaš u Health.cs:
        // healthPoints, respawnHealthPoints, numberOfLives
        hpText.text = $"HP: {Mathf.CeilToInt(targetHealth.healthPoints)} / {Mathf.CeilToInt(targetHealth.respawnHealthPoints)}";
        livesText.text = $"Lives: {targetHealth.numberOfLives}";
    }

    private Health FindLocalPlayerHealth()
    {
        foreach (var h in FindObjectsOfType<Health>())
        {
            var no = h.GetComponent<NetworkObject>();
            if (no != null && no.HasInputAuthority)
                return h;
        }
        return null;
    }
}
