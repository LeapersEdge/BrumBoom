using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NetGame
{
    public partial class StartMenuUI
    {
        private void InitializeMapDropdown()
        {
            if (mapDropdown == null && createPanel != null)
                mapDropdown = CreateDropdown("MapDropdown", createPanel.transform);

            if (mapDropdown == null)
            {
                Debug.LogWarning("[StartMenuUI] MapDropdown is null. Cannot initialize.");
                return;
            }

            if (forceRuntimeMapDropdown && !_runtimeMapDropdownCreated)
                ReplaceMapDropdownWithRuntime();

            var existingOptions = new List<string>();
            if (mapDropdown.options != null && mapDropdown.options.Count > 0)
            {
                for (int i = 0; i < mapDropdown.options.Count; i++)
                {
                    var opt = mapDropdown.options[i];
                    if (opt != null && !string.IsNullOrWhiteSpace(opt.text))
                        existingOptions.Add(opt.text);
                }
            }

            Debug.Log($"[StartMenuUI] MapDropdown existing options: {existingOptions.Count} ({string.Join(", ", existingOptions)})");

            _mapSceneOptions.Clear();
            _mapDisplayOptions.Clear();

            bool useInspectorOptions = existingOptions.Count > 0 && (mapSceneNames == null || mapSceneNames.Length == 0);

            if (mapSceneNames != null && mapSceneNames.Length > 0)
            {
                for (int i = 0; i < mapSceneNames.Length; i++)
                {
                    string scene = mapSceneNames[i];
                    if (string.IsNullOrWhiteSpace(scene))
                        continue;
                    if (excludeGameplayFromMapList && scene == gameplaySceneName)
                        continue;
                    _mapSceneOptions.Add(scene);
                }
                Debug.Log($"[StartMenuUI] mapSceneNames used: {_mapSceneOptions.Count} ({string.Join(", ", _mapSceneOptions)})");
            }
            else if (useInspectorOptions)
            {
                for (int i = 0; i < existingOptions.Count; i++)
                {
                    string scene = existingOptions[i];
                    if (string.IsNullOrWhiteSpace(scene))
                        continue;
                    if (excludeGameplayFromMapList && scene == gameplaySceneName)
                        continue;
                    _mapSceneOptions.Add(scene);
                }
                Debug.Log($"[StartMenuUI] Inspector options used: {_mapSceneOptions.Count} ({string.Join(", ", _mapSceneOptions)})");
            }

            if (_mapSceneOptions.Count <= 1)
            {
                AppendBuildSettingsScenes();
                useInspectorOptions = false;
                Debug.Log($"[StartMenuUI] Build settings appended: {_mapSceneOptions.Count} ({string.Join(", ", _mapSceneOptions)})");
            }

            if (_mapSceneOptions.Count == 0)
            {
                _mapSceneOptions.Add(gameplaySceneName);
                _mapDisplayOptions.Add(gameplaySceneName);
                Debug.Log($"[StartMenuUI] Fallback to gameplay scene: {gameplaySceneName}");
            }
            else if (mapDisplayNames != null && mapDisplayNames.Length > 0 && mapDisplayNames.Length == _mapSceneOptions.Count)
            {
                _mapDisplayOptions.AddRange(mapDisplayNames);
                useInspectorOptions = false;
                Debug.Log($"[StartMenuUI] mapDisplayNames used: {_mapDisplayOptions.Count} ({string.Join(", ", _mapDisplayOptions)})");
            }
            else if (!useInspectorOptions)
            {
                _mapDisplayOptions.AddRange(_mapSceneOptions);
                Debug.Log($"[StartMenuUI] mapSceneOptions used for display: {_mapDisplayOptions.Count} ({string.Join(", ", _mapDisplayOptions)})");
            }

            if (!useInspectorOptions)
            {
                mapDropdown.ClearOptions();
                mapDropdown.AddOptions(_mapDisplayOptions);
            }

            EnsureDropdownClickForwarder(mapDropdown);

            mapDropdown.onValueChanged.RemoveListener(OnMapDropdownChanged);
            mapDropdown.onValueChanged.AddListener(OnMapDropdownChanged);

            if (_mapSceneOptions.Count > 1)
            {
                bool auto = PlayerPrefs.GetInt(MapIndexAutoKey, 1) == 1;
                if (randomizeMapSelectionOnLaunch && auto)
                {
                    int idx = Random.Range(0, _mapSceneOptions.Count);
                    mapDropdown.value = idx;
                    PlayerPrefs.SetInt(MapIndexKey, idx);
                    PlayerPrefs.SetInt(MapIndexAutoKey, 1);
                    PlayerPrefs.Save();
                }
                else if (PlayerPrefs.HasKey(MapIndexKey))
                {
                    int idx = Mathf.Clamp(PlayerPrefs.GetInt(MapIndexKey, 0), 0, _mapSceneOptions.Count - 1);
                    mapDropdown.value = idx;
                }
            }

            mapDropdown.RefreshShownValue();
            Debug.Log($"[StartMenuUI] Final dropdown options: {mapDropdown.options.Count} ({string.Join(", ", GetDropdownTexts(mapDropdown))})");
            Debug.Log($"[StartMenuUI] Current dropdown value index: {mapDropdown.value}");
            Debug.Log($"[StartMenuUI] Dropdown interactable={mapDropdown.interactable}, template={(mapDropdown.template != null ? mapDropdown.template.name : "null")}");
        }

        private static IEnumerable<string> GetDropdownTexts(TMP_Dropdown dropdown)
        {
            if (dropdown == null || dropdown.options == null)
                yield break;
            for (int i = 0; i < dropdown.options.Count; i++)
            {
                var opt = dropdown.options[i];
                yield return opt != null ? opt.text : "<null>";
            }
        }

        private static void EnsureDropdownClickForwarder(TMP_Dropdown dropdown)
        {
            if (dropdown == null)
                return;
            var forwarder = dropdown.GetComponent<DropdownClickForwarder>();
            if (forwarder == null)
                forwarder = dropdown.gameObject.AddComponent<DropdownClickForwarder>();
            forwarder.Bind(dropdown);
        }

        private void ReplaceMapDropdownWithRuntime()
        {
            if (mapDropdown == null)
                return;

            var oldRect = mapDropdown.GetComponent<RectTransform>();
            Transform parent = oldRect != null ? oldRect.parent : createPanel != null ? createPanel.transform : null;
            if (parent == null)
                return;

            int siblingIndex = oldRect != null ? oldRect.GetSiblingIndex() : -1;
            Vector2 anchoredPosition = oldRect != null ? oldRect.anchoredPosition : Vector2.zero;
            Vector2 sizeDelta = oldRect != null ? oldRect.sizeDelta : new Vector2(480, 34);
            Vector2 anchorMin = oldRect != null ? oldRect.anchorMin : new Vector2(0.5f, 0.5f);
            Vector2 anchorMax = oldRect != null ? oldRect.anchorMax : new Vector2(0.5f, 0.5f);
            Vector2 pivot = oldRect != null ? oldRect.pivot : new Vector2(0.5f, 0.5f);

            var oldLayout = mapDropdown.GetComponent<LayoutElement>();

            mapDropdown.gameObject.SetActive(false);

            mapDropdown = CreateDropdown("MapDropdown", parent);
            var newRect = mapDropdown.GetComponent<RectTransform>();
            if (newRect != null)
            {
                newRect.anchorMin = anchorMin;
                newRect.anchorMax = anchorMax;
                newRect.pivot = pivot;
                newRect.anchoredPosition = anchoredPosition;
                newRect.sizeDelta = sizeDelta;
            }

            if (siblingIndex >= 0)
                mapDropdown.transform.SetSiblingIndex(siblingIndex);

            if (oldLayout != null)
            {
                var newLayout = mapDropdown.GetComponent<LayoutElement>();
                if (newLayout == null)
                    newLayout = mapDropdown.gameObject.AddComponent<LayoutElement>();
                newLayout.minWidth = oldLayout.minWidth;
                newLayout.minHeight = oldLayout.minHeight;
                newLayout.preferredWidth = oldLayout.preferredWidth;
                newLayout.preferredHeight = oldLayout.preferredHeight;
                newLayout.flexibleWidth = oldLayout.flexibleWidth;
                newLayout.flexibleHeight = oldLayout.flexibleHeight;
                newLayout.ignoreLayout = oldLayout.ignoreLayout;
            }

            _runtimeMapDropdownCreated = true;
            Debug.Log("[StartMenuUI] MapDropdown replaced with runtime dropdown.");
        }

        private void AppendBuildSettingsScenes()
        {
            int count = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < count; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                if (excludeGameplayFromMapList && name == gameplaySceneName)
                    continue;
                if (!ContainsSceneName(_mapSceneOptions, name))
                    _mapSceneOptions.Add(name);
            }
        }

        private static bool ContainsSceneName(List<string> scenes, string name)
        {
            for (int i = 0; i < scenes.Count; i++)
            {
                if (string.Equals(scenes[i], name, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private string GetSelectedMapScene()
        {
            if (_mapSceneOptions.Count > 0 && mapDropdown != null)
            {
                int idx = Mathf.Clamp(mapDropdown.value, 0, _mapSceneOptions.Count - 1);
                return _mapSceneOptions[idx];
            }

            return gameplaySceneName;
        }

        private void OnMapDropdownChanged(int index)
        {
            PlayerPrefs.SetInt(MapIndexKey, index);
            PlayerPrefs.SetInt(MapIndexAutoKey, 0);
            PlayerPrefs.Save();
        }
    }

    internal sealed class DropdownClickForwarder : MonoBehaviour, IPointerClickHandler
    {
        private TMP_Dropdown _dropdown;

        public void Bind(TMP_Dropdown dropdown)
        {
            _dropdown = dropdown;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_dropdown == null)
                return;
            Debug.Log("[StartMenuUI] Dropdown clicked -> force Show()");
            _dropdown.Show();
        }
    }
}
