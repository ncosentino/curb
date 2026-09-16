using AwesomeAssertions;
using Microsoft.CodeAnalysis.Text;
using Nullean.Curb.Cleanup;
using Nullean.Curb.Verification;

namespace Nullean.Curb.Tests.Cleanup;

public class ParenthesesRemovalTests
{
	[Test]
	[Arguments("a + (b * c)", "a + b * c")]
	[Arguments("((a))", "a")]
	[Arguments("a + ((b + c))", "a + (b + c)")]
	[Arguments("a + (/* keep */ b * c)", "a + /* keep */ b * c")]
	public void Removes_only_syntax_preserving_parentheses(string expression, string expected)
	{
		var source = Wrap(expression);
		var start = source.IndexOf(expression, StringComparison.Ordinal) + expression.IndexOf('(');
		var result = Clean(source, Diagnostic(source, start));

		result.Success.Should().BeTrue(result.Message);
		result.Text.Should().Be(Wrap(expected));
		result.Applied.Should().Be(1);
		result.Refusals.Should().BeEmpty();
	}

	[Test]
	public void A_multiline_primary_span_can_cover_only_the_opening_token()
	{
		const string source = "class C { int M(int a, int b) => (\n a + b\n); }";
		var start = source.IndexOf("(\n", StringComparison.Ordinal);
		var result = Clean(source, Diagnostic(source, start, start + 1));
		result.Success.Should().BeTrue(result.Message);
		result.Text.Should().Be("class C { int M(int a, int b) => \n a + b\n; }");
	}

	[Test]
	public void Nested_and_duplicate_diagnostics_share_one_complete_plan()
	{
		var source = Wrap("((a))");
		var start = source.IndexOf("((a))", StringComparison.Ordinal);
		var result = Clean(source, Diagnostic(source, start), Diagnostic(source, start + 1), Diagnostic(source, start));

		result.Success.Should().BeTrue(result.Message);
		result.Text.Should().Be(Wrap("a"));
		result.Applied.Should().Be(3);
		result.Unfixed.Should().BeEmpty();
	}

	[Test]
	[Arguments("(a + b) * c")]
	[Arguments("a + (b + c)")]
	[Arguments("(a?.Length).ToString()")]
	[Arguments("(stackalloc int[2])")]
	public void Refuses_parentheses_that_change_association_or_binding(string expression)
	{
		var source = Wrap(expression);
		var start = source.IndexOf(expression, StringComparison.Ordinal) + expression.IndexOf('(');
		var result = Clean(source, Diagnostic(source, start));

		result.Success.Should().BeTrue();
		result.Changed.Should().BeFalse();
		result.Applied.Should().Be(0);
		result.Refusals.Should().ContainSingle();
	}

	[Test]
	public void Does_not_change_inferred_tuple_names()
	{
		const string source = "class C { object M(int a, int b) => ((a), b); }";
		var start = source.IndexOf("(a)", StringComparison.Ordinal);
		var result = Clean(source, Diagnostic(source, start));
		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle();
	}

	[Test]
	public void Refuses_wrong_locations_and_conditional_build_verdicts()
	{
		var source = Wrap("(a)");
		var wrong = Clean(source, Diagnostic(source, source.IndexOf("=>", StringComparison.Ordinal)));
		wrong.Changed.Should().BeFalse();
		wrong.Refusals.Should().ContainSingle();
		var conditional = "#if FEATURE\n#endif\n" + source;
		var refused = Clean(conditional, Diagnostic(conditional, conditional.IndexOf("(a)", StringComparison.Ordinal)));
		refused.Changed.Should().BeFalse();
		refused.Refusals.Should().ContainSingle();
	}

	[Test]
	public void A_shared_plan_abandoned_for_overlap_abandons_its_duplicates()
	{
		const string source = "using A;\nusing B;\nclass C { }";
		var first = Diagnostic(source, 0, source.IndexOf("class", StringComparison.Ordinal), "IDE0005");
		var second = Diagnostic(source, source.IndexOf("using B", StringComparison.Ordinal), source.IndexOf("class", StringComparison.Ordinal), "IDE0005");
		var result = new CSharpCleaner().Clean(source, [first, first, second]);
		result.Success.Should().BeTrue(result.Message);
		result.Changed.Should().BeFalse();
		result.Applied.Should().Be(0);
		result.Unfixed.Should().HaveCount(3);
	}

	[Test]
	public void Declared_parenthesis_deletions_do_not_excuse_other_token_changes()
	{
		var source = Wrap("(a)");
		var start = source.IndexOf("(a)", StringComparison.Ordinal);
		TextSpan[] dropped = [new(start, 1), new(start + 2, 1)];
		CSharpSource.TryParse(source, out var parsed, out _).Should().BeTrue();
		var correct = Wrap("a");
		ContentVerifier.Verify(source, correct, out _, dropped: dropped).Should().BeTrue();
		TokenStreamComparer.Verify(parsed.Root, source, correct, correct, out _, dropped: dropped).Should().BeTrue();
		var wrong = Wrap("b");
		ContentVerifier.Verify(source, wrong, out _, dropped: dropped).Should().BeFalse();
		TokenStreamComparer.Verify(parsed.Root, source, wrong, wrong, out _, dropped: dropped).Should().BeFalse();
	}

	private static string Wrap(string expression) => $"class C {{ int M(int a, int b, int c) => {expression}; }}";

	private static CleanupDiagnostic Diagnostic(string source, int start, int? end = null, string rule = "IDE0047")
	{
		var text = SourceText.From(source);
		return new CleanupDiagnostic(rule, "/repo/Case.cs", text.Lines.GetLinePosition(start),
			end is { } finish ? text.Lines.GetLinePosition(finish) : null);
	}

	private static CleanupResult Clean(string source, params CleanupDiagnostic[] diagnostics)
	{
		var result = new CSharpCleaner().Clean(source, diagnostics);
		CleanupExpectationDump.Record(result, diagnostics.Select(diagnostic => diagnostic.RuleId));
		return result;
	}
}
