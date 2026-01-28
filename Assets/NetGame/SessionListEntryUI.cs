using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NetGame
{
    public class SessionListEntryUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TMP_Text gameNameText;
        [SerializeField] private TMP_Text hostNameText;
        [SerializeField] private TMP_Text mapNameText;
        [SerializeField] private TMP_Text playerCountText;
        [SerializeField] private Button joinButton;
        private Action _onJoin;

        public void Bind(TMP_Text gameName, TMP_Text hostName, TMP_Text mapName, TMP_Text playerCount, Button join)
        {
            gameNameText = gameName;
            hostNameText = hostName;
            mapNameText = mapName;
            playerCountText = playerCount;
            joinButton = join;
        }

        public void SetData(string gameName, string hostName, string mapName, string playerCount, Action onJoin)
        {
            if (gameNameText != null) gameNameText.text = gameName;
            if (hostNameText != null) hostNameText.text = hostName;
            if (mapNameText != null) mapNameText.text = mapName;
            if (playerCountText != null) playerCountText.text = playerCount;
            _onJoin = onJoin;

            if (joinButton != null)
            {
                joinButton.onClick.RemoveAllListeners();
                joinButton.onClick.AddListener(() => onJoin?.Invoke());
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _onJoin?.Invoke();
        }
    }
}
