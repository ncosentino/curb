using System.Text.Json;
using Nullean.Curb.LayoutRules;

namespace Nullean.Curb.Cli;

internal static class LayoutRuleCompiler
{
	internal const int MaximumBytes = 1024 * 1024;

	internal static LayoutRuleDefinition[] Compile(ReadOnlyMemory<byte> bytes)
	{
		if (bytes.Length > MaximumBytes)
			throw new LayoutRuleConfigurationException("The layout rule file exceeds 1 MiB.");
		if (bytes.Span.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
			bytes = bytes[3..];
		try
		{
			using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
			var root = document.RootElement;
			RequireObject(root, "schemaVersion", "rules");
			if (!root.GetProperty("schemaVersion").TryGetInt32(out var version) || version != 1)
				throw new LayoutRuleConfigurationException("Only layout rule schemaVersion 1 is supported.");
			var rules = root.GetProperty("rules");
			if (rules.ValueKind != JsonValueKind.Array || rules.GetArrayLength() is < 1 or > 256)
				throw new LayoutRuleConfigurationException("A layout rule file must contain 1 to 256 rules.");
			var definitions = new List<LayoutRuleDefinition>();
			var ids = new HashSet<string>(StringComparer.Ordinal);
			foreach (var rule in rules.EnumerateArray())
			{
				RequireObject(rule, "id", "files", "match", "layout");
				var id = ReadString(rule, "id");
				if (!ids.Add(id))
					throw new LayoutRuleConfigurationException("Layout rule IDs must be unique.");
				var files = ReadStrings(rule.GetProperty("files"), 32);
				foreach (var pattern in files)
				{
					if (pattern.Length > 256 || pattern.StartsWith('/') || pattern.StartsWith('\\')
						|| pattern.Contains(':') || pattern.Split('/', '\\').Contains("..")
						|| pattern.IndexOfAny(['{', '}', '[', ']', '(', ')', '!', '#', '\r', '\n']) >= 0)
						throw new LayoutRuleConfigurationException("Layout file filters must be relative paths using only literal segments, *, ** and ?.");
				}
				var match = rule.GetProperty("match");
				RequireObject(match, "kind", "owner", "calleeSyntax", "callbackArgument", "callbackParameters", "terminalBody", "awaitTokens");
				RequireValue(match, "kind", "lambda-wrapper-chain");
				RequireValue(match, "owner", "expression-bodied-method");
				RequireValue(match, "callbackArgument", "last");
				RequireValue(match, "callbackParameters", "empty");
				RequireValue(match, "terminalBody", "block");
				RequireValue(match, "awaitTokens", "preserve");
				var layout = rule.GetProperty("layout");
				RequireObject(layout, "recipe", "anchor", "parameterClose", "arrowAndRootAwait", "wrapperIndent", "lambdaBraceIndent", "bodyIndent", "closeInvocations");
				RequireValue(layout, "recipe", "vertical-wrapper-chain");
				RequireValue(layout, "anchor", "declaration");
				RequireValue(layout, "parameterClose", "with-last-parameter");
				RequireValue(layout, "arrowAndRootAwait", "with-header");
				RequireValue(layout, "closeInvocations", "compact");
				RequireNumber(layout, "wrapperIndent", 0);
				RequireNumber(layout, "lambdaBraceIndent", 0);
				RequireNumber(layout, "bodyIndent", 1);
				definitions.Add(new LayoutRuleDefinition(new WrapperLayoutRule(id, ReadStrings(match.GetProperty("calleeSyntax"), 64)), files));
			}
			return [.. definitions];
		}
		catch (JsonException exception)
		{
			throw new LayoutRuleConfigurationException($"Invalid layout JSON at line {exception.LineNumber}, byte {exception.BytePositionInLine}.");
		}
		catch (ArgumentException)
		{
			throw new LayoutRuleConfigurationException("A layout rule contains an invalid identifier or callee spelling.");
		}
		catch (InvalidOperationException)
		{
			throw new LayoutRuleConfigurationException("A layout rule property has the wrong JSON type.");
		}
	}

	private static void RequireObject(JsonElement element, params string[] keys)
	{
		if (element.ValueKind != JsonValueKind.Object)
			throw new LayoutRuleConfigurationException("A layout rule object was expected.");
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (var property in element.EnumerateObject())
		{
			if (!seen.Add(property.Name) || !keys.Contains(property.Name, StringComparer.Ordinal))
				throw new LayoutRuleConfigurationException("A layout rule contains a duplicate or unsupported property.");
		}
		if (seen.Count != keys.Length)
			throw new LayoutRuleConfigurationException("A required layout rule property is missing.");
	}

	private static string ReadString(JsonElement element, string name)
	{
		var property = element.GetProperty(name);
		if (property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
			throw new LayoutRuleConfigurationException($"Layout property '{name}' must be a nonempty string.");
		return property.GetString()!;
	}

	private static string[] ReadStrings(JsonElement element, int maximum)
	{
		if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() is 0 || element.GetArrayLength() > maximum)
			throw new LayoutRuleConfigurationException("A layout rule string list is empty or exceeds its budget.");
		var values = new List<string>();
		foreach (var item in element.EnumerateArray())
		{
			if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
				throw new LayoutRuleConfigurationException("A layout rule list contains an invalid string.");
			values.Add(item.GetString()!);
		}
		return [.. values];
	}

	private static void RequireValue(JsonElement element, string name, string expected)
	{
		if (ReadString(element, name) != expected)
			throw new LayoutRuleConfigurationException($"Layout property '{name}' is not supported by schema version 1.");
	}

	private static void RequireNumber(JsonElement element, string name, int expected)
	{
		if (!element.GetProperty(name).TryGetInt32(out var number) || number != expected)
			throw new LayoutRuleConfigurationException($"Layout property '{name}' is not supported by the vertical-wrapper-chain recipe.");
	}
}
