using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullean.Curb.LayoutRules;
using Nullean.Curb.Tests.Formatting;

namespace Nullean.Curb.Tests.LayoutRules;

public class RawStringContextTests
{
	[Test]
	public void Every_parent_context_uses_the_same_literal_boundary_for_each_raw_family()
	{
		var contexts = new[]
		{
			"class C\n{\n    void M()\n    {\n        var value = $literal;\n    }\n}",
			"class C\n{\n    public $type Value { get; } = $literal;\n}",
			"class C\n{\n    void M()\n    {\n        Consume(text: $literal);\n    }\n}",
			"class C\n{\n    $type M()\n    {\n        return $literal;\n    }\n}",
			"class C\n{\n    $type M() => $literal;\n}",
			"class C\n{\n    $type M(bool flag) => flag ? $literal : $literal;\n}",
		};
		var families = new[] { (Prefix: "", Suffix: ""), (Prefix: "$", Suffix: ""), (Prefix: "$$", Suffix: ""), (Prefix: "", Suffix: "u8") };
		var options = TestOptions.Parse(RawStringLayoutSamples.Config);
		var rules = new LayoutRuleSet([new MultilineRawStringLayoutRule("raw-openers")]);
		using var formatter = new CSharpFormatter();
		foreach (var template in contexts)
			foreach (var (prefix, suffix) in families)
			{
				var payload = prefix.Length switch { 1 => "value {name}", 2 => "value {{name}}", _ => "value" };
				var literal = prefix + "\"\"\"\n            " + payload + "\n            \"\"\"" + suffix;
				var source = template.Replace("$literal", literal, StringComparison.Ordinal)
					.Replace("$type", suffix.Length == 0 ? "string" : "System.ReadOnlySpan<byte>", StringComparison.Ordinal);
				var first = formatter.Format(source, options, layoutRules: rules);
				first.Success.Should().BeTrue(first.Message);
				first.Text.Should().NotBeNull();
				var before = RawNodes(source);
				var after = RawNodes(first.Text);
				after.Count.Should().Be(before.Count);
				first.LayoutApplications.Count.Should().Be(before.Count);
				for (var i = 0; i < after.Count; i++)
				{
					after[i].Content.Should().Be(before[i].Content);
					after[i].Prefix.Should().Be("            ");
				}
				var second = formatter.Format(first.Text, options, layoutRules: rules);
				second.Success.Should().BeTrue(second.Message);
				second.Changed.Should().BeFalse();
				second.Text.Should().Be(first.Text);
			}
	}

	[Test]
	public void Nearby_comments_and_significant_blank_lines_are_preserved()
	{
		var source = RawStringLayoutSamples.Attached
			.Replace("var value = ", "var value = // boundary comment\n            // literal comment\n            ", StringComparison.Ordinal)
			.Replace("            content", "            first  \n\n            last", StringComparison.Ordinal);
		using var formatter = new CSharpFormatter();
		var rules = new LayoutRuleSet([new MultilineRawStringLayoutRule("raw-openers")]);
		var options = TestOptions.Parse(RawStringLayoutSamples.Config);
		var first = formatter.Format(source, options, layoutRules: rules);
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().Contain("// boundary comment");
		first.Text.Should().Contain("// literal comment");
		RawNodes(first.Text).Single().Content.Should().Be(RawNodes(source).Single().Content);
		var second = formatter.Format(first.Text, options, layoutRules: rules);
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(first.Text);
	}

	[Test]
	public void Source_suppression_remains_authoritative()
	{
		var source = "#pragma warning disable IDE0055\n" + RawStringLayoutSamples.Attached + "\n#pragma warning restore IDE0055\n";
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(source, TestOptions.Parse(RawStringLayoutSamples.Config),
			layoutRules: new LayoutRuleSet([new MultilineRawStringLayoutRule("raw-openers")]));
		result.Success.Should().BeTrue(result.Message);
		result.Text.Should().Contain(RawStringLayoutSamples.Attached);
		result.LayoutApplications.Should().BeEmpty();
	}

	[Test]
	public void Duplicate_raw_rules_refuse_before_output()
	{
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(RawStringLayoutSamples.Attached, TestOptions.Parse(RawStringLayoutSamples.Config),
			layoutRules: new LayoutRuleSet([new MultilineRawStringLayoutRule("one"), new MultilineRawStringLayoutRule("two")]));
		result.Status.Should().Be(FormatStatus.VerificationFailed);
		result.Text.Should().BeNull();
		result.Message.Should().Contain("same raw-string boundary");
	}

