using System.Text;
using AwesomeAssertions;
using Nullean.Curb.Cli;
using Nullean.Curb.LayoutRules;
using Nullean.Curb.Tests.Formatting;

namespace Nullean.Curb.Tests.LayoutRules;

public class ExpressionLambdaLayoutTests
{
	private static LayoutRuleSet Rules() =>
		new(LayoutRuleCompiler.Compile(Encoding.UTF8.GetBytes(ExpressionLambdaLayoutSamples.Policy)).Select(definition => definition.Rule));

	private static void AssertLayout(string source, string expected, string? settings = null, LayoutRuleSet? rules = null)
	{
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(ExpressionLambdaLayoutSamples.Config + "\n" + settings);
		rules ??= Rules();
		var first = formatter.Format(source, options, verifyRoundTrip: true, forceRoundTrip: true, layoutRules: rules);
		first.Success.Should().BeTrue(first.Message);
		first.Text!.TrimEnd('\r', '\n').Should().Be(expected);
		var second = formatter.Format(first.Text, options, verifyRoundTrip: true, forceRoundTrip: true, layoutRules: rules);
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(first.Text);
	}

	[Test]
	public void The_schema_compiles_to_a_typed_rule()
	{
		var definitions = LayoutRuleCompiler.Compile(Encoding.UTF8.GetBytes(ExpressionLambdaLayoutSamples.Policy));
		definitions.Should().ContainSingle().Which.Rule.Should().BeOfType<ExpressionLambdaLayoutRule>();
	}

	[Test]
	public void Pattern_predicates_keep_the_call_attached_to_the_if_header() =>
		AssertLayout(ExpressionLambdaLayoutSamples.PatternSource, ExpressionLambdaLayoutSamples.PatternExpected);

	[Test]
	public void Projections_keep_the_constructor_introducer_attached() =>
		AssertLayout(ExpressionLambdaLayoutSamples.ProjectionSource, ExpressionLambdaLayoutSamples.ProjectionExpected);

	[Test]
	public void Already_expanded_input_converges_to_the_same_pattern_layout() =>
		AssertLayout(ExpressionLambdaLayoutSamples.PatternSource.Replace(
			"if (entries.Any(entry => ", "if (\nentries.Any(\nentry =>\n", StringComparison.Ordinal)
			.Replace("NotApplicable)))", "NotApplicable)\n)\n)", StringComparison.Ordinal),
			ExpressionLambdaLayoutSamples.PatternExpected);

	[Test]
	[Arguments("entry =>")]
	[Arguments("(entry) =>")]
	public void Fitting_callbacks_remain_inline(string header)
	{
		var source = $"class C {{ object M() {{ return entries.Select({header} entry.Id); }} }}";
		var expected = $$"""
			class C
			{
			    object M()
			    {
			        return entries.Select({{header}} entry.Id);
			    }
			}
			""";
		AssertLayout(source, expected);
	}

	[Test]
	[Arguments(32, false)]
	[Arguments(31, true)]
	public void The_width_boundary_includes_the_statement_semicolon(int width, bool broken)
	{
		var expected = broken ? """
			class C
			{
			    object M()
			    {
			        return xs.Any(x =>
			            x.A);
			    }
			}
			""" : """
			class C
			{
			    object M()
			    {
			        return xs.Any(x => x.A);
			    }
			}
			""";
		AssertLayout("class C { object M() { return xs.Any(x => x.A); } }", expected, $"max_line_length = {width}");
	}

	[Test]
	public void Nonmatching_arguments_and_logical_callbacks_retain_their_existing_layout()
	{
		const string source = "class C { void M() { Call(x => x.A, other); Call(x => { return x.A; }); Call(x => x.A && x.B); } }";
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(ExpressionLambdaLayoutSamples.Config);
		var ordinary = formatter.Format(source, options);
		ordinary.Success.Should().BeTrue(ordinary.Message);
		var selected = formatter.Format(source, options, layoutRules: Rules());
		selected.Success.Should().BeTrue(selected.Message);
		selected.Text.Should().Be(ordinary.Text);
		selected.LayoutApplications.Should().BeEmpty();
	}

	[Test]
	public void Ordinary_declaration_parameters_still_obey_forced_chopping()
	{
		const string source = "class C { object M(int first, int second) { return entries.Select((entry, index) => entry.Id); } }";
		const string expected = """
			class C
			{
			    object M(
			        int first,
			        int second
			    )
			    {
			        return entries.Select((entry, index) => entry.Id);
			    }
			}
			""";
		AssertLayout(source, expected);
	}

	[Test]
	public void Absence_of_the_recipe_preserves_the_default_path()
	{
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(ExpressionLambdaLayoutSamples.Config);
		var ordinary = formatter.Format(ExpressionLambdaLayoutSamples.PatternSource, options);
		ordinary.Success.Should().BeTrue(ordinary.Message);
		ordinary.LayoutApplications.Should().BeEmpty();
		var second = formatter.Format(ordinary.Text, options);
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(ordinary.Text);
	}
}
