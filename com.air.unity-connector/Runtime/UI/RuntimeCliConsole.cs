using Air.UnityConnector.Job;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Air.UnityConnector.Invoke;
using Air.UnityConnector.Host;
using Air.UnityGameCore.Runtime.Serialization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Air.UnityConnector.Cli;

namespace Air.UnityConnector
{
    /// <summary>
    /// Runtime in-game command console for quick local command execution.
    /// </summary>
    public sealed partial class RuntimeCliConsole : MonoBehaviour, IInvokeJobNotifier
    {
        [Header("UI References")]
        [SerializeField] private InputField _inputField;
        [SerializeField] private ScrollRect _historyScrollRect;
        [SerializeField] private ScrollRect _suggestionScrollRect;
        [SerializeField] private Button _suggestionItemPrefab;

        [Header("Behavior")]
        [SerializeField] private int _maxHistoryLines = 80;
        [SerializeField] private int _maxCommandHistoryCount = 200;
        [SerializeField] private bool _clearInputAfterExecute = true;
        [SerializeField] private string _hostNameOverride = "";

        private readonly List<Text> _historyItems = new();
        private readonly List<string> _executedCommandHistory = new();
        private readonly Dictionary<string, string> _pendingCommandNames = new();
        private readonly List<SuggestionItem> _suggestions = new();
        private readonly List<Button> _suggestionButtons = new();
        private int _historyBrowseIndex = -1;
        private string _historyBrowseDraft = "";
        private bool _suppressBrowseResetOnInputChanged;
        private string _tabCompletionSeed = "";
        private int _tabCompletionIndex = -1;
        private bool _historyScrollPending;

        private struct SuggestionItem
        {
            public string CommandName;
            public string[] Aliases;
            public string[] ParamDescriptions;
        }

        private void Awake()
        {
            if (_inputField != null)
                _inputField.onValueChanged.AddListener(OnInputChanged);
            EnsureHistoryScrollLayout();
            RefreshSuggestions(_inputField != null ? _inputField.text : "");
            AppendHistory(FormatHistoryLine("system", $"Runtime CLI ready ({ResolveHostName()})."));
        }

        private void OnDestroy()
        {
            if (_inputField != null)
                _inputField.onValueChanged.RemoveListener(OnInputChanged);
            ClearSuggestionButtons();
            _historyItems.Clear();
        }

        private void Update()
        {
            if (_inputField == null)
                return;

            if (Input.GetKeyDown(KeyCode.UpArrow)) HandleUpKey();
            else if (Input.GetKeyDown(KeyCode.DownArrow)) HandleDownKey();
            else if (Input.GetKeyDown(KeyCode.Tab)) HandleTabCompletion();
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) HandleSubmit();
        }

        private void LateUpdate()
        {
            if (!_historyScrollPending)
                return;
            ScrollHistoryToBottomImmediate();
            _historyScrollPending = false;
        }
        private void AppendHistory(string line)
        {
            if (_historyScrollRect == null || _historyScrollRect.content == null)
                return;

            var item = CreateHistoryItem(line ?? "");
            _historyItems.Add(item);
            while (_historyItems.Count > Math.Max(10, _maxHistoryLines))
                RemoveOldestHistoryItem();

            ScrollHistoryToBottom();
        }

        private void EnsureHistoryScrollLayout()
        {
            if (_historyScrollRect == null || _historyScrollRect.content == null)
                return;

            var content = _historyScrollRect.content;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
            }

            if (content.GetComponent<ContentSizeFitter>() == null)
            {
                var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        private Text CreateHistoryItem(string line)
        {
            var go = new GameObject("HistoryItem", typeof(RectTransform));
            go.transform.SetParent(_historyScrollRect.content, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = Vector2.zero;

            var text = go.AddComponent<Text>();
            ApplyHistoryItemStyle(text);
            text.text = line;

            var itemFitter = go.AddComponent<ContentSizeFitter>();
            itemFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            itemFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return text;
        }

        private void ApplyHistoryItemStyle(Text text)
        {
            if (text == null)
                return;

            text.supportRichText = true;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.fontSize = 50;
            text.lineSpacing = 1f;
            text.color = Color.black;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private void RemoveOldestHistoryItem()
        {
            if (_historyItems.Count == 0)
                return;

            var oldest = _historyItems[0];
            _historyItems.RemoveAt(0);
            if (oldest == null)
                return;
            if (Application.isPlaying)
                Destroy(oldest.gameObject);
            else
                DestroyImmediate(oldest.gameObject);
        }

        private void ScrollHistoryToBottom()
        {
            _historyScrollPending = true;
            ScrollHistoryToBottomImmediate();
        }

        private void ScrollHistoryToBottomImmediate()
        {
            if (_historyScrollRect == null || _historyScrollRect.content == null)
                return;

            var content = _historyScrollRect.content;
            var viewport = _historyScrollRect.viewport != null
                ? _historyScrollRect.viewport
                : _historyScrollRect.GetComponent<RectTransform>();

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            _historyScrollRect.StopMovement();

            var overflow = content.rect.height - viewport.rect.height;
            if (overflow > 0f)
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, overflow);
            else
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);

            _historyScrollRect.verticalNormalizedPosition = 0f;
        }

        private string ResolveHostName()
        {
            if (!string.IsNullOrWhiteSpace(_hostNameOverride))
                return _hostNameOverride.Trim().ToLowerInvariant();

            return Application.isEditor ? HostKind.EditorPlay : HostKind.Player;
        }
        private static string FormatHistoryLine(string level, string message)
        {
            var text = message ?? "";
            return level switch
            {
                "input" => $"<color=#1F4E79>> {text}</color>",
                "ok" => $"<color=#0B6E4F>[ok]</color> {text}",
                "error" => $"<color=#8B0000>[error]</color> {text}",
                "pending" => $"<color=#7A5C00>[pending]</color> {text}",
                "running" => $"<color=#5B2C6F>[running]</color> {text}",
                "system" => $"<color=#404040>[system]</color> {text}",
                _ => text,
            };
        }
    }
}