	[Test]
	public void An_overlapping_custom_condition_refuses_without_overriding_its_header_policy()
	{
		const string source = "class C { void M() { if (\"\"\"\n    value\n    \"\"\" == text && other) { Call(); } } }";
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(source, TestOptions.Parse(RawStringLayoutSamples.Config),
			layoutRules: new LayoutRuleSet([new MultilineRawStringLayoutRule("raw"), new LogicalConditionLayoutRule("condition")]));
		result.Status.Should().Be(FormatStatus.VerificationFailed);
		result.Text.Should().BeNull();
		result.Message.Should().Contain("overlaps");
	}

	[Test]
	public void A_raw_value_inside_an_owned_condition_operand_can_compose()
	{
		const string source = "class C { void M() { if (text == \"\"\"\n            value\n            \"\"\" && other) { Call(); } } }";
		using var formatter = new CSharpFormatter();
		var rules = new LayoutRuleSet([new MultilineRawStringLayoutRule("raw"), new LogicalConditionLayoutRule("condition")]);
		var options = TestOptions.Parse(RawStringLayoutSamples.Config);
		var first = formatter.Format(source, options, layoutRules: rules);
		first.Success.Should().BeTrue(first.Message);
		first.LayoutApplications.Count.Should().Be(2);
		formatter.Format(first.Text!, options, layoutRules: rules).Text.Should().Be(first.Text);
	}

	[Test]
	public void Directives_and_disabled_text_around_a_selected_literal_are_preserved()
	{
		const string source = "class C\n{\n    string M()\n    {\n        var value =\n#if true\n            \"\"\"\n            selected\n            \"\"\"\n#else\n            \"inactive\"\n#endif\n            ;\n        return value;\n    }\n}";
		using var formatter = new CSharpFormatter();
		var rules = new LayoutRuleSet([new MultilineRawStringLayoutRule("raw")]);
		var options = TestOptions.Parse(RawStringLayoutSamples.Config);
		var first = formatter.Format(source, options, layoutRules: rules);
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().Contain("#if true");
		first.Text.Should().Contain("\"inactive\"");
		first.Text.Should().Contain("#endif");
		formatter.Format(first.Text, options, layoutRules: rules).Text.Should().Be(first.Text);
	}

	[Test]
	public void Wrapper_headers_delegate_raw_prefix_arguments_to_the_literal_policy()
	{
		const string source = "class C { Task M() => TraceScope.RunAsync(context: \"\"\"\n        payload\n        \"\"\", async () => { Call(); }); }";
		var rules = new LayoutRuleSet([new WrapperLayoutRule("wrapper", ["TraceScope.RunAsync"]), new MultilineRawStringLayoutRule("raw")]);
		var options = TestOptions.Parse(RawStringLayoutSamples.Config);
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(source, options, layoutRules: rules);
		first.Success.Should().BeTrue(first.Message);
		first.LayoutApplications.Count.Should().Be(2);
		RawNodes(first.Text!).Single().Content.Should().Be(RawNodes(source).Single().Content);
		formatter.Format(first.Text!, options, layoutRules: rules).Text.Should().Be(first.Text);
	}

	private static List<(string Content, string Prefix)> RawNodes(string source)
	{
		var tree = CSharpSyntaxTree.ParseText(source);
		tree.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeFalse();
		var text = tree.GetText();
		var result = new List<(string Content, string Prefix)>();
		foreach (var node in tree.GetRoot().DescendantNodes())
		{
			var raw = node switch
			{
				LiteralExpressionSyntax literal => literal.Token.Kind() is SyntaxKind.MultiLineRawStringLiteralToken or SyntaxKind.Utf8MultiLineRawStringLiteralToken,
				InterpolatedStringExpressionSyntax interpolated => interpolated.StringStartToken.IsKind(SyntaxKind.InterpolatedMultiLineRawStringStartToken),
				_ => false,
			};
			if (!raw)
				continue;
			var line = text.Lines.GetLineFromPosition(node.SpanStart);
			result.Add((source[node.Span.Start..node.Span.End], source[line.Start..node.SpanStart]));
		}
		return result;
	}
}
