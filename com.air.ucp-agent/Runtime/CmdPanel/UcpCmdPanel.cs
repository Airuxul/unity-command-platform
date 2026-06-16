using System;
using System.Collections.Generic;
using Air.UcpAgent.Protocol;
using Air.UcpAgent.Runtime.Shell;
using UnityEngine;
using UnityEngine.UI;

namespace Air.UcpAgent.Runtime.CmdPanel
{
    [DisallowMultipleComponent]
    public sealed class UcpCmdPanel : MonoBehaviour
    {
        const int MaxHistoryEntries = 100;

        static readonly Color SystemLineColor = new(0.65f, 0.72f, 0.78f, 1f);
        static readonly Color CommandLineColor = new(0.55f, 0.92f, 0.72f, 1f);
        static readonly Color SuccessResultColor = new(0.82f, 0.84f, 0.86f, 1f);
        static readonly Color ErrorResultColor = new(0.95f, 0.45f, 0.42f, 1f);

        [SerializeField] int _fontSize = 14;
        [SerializeField] float _lineHeight = 50f;
        [SerializeField] ScrollRect _historyScroll;
        [SerializeField] RectTransform _historyContent;
        [SerializeField] Text _hintText;
        [SerializeField] InputField _inputField;

        readonly List<string> _commandHistory = new();
        int _historyBrowseIndex = -1;
        string _historyDraft = "";

        List<ShellCompletionCandidate> _completionCandidates = new();
        int _completionIndex = -1;
        string _completionToken = "";
        bool _completionCommonPrefixApplied;

        Font _runtimeFont;
        bool _visible = true;
        bool _initialized;

        public static UcpCmdPanel Instance { get; private set; }

        public int FontSize
        {
            get => _fontSize;
            set
            {
                _fontSize = Mathf.Max(8, value);
                ApplyFontSize();
            }
        }

        void Awake()
        {
            Instance = this;
            if (!_initialized)
                Initialize();
        }

        void Initialize()
        {
            if (_initialized)
                return;

            if (_historyScroll == null || _inputField == null)
                throw new InvalidOperationException("UcpCmdPanel prefab references are not assigned.");

            if (_historyContent == null)
                _historyContent = _historyScroll.content;

            _runtimeFont = _hintText != null ? _hintText.font : _inputField.textComponent?.font;

            _inputField.onValidateInput = static (text, index, addedChar) =>
                addedChar == '\t' ? '\0' : addedChar;
            _inputField.onValueChanged.AddListener(_ => OnInputChanged());
            _inputField.onEndEdit.AddListener(OnInputEndEdit);

            ApplyFontSize();
            _initialized = true;
            RefreshHints();
            AppendSystemLine("UCP Cmd Panel ready. Press ` to toggle. Tab to complete, Enter to run.");
            FocusInput();
        }

        void ApplyFontSize()
        {
            if (_hintText != null)
                _hintText.fontSize = _fontSize;

            if (_inputField == null)
                return;

            if (_inputField.textComponent != null)
                _inputField.textComponent.fontSize = _fontSize;

            if (_inputField.placeholder is Text placeholder)
                placeholder.fontSize = _fontSize;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote))
                SetVisible(!_visible);

            if (!_visible || _inputField == null || !_inputField.isFocused)
                return;

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                var reverse = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                ApplyTabCompletion(reverse);
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                BrowseHistory(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
                BrowseHistory(1);
        }

        void OnInputEndEdit(string _)
        {
            if (!_visible || _inputField == null)
                return;

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                SubmitCurrentLine();
        }

        void FocusInput()
        {
            if (_inputField == null)
                return;

            _inputField.Select();
            _inputField.ActivateInputField();
        }

        void OnInputChanged()
        {
            _completionCandidates.Clear();
            _completionIndex = -1;
            _completionToken = "";
            _completionCommonPrefixApplied = false;
            RefreshHints();
        }

        void RefreshHints()
        {
            if (_hintText == null)
                return;

            var hintsArea = _hintText.transform.parent != null
                ? _hintText.transform.parent.gameObject
                : null;
            var input = _inputField != null ? _inputField.text : "";

            if (string.IsNullOrWhiteSpace(input))
            {
                _hintText.text = "";
                SetHintsAreaVisible(hintsArea, false);
                return;
            }

            var hints = ShellHintService.BuildHints(input);
            if (hints.Count == 0)
            {
                _hintText.text = "";
                SetHintsAreaVisible(hintsArea, false);
                return;
            }

            SetHintsAreaVisible(hintsArea, true);
            _hintText.text = string.Join("\n", hints);
            RebuildHintsLayout(hintsArea);
        }

        static void SetHintsAreaVisible(GameObject hintsArea, bool visible)
        {
            if (hintsArea == null || hintsArea.activeSelf == visible)
                return;

            hintsArea.SetActive(visible);

            if (hintsArea.transform.parent is RectTransform panelRect)
                LayoutRebuilder.MarkLayoutForRebuild(panelRect);
        }

