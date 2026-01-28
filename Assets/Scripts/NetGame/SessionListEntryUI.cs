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
        [SerializeField] private Image background;
        [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.98f);
        [SerializeField] private Color selectedColor = new Color(0.8f, 0.9f, 1f, 0.98f);
        private Action _onSelect;

        public void Bind(TMP_Text gameName, TMP_Text hostName, TMP_Text mapName, TMP_Text playerCount, Button join, Image bg)
        {
            gameNameText = gameName;
            hostNameText = hostName;
            mapNameText = mapName;
            playerCountText = playerCount;
            joinButton = join;
            background = bg;
        }

        public void SetData(string gameName, string hostName, string mapName, string playerCount, Action onSelect)
        {
            if (gameNameText != null) gameNameText.text = gameName;
            if (hostNameText != null) hostNameText.text = hostName;
            if (mapNameText != null) mapNameText.text = mapName;
            if (playerCountText != null) playerCountText.text = playerCount;
            _onSelect = onSelect;

            if (joinButton != null)
            {
                joinButton.onClick.RemoveAllListeners();
                joinButton.gameObject.SetActive(false);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _onSelect?.Invoke();
        }

        public bool HasValidLayout()
        {
            return gameNameText != null && hostNameText != null && mapNameText != null && playerCountText != null;
        }

        public void SetSelected(bool selected)
        {
            EnsureBackground();
            if (background != null)
                background.color = selected ? selectedColor : normalColor;
        }

        public void ApplyStyle()
        {
            EnsureLayout();
            ApplyTextStyle(gameNameText, 16f);
            ApplyTextStyle(hostNameText, 14f);
            ApplyTextStyle(mapNameText, 14f);
            ApplyTextStyle(playerCountText, 14f);

            EnsureBackground();
            if (background != null)
                background.color = normalColor;
        }

        public void ApplyFont(TMP_FontAsset font)
        {
            if (font == null)
                return;

            if (gameNameText != null) gameNameText.font = font;
            if (hostNameText != null) hostNameText.font = font;
            if (mapNameText != null) mapNameText.font = font;
            if (playerCountText != null) playerCountText.font = font;
        }

        public void EnsureVisible()
        {
            gameObject.SetActive(true);
            if (gameNameText != null) gameNameText.gameObject.SetActive(true);
            if (hostNameText != null) hostNameText.gameObject.SetActive(true);
            if (mapNameText != null) mapNameText.gameObject.SetActive(true);
            if (playerCountText != null) playerCountText.gameObject.SetActive(true);
        }

        public void EnsureLayout()
        {
            var rect = GetComponent<RectTransform>();
            if (rect != null)
                rect.sizeDelta = new Vector2(360, 82);

            var layoutElement = GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 360;
            layoutElement.preferredHeight = 82;

            var layout = GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<VerticalLayoutGroup>();
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlHeight = true;
                layout.childControlWidth = true;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = true;
                layout.spacing = 2;
                layout.padding = new RectOffset(8, 8, 6, 6);
            }
        }

        private static void ApplyTextStyle(TMP_Text text, float size)
        {
            if (text == null)
                return;

            text.fontSize = size;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = Color.black;
            text.alpha = 1f;
            text.enableAutoSizing = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            if (text.font == null)
                text.font = TMP_Settings.defaultFontAsset;

            var rect = text.GetComponent<RectTransform>();
            if (rect != null)
                rect.sizeDelta = new Vector2(0f, size + 6f);

            var layout = text.GetComponent<LayoutElement>();
            if (layout == null)
                layout = text.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = size + 6f;
            layout.minHeight = size + 6f;
            layout.flexibleWidth = 1f;
        }

        public string GetGameName()
        {
            return gameNameText != null ? gameNameText.text : null;
        }

        private void EnsureBackground()
        {
            if (background == null)
                background = GetComponent<Image>();
        }
    }
}
