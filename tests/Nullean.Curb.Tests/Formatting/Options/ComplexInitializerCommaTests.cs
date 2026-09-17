using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nullean.Curb.Tests.Formatting.Options;

public class ComplexInitializerCommaTests : FormattingTest
{
	private const string Source = """
        using System.Collections.Generic;

        namespace FormatterRepro;

        public static class Sample
        {
            public static Dictionary<string, string> Values => new()
            {
                { "key", "This value is intentionally long enough to wrap onto another line." }
            };
        }
        """;

	[Test]
	public Task Only_the_outer_collection_accepts_a_trailing_comma() => Formats(
		Source,
		"""
        using System.Collections.Generic;

        namespace FormatterRepro;

        public static class Sample
        {
            public static Dictionary<string, string> Values =>
                new()
                {
                    {
                        "key",
                        "This value is intentionally long enough to wrap onto another line."
                    },
                };
        }
        """,
		"max_line_length = 60\ncsharp_keep_existing_linebreaks = false\ncsharp_trailing_comma_in_multiline_lists = true");

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void Comma_policy_reparses_without_a_caller_opt_in(bool produceText)
	{
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse("csharp_trailing_comma_in_singleline_lists = true");
		var result = formatter.Format("class C { int[] Values = new[] { 1, 2 }; }", options, produceText: produceText);
		result.Success.Should().BeTrue(result.Message);
		formatter.RoundTripsChecked.Should().Be(1);
	}

	[Test]
	[Arguments(false, false)]
	[Arguments(false, true)]
	[Arguments(true, false)]
	[Arguments(true, true)]
	public void Both_layout_policies_keep_complex_elements_valid(bool multiline, bool singleline)
	{
		var options = TestOptions.Parse($"max_line_length = 60\ncsharp_keep_existing_linebreaks = false\ncsharp_trailing_comma_in_multiline_lists = {multiline}\ncsharp_trailing_comma_in_singleline_lists = {singleline}");
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(Source, options);
		first.Success.Should().BeTrue(first.Message);
		formatter.RoundTripsChecked.Should().Be(multiline || singleline ? 1 : 0);
		CSharpSource.TryParse(first.Text!, out var parsed, out _).Should().BeTrue();
		var complex = parsed.Root.DescendantNodes().OfType<InitializerExpressionSyntax>()
			.Single(node => node.IsKind(SyntaxKind.ComplexElementInitializerExpression));
		complex.Expressions.SeparatorCount.Should().Be(complex.Expressions.Count - 1);
		var outer = (InitializerExpressionSyntax)complex.Parent!;
		outer.Expressions.SeparatorCount.Should().Be(multiline ? outer.Expressions.Count : outer.Expressions.Count - 1);
		var second = formatter.Format(first.Text!, options);
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(first.Text);
	}

	[Test]
	public void Removing_a_comma_also_requires_a_reparse()
	{
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse("csharp_trailing_comma_in_singleline_lists = false\ncsharp_trailing_comma_in_multiline_lists = true");
		var result = formatter.Format("class C { int[] Values = new[] { 1, 2, }; }", options);
		result.Success.Should().BeTrue(result.Message);
		formatter.RoundTripsChecked.Should().Be(1);
		result.Text.Should().Contain("new[] { 1, 2 }");
	}

	[Test]
	public void Comma_risk_does_not_leak_to_the_next_file()
	{
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse("csharp_trailing_comma_in_singleline_lists = true");
		formatter.Format("class C { int[] Values = new[] { 1, 2 }; }", options).Success.Should().BeTrue();
		var checkedBefore = formatter.RoundTripsChecked;
		formatter.Format("class C { void M() { Call(1, 2); } }", options).Success.Should().BeTrue();
		formatter.RoundTripsChecked.Should().Be(checkedBefore);
	}

	[Test]
	public Task A_single_line_complex_element_never_receives_an_inner_comma() => Formats(
		"class C { object Values = new D { { 1, 2 } }; }",
		"class C { object Values = new D { { 1, 2 }, }; }",
		"csharp_trailing_comma_in_singleline_lists = true\ncsharp_trailing_comma_in_multiline_lists = true");

	[Test]
	[Arguments("class C { int[] Values = [1, 2]; }")]
	[Arguments("enum E { One, Two }")]
	[Arguments("class C { object Value = new { One = 1, Two = 2 }; }")]
	[Arguments("class C { int M(int x) => x switch { 1 => 1, _ => 0 }; }")]
	[Arguments("class C { bool M(int[] x) => x is [1, 2]; }")]
	public void Every_supported_list_rewrite_uses_syntax_verification(string source)
	{
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse("csharp_trailing_comma_in_singleline_lists = true\ncsharp_trailing_comma_in_multiline_lists = true");
		var first = formatter.Format(source, options);
		first.Success.Should().BeTrue(first.Message);
		formatter.RoundTripsChecked.Should().Be(1);
		CSharpSource.TryParse(first.Text!, out _, out _).Should().BeTrue();
	}
}