        void RebuildHintsLayout(GameObject hintsArea)
        {
            if (hintsArea == null)
                return;

            Canvas.ForceUpdateCanvases();
            var hintsRect = hintsArea.transform as RectTransform;
            if (hintsRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(hintsRect);

            if (transform is RectTransform panelRect)
                LayoutRebuilder.MarkLayoutForRebuild(panelRect);
        }

        void ApplyTabCompletion(bool reverse = false)
        {
            var input = _inputField.text ?? "";
            var caret = _inputField.caretPosition;
            var (_, _, tokenValue) = ShellCompletionService.GetTokenAtCaret(input, caret);

            if (_completionCandidates.Count == 0 ||
                !string.Equals(_completionToken, tokenValue, StringComparison.Ordinal))
            {
                _completionCandidates = ShellCompletionService.GetCandidates(input, caret);
                _completionIndex = -1;
                _completionToken = tokenValue;
                _completionCommonPrefixApplied = false;
            }

            if (_completionCandidates.Count == 0)
                return;

            if (!reverse &&
                !_completionCommonPrefixApplied &&
                _completionCandidates.Count > 1)
            {
                var commonPrefix = ShellCompletionService.GetLongestCommonPrefix(_completionCandidates);
                if (commonPrefix.Length > tokenValue.Length)
                {
                    _inputField.text = ShellCompletionService.ApplyReplacement(
                        input,
                        caret,
                        commonPrefix,
                        appendSpace: false);
                    _inputField.caretPosition = ShellCompletionService.GetCaretAfterReplacement(
                        input,
                        caret,
                        commonPrefix,
                        appendSpace: false);
                    _completionToken = commonPrefix;
                    _completionCommonPrefixApplied = true;
                    RefreshHints();
                    return;
                }
            }

            if (reverse)
            {
                _completionIndex = _completionIndex <= 0
                    ? _completionCandidates.Count - 1
                    : _completionIndex - 1;
            }
            else
            {
                _completionIndex = (_completionIndex + 1) % _completionCandidates.Count;
            }

            var candidate = _completionCandidates[_completionIndex];
            _inputField.text = ShellCompletionService.ApplyCandidate(input, caret, candidate);
            _inputField.caretPosition = ShellCompletionService.GetCaretAfterApply(input, caret, candidate);
            _completionToken = candidate.Replacement;
            RefreshHints();
        }

        void SubmitCurrentLine()
        {
            var line = (_inputField.text ?? "").Trim();
            _inputField.text = "";
            _inputField.ActivateInputField();
            ResetHistoryBrowse();
            _completionCandidates.Clear();
            _completionIndex = -1;
            _completionToken = "";
            _completionCommonPrefixApplied = false;

            if (string.IsNullOrEmpty(line))
            {
                AppendSystemLine("Enter a command.");
                RefreshHints();
                return;
            }

            _commandHistory.Add(line);
            if (_commandHistory.Count > MaxHistoryEntries)
                _commandHistory.RemoveAt(0);

            AppendHistoryCommand(line);

            var (result, error) = ShellCommandExecutor.ExecuteLine(line);
            AppendHistoryResult(result, error);
            RefreshHints();
            ScrollHistoryToBottom();
        }

        void BrowseHistory(int delta)
        {
            if (_commandHistory.Count == 0)
                return;

            if (_historyBrowseIndex < 0)
                _historyDraft = _inputField.text ?? "";

            var next = _historyBrowseIndex < 0
                ? _commandHistory.Count - 1
                : _historyBrowseIndex + delta;

            if (next < 0 || next >= _commandHistory.Count)
            {
                if (next >= _commandHistory.Count)
                {
                    _historyBrowseIndex = -1;
                    _inputField.text = _historyDraft;
                }

                RefreshHints();
                return;
            }

            _historyBrowseIndex = next;
            _inputField.text = _commandHistory[_historyBrowseIndex];
            _inputField.caretPosition = _inputField.text.Length;
            RefreshHints();
        }

        void ResetHistoryBrowse()
        {
            _historyBrowseIndex = -1;
            _historyDraft = "";
        }

        void AppendSystemLine(string message) => AppendLine(message, SystemLineColor);

        void AppendHistoryCommand(string command) => AppendLine("> " + command, CommandLineColor);

        void AppendHistoryResult(UcpResult result, string executionError)
        {
            var text = ShellCommandExecutor.FormatResult(result, executionError);
            var isError = !string.IsNullOrEmpty(executionError) || result == null || !result.success;
            AppendLine(text, isError ? ErrorResultColor : SuccessResultColor);
        }

        void AppendLine(string text, Color color)
        {
            if (_historyContent == null)
                return;

            var lineGo = new GameObject("Line", typeof(RectTransform));
            lineGo.transform.SetParent(_historyContent, false);

            var rect = lineGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            var textComp = lineGo.AddComponent<Text>();
            if (_runtimeFont != null)
                textComp.font = _runtimeFont;
            textComp.fontSize = _fontSize;
            textComp.alignment = TextAnchor.UpperLeft;
            textComp.horizontalOverflow = HorizontalWrapMode.Wrap;
            textComp.verticalOverflow = VerticalWrapMode.Overflow;
            textComp.color = color;
            textComp.text = text ?? "";
            textComp.raycastTarget = false;

            var fitter = lineGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var layout = lineGo.AddComponent<LayoutElement>();
            layout.minHeight = _lineHeight;
            layout.flexibleWidth = 1f;
        }

        void ScrollHistoryToBottom()
        {
            if (_historyScroll == null || _historyContent == null)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(_historyContent);
            Canvas.ForceUpdateCanvases();
            _historyScroll.verticalNormalizedPosition = 0f;
        }

        void SetVisible(bool visible)
        {
            _visible = visible;
            gameObject.SetActive(visible);

            if (visible)
                FocusInput();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
