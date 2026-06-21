using System.Globalization;
using System.Text;

namespace TerrariaSplit;

internal static class SplitConditionText
{
    public static bool TryParse(string text, string? language, out SplitCondition condition, out string errorMessage)
    {
        condition = SplitCondition.All([]);
        errorMessage = string.Empty;
        try
        {
            var parser = new Parser(text, language);
            condition = parser.Parse();
            condition.Normalize();
            return true;
        }
        catch (ConditionTextParseException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public static string Format(SplitCondition condition, string? language)
    {
        return FormatNode(condition ?? SplitCondition.All([]), language, 0);
    }

    private static string FormatNode(SplitCondition condition, string? language, int indent)
    {
        string kind = SplitConditionKind.Normalize(condition.Kind);
        return kind switch
        {
            SplitConditionKind.All => FormatGroup("ALL", 0, condition.Children, language, indent),
            SplitConditionKind.Any => FormatGroup("ATLEAST", 1, condition.Children, language, indent),
            SplitConditionKind.AtLeast => FormatGroup("ATLEAST", Math.Max(1, condition.Value), condition.Children, language, indent),
            _ => FormatFact(condition, language)
        };
    }

    private static string FormatGroup(
        string functionName,
        int requiredCount,
        IReadOnlyList<SplitCondition> children,
        string? language,
        int indent)
    {
        if (children.Count == 0)
        {
            return functionName == "ATLEAST"
                ? $"ATLEAST({requiredCount.ToString(CultureInfo.InvariantCulture)})"
                : "ALL()";
        }

        string currentIndent = new(' ', indent * 2);
        string childIndent = new(' ', (indent + 1) * 2);
        string header = functionName == "ATLEAST"
            ? $"ATLEAST({requiredCount.ToString(CultureInfo.InvariantCulture)},"
            : "ALL(";
        var builder = new StringBuilder();
        builder.Append(header);
        builder.AppendLine();
        for (int i = 0; i < children.Count; i++)
        {
            builder.Append(childIndent);
            builder.Append(FormatNode(children[i], language, indent + 1));
            if (i < children.Count - 1)
            {
                builder.Append(',');
            }

            builder.AppendLine();
        }

        builder.Append(currentIndent);
        builder.Append(')');
        return builder.ToString();
    }

    private static string FormatFact(SplitCondition condition, string? language)
    {
        string targetText = condition.FactKey;
        if (SplitCatalog.TryGetTargetByFactKey(condition.FactKey, out SplitTargetDefinition target))
        {
            targetText = SplitTargetTokenFormatter.Format(target);
        }

        targetText = QuoteIfNeeded(targetText);
        string comparison = SplitFactComparison.Normalize(condition.Comparison);
        return comparison switch
        {
            SplitFactComparison.AtLeast => $"{targetText} >= {Math.Max(1, condition.Value).ToString(CultureInfo.InvariantCulture)}",
            SplitFactComparison.Equal => $"{targetText} = {condition.Value.ToString(CultureInfo.InvariantCulture)}",
            SplitFactComparison.IsFalse => $"{targetText} = false",
            _ => targetText
        };
    }

    private static string QuoteIfNeeded(string value)
    {
        string trimmed = value.Trim();
        bool needsQuote = trimmed.Length == 0 ||
            trimmed.Length != value.Length ||
            trimmed.Contains('\r') ||
            trimmed.Contains('\n') ||
            trimmed.Contains(',') ||
            trimmed.Contains('(') ||
            trimmed.Contains(')') ||
            trimmed.Contains('=') ||
            trimmed.Contains('>') ||
            string.Equals(trimmed, "ALL", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "ATLEAST", StringComparison.OrdinalIgnoreCase);
        if (!needsQuote)
        {
            return trimmed;
        }

        return "\"" + trimmed.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private sealed class Parser
    {
        private readonly List<Token> tokens;
        private readonly string? language;
        private int position;

        public Parser(string text, string? language)
        {
            tokens = Tokenize(text);
            this.language = language;
        }

        public SplitCondition Parse()
        {
            SplitCondition condition = ParseNode();
            if (Current.Kind != TokenKind.End)
            {
                throw Error($"Unexpected token: {Current.Text}");
            }

            return condition;
        }

        private SplitCondition ParseNode()
        {
            Token token = Current;
            if (token.Kind != TokenKind.Text)
            {
                throw Error("Expected ALL, ATLEAST, or a Type:ID target.");
            }

            Advance();
            if (IsFunction(token.Text) && Match(TokenKind.LeftParen))
            {
                return ParseFunction(token.Text);
            }

            return ParseFact(token.Text);
        }

        private SplitCondition ParseFunction(string functionName)
        {
            if (string.Equals(functionName, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                List<SplitCondition> children = ParseConditionList();
                Expect(TokenKind.RightParen, "Expected ')' after ALL group.");
                return SplitCondition.All(children);
            }

            int requiredCount = ParseRequiredCount();
            if (Match(TokenKind.RightParen))
            {
                return SplitCondition.AtLeast([], requiredCount);
            }

            Expect(TokenKind.Comma, "Expected ',' after ATLEAST count.");
            List<SplitCondition> atLeastChildren = ParseConditionList();
            Expect(TokenKind.RightParen, "Expected ')' after ATLEAST group.");
            return SplitCondition.AtLeast(atLeastChildren, requiredCount);
        }

        private int ParseRequiredCount()
        {
            Token token = Expect(TokenKind.Text, "Expected ATLEAST count.");
            if (!int.TryParse(token.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) ||
                count < 1)
            {
                throw Error("ATLEAST count must be at least 1.");
            }

            return count;
        }

        private List<SplitCondition> ParseConditionList()
        {
            var children = new List<SplitCondition>();
            if (Current.Kind == TokenKind.RightParen)
            {
                return children;
            }

            while (true)
            {
                children.Add(ParseNode());
                if (!Match(TokenKind.Comma))
                {
                    break;
                }

                if (Current.Kind == TokenKind.RightParen)
                {
                    break;
                }
            }

            return children;
        }

        private SplitCondition ParseFact(string rawTarget)
        {
            string targetText = rawTarget.Trim();
            string comparison = SplitFactComparison.IsTrue;
            int value = 1;
            bool explicitComparison = false;

            if (Match(TokenKind.GreaterOrEqual))
            {
                comparison = SplitFactComparison.AtLeast;
                value = ParseIntegerValue("Expected a number after '>='.");
                explicitComparison = true;
            }
            else if (Match(TokenKind.Equal))
            {
                Token valueToken = Expect(TokenKind.Text, "Expected a value after '='.");
                string text = valueToken.Text.Trim();
                if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
                {
                    comparison = SplitFactComparison.IsFalse;
                }
                else if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
                {
                    comparison = SplitFactComparison.IsTrue;
                }
                else if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int equalValue))
                {
                    comparison = SplitFactComparison.Equal;
                    value = equalValue;
                }
                else
                {
                    throw Error("Expected true, false, or a number after '='.");
                }

                explicitComparison = true;
            }

            if (!TryResolveTarget(targetText, out SplitTargetDefinition target, out string resolveError))
            {
                throw Error(resolveError);
            }

            SplitCondition fact = CreateFactCondition(target);
            if (explicitComparison)
            {
                fact.Comparison = comparison;
                fact.Value = Math.Max(1, value);
            }

            return fact;
        }

        private int ParseIntegerValue(string error)
        {
            Token token = Expect(TokenKind.Text, error);
            if (!int.TryParse(token.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ||
                value < 1)
            {
                throw Error(error);
            }

            return value;
        }

        private Token Current => position < tokens.Count ? tokens[position] : tokens[^1];

        private void Advance()
        {
            if (position < tokens.Count - 1)
            {
                position++;
            }
        }

        private bool Match(TokenKind kind)
        {
            if (Current.Kind != kind)
            {
                return false;
            }

            Advance();
            return true;
        }

        private Token Expect(TokenKind kind, string message)
        {
            if (Current.Kind != kind)
            {
                throw Error(message);
            }

            Token token = Current;
            Advance();
            return token;
        }

        private ConditionTextParseException Error(string message)
        {
            return new ConditionTextParseException(message);
        }
    }

    private static bool IsFunction(string text)
    {
        return string.Equals(text.Trim(), "ALL", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(text.Trim(), "ATLEAST", StringComparison.OrdinalIgnoreCase);
    }

    private static SplitCondition CreateFactCondition(SplitTargetDefinition target)
    {
        if (target.Kind == SplitTargetKind.Item && SplitCatalog.TryParseItemTargetId(target.Id, out int itemId))
        {
            return SplitCatalog.CreateItemEverOwnedCondition(itemId, 1);
        }

        if (target.Kind == SplitTargetKind.Npc && SplitCatalog.TryParseNpcTargetId(target.Id, out int npcId))
        {
            return SplitCatalog.CreateNpcPresentCondition(npcId);
        }

        if (target.Kind == SplitTargetKind.Biome && SplitCatalog.TryParseBiomeTargetId(target.Id, out string biomeId))
        {
            return SplitCatalog.CreateBiomeActiveCondition(biomeId);
        }

        return SplitCatalog.CreateBossFactCondition(target.Id);
    }

    private static bool TryResolveTarget(
        string value,
        out SplitTargetDefinition target,
        out string errorMessage)
    {
        target = null!;
        errorMessage = string.Empty;
        string query = value.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            errorMessage = "Target ID cannot be empty.";
            return false;
        }

        if (TryParseTypedTargetToken(query, out string targetId) &&
            SplitCatalog.TryGetTarget(targetId, out target))
        {
            return true;
        }

        errorMessage = $"Unknown target: {query}. Use Boss:ID, Item:ID, NPC:ID, or Biome:ID.";
        return false;
    }

    private static bool TryParseTypedTargetToken(string value, out string targetId)
    {
        targetId = string.Empty;
        string trimmed = value.Trim();
        int separator = trimmed.IndexOf(':');
        if (separator <= 0 || separator == trimmed.Length - 1)
        {
            return false;
        }

        string type = trimmed[..separator].Trim();
        string id = trimmed[(separator + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        if (string.Equals(type, "Boss", StringComparison.OrdinalIgnoreCase))
        {
            string normalized = id.StartsWith("boss:", StringComparison.OrdinalIgnoreCase)
                ? id
                : $"boss:{id}";
            targetId = normalized.ToLowerInvariant();
            return true;
        }

        if (string.Equals(type, "Item", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId))
        {
            targetId = SplitCatalog.CreateItemTargetId(itemId);
            return true;
        }

        if (string.Equals(type, "NPC", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int npcId))
        {
            targetId = SplitCatalog.CreateNpcTargetId(npcId);
            return true;
        }

        if (string.Equals(type, "Biome", StringComparison.OrdinalIgnoreCase) &&
            TryResolveBiomeToken(id, out string biomeId))
        {
            targetId = SplitCatalog.CreateBiomeTargetId(biomeId);
            return true;
        }

        return false;
    }

    private static bool TryResolveBiomeToken(string value, out string biomeId)
    {
        biomeId = string.Empty;
        string query = value.Trim();
        string normalized = query.ToLowerInvariant();
        if (TerrariaBiomeCatalog.ById.ContainsKey(normalized))
        {
            biomeId = normalized;
            return true;
        }

        foreach (string id in TerrariaBiomeCatalog.ById.Keys)
        {
            if (string.Equals(SplitTargetTokenFormatter.ToPascalToken(id), query, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id.Replace("-", string.Empty, StringComparison.Ordinal), normalized, StringComparison.OrdinalIgnoreCase))
            {
                biomeId = id;
                return true;
            }
        }

        return false;
    }
    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        int index = 0;
        while (index < text.Length)
        {
            char ch = text[index];
            if (char.IsWhiteSpace(ch))
            {
                index++;
                continue;
            }

            if (ch == '(')
            {
                tokens.Add(new Token(TokenKind.LeftParen, "(", index++));
                continue;
            }

            if (ch == ')')
            {
                tokens.Add(new Token(TokenKind.RightParen, ")", index++));
                continue;
            }

            if (ch == ',')
            {
                tokens.Add(new Token(TokenKind.Comma, ",", index++));
                continue;
            }

            if (ch == '>' && index + 1 < text.Length && text[index + 1] == '=')
            {
                tokens.Add(new Token(TokenKind.GreaterOrEqual, ">=", index));
                index += 2;
                continue;
            }

            if (ch == '=')
            {
                int start = index;
                index++;
                if (index < text.Length && text[index] == '=')
                {
                    index++;
                }

                tokens.Add(new Token(TokenKind.Equal, "=", start));
                continue;
            }

            if (ch == '"' || ch == '\'')
            {
                tokens.Add(ReadQuotedToken(text, ref index));
                continue;
            }

            tokens.Add(ReadTextToken(text, ref index));
        }

        tokens.Add(new Token(TokenKind.End, string.Empty, text.Length));
        return tokens;
    }

    private static Token ReadQuotedToken(string text, ref int index)
    {
        int start = index;
        char quote = text[index++];
        var builder = new StringBuilder();
        while (index < text.Length)
        {
            char ch = text[index++];
            if (ch == quote)
            {
                return new Token(TokenKind.Text, builder.ToString(), start);
            }

            if (ch == '\\' && index < text.Length)
            {
                builder.Append(text[index++]);
                continue;
            }

            builder.Append(ch);
        }

        throw new ConditionTextParseException("Unclosed quoted target name.");
    }

    private static Token ReadTextToken(string text, ref int index)
    {
        int start = index;
        while (index < text.Length)
        {
            char ch = text[index];
            if (ch == '(' || ch == ')' || ch == ',' || ch == '=' || ch == '>')
            {
                break;
            }

            index++;
        }

        string value = text[start..index].Trim();
        if (value.Length == 0)
        {
            throw new ConditionTextParseException("Unexpected token.");
        }

        return new Token(TokenKind.Text, value, start);
    }

    private readonly record struct Token(TokenKind Kind, string Text, int Position);

    private enum TokenKind
    {
        Text,
        LeftParen,
        RightParen,
        Comma,
        GreaterOrEqual,
        Equal,
        End
    }

    private sealed class ConditionTextParseException : Exception
    {
        public ConditionTextParseException(string message)
            : base(message)
        {
        }
    }
}
