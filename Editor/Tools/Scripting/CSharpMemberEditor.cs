// Copyright (C) KitWright. Licensed under MIT.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace KitWright.Editor.Tools.Scripting
{
    /// <summary>
    /// Method-level edits to a C# file. patch_script matches text, so it breaks on the things that
    /// vary between a model's memory of a file and the file itself -- indentation, an attribute that
    /// moved, a comment. These edits address a member by name instead, and every span they touch is
    /// found by matching braces, so a replacement cannot half-overwrite the next member.
    ///
    /// Not a parser. It masks literals and comments, then works on the masked text, which is enough
    /// to find a type body and the members directly inside it. What it does not model: nested types
    /// with a member of the same name as the outer type's, and preprocessor branches -- an <c>#if</c>
    /// that hides a brace is invisible to it, so a file that uses one around a member boundary
    /// should be edited with patch_script.
    /// </summary>
    internal static class CSharpMemberEditor
    {
        internal const string OpReplace = "replace_method";
        internal const string OpInsert = "insert_method";
        internal const string OpDelete = "delete_method";

        internal sealed class MemberEdit
        {
            public string Op;
            public string ClassName;
            public string MethodName;
            public string Replacement;
            public string Position;      // insert_method: start | end | after | before
            public string AnchorMethod;  // insert_method with position after/before
        }

        internal sealed class EditOutcome
        {
            public bool Success;
            public string Source;
            public string ErrorCode;
            public string Message;
            public string[] Candidates;
        }

        public static EditOutcome Apply(string source, IReadOnlyList<MemberEdit> edits)
        {
            if (source == null)
                return Fail("EMPTY_SOURCE", "The file is empty.");
            if (edits == null || edits.Count == 0)
                return Fail("NO_EDITS", "No edits were supplied.");

            var current = source;
            for (var i = 0; i < edits.Count; i++)
            {
                // Re-masked per edit rather than tracking offsets through the previous one: an edit
                // shifts every position after it, and a stale offset silently cuts the wrong span.
                var outcome = ApplyOne(current, edits[i]);
                if (!outcome.Success)
                {
                    outcome.Message = $"Edit {i + 1} of {edits.Count} ({edits[i].Op}): {outcome.Message}";
                    return outcome;
                }

                current = outcome.Source;
            }

            return new EditOutcome { Success = true, Source = MatchLineEndings(source, current) };
        }

        // Edits are assembled with '\n'. On a file that is uniformly CRLF that would leave the edited
        // members as the only LF lines in it; a file that already mixes the two is left alone, since
        // rewriting it would touch lines no edit asked for.
        private static string MatchLineEndings(string original, string edited)
        {
            if (original.IndexOf("\r\n", StringComparison.Ordinal) < 0) return edited;
            if (Regex.IsMatch(original, @"(?<!\r)\n")) return edited;

            return Regex.Replace(edited, @"(?<!\r)\n", "\r\n");
        }

        private static EditOutcome ApplyOne(string source, MemberEdit edit)
        {
            if (edit == null)
                return Fail("INVALID_EDIT", "The edit is null.");
            if (string.IsNullOrWhiteSpace(edit.ClassName))
                return Fail("INVALID_EDIT", "class_name is required.");

            var mask = Mask(source);
            if (!TryFindTypeBody(mask, edit.ClassName, out var bodyOpen, out var bodyClose))
                return Fail("TYPE_NOT_FOUND",
                    $"No class, struct, interface or record named '{edit.ClassName}' in this file.",
                    DeclaredTypeNames(mask));

            switch (edit.Op)
            {
                case OpReplace: return Replace(source, mask, bodyOpen, bodyClose, edit);
                case OpDelete: return Delete(source, mask, bodyOpen, bodyClose, edit);
                case OpInsert: return Insert(source, mask, bodyOpen, bodyClose, edit);
                default:
                    return Fail("UNKNOWN_OP",
                        $"Unknown op '{edit.Op}'. Expected {OpReplace}, {OpInsert} or {OpDelete}.");
            }
        }

        private static EditOutcome Replace(string source, string mask, int bodyOpen, int bodyClose, MemberEdit edit)
        {
            if (edit.Replacement == null)
                return Fail("INVALID_EDIT", "replacement is required for " + OpReplace + ".");

            var found = ResolveSingleMethod(source, mask, bodyOpen, bodyClose, edit.MethodName, out var failure);
            if (found == null) return failure;

            var indent = IndentOfLineAt(source, found.Start);
            return Replaced(source, found.Start, found.End, Reindent(edit.Replacement, indent));
        }

        private static EditOutcome Delete(string source, string mask, int bodyOpen, int bodyClose, MemberEdit edit)
        {
            var found = ResolveSingleMethod(source, mask, bodyOpen, bodyClose, edit.MethodName, out var failure);
            if (found == null) return failure;

            // Take the member's own line break and the blank line that usually follows it, so a
            // delete leaves the gap between its neighbours looking like every other gap.
            var end = ConsumeLineBreak(source, found.End);
            var afterBlank = ConsumeLineBreak(source, SkipSpacesAndTabs(source, end));
            if (afterBlank > end && source.Substring(end, afterBlank - end).Trim().Length == 0)
                end = afterBlank;

            return Replaced(source, LineStart(source, found.Start), end, string.Empty);
        }

        private static EditOutcome Insert(string source, string mask, int bodyOpen, int bodyClose, MemberEdit edit)
        {
            if (string.IsNullOrWhiteSpace(edit.Replacement))
                return Fail("INVALID_EDIT", "replacement is required for " + OpInsert + ".");

            var position = string.IsNullOrWhiteSpace(edit.Position) ? "end" : edit.Position.Trim().ToLowerInvariant();
            int at;
            string indent;

            if (position == "after" || position == "before")
            {
                if (string.IsNullOrWhiteSpace(edit.AnchorMethod))
                    return Fail("INVALID_EDIT", $"anchor_method is required when position is '{position}'.");

                var anchor = ResolveSingleMethod(source, mask, bodyOpen, bodyClose, edit.AnchorMethod, out var failure);
                if (anchor == null) return failure;

                indent = IndentOfLineAt(source, anchor.Start);
                at = position == "after" ? anchor.End : LineStart(source, anchor.Start);
            }
            else if (position == "start" || position == "end")
            {
                indent = MemberIndent(source, bodyOpen, bodyClose);
                at = position == "start" ? bodyOpen + 1 : LineStart(source, bodyClose);
            }
            else
            {
                return Fail("INVALID_EDIT", $"Unknown position '{edit.Position}'. Expected start, end, after or before.");
            }

            // Each anchor lands on a different side of an existing line break, so the padding that
            // leaves exactly one blank line between members differs per position.
            var body = Reindent(edit.Replacement, indent);
            string text;
            switch (position)
            {
                case "after": text = "\n\n" + body; break;
                case "before": text = body + "\n\n"; break;
                default: text = "\n" + body + "\n"; break; // start and end insert at a line boundary
            }

            return Replaced(source, at, at, text);
        }

        private sealed class MethodSpan
        {
            public int Start;
            public int End;
            public string Signature;
        }

        private static MethodSpan ResolveSingleMethod(
            string source, string mask, int bodyOpen, int bodyClose, string methodName, out EditOutcome failure)
        {
            failure = null;

            if (string.IsNullOrWhiteSpace(methodName))
            {
                failure = Fail("INVALID_EDIT", "method_name is required.");
                return null;
            }

            var matches = FindMethods(source, mask, bodyOpen, bodyClose, methodName);
            if (matches.Count == 0)
            {
                failure = Fail("METHOD_NOT_FOUND",
                    $"No method named '{methodName}' directly inside that type.",
                    MethodNames(source, mask, bodyOpen, bodyClose));
                return null;
            }

            if (matches.Count > 1)
            {
                var signatures = new string[matches.Count];
                for (var i = 0; i < matches.Count; i++) signatures[i] = matches[i].Signature;

                failure = Fail("AMBIGUOUS_METHOD",
                    $"'{methodName}' is overloaded {matches.Count} times here; this tool addresses a member by name, " +
                    "so it cannot tell them apart. Use patch_script for one overload.",
                    signatures);
                return null;
            }

            return matches[0];
        }

        // ----- source scanning -----

        /// <summary>
        /// A copy of the source with every string, char literal and comment blanked to spaces, and
        /// line breaks kept. Index math on it maps back to the original one-to-one, so a brace found
        /// here is a real brace and never one inside a string.
        /// </summary>
        internal static string Mask(string source) => Mask(source, out _);

        /// <param name="unterminated">
        /// The construct still open at end of file (a block comment or a verbatim string), or null.
        /// Everything after it was blanked, so a caller that only counts braces would report the
        /// wrong problem without this.
        /// </param>
        internal static string Mask(string source, out string unterminated)
        {
            unterminated = null;
            if (string.IsNullOrEmpty(source)) return source ?? string.Empty;

            var masked = new char[source.Length];
            bool inString = false, inVerbatim = false, inInterpolated = false, inChar = false;
            bool inLineComment = false, inBlockComment = false;

            for (var i = 0; i < source.Length; i++)
            {
                char c = source[i];
                char next = i + 1 < source.Length ? source[i + 1] : '\0';
                bool hidden = inString || inChar || inLineComment || inBlockComment;

                if (c == '\n')
                {
                    inLineComment = false;
                    // A non-verbatim string cannot span lines; treat the newline as closing it so one
                    // stray quote does not blank the rest of the file.
                    if (inString && !inVerbatim) { inString = false; inInterpolated = false; }
                    masked[i] = c;
                    continue;
                }

                if (inLineComment) { masked[i] = ' '; continue; }

                if (inBlockComment)
                {
                    masked[i] = ' ';
                    if (c == '*' && next == '/') { inBlockComment = false; masked[i] = ' '; if (i + 1 < source.Length) { masked[++i] = ' '; } }
                    continue;
                }

                if (inString)
                {
                    masked[i] = ' ';
                    if (inVerbatim)
                    {
                        if (c == '"' && next == '"') { masked[++i] = ' '; continue; }
                        if (c == '"') { inString = false; inVerbatim = false; inInterpolated = false; }
                    }
                    else
                    {
                        if (c == '\\' && i + 1 < source.Length) { masked[++i] = ' '; continue; }
                        if (c == '"') { inString = false; inInterpolated = false; }
                    }
                    continue;
                }

                if (inChar)
                {
                    masked[i] = ' ';
                    if (c == '\\' && i + 1 < source.Length) { masked[++i] = ' '; continue; }
                    if (c == '\'') inChar = false;
                    continue;
                }

                if (c == '/' && next == '/') { inLineComment = true; masked[i] = ' '; continue; }
                if (c == '/' && next == '*') { inBlockComment = true; masked[i] = ' '; continue; }

                if (c == '@' && next == '"') { inString = true; inVerbatim = true; masked[i] = ' '; masked[++i] = ' '; continue; }
                if (c == '$' && next == '"') { inString = true; inInterpolated = true; masked[i] = ' '; masked[++i] = ' '; continue; }
                if (c == '$' && next == '@' && i + 2 < source.Length && source[i + 2] == '"')
                {
                    inString = true; inVerbatim = true; inInterpolated = true;
                    masked[i] = ' '; masked[++i] = ' '; masked[++i] = ' ';
                    continue;
                }
                if (c == '"') { inString = true; masked[i] = ' '; continue; }
                if (c == '\'') { inChar = true; masked[i] = ' '; continue; }

                masked[i] = hidden ? ' ' : c;
            }

            if (inBlockComment) unterminated = "block comment";
            else if (inString && inVerbatim) unterminated = "verbatim string";

            return new string(masked);
        }

        internal static bool TryFindTypeBody(string mask, string typeName, out int bodyOpen, out int bodyClose)
        {
            bodyOpen = bodyClose = -1;

            var declaration = new Regex($@"\b(class|struct|interface|record)\s+{Regex.Escape(typeName)}\b");
            foreach (Match match in declaration.Matches(mask))
            {
                var open = mask.IndexOf('{', match.Index + match.Length);
                if (open < 0) continue;

                // A ';' before the '{' means this was a forward reference, not the definition.
                if (mask.IndexOf(';', match.Index + match.Length, open - match.Index - match.Length) >= 0)
                    continue;

                if (!TryMatchBrace(mask, open, out var close)) continue;

                bodyOpen = open;
                bodyClose = close;
                return true;
            }

            return false;
        }

        internal static bool TryMatchBrace(string mask, int open, out int close)
        {
            close = -1;
            var depth = 0;

            for (var i = open; i < mask.Length; i++)
            {
                if (mask[i] == '{') depth++;
                else if (mask[i] == '}')
                {
                    depth--;
                    if (depth == 0) { close = i; return true; }
                }
            }

            return false;
        }

        private static List<MethodSpan> FindMethods(string source, string mask, int bodyOpen, int bodyClose, string methodName)
        {
            var results = new List<MethodSpan>();
            var identifier = new Regex($@"\b{Regex.Escape(methodName)}\b");

            foreach (Match match in identifier.Matches(mask))
            {
                if (match.Index <= bodyOpen || match.Index >= bodyClose) continue;
                if (DepthBetween(mask, bodyOpen, match.Index) != 1) continue;

                var afterName = SkipWhitespace(mask, match.Index + match.Length);
                // A generic method: the type parameter list sits between the name and the arguments.
                if (afterName < mask.Length && mask[afterName] == '<')
                {
                    if (!TryMatchAngle(mask, afterName, out var closeAngle)) continue;
                    afterName = SkipWhitespace(mask, closeAngle + 1);
                }

                if (afterName >= mask.Length || mask[afterName] != '(') continue;
                if (!TryMatchParen(mask, afterName, out var closeParen)) continue;
                if (!LooksLikeDeclaration(mask, bodyOpen, match.Index)) continue;

                var end = FindMemberEnd(mask, closeParen + 1);
                if (end < 0) continue;

                var start = DeclarationStart(source, mask, bodyOpen, match.Index);
                results.Add(new MethodSpan
                {
                    Start = start,
                    End = end,
                    Signature = Collapse(source.Substring(match.Index, closeParen + 1 - match.Index))
                });
            }

            return results;
        }

        /// <summary>
        /// Separates a declaration from a call. Everything between the end of the previous member and
        /// the name must be modifiers and a return type -- no '=' (a field holding a lambda that calls
        /// the same name) and no '(' (a call inside an initializer).
        /// </summary>
        private static bool LooksLikeDeclaration(string mask, int bodyOpen, int nameIndex)
        {
            for (var i = nameIndex - 1; i > bodyOpen; i--)
            {
                var c = mask[i];
                if (c == ';' || c == '{' || c == '}' || c == ']') return true;
                if (c == '=' || c == '(' || c == ',') return false;
            }

            return true;
        }

        // A member ends at its closing brace, at the ';' of an expression body, or at the ';' of a
        // declaration with no body at all (abstract, extern, interface).
        private static int FindMemberEnd(string mask, int afterArguments)
        {
            for (var i = afterArguments; i < mask.Length; i++)
            {
                var c = mask[i];
                if (char.IsWhiteSpace(c)) continue;

                if (c == '{')
                    return TryMatchBrace(mask, i, out var close) ? close + 1 : -1;

                if (c == ';')
                    return i + 1;

                if (c == '=' && i + 1 < mask.Length && mask[i + 1] == '>')
                {
                    var terminator = mask.IndexOf(';', i);
                    return terminator < 0 ? -1 : terminator + 1;
                }
            }

            return -1;
        }

        // Attributes and the doc comment above a member are part of it: leaving them behind would
        // duplicate whatever the replacement carries.
        private static int DeclarationStart(string source, string mask, int bodyOpen, int nameIndex)
        {
            var start = LineStart(source, nameIndex);

            while (start > bodyOpen)
            {
                // start - 2, not start - 1: at start - 1 sits the line break that ends the previous
                // line, and LineStart of a line break is the line after it -- which is this one.
                var previousStart = LineStart(source, Math.Max(0, start - 2));
                if (previousStart >= start) break;

                var raw = source.Substring(previousStart, start - previousStart);
                if (raw.Trim().Length == 0) break;

                // The attribute test reads the masked line so a bracket inside a string does not
                // count; the comment test reads the raw line, because a comment masks to blank.
                var masked = mask.Substring(previousStart, start - previousStart).Trim();
                var isAttribute = masked.StartsWith("[", StringComparison.Ordinal) && masked.EndsWith("]", StringComparison.Ordinal);
                var isComment = raw.TrimStart().StartsWith("//", StringComparison.Ordinal);
                if (!isAttribute && !isComment) break;

                start = previousStart;
            }

            return start;
        }

        private static int DepthBetween(string mask, int from, int to)
        {
            var depth = 0;
            for (var i = from; i < to; i++)
            {
                if (mask[i] == '{') depth++;
                else if (mask[i] == '}') depth--;
            }

            return depth;
        }

        private static bool TryMatchParen(string mask, int open, out int close) =>
            TryMatchPair(mask, open, '(', ')', out close);

        private static bool TryMatchAngle(string mask, int open, out int close) =>
            TryMatchPair(mask, open, '<', '>', out close);

        private static bool TryMatchPair(string mask, int open, char openChar, char closeChar, out int close)
        {
            close = -1;
            var depth = 0;

            for (var i = open; i < mask.Length; i++)
            {
                if (mask[i] == openChar) depth++;
                else if (mask[i] == closeChar)
                {
                    depth--;
                    if (depth == 0) { close = i; return true; }
                }
                else if (mask[i] == '{' || mask[i] == ';')
                {
                    // A generic argument list never spans a statement; bail rather than run away
                    // through the rest of the file on a stray '<'.
                    if (openChar == '<') return false;
                }
            }

            return false;
        }

        private static string[] MethodNames(string source, string mask, int bodyOpen, int bodyClose)
        {
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (Match match in new Regex(@"\b([A-Za-z_]\w*)\s*(<[^;{}]*>)?\s*\(").Matches(mask))
            {
                if (match.Index <= bodyOpen || match.Index >= bodyClose) continue;
                if (DepthBetween(mask, bodyOpen, match.Index) != 1) continue;
                if (!LooksLikeDeclaration(mask, bodyOpen, match.Index)) continue;

                var name = match.Groups[1].Value;
                if (seen.Add(name)) names.Add(name);
            }

            return names.ToArray();
        }

        private static string[] DeclaredTypeNames(string mask)
        {
            var names = new List<string>();
            foreach (Match match in new Regex(@"\b(?:class|struct|interface|record)\s+([A-Za-z_]\w*)").Matches(mask))
                names.Add(match.Groups[1].Value);

            return names.ToArray();
        }

        // ----- text helpers -----

        private static int LineStart(string source, int index)
        {
            var start = source.LastIndexOf('\n', Math.Max(0, Math.Min(index, source.Length - 1)));
            return start < 0 ? 0 : start + 1;
        }

        private static string IndentOfLineAt(string source, int index)
        {
            var start = LineStart(source, index);
            var end = start;
            while (end < source.Length && (source[end] == ' ' || source[end] == '\t')) end++;
            return source.Substring(start, end - start);
        }

        // The indentation members of this type already use, so an inserted one lines up with them.
        private static string MemberIndent(string source, int bodyOpen, int bodyClose)
        {
            for (var i = bodyOpen + 1; i < bodyClose; i++)
            {
                if (source[i] == '\n') continue;
                if (char.IsWhiteSpace(source[i])) continue;
                return IndentOfLineAt(source, i);
            }

            return IndentOfLineAt(source, bodyOpen) + "    ";
        }

        /// <summary>
        /// Re-indents a block to sit at <paramref name="indent"/>, keeping its internal shape: the
        /// caller writes a method the way it reads, not the way it has to line up at this nesting.
        /// </summary>
        internal static string Reindent(string text, string indent)
        {
            var lines = text.Replace("\r\n", "\n").Trim('\n').Split('\n');
            var common = int.MaxValue;

            foreach (var line in lines)
            {
                if (line.Trim().Length == 0) continue;
                var leading = 0;
                while (leading < line.Length && (line[leading] == ' ' || line[leading] == '\t')) leading++;
                common = Math.Min(common, leading);
            }

            if (common == int.MaxValue) common = 0;

            var builder = new StringBuilder();
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0) builder.Append('\n');
                var line = lines[i];
                if (line.Trim().Length == 0) continue;
                builder.Append(indent).Append(line.Substring(Math.Min(common, line.Length)).TrimEnd());
            }

            return builder.ToString();
        }

        private static int SkipWhitespace(string mask, int index)
        {
            while (index < mask.Length && char.IsWhiteSpace(mask[index])) index++;
            return index;
        }

        private static int SkipSpacesAndTabs(string source, int index)
        {
            while (index < source.Length && (source[index] == ' ' || source[index] == '\t')) index++;
            return index;
        }

        private static int ConsumeLineBreak(string source, int index)
        {
            if (index < source.Length && source[index] == '\r') index++;
            if (index < source.Length && source[index] == '\n') index++;
            return index;
        }

        private static string Collapse(string text) =>
            Regex.Replace(text.Replace('\n', ' ').Replace('\r', ' '), @"\s+", " ").Trim();

        private static EditOutcome Replaced(string source, int start, int end, string text) =>
            new EditOutcome { Success = true, Source = source.Substring(0, start) + text + source.Substring(end) };

        private static EditOutcome Fail(string code, string message, string[] candidates = null) =>
            new EditOutcome { Success = false, ErrorCode = code, Message = message, Candidates = candidates };
    }

    /// <summary>
    /// Structural check on C# source, for use before it reaches disk. Deliberately not a compiler:
    /// the authoritative answer is Unity's own, via request_recompile + get_compilation_errors, which
    /// builds the whole assembly with its real define symbols and its sibling files. Compiling one
    /// file on its own — which is what a per-file validator has to do — misreports every partial
    /// class and every <c>#if</c> branch whose symbol it does not know. What is checked here is what
    /// can be checked reliably from one file, and it is the class of damage an edit actually causes.
    /// </summary>
    internal static class CSharpSyntaxCheck
    {
        /// <returns>Null when the source is structurally sound, otherwise the first problem found.</returns>
        internal static string FindProblem(string source)
        {
            if (string.IsNullOrEmpty(source)) return "The file is empty.";

            var mask = CSharpMemberEditor.Mask(source, out var unterminated);
            if (unterminated != null)
                return $"Unterminated {unterminated}: it is still open at end of file.";

            var openers = new Stack<int>();
            var line = 1;

            for (var i = 0; i < mask.Length; i++)
            {
                var c = mask[i];
                if (c == '\n') { line++; continue; }

                if (c == '{' || c == '(' || c == '[')
                {
                    openers.Push(i);
                    continue;
                }

                if (c != '}' && c != ')' && c != ']') continue;

                if (openers.Count == 0)
                    return $"Line {line}: '{c}' closes nothing.";

                var opener = mask[openers.Pop()];
                var expected = opener == '{' ? '}' : opener == '(' ? ')' : ']';
                if (expected != c)
                    return $"Line {line}: '{c}' does not close the '{opener}' it was matched with; expected '{expected}'.";
            }

            if (openers.Count > 0)
            {
                var index = openers.Pop();
                return $"Line {LineOf(mask, index)}: '{mask[index]}' is never closed.";
            }

            return null;
        }

        private static int LineOf(string text, int index)
        {
            var line = 1;
            for (var i = 0; i < index && i < text.Length; i++)
                if (text[i] == '\n') line++;

            return line;
        }
    }
}
