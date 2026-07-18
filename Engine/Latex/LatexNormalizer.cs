using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Taiji.Engine.Latex
{
    /// <summary>
    /// Markdown 解析前的 LaTeX 保护与规范化；渲染前 <see cref="CleanFormula"/> 供 RaTeX 使用。
    /// </summary>
    public static class LatexNormalizer
    {
        private static readonly Regex FenceSplit = new Regex(
            @"(```[\s\S]*?```|~~~[\s\S]*?~~~)",
            RegexOptions.Compiled);

        private static readonly Regex BeginEnv = new Regex(
            @"\\begin\{([a-zA-Z*]+)\}",
            RegexOptions.Compiled);

        public static string Normalize(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return markdown ?? "";

            var sb = new StringBuilder(markdown.Length + 32);
            var idx = 0;
            foreach (Match m in FenceSplit.Matches(markdown))
            {
                if (m.Index > idx)
                    sb.Append(NormalizeOutsideCode(markdown.Substring(idx, m.Index - idx)));
                sb.Append(NormalizeFence(m.Value));
                idx = m.Index + m.Length;
            }
            if (idx < markdown.Length)
                sb.Append(NormalizeOutsideCode(markdown.Substring(idx)));
            return sb.ToString();
        }

        /// <summary>去掉定界符并规整空白，供 RaTeX 渲染。</summary>
        public static string CleanFormula(string latex)
        {
            if (string.IsNullOrEmpty(latex)) return "";
            var s = latex.Trim();
            if (s.StartsWith("$$", StringComparison.Ordinal) && s.EndsWith("$$", StringComparison.Ordinal) && s.Length >= 4)
                s = s.Substring(2, s.Length - 4);
            else if (s.StartsWith("$", StringComparison.Ordinal) && s.EndsWith("$", StringComparison.Ordinal) && s.Length >= 2)
                s = s.Substring(1, s.Length - 2);
            else if (s.StartsWith(@"\[", StringComparison.Ordinal) && s.EndsWith(@"\]", StringComparison.Ordinal) && s.Length >= 4)
                s = s.Substring(2, s.Length - 4);
            else if (s.StartsWith(@"\(", StringComparison.Ordinal) && s.EndsWith(@"\)", StringComparison.Ordinal) && s.Length >= 4)
                s = s.Substring(2, s.Length - 4);
            return FlattenWs(s);
        }

        public static bool LooksLikeLatexDocument(string lang, string code)
        {
            var l = (lang ?? "").Trim().ToLowerInvariant();
            if (l == "latex" || l == "tex" || l == "math" || l == "latex2e" || l == "amsmath")
                return true;
            var t = (code ?? "").TrimStart();
            if (t.Length == 0) return false;
            if (t.StartsWith("\\begin{", StringComparison.Ordinal)) return true;
            if (t.StartsWith("\\[", StringComparison.Ordinal) || t.StartsWith("$$", StringComparison.Ordinal))
                return true;
            if (l.Length == 0 && t.IndexOf('\\') >= 0)
            {
                var cmd = Regex.Matches(t, @"\\[a-zA-Z]+").Count;
                return cmd >= 3 || t.IndexOf("\\frac", StringComparison.Ordinal) >= 0
                    || t.IndexOf("\\sum", StringComparison.Ordinal) >= 0
                    || t.IndexOf("\\int", StringComparison.Ordinal) >= 0;
            }
            return false;
        }

        private static string NormalizeFence(string fence)
        {
            if (fence.Length < 6) return fence;
            var openLen = 3;
            if (fence.StartsWith("~~~", StringComparison.Ordinal)) openLen = 3;
            var firstNl = fence.IndexOf('\n');
            if (firstNl < 0) return fence;
            var header = fence.Substring(openLen, firstNl - openLen).Trim();
            var close = fence.LastIndexOf(fence.StartsWith("```", StringComparison.Ordinal) ? "```" : "~~~", StringComparison.Ordinal);
            if (close <= firstNl) return fence;
            var body = fence.Substring(firstNl + 1, close - firstNl - 1);
            if (body.EndsWith("\r\n")) body = body.Substring(0, body.Length - 2);
            else if (body.EndsWith("\n")) body = body.Substring(0, body.Length - 1);

            if (!LooksLikeLatexDocument(header, body))
                return fence;

            var flat = FlattenWs(StripOuterMathDelims(body));
            var sb = new StringBuilder();
            sb.Append("\n$$\n").Append(flat).Append("\n$$\n");
            return sb.ToString();
        }

        private static string StripOuterMathDelims(string body)
        {
            var s = (body ?? "").Trim();
            if (s.StartsWith("$$", StringComparison.Ordinal) && s.EndsWith("$$", StringComparison.Ordinal) && s.Length >= 4)
                return s.Substring(2, s.Length - 4).Trim();
            if (s.StartsWith("\\[", StringComparison.Ordinal) && s.EndsWith("\\]", StringComparison.Ordinal) && s.Length >= 4)
                return s.Substring(2, s.Length - 4).Trim();
            if (s.StartsWith("$", StringComparison.Ordinal) && s.EndsWith("$", StringComparison.Ordinal) && s.Length >= 2)
                return s.Substring(1, s.Length - 2).Trim();
            return s;
        }

        private static string NormalizeOutsideCode(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var sb = new StringBuilder(text.Length + 64);
            var i = 0;
            while (i < text.Length)
            {
                if (TryTakeEnvironment(text, i, sb, out i)) continue;
                if (TryTakeDelim(text, i, "\\[", "\\]", sb, true, out i)) continue;
                if (i + 1 < text.Length && text[i] == '$' && text[i + 1] == '$')
                {
                    var end = text.IndexOf("$$", i + 2, StringComparison.Ordinal);
                    if (end >= 0)
                    {
                        var body = FlattenWs(text.Substring(i + 2, end - (i + 2)));
                        AppendDisplay(sb, body);
                        i = end + 2;
                        continue;
                    }
                }
                if (TryTakeDelim(text, i, "\\(", "\\)", sb, false, out i)) continue;
                if (text[i] == '$' && !(i + 1 < text.Length && text[i + 1] == '$'))
                {
                    var end = IndexOfUnescapedDollar(text, i + 1);
                    if (end > i)
                    {
                        var body = text.Substring(i + 1, end - (i + 1));
                        if (LooksLikeLatex(body))
                        {
                            var flat = FlattenWs(body);
                            if (body.IndexOf('\n') >= 0 || flat.Length > 48)
                                AppendDisplay(sb, flat);
                            else
                                sb.Append('$').Append(flat).Append('$');
                            i = end + 1;
                            continue;
                        }
                    }
                }

                sb.Append(text[i]);
                i++;
            }
            return sb.ToString();
        }

        private static bool TryTakeEnvironment(string text, int i, StringBuilder sb, out int next)
        {
            next = i;
            var m = BeginEnv.Match(text, i);
            if (!m.Success || m.Index != i) return false;
            var env = m.Groups[1].Value;
            if (!IsMathEnvironment(env)) return false;
            var open = m.Value;
            var close = $"\\end{{{env}}}";
            var start = i + open.Length;
            var end = text.IndexOf(close, start, StringComparison.Ordinal);
            if (end < 0) return false;
            var body = $"{open}{text.Substring(start, end - start)}{close}";
            AppendDisplay(sb, FlattenWs(body));
            next = end + close.Length;
            return true;
        }

        private static bool IsMathEnvironment(string env)
        {
            if (string.IsNullOrEmpty(env)) return false;
            env = env.TrimEnd('*').ToLowerInvariant();
            return env == "aligned" || env == "align" || env == "alignat"
                || env == "eqnarray" || env == "gather" || env == "equation"
                || env == "multline" || env == "flalign" || env == "math"
                || env == "displaymath" || env == "cases" || env == "matrix"
                || env == "pmatrix" || env == "bmatrix" || env == "vmatrix"
                || env == "array";
        }

        private static bool TryTakeDelim(string text, int i, string open, string close, StringBuilder sb, bool display, out int next)
        {
            next = i;
            if (i + open.Length > text.Length) return false;
            if (string.CompareOrdinal(text, i, open, 0, open.Length) != 0) return false;
            var start = i + open.Length;
            var end = text.IndexOf(close, start, StringComparison.Ordinal);
            if (end < 0) return false;
            var flat = FlattenWs(text.Substring(start, end - start));
            if (display) AppendDisplay(sb, flat);
            else sb.Append('$').Append(flat).Append('$');
            next = end + close.Length;
            return true;
        }

        private static void AppendDisplay(StringBuilder sb, string body)
        {
            if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
                sb.Append('\n');
            sb.Append("$$\n").Append(body).Append("\n$$\n");
        }

        private static int IndexOfUnescapedDollar(string text, int from)
        {
            for (var i = from; i < text.Length; i++)
            {
                if (text[i] != '$') continue;
                if (i + 1 < text.Length && text[i + 1] == '$') continue;
                if (i > 0 && text[i - 1] == '\\') continue;
                return i;
            }
            return -1;
        }

        private static bool LooksLikeLatex(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return false;
            return body.IndexOf('\\') >= 0
                || body.IndexOf('_') >= 0
                || body.IndexOf('^') >= 0
                || body.IndexOf('{') >= 0;
        }

        internal static string FlattenWs(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var parts = s.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts).Trim();
        }
    }
}
