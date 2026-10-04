using System.Text;
using AwesomeAssertions;
using Nullean.Curb.Cli;
using Nullean.Curb.LayoutRules;
using Nullean.Curb.Tests.Formatting;

namespace Nullean.Curb.Tests.LayoutRules;

public class LogicalLambdaLayoutTests
{
	private static LayoutRuleSet Rules() =>
		new(LayoutRuleCompiler.Compile(Encoding.UTF8.GetBytes(LogicalLambdaLayoutSamples.Policy)).Select(definition => definition.Rule));

	[Test]
	public void The_issue_shape_keeps_the_call_and_lambda_header_inline()
	{
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LogicalLambdaLayoutSamples.Config);
		var first = formatter.Format(LogicalLambdaLayoutSamples.Source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text!.TrimEnd().Should().Be(LogicalLambdaLayoutSamples.Expected);
		first.LayoutApplications.Should().ContainSingle().Which.Recipe.Should().Be("hanging-logical-lambda");
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	[Arguments("entry =>")]
	[Arguments("(entry) =>")]
	public void Fitting_predicates_remain_inline(string header)
	{
		var source = $"class C {{ bool M() {{ return entries.Any({header} entry.Enabled && entry.Ready); }} }}";
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LogicalLambdaLayoutSamples.Config);
		var ordinary = formatter.Format(source, options);
		ordinary.Success.Should().BeTrue(ordinary.Message);
		var selected = formatter.Format(source, options, layoutRules: Rules());
		selected.Success.Should().BeTrue(selected.Message);
		selected.Text.Should().Be(ordinary.Text);
	}

	[Test]
	public void Author_wrapping_does_not_change_the_canonical_layout()
	{
		var source = LogicalLambdaLayoutSamples.Source.Replace("Any(entry => ", "Any(\nentry =>\n", StringComparison.Ordinal)
			.Replace(" && ", "\n&& ", StringComparison.Ordinal);
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(source, TestOptions.Parse(LogicalLambdaLayoutSamples.Config), layoutRules: Rules());
		result.Success.Should().BeTrue(result.Message);
		result.Text!.TrimEnd().Should().Be(LogicalLambdaLayoutSamples.Expected);
	}

	[Test]
	[Arguments(46, false)]
	[Arguments(45, true)]
	public void Width_counts_the_complete_inline_predicate(int width, bool broken)
	{
		const string source = "class C { bool M() { return xs.Any(x => x.A && x.B); } }";
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LogicalLambdaLayoutSamples.Config + $"\nmax_line_length = {width}");
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().Contain(broken ? "return xs.Any(x =>\n            x.A &&\n            x.B);" : "return xs.Any(x => x.A && x.B);");
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	[Arguments("max_line_length = off")]
	[Arguments("csharp_keep_existing_linebreaks = true")]
	[Arguments("csharp_space_around_binary_operators = ignore")]
	public void Unsupported_modes_fail_without_output(string setting)
	{
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(LogicalLambdaLayoutSamples.Source,
			TestOptions.Parse(LogicalLambdaLayoutSamples.Config + "\n" + setting), layoutRules: Rules());
		result.Status.Should().Be(FormatStatus.VerificationFailed);
		result.Text.Should().BeNull();
	}

	[Test]
	[Arguments("entry =>", "entry /* seam */ =>")]
	[Arguments("entry =>", "entry => /* seam */")]
	[Arguments(" && ", " /* seam */ && ")]
	[Arguments(" && ", "\n#if FLAG\n&& entry.Other\n#endif\n&& ")]
	public void Unsupported_boundary_trivia_fails_without_output(string before, string after)
	{
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(LogicalLambdaLayoutSamples.Source.Replace(before, after, StringComparison.Ordinal),
			TestOptions.Parse(LogicalLambdaLayoutSamples.Config), layoutRules: Rules());
		result.Status.Should().Be(FormatStatus.VerificationFailed);
		result.Text.Should().BeNull();
		result.Changed.Should().BeFalse();
	}

	[Test]
	[Arguments("\"sole-invocation-argument\"", "\"argument\"")]
	[Arguments("\"inline-if-fits\"", "\"always-break\"")]
	[Arguments("\"trailing\"", "\"leading\"")]
	[Arguments("\"with-final-operand\"", "\"own-line\"")]
	[Arguments("\"wrap\": \"if-long\",", "\"wrap\": \"if-long\", \"unknown\": true,")]
	public void Unsupported_schema_values_are_rejected(string before, string after)
	{
		Action compile = () => LayoutRuleCompiler.Compile(Encoding.UTF8.GetBytes(
			LogicalLambdaLayoutSamples.Policy.Replace(before, after, StringComparison.Ordinal)));
		compile.Should().Throw<LayoutRuleConfigurationException>();
	}

	[Test]
	public void No_rule_leaves_ordinary_formatting_in_charge()
	{
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LogicalLambdaLayoutSamples.Config);
		var ordinary = formatter.Format(LogicalLambdaLayoutSamples.Source, options);
		ordinary.Success.Should().BeTrue(ordinary.Message);
		ordinary.LayoutApplications.Should().BeEmpty();
		formatter.Format(ordinary.Text, options).Text.Should().Be(ordinary.Text);
	}
}
