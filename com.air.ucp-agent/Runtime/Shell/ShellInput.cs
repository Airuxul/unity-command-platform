using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Invoke;

namespace Air.UcpAgent.Runtime.Shell
{
    public readonly struct ShellToken
    {
        public readonly string Value;
        public readonly int Start;
        public readonly int Length;

        public ShellToken(string value, int start, int length)
        {
            Value = value;
            Start = start;
            Length = length;
        }

        public int End => Start + Length;
    }

    public sealed class ShellParseResult
    {
        public string CommandName;
        public Dictionary<string, object> Args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public string Error;
        public bool IsValid => string.IsNullOrEmpty(Error);
    }

    public static class ShellTokenizer
    {
        public static List<ShellToken> Tokenize(string input)
        {
            var tokens = new List<ShellToken>();
            if (string.IsNullOrEmpty(input))
                return tokens;

            var i = 0;
            while (i < input.Length)
            {
                while (i < input.Length && char.IsWhiteSpace(input[i]))
                    i++;

                if (i >= input.Length)
                    break;

                var start = i;
                if (input[i] is '"' or '\'')
                {
                    var quote = input[i++];
                    var sb = new StringBuilder();
                    while (i < input.Length && input[i] != quote)
                    {
                        if (input[i] == '\\' && i + 1 < input.Length)
                        {
                            sb.Append(input[++i]);
                            i++;
                            continue;
                        }

                        sb.Append(input[i++]);
                    }

                    if (i < input.Length)
                        i++;

                    tokens.Add(new ShellToken(sb.ToString(), start, i - start));
                    continue;
                }

                while (i < input.Length && !char.IsWhiteSpace(input[i]))
                    i++;

                tokens.Add(new ShellToken(input.Substring(start, i - start), start, i - start));
            }

            return tokens;
        }
    }

    public static class ShellLineParser
    {
        public static ShellParseResult Parse(string line)
        {
            var trimmed = line?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return new ShellParseResult { Error = "empty_input" };

            var tokens = ShellTokenizer.Tokenize(trimmed);
            if (tokens.Count == 0)
                return new ShellParseResult { Error = "empty_input" };

            var result = new ShellParseResult
            {
                CommandName = ResolveRuntimeCommand(tokens[0].Value),
            };

            if (string.IsNullOrEmpty(result.CommandName))
            {
                result.Error = "unknown_command";
                return result;
            }

            for (var i = 1; i < tokens.Count; i++)
            {
                var token = tokens[i].Value;
                if (!token.StartsWith("--", StringComparison.Ordinal))
                    continue;

                var eq = token.IndexOf('=');
                if (eq > 2)
                {
                    var key = token.Substring(2, eq - 2);
                    result.Args[key] = tokens[i].Value.Substring(eq + 1);
                    continue;
                }

                var flag = token.Substring(2);
                if (string.IsNullOrEmpty(flag))
                    continue;

                if (i + 1 < tokens.Count && !tokens[i + 1].Value.StartsWith("--", StringComparison.Ordinal))
                {
                    result.Args[flag] = tokens[i + 1].Value;
                    i++;
                }
                else
                {
                    result.Args[flag] = true;
                }
            }

            return result;
        }

        public static string ResolveRuntimeCommand(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var handler = CliCommandDiscovery.FindForHost(name.Trim(), "runtime");
            if (handler != null && InvokeAvailability.IsAvailableForHost(handler.Scope, "runtime"))
                return handler.Name;

            foreach (var candidate in CliCommandDiscovery.Handlers)
            {
                if (!InvokeAvailability.IsAvailableForHost(candidate.Scope, "runtime"))
                    continue;

                if (candidate.Aliases != null &&
                    candidate.Aliases.Any(alias =>
                        string.Equals(alias, name, StringComparison.OrdinalIgnoreCase)))
                {
                    return candidate.Name;
                }
            }

            return null;
        }
    }

    public sealed class ShellCompletionCandidate
    {
        public string Replacement;
        public bool AppendSpace;
    }

    public static class ShellCompletionService
    {
        static readonly Regex ParamNameRegex = new(@"^--([A-Za-z0-9_]+)", RegexOptions.Compiled);

        public static List<ShellCompletionCandidate> GetCandidates(string input, int caretIndex)
        {
            var text = input ?? "";
            var (tokenStart, tokenEnd, tokenValue) = GetTokenAtCaret(text, caretIndex);
            var tokenIndex = CountTokensBefore(text, tokenStart);

            if (tokenIndex <= 0)
                return CompleteCommandNames(tokenValue);

            var commandName = ShellLineParser.ResolveRuntimeCommand(ShellTokenizer.Tokenize(text).FirstOrDefault().Value);
            if (string.IsNullOrEmpty(commandName))
                return CompleteCommandNames(tokenValue);

            var handler = CliCommandDiscovery.FindForHost(commandName, "runtime");
            if (handler == null)
                return new List<ShellCompletionCandidate>();

            if (tokenValue.StartsWith("--", StringComparison.Ordinal) || tokenIndex > 0 && string.IsNullOrEmpty(tokenValue))
                return CompleteParameters(handler, text, tokenStart, tokenValue);

            if (tokenIndex >= 2)
                return CompleteParameterValue(handler, text, tokenStart, tokenValue);

            return CompleteParameters(handler, text, tokenStart, tokenValue);
        }

