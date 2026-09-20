using AwesomeAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Nullean.Curb.LayoutRules;
using Nullean.Curb.Tests.Formatting;

namespace Nullean.Curb.Tests.LayoutRules;

public class RawStringLayoutTests
{
	private static LayoutRuleSet Rules() => new([new MultilineRawStringLayoutRule("raw-openers")]);

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void Attached_and_detached_openers_converge_without_touching_content(bool detached)
	{
		Check(detached ? RawStringLayoutSamples.Detached : RawStringLayoutSamples.Attached,
			RawStringLayoutSamples.Detached, RawStringLayoutSamples.Config);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void The_selected_boundary_is_canonical_in_preservation_and_deterministic_modes(bool preserve)
	{
		Check(RawStringLayoutSamples.Attached, RawStringLayoutSamples.Detached,
			RawStringLayoutSamples.Config + $"\ncsharp_keep_existing_linebreaks = {preserve}");
	}

	[Test]
	public void No_width_is_needed_for_a_syntax_selected_boundary()
	{
		Check(RawStringLayoutSamples.Attached, RawStringLayoutSamples.Detached, "end_of_line = lf");
	}

	[Test]
	[Arguments("", "", 3)]
	[Arguments("$", "", 3)]
	[Arguments("$$", "", 4)]
	[Arguments("$$$", "", 4)]
	[Arguments("", "u8", 3)]
	[Arguments("", "u8", 4)]
	public void All_multiline_raw_families_keep_their_token_text(string dollars, string suffix, int quoteCount)
	{
		var quotes = new string('"', quoteCount);
		var payload = dollars.Length switch
		{
			1 => "value {name}",
			2 => "value \"\"\" {{name}}",
			3 => "value \"\"\" {{{name}}}",
			_ => quoteCount > 3 ? "value \"\"\" quotes" : "value",
		};
		var source = "class C\n{\n    void M()\n    {\n        var value = " + dollars + quotes + "\n            " + payload + "\n            " + quotes + suffix + ";\n    }\n}";
		var expected = source.Replace("var value = ", "var value =\n            ", StringComparison.Ordinal);
		Check(source, expected, RawStringLayoutSamples.Config);
	}

	[Test]
	public void Significant_indentation_inside_the_value_is_unchanged()
	{
		var source = RawStringLayoutSamples.Attached.Replace("            content", "            first\n                second", StringComparison.Ordinal);
		var expected = RawStringLayoutSamples.Detached.Replace("            content", "            first\n                second", StringComparison.Ordinal);
		Check(source, expected, RawStringLayoutSamples.Config);
	}

	[Test]
	public void A_detached_but_misaligned_opener_uses_the_preserved_closing_prefix()
	{
		var source = RawStringLayoutSamples.Detached.Replace("=\n            \"\"\"", "=\n                    \"\"\"", StringComparison.Ordinal);
		Check(source, RawStringLayoutSamples.Detached, RawStringLayoutSamples.Config);
	}

	[Test]
	[Arguments("\n", "crlf")]
	[Arguments("\r\n", "lf")]
	public void Literal_line_endings_survive_a_different_file_line_ending(string literalEnding, string fileEnding)
	{
		var source = RawStringLayoutSamples.Attached.ReplaceLineEndings(literalEnding);
		var options = TestOptions.Parse(RawStringLayoutSamples.Config + "\nend_of_line = " + fileEnding);
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		AssertTokens(source, first.Text!);
		var token = CSharpSyntaxTree.ParseText(first.Text!).GetRoot().DescendantTokens()
			.Single(token => token.RawKind == (int)SyntaxKind.MultiLineRawStringLiteralToken);
		token.Text.Should().Contain("content" + literalEnding);
		formatter.Format(first.Text!, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	public void Closing_tabs_are_used_without_reindenting_literal_bytes()
	{
		var source = RawStringLayoutSamples.Attached.Replace("            content", "\t\t\tcontent", StringComparison.Ordinal)
			.Replace("            \"\"\";", "\t\t\t\"\"\";", StringComparison.Ordinal);
		var expected = RawStringLayoutSamples.Detached.Replace("            \"\"\"", "\t\t\t\"\"\"", StringComparison.Ordinal)
			.Replace("            content", "\t\t\tcontent", StringComparison.Ordinal);
		Check(source, expected, RawStringLayoutSamples.Config);
	}

	[Test]
	public void Ordinary_and_single_line_literals_keep_the_default_rendering()
	{
		const string source = "class C { string A = \"hello\\nworld\"; string B = @\"C:\\temp\"; string C1 = \"\"\"hello\"\"\"; string D = $\"hello {name}\"; string E = $$\"\"\"hello {{name}}\"\"\"; string F = @\"first\nsecond\"; }";
		var options = TestOptions.Parse(RawStringLayoutSamples.Config);
		using var formatter = new CSharpFormatter();
		var expected = formatter.Format(source, options);
		var selected = formatter.Format(source, options, layoutRules: Rules());
		selected.Success.Should().BeTrue(selected.Message);
		selected.Text.Should().Be(expected.Text);
		selected.LayoutApplications.Should().BeEmpty();
	}

	[Test]
	public void Disabling_the_rule_retains_the_existing_literal_path()
	{
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(RawStringLayoutSamples.Attached, TestOptions.Parse(RawStringLayoutSamples.Config));
		result.Success.Should().BeTrue(result.Message);
		result.Text.Should().Contain("var value = \"\"\"");
		result.LayoutApplications.Should().BeEmpty();
	}

	private static void Check(string source, string expected, string configuration)
	{
		var options = TestOptions.Parse(configuration);
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text!.TrimEnd('\r', '\n').Should().Be(expected);
		first.LayoutApplications.Should().ContainSingle().Which.Recipe.Should().Be("standalone-raw-string");
		formatter.RoundTripsChecked.Should().Be(1);
		AssertTokens(source, first.Text);
		var second = formatter.Format(first.Text, options, layoutRules: Rules());
		second.Success.Should().BeTrue(second.Message);
		second.Changed.Should().BeFalse();
		second.Text.Should().Be(first.Text);
	}

	private static void AssertTokens(string source, string output)
	{
		var before = CSharpSyntaxTree.ParseText(source).GetRoot().DescendantTokens().Select(token => (token.RawKind, token.Text, token.ValueText));
		var after = CSharpSyntaxTree.ParseText(output).GetRoot().DescendantTokens().Select(token => (token.RawKind, token.Text, token.ValueText));
		after.Should().Equal(before);
	}
}
