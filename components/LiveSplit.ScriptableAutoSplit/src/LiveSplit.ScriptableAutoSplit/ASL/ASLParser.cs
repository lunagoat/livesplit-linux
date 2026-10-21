using LiveSplit.ComponentUtil;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace LiveSplit.ASL;

// ASL's outer grammar is deliberately small. Roslyn tokenizes the embedded C#,
// so braces in comments and string literals cannot terminate a method body.
public static class ASLParser
{
    public static ASLScript Parse(string code) => new Reader(code).Parse();
    private sealed class Reader
    {
        private readonly string source;
        private readonly SyntaxToken[] tokens;
        private int position;
        public Reader(string source)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            tokens = SyntaxFactory.ParseTokens(source).Where(t => !t.IsKind(SyntaxKind.EndOfFileToken)).ToArray();
        }
        private string Current => position < tokens.Length ? tokens[position].Text : "<end>";
        private Exception Error(string message)
        {
            int offset = position < tokens.Length ? tokens[position].SpanStart : source.Length;
            int line = source.Take(offset).Count(c => c == '\n') + 1;
            return new FormatException($"ASL parse error at Line {line}: {message}; got '{Current}'.");
        }
        private SyntaxToken Take() => position < tokens.Length ? tokens[position++] : throw Error("Unexpected end of script");
        private void Expect(string text) { if (Current != text) throw Error("Expected " + text); position++; }
        private string String()
        {
            if (position >= tokens.Length || !tokens[position].IsKind(SyntaxKind.StringLiteralToken)) throw Error("Expected string literal");
            return Take().ValueText;
        }
        private int Number()
        {
            bool negative = Current == "-";
            if (negative || Current == "+") Take();
            var token = Take();
            if (!token.IsKind(SyntaxKind.NumericLiteralToken)) throw Error("Expected integer offset");
            try
            {
                long value = Convert.ToInt64(token.Value, CultureInfo.InvariantCulture);
                return unchecked((int)(negative ? -value : value));
            }
            catch (Exception) { throw Error("Invalid integer offset"); }
        }
        public ASLScript Parse()
        {
            var states = new Dictionary<string, List<ASLState>>(StringComparer.OrdinalIgnoreCase);
            var methods = new ASLScript.Methods();
            while (Current == "state")
            {
                Take(); Expect("("); string process = String(); string version = "";
                if (Current == ",") { Take(); version = String(); }
                Expect(")"); Expect("{");
                var state = new ASLState { GameVersion = version };
                while (Current != "}")
                {
                    if (Current == ";") { Take(); continue; }
                    string type = Take().ValueText; string name = Take().ValueText; Expect(":");
                    string module = "";
                    if (position < tokens.Length && tokens[position].IsKind(SyntaxKind.StringLiteralToken)) { module = String(); Expect(","); }
                    int moduleBase = Number(); var offsets = new List<int>();
                    while (Current == ",") { Take(); offsets.Add(Number()); }
                    state.ValueDefinitions.Add(new ASLValueDefinition { Type = type, Identifier = name, Pointer = new DeepPointer(module, moduleBase, offsets.ToArray()) });
                    if (Current != "}") Expect(";");
                }
                Expect("}");
                if (!states.TryGetValue(process, out var list)) states.Add(process, list = []);
                list.Add(state);
            }
            while (position < tokens.Length)
            {
                string name = Take().ValueText;
                var field = typeof(ASLScript.Methods).GetField(name);
                if (field == null || field.FieldType != typeof(ASLMethod)) throw Error("Unknown method " + name);
                if (Current != "{") throw Error("Expected method body");
                int start = Take().Span.End, depth = 1, end = start;
                while (depth > 0)
                {
                    var token = Take();
                    if (token.IsKind(SyntaxKind.OpenBraceToken)) depth++;
                    if (token.IsKind(SyntaxKind.CloseBraceToken)) depth--;
                    if (depth == 0) end = token.SpanStart;
                }
                int line = source.Take(start).Count(c => c == '\n') + 1;
                field.SetValue(methods, new ASLMethod(source[start..end], name, line) { ScriptMethods = methods });
            }
            return new ASLScript(methods, states);
        }
    }
}
