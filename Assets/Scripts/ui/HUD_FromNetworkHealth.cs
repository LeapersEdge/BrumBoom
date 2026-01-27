using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUD_FromNetworkHealth : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text livesText;
    [SerializeField] private TMP_Text spectatorHint;

    private NetworkHealth target;

    void Update()
    {
        if (target == null)
        {
            target = FindLocalPlayerNetworkHealth();
            if (target == null) return;

            healthSlider.minValue = 0;
            healthSlider.maxValue = target.MaxHealth;
        }

        healthSlider.value = target.Health;
        hpText.text = $"HP: {Mathf.CeilToInt(target.Health)} / {Mathf.CeilToInt(target.MaxHealth)}";
        livesText.text = target.IsEliminated ? "Eliminated - Spectating" : $"Lives: {target.Lives}";

        if (spectatorHint != null)
            spectatorHint.text = target.IsEliminated ? "Spectate: Q/E" : string.Empty;
    }

    private NetworkHealth FindLocalPlayerNetworkHealth()
    {
        foreach (var nh in FindObjectsOfType<NetworkHealth>())
        {
            var no = nh.GetComponent<NetworkObject>();
            if (no != null && no.HasInputAuthority)
                return nh;
        }
        return null;
    }
}
