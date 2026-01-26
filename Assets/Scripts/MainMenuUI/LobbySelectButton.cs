using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LobbySelectButton : MonoBehaviour
{
    public int numOfActivePlayers = 0;
    public int numOfMaxPlayers = 0;
    public int lobbyIndex = 0;

    TMP_Text buttonText;
        

    void Start()
    {
        buttonText = GetComponentInChildren<TMP_Text>();
        UpdateButtonText();
    }

    public void UpdateButtonText()
    {
        buttonText.text = $"Lobby {lobbyIndex}: {numOfActivePlayers}/{numOfMaxPlayers}";
    }

    public void OnClick()
    {
        Debug.Log("clicked button text: " + buttonText.text);
        // Handle the button click logic here
    }
}
