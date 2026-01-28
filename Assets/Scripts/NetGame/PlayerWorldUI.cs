using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NetGame
{
    /// <summary>
    /// World-space name + health bar for remote players only.
    /// </summary>
    public class PlayerWorldUI : MonoBehaviour
    {
        [SerializeField] private Vector3 offset = new Vector3(0f, 2.2f, 0f);
        [SerializeField] private float scale = 0.01f;
        [SerializeField] private Color barColor = new Color(0.85f, 0.15f, 0.15f, 0.95f);

        private NetworkHealth _health;
        private Canvas _canvas;
        private RectTransform _root;
        private TMP_Text _nameText;
        private Image _barFill;
        private Sprite _uiSprite;
        private static Sprite _fallbackSprite;

        private void Awake()
        {
            _health = GetComponent<NetworkHealth>();
            BuildUI();
        }

        private void LateUpdate()
        {
            if (_health == null)
                _health = GetComponent<NetworkHealth>();

            if (_health == null || _health.Object == null)
                return;

            bool isLocal = _health.Object.HasInputAuthority;
            bool show = !isLocal && !_health.IsEliminated;

            if (_canvas != null)
                _canvas.gameObject.SetActive(show);

            if (!show)
                return;

            var cam = Camera.main;
            if (cam == null)
                return;

            _canvas.transform.position = transform.position + offset;
            _canvas.transform.rotation = Quaternion.LookRotation(_canvas.transform.position - cam.transform.position);

            string name = _health.PlayerName.ToString();
            _nameText.text = string.IsNullOrWhiteSpace(name) ? "Player" : name;

            float max = Mathf.Max(1f, _health.MaxHealth);
            if (_barFill != null)
                _barFill.fillAmount = Mathf.Clamp01(_health.Health / max);
        }

        private void BuildUI()
        {
            if (_canvas != null)
                return;

            var canvasGo = new GameObject("PlayerWorldUI");
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 100;

            var canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(200f, 80f);
            canvasRect.localScale = Vector3.one * scale;

            _root = canvasRect;
            _uiSprite = GetFallbackSprite();

            // Name text
            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(_root, false);
            var nameRect = nameGo.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0.5f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.offsetMin = new Vector2(0f, 20f);
            nameRect.offsetMax = new Vector2(0f, 0f);

            _nameText = nameGo.AddComponent<TextMeshProUGUI>();
            _nameText.alignment = TextAlignmentOptions.Center;
            _nameText.fontSize = 24f;

            // Bar background
            var barBackGo = new GameObject("BarBack");
            barBackGo.transform.SetParent(_root, false);
            var barBackRect = barBackGo.AddComponent<RectTransform>();
            barBackRect.anchorMin = new Vector2(0.1f, 0.1f);
            barBackRect.anchorMax = new Vector2(0.9f, 0.35f);
            barBackRect.offsetMin = Vector2.zero;
            barBackRect.offsetMax = Vector2.zero;

            var barBack = barBackGo.AddComponent<Image>();
            if (_uiSprite != null)
                barBack.sprite = _uiSprite;
            barBack.color = new Color(0f, 0f, 0f, 0.6f);

            // Bar fill
            var barFillGo = new GameObject("BarFill");
            barFillGo.transform.SetParent(barBackRect, false);
            var barFillRect = barFillGo.AddComponent<RectTransform>();
            barFillRect.anchorMin = Vector2.zero;
            barFillRect.anchorMax = Vector2.one;
            barFillRect.offsetMin = Vector2.zero;
            barFillRect.offsetMax = Vector2.zero;

            _barFill = barFillGo.AddComponent<Image>();
            if (_uiSprite != null)
                _barFill.sprite = _uiSprite;
            _barFill.color = barColor;
            _barFill.type = Image.Type.Filled;
            _barFill.fillMethod = Image.FillMethod.Horizontal;
            _barFill.fillOrigin = 0;
            _barFill.fillAmount = 1f;
        }

        private static Sprite GetFallbackSprite()
        {
            if (_fallbackSprite != null)
                return _fallbackSprite;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();

            _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            return _fallbackSprite;
        }
    }
}
