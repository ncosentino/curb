using AwesomeAssertions;
using Microsoft.CodeAnalysis.Text;
using Nullean.Curb.Cleanup;
using Nullean.Curb.Verification;

namespace Nullean.Curb.Tests.Cleanup;

public class ClarityParenthesesTests
{
	[Test]
	public void Multiple_operator_diagnostics_wrap_the_complete_chain_once()
	{
		const string source = "class C { int M(int a, int b, int c, int d) => a + b * c * d; }";
		var first = source.IndexOf('*');
		var second = source.LastIndexOf('*');
		var result = Clean(source, Diagnostic(source, first), Diagnostic(source, second), Diagnostic(source, first));

		result.Success.Should().BeTrue(result.Message);
		result.Text.Should().Be("class C { int M(int a, int b, int c, int d) => a + (b * c * d); }");
		result.Applied.Should().Be(3);
		result.Unfixed.Should().BeEmpty();
	}

	[Test]
	public void Logical_precedence_keeps_its_original_association()
	{
		const string source = "class C { bool M(bool a, bool b, bool c) => a || b && c; }";
		var result = Clean(source, Diagnostic(source, source.IndexOf("&&", StringComparison.Ordinal), 2));
		result.Success.Should().BeTrue(result.Message);
		result.Text.Should().Be("class C { bool M(bool a, bool b, bool c) => a || (b && c); }");
	}

	[Test]
	public void Both_relational_operands_can_be_wrapped()
	{
		const string source = "class C { bool M(int a, int b, int c, int d) => a < b == c > d; }";
		var result = Clean(source, Diagnostic(source, source.IndexOf('<')), Diagnostic(source, source.LastIndexOf('>')));
		result.Success.Should().BeTrue(result.Message);
		result.Text.Should().Be("class C { bool M(int a, int b, int c, int d) => (a < b) == (c > d); }");
	}

	[Test]
	public void Coalescing_keeps_the_arithmetic_operand_together()
	{
		const string source = "class C { int? M(int? a, int? b, int? c) => a + b ?? c; }";
		var result = Clean(source, Diagnostic(source, source.IndexOf('+')));
		result.Success.Should().BeTrue(result.Message);
		result.Text.Should().Be("class C { int? M(int? a, int? b, int? c) => (a + b) ?? c; }");
	}

	[Test]
	public void Comments_and_nested_expressions_are_preserved()
	{
		const string source = "class C { int M(int a, int b, int c) => a + b /* keep */ * (c - 1); }";
		var result = Clean(source, Diagnostic(source, source.IndexOf('*', source.IndexOf("*/", StringComparison.Ordinal) + 2)));
		result.Success.Should().BeTrue(result.Message);
		result.Text.Should().Be("class C { int M(int a, int b, int c) => a + (b /* keep */ * (c - 1)); }");
	}

	[Test]
	public void Already_parenthesized_and_wrong_locations_are_refused()
	{
		const string source = "class C { int M(int a, int b, int c) => a + (b * c); }";
		var result = Clean(source, Diagnostic(source, source.IndexOf('*')));
		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle();
		var wrong = Clean(source, Diagnostic(source, 0));
		wrong.Changed.Should().BeFalse();
		wrong.Refusals.Should().ContainSingle();
	}

	[Test]
	public void Conditional_configurations_are_not_guessed()
	{
		const string source = "#if FEATURE\n#endif\nclass C { int M(int a, int b, int c) => a + b * c; }";
		var result = Clean(source, Diagnostic(source, source.IndexOf('*')));
		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle();
	}

	[Test]
	public void Inserted_parentheses_do_not_excuse_unrelated_token_changes()
	{
		const string source = "class C { int M(int a, int b, int c) => a + b * c; }";
		const string output = "class C { int M(int a, int b, int c) => a + (b * c); }";
		var start = output.IndexOf("(b", StringComparison.Ordinal);
		InsertedToken[] inserted = [new(start, "("), new(start + 6, ")")];
		CSharpSource.TryParse(source, out var parsed, out _).Should().BeTrue();
		ContentVerifier.Verify(source, output, out _, inserted: inserted).Should().BeTrue();
		TokenStreamComparer.Verify(parsed.Root, source, output, output, out _, inserted: inserted).Should().BeTrue();
		var wrong = output.Replace("b * c", "b + c", StringComparison.Ordinal);
		ContentVerifier.Verify(source, wrong, out _, inserted: inserted).Should().BeFalse();
		TokenStreamComparer.Verify(parsed.Root, source, wrong, wrong, out _, inserted: inserted).Should().BeFalse();
		ContentVerifier.Verify(source, output, out _, inserted: [inserted[0]]).Should().BeFalse();
		TokenStreamComparer.Verify(parsed.Root, source, output, output, out _, inserted: [inserted[0]]).Should().BeFalse();
		ContentVerifier.Verify(source, output, out _, inserted: [new(start + 1, "("), inserted[1]]).Should().BeFalse();
		TokenStreamComparer.Verify(parsed.Root, source, output, output, out _, inserted: [new(start + 1, "("), inserted[1]]).Should().BeFalse();
		var extra = output.Insert(start, "(");
		ContentVerifier.Verify(source, extra, out _, inserted: inserted).Should().BeFalse();
		TokenStreamComparer.Verify(parsed.Root, source, extra, extra, out _, inserted: inserted).Should().BeFalse();
	}

	private static CleanupDiagnostic Diagnostic(string source, int position, int length = 1)
	{
		var text = SourceText.From(source);
		return new CleanupDiagnostic("IDE0048", "/repo/Case.cs", text.Lines.GetLinePosition(position), text.Lines.GetLinePosition(position + length));
	}

	private static CleanupResult Clean(string source, params CleanupDiagnostic[] diagnostics)
	{
		var result = new CSharpCleaner().Clean(source, diagnostics);
		CleanupExpectationDump.Record(result, diagnostics.Select(diagnostic => diagnostic.RuleId));
		return result;
	}
}