        static List<ShellCompletionCandidate> CompleteCommandNames(string prefix)
        {
            var matches = new List<ShellCompletionCandidate>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var handler in CliCommandDiscovery.Handlers)
            {
                if (!InvokeAvailability.IsAvailableForHost(handler.Scope, "runtime"))
                    continue;

                TryAddName(matches, seen, handler.Name, prefix);
                if (handler.Aliases == null)
                    continue;

                foreach (var alias in handler.Aliases)
                    TryAddName(matches, seen, alias, prefix);
            }

            return matches
                .OrderBy(c => c.Replacement, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        static void TryAddName(
            List<ShellCompletionCandidate> matches,
            HashSet<string> seen,
            string name,
            string prefix)
        {
            if (string.IsNullOrEmpty(name) || !seen.Add(name))
                return;

            if (!StartsWithIgnoreCase(name, prefix))
                return;

            matches.Add(new ShellCompletionCandidate
            {
                Replacement = name,
                AppendSpace = true,
            });
        }

        static List<ShellCompletionCandidate> CompleteParameters(
            IInvokeHandler handler,
            string input,
            int tokenStart,
            string tokenValue)
        {
            var used = CollectUsedParameterKeys(input);
            var prefix = tokenValue.StartsWith("--", StringComparison.Ordinal)
                ? tokenValue.Substring(2)
                : tokenValue;
            var matches = new List<ShellCompletionCandidate>();

            foreach (var desc in handler.ParamDescriptions ?? Array.Empty<string>())
            {
                var match = ParamNameRegex.Match(desc);
                if (!match.Success)
                    continue;

                var key = match.Groups[1].Value;
                if (used.Contains(key))
                    continue;

                if (!StartsWithIgnoreCase(key, prefix))
                    continue;

                matches.Add(new ShellCompletionCandidate
                {
                    Replacement = "--" + key,
                    AppendSpace = true,
                });
            }

            return matches.OrderBy(c => c.Replacement, StringComparer.OrdinalIgnoreCase).ToList();
        }

        static List<ShellCompletionCandidate> CompleteParameterValue(
            IInvokeHandler handler,
            string input,
            int tokenStart,
            string tokenValue)
        {
            var tokens = ShellTokenizer.Tokenize(input);
            if (tokens.Count < 2)
                return new List<ShellCompletionCandidate>();

            var previous = tokens[tokens.Count - 2].Value;
            if (!previous.StartsWith("--", StringComparison.Ordinal))
                return new List<ShellCompletionCandidate>();

            var key = previous.Substring(2);
            var desc = (handler.ParamDescriptions ?? Array.Empty<string>())
                .FirstOrDefault(line => line.StartsWith("--" + key, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(desc))
                return new List<ShellCompletionCandidate>();

            var bracket = Regex.Match(desc, @"\[([^\]]+)\]");
            if (!bracket.Success)
                return new List<ShellCompletionCandidate>();

            return bracket.Groups[1].Value
                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .Where(v => StartsWithIgnoreCase(v, tokenValue))
                .Select(v => new ShellCompletionCandidate { Replacement = v, AppendSpace = true })
                .ToList();
        }

        static HashSet<string> CollectUsedParameterKeys(string input)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var token in ShellTokenizer.Tokenize(input))
            {
                if (!token.Value.StartsWith("--", StringComparison.Ordinal))
                    continue;

                var eq = token.Value.IndexOf('=');
                var key = eq > 2 ? token.Value.Substring(2, eq - 2) : token.Value.Substring(2);
                if (!string.IsNullOrEmpty(key))
                    used.Add(key);
            }

            return used;
        }

        public static string ApplyCandidate(string input, int caretIndex, ShellCompletionCandidate candidate)
        {
            return ApplyReplacement(
                input,
                caretIndex,
                candidate.Replacement,
                candidate.AppendSpace);
        }

        public static string ApplyReplacement(string input, int caretIndex, string replacement, bool appendSpace)
        {
            var text = input ?? "";
            var (tokenStart, tokenEnd, _) = GetTokenAtCaret(text, caretIndex);
            var suffix = appendSpace ? " " : "";
            return text.Substring(0, tokenStart) + replacement + suffix + text.Substring(tokenEnd);
        }

        public static string GetLongestCommonPrefix(IReadOnlyList<ShellCompletionCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return "";

            if (candidates.Count == 1)
                return candidates[0].Replacement ?? "";

            var reference = candidates[0].Replacement ?? "";
            var maxLength = reference.Length;

            for (var i = 1; i < candidates.Count; i++)
            {
                var value = candidates[i].Replacement ?? "";
                maxLength = Math.Min(maxLength, value.Length);

                for (var j = 0; j < maxLength; j++)
                {
                    if (char.ToLowerInvariant(reference[j]) != char.ToLowerInvariant(value[j]))
                    {
                        maxLength = j;
                        break;
                    }
                }
            }

            return reference.Substring(0, maxLength);
        }

        public static (int start, int end, string value) GetTokenAtCaret(string text, int caret)
        {
            caret = Math.Max(0, Math.Min(caret, text.Length));
            var start = caret;
            while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
                start--;

            var end = caret;
            while (end < text.Length && !char.IsWhiteSpace(text[end]))
                end++;

            return (start, end, text.Substring(start, end - start));
        }

        public static int GetCaretAfterApply(string input, int caretIndex, ShellCompletionCandidate candidate)
        {
            var text = input ?? "";
            var (tokenStart, _, _) = GetTokenAtCaret(text, caretIndex);
            var replacement = candidate.Replacement + (candidate.AppendSpace ? " " : "");
            return tokenStart + replacement.Length;
        }

        public static int GetCaretAfterReplacement(string input, int caretIndex, string replacement, bool appendSpace)
        {
            var text = input ?? "";
            var (tokenStart, _, _) = GetTokenAtCaret(text, caretIndex);
            return tokenStart + replacement.Length + (appendSpace ? 1 : 0);
        }

        static int CountTokensBefore(string text, int index)
        {
            var count = 0;
            foreach (var token in ShellTokenizer.Tokenize(text))
            {
                if (token.Start >= index)
                    break;
                count++;
            }

            return count;
        }

        static bool StartsWithIgnoreCase(string value, string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                return true;

            return value != null &&
                   value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static class ShellHintService
    {
        public static List<string> BuildHints(string input)
        {
            var trimmed = input?.Trim() ?? "";
            if (string.IsNullOrEmpty(trimmed))
                return new List<string>();

            var tokens = ShellTokenizer.Tokenize(trimmed);
            if (tokens.Count == 0)
                return new List<string>();

            var first = tokens[0].Value;
            if (tokens.Count == 1 && !trimmed.EndsWith(" ", StringComparison.Ordinal))
                return BuildUnknownOrMatchingCommandHints(first);

            var commandName = ShellLineParser.ResolveRuntimeCommand(first);
            if (string.IsNullOrEmpty(commandName))
                return BuildUnknownOrMatchingCommandHints(first);

            var handler = CliCommandDiscovery.FindForHost(commandName, "runtime");
            if (handler == null)
                return BuildUnknownOrMatchingCommandHints(first);

            var lines = new List<string>
            {
                FormatHandlerHint(handler),
            };

            var validationError = ShellValidationService.ValidateBeforeExecute(
                ShellLineParser.Parse(trimmed));
            if (!string.IsNullOrEmpty(validationError))
                lines.Add("[!] " + validationError);

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var token in tokens.Skip(1))
            {
                if (!token.Value.StartsWith("--", StringComparison.Ordinal))
                    continue;

                var eq = token.Value.IndexOf('=');
                var key = eq > 2 ? token.Value.Substring(2, eq - 2) : token.Value.Substring(2);
                if (!string.IsNullOrEmpty(key))
                    used.Add(key);
            }

            var paramPrefix = GetActiveParamPrefix(trimmed, tokens);
            foreach (var desc in handler.ParamDescriptions ?? Array.Empty<string>())
            {
                var key = ExtractParamKey(desc);
                if (string.IsNullOrEmpty(key) || used.Contains(key))
                    continue;

                if (!string.IsNullOrEmpty(paramPrefix) &&
                    !StartsWithIgnoreCase(key, paramPrefix))
                {
                    continue;
                }

                lines.Add("  " + desc);
            }

            return lines;
        }

        public static List<string> BuildHelpLines()
        {
            var lines = new List<string> { "Available runtime commands:" };
            foreach (var hint in ListAllCommands())
                lines.Add("  " + hint);

            return lines;
        }

        static List<string> BuildUnknownOrMatchingCommandHints(string first)
        {
            var prefixMatches = ListCommandsMatching(first);
            if (prefixMatches.Count > 0)
                return prefixMatches;

            var similar = ShellCommandMatcher.RankSimilar(first);
            if (similar.Count == 0)
                return new List<string> { $"Unknown command '{first}'." };

            var lines = new List<string> { $"Unknown command '{first}'. Did you mean:" };
            foreach (var handler in similar)
                lines.Add("  " + FormatHandlerHint(handler));

            return lines;
        }

        static string GetActiveParamPrefix(string trimmed, List<ShellToken> tokens)
        {
            if (tokens.Count < 2 || trimmed.EndsWith(" ", StringComparison.Ordinal))
                return null;

            var last = tokens[tokens.Count - 1].Value;
            if (!last.StartsWith("--", StringComparison.Ordinal))
                return null;

            var eq = last.IndexOf('=');
            return eq > 2 ? last.Substring(2, eq - 2) : last.Substring(2);
        }

        static bool StartsWithIgnoreCase(string value, string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                return true;

            return value != null &&
                   value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        static List<string> ListAllCommands()
        {
            return CliCommandDiscovery.Handlers
                .Where(h => InvokeAvailability.IsAvailableForHost(h.Scope, "runtime"))
                .OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
                .Select(FormatHandlerHint)
                .ToList();
        }

        static List<string> ListCommandsMatching(string prefix)
        {
            return CliCommandDiscovery.Handlers
                .Where(h => InvokeAvailability.IsAvailableForHost(h.Scope, "runtime"))
                .Where(h => h.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                            (h.Aliases ?? Array.Empty<string>()).Any(a =>
                                a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
                .Select(FormatHandlerHint)
                .ToList();
        }

        static string FormatHandlerHint(IInvokeHandler handler)
        {
            var paramText = handler.ParamDescriptions != null && handler.ParamDescriptions.Length > 0
                ? " " + string.Join(" ", handler.ParamDescriptions)
                : "";

            var description = string.IsNullOrEmpty(handler.Description) ? "" : " — " + handler.Description;
            return handler.Name + paramText + description;
        }

        static string ExtractParamKey(string desc)
        {
            if (string.IsNullOrEmpty(desc) || !desc.StartsWith("--", StringComparison.Ordinal))
                return null;

            var end = 2;
            while (end < desc.Length && (char.IsLetterOrDigit(desc[end]) || desc[end] == '_'))
                end++;

            return end > 2 ? desc.Substring(2, end - 2) : null;
        }
    }

    public static class ShellValidationService
    {
        public static string ValidateBeforeExecute(ShellParseResult parse)
        {
            if (parse == null || !parse.IsValid)
                return null;

            var handler = CliCommandDiscovery.FindForHost(parse.CommandName, "runtime");
            if (handler?.ParamType == null)
                return null;

            var (_, error) = InvokeParameterBinding.BindRequired(handler.ParamType, parse.Args);
            return error;
        }
    }

    public static class ShellCommandMatcher
    {
        public static IEnumerable<IInvokeHandler> GetRuntimeHandlers()
        {
            foreach (var handler in CliCommandDiscovery.Handlers)
            {
                if (InvokeAvailability.IsAvailableForHost(handler.Scope, "runtime"))
                    yield return handler;
            }
        }

        public static List<IInvokeHandler> RankSimilar(string input, int maxResults = 5)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new List<IInvokeHandler>();

            var query = input.Trim();
            return GetRuntimeHandlers()
                .Select(handler => (handler, score: Score(query, handler)))
                .Where(pair => pair.score > 0)
                .OrderByDescending(pair => pair.score)
                .ThenBy(pair => pair.handler.Name, StringComparer.OrdinalIgnoreCase)
                .Take(maxResults)
                .Select(pair => pair.handler)
                .ToList();
        }

        static int Score(string query, IInvokeHandler handler)
        {
            var best = ScoreName(query, handler.Name);
            foreach (var alias in handler.Aliases ?? Array.Empty<string>())
                best = Math.Max(best, ScoreName(query, alias));

            return best;
        }

        static int ScoreName(string query, string name)
        {
            if (string.IsNullOrEmpty(name))
                return 0;

            if (string.Equals(query, name, StringComparison.OrdinalIgnoreCase))
                return 1000;

            if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                return 500 + query.Length;

            var distance = LevenshteinDistance(query.ToLowerInvariant(), name.ToLowerInvariant());
            var threshold = Math.Max(2, name.Length / 3);
            if (distance <= threshold)
                return 200 - distance * 10;

            return 0;
        }

        static int LevenshteinDistance(string left, string right)
        {
            if (left.Length == 0)
                return right.Length;

            if (right.Length == 0)
                return left.Length;

            var previous = new int[right.Length + 1];
            var current = new int[right.Length + 1];

            for (var j = 0; j <= right.Length; j++)
                previous[j] = j;

            for (var i = 1; i <= left.Length; i++)
            {
                current[0] = i;
                for (var j = 1; j <= right.Length; j++)
                {
                    var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                    current[j] = Math.Min(
                        Math.Min(current[j - 1] + 1, previous[j] + 1),
                        previous[j - 1] + cost);
                }

                (previous, current) = (current, previous);
            }

            return previous[right.Length];
        }
    }
}
