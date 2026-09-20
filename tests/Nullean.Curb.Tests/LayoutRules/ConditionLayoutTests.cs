using AwesomeAssertions;
using Nullean.Curb.LayoutRules;
using Nullean.Curb.Tests.Formatting;

namespace Nullean.Curb.Tests.LayoutRules;

public class ConditionLayoutTests
{
	private static LayoutRuleSet Rules() => new([new LogicalConditionLayoutRule("logical-headers")]);

	[Test]
	[Arguments("chop_if_long")]
	[Arguments("chop_always")]
	[Arguments("wrap_if_long")]
	public void The_reported_condition_has_independent_delimiters_and_intact_comparisons(string generalStyle)
	{
		var options = TestOptions.Parse(ConditionLayoutSamples.Config + $"\ncsharp_wrap_chained_binary_expressions = {generalStyle}");
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(ConditionLayoutSamples.Source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text!.TrimEnd().Should().Be(ConditionLayoutSamples.Expected);
		first.LayoutApplications.Should().ContainSingle().Which.Recipe.Should().Be("hanging-logical-condition");
		formatter.RoundTripsChecked.Should().Be(1);
		var second = formatter.Format(first.Text, options, layoutRules: Rules());
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(first.Text);
	}

	[Test]
	[Arguments(28, false)]
	[Arguments(27, true)]
	public void Width_measurement_includes_the_prefix_and_closing_parenthesis(int width, bool broken)
	{
		const string source = "class C\n{\n    void M()\n    {\n        if (first && second)\n        {\n            Call();\n        }\n    }\n}";
		var options = TestOptions.Parse(ConditionLayoutSamples.Config + $"\nmax_line_length = {width}\ncsharp_wrap_chained_binary_expressions = chop_always");
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(source, options, layoutRules: Rules());
		result.Success.Should().BeTrue(result.Message);
		result.Text.Should().Contain(broken ? "        if (first &&\n            second\n        )" : "        if (first && second)");
		formatter.Format(result.Text, options, layoutRules: Rules()).Text.Should().Be(result.Text);
	}

	[Test]
	public void Differently_wrapped_sources_converge()
	{
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(ConditionLayoutSamples.Config);
		var source = ConditionLayoutSamples.Source.Replace("if (candidate", "if (\n                candidate", StringComparison.Ordinal)
			.Replace(" && ", "\n&& ", StringComparison.Ordinal);
		var result = formatter.Format(source, options, layoutRules: Rules());
		result.Success.Should().BeTrue(result.Message);
		result.Text!.TrimEnd().Should().Be(ConditionLayoutSamples.Expected);
	}

	[Test]
	public void Other_headers_and_nonlogical_conditions_keep_default_behavior()
	{
		const string source = "class C { void M() { while (first && second) { Call(); } if (number == expected) { Call(); } } }";
		var options = TestOptions.Parse(ConditionLayoutSamples.Config);
		using var formatter = new CSharpFormatter();
		var ordinary = formatter.Format(source, options);
		var selected = formatter.Format(source, options, layoutRules: Rules());
		selected.Success.Should().BeTrue(selected.Message);
		selected.Text.Should().Be(ordinary.Text);
		selected.LayoutApplications.Should().BeEmpty();
	}

	[Test]
	public void Conflicting_condition_rules_refuse_without_output()
	{
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(ConditionLayoutSamples.Source, TestOptions.Parse(ConditionLayoutSamples.Config),
			layoutRules: new LayoutRuleSet([new LogicalConditionLayoutRule("one"), new LogicalConditionLayoutRule("two")]));
		result.Status.Should().Be(FormatStatus.VerificationFailed);
		result.Text.Should().BeNull();
		result.Message.Should().Contain("same condition header");
	}

	[Test]
	[Arguments("max_line_length = off")]
	[Arguments("csharp_keep_existing_linebreaks = true")]
	[Arguments("csharp_space_around_binary_operators = ignore")]
	public void Unsupported_modes_are_explicit_failures(string setting)
	{
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(ConditionLayoutSamples.Source, TestOptions.Parse(ConditionLayoutSamples.Config + "\n" + setting), layoutRules: Rules());
		result.Status.Should().Be(FormatStatus.VerificationFailed);
		result.Text.Should().BeNull();
	}

	[Test]
	[Arguments(false, "\n")]
	[Arguments(true, "\n")]
	[Arguments(false, "\r\n")]
	[Arguments(true, "\r\n")]
	public void Alignment_uses_output_columns_with_tabs_and_both_line_endings(bool tabs, string ending)
	{
		var options = TestOptions.Parse(ConditionLayoutSamples.Config + $"\nindent_style = {(tabs ? "tab" : "space")}\nend_of_line = {(ending == "\n" ? "lf" : "crlf")}");
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(ConditionLayoutSamples.Source.ReplaceLineEndings(ending), options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		var expected = (tabs ? ConditionLayoutSamples.Expected.Replace("    ", "\t", StringComparison.Ordinal) : ConditionLayoutSamples.Expected).ReplaceLineEndings(ending);
		first.Text!.TrimEnd().Should().Be(expected);
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	public void Explicit_grouping_and_mixed_operators_are_not_flattened_into_a_different_expression()
	{
		const string source = """
			class C
			{
			    bool M()
			    {
			        if (firstComparison == RequiredFirstValue && (secondComparison == RequiredSecondValue || thirdComparison == RequiredThirdValue) && finalComparison != RejectedFinalValue)
			        {
			            return true;
			        }
			        return false;
			    }
			}
			""";
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(ConditionLayoutSamples.Config);
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().Contain("if (firstComparison == RequiredFirstValue &&\n            (secondComparison == RequiredSecondValue || thirdComparison == RequiredThirdValue) &&\n            finalComparison != RejectedFinalValue\n        )");
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	public void An_oversized_operand_uses_internal_wrapping_without_changing_tokens()
	{
		const string source = """
			class C
			{
			    void M()
			    {
			        if (VeryLongFunctionName(firstLongArgument, secondLongArgument, thirdLongArgument) == expectedValue && otherCondition)
			        {
			            Call();
			        }
			    }
			}
			""";
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(ConditionLayoutSamples.Config + "\nmax_line_length = 60");
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().Contain("if (VeryLongFunctionName(");
		first.Text.Should().Contain("\n            otherCondition\n        )");
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	public void Nested_condition_headers_cannot_overwrite_the_outer_alignment()
	{
		const string source = """
			class C
			{
			    void M()
			    {
			        if (Evaluate(() =>
			        {
			            if (innerLeft == RequiredInnerLeft && innerRight == RequiredInnerRight && innerFinal == RequiredInnerFinal)
			            {
			                return true;
			            }
			            return false;
			        }) && outerFinal == RequiredOuterFinal)
			        {
			            Call();
			        }
			    }
			}
			""";
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(ConditionLayoutSamples.Config + "\nmax_line_length = 70");
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.LayoutApplications.Count.Should().Be(2);
		first.Text.Should().Contain("\n            outerFinal == RequiredOuterFinal\n        )");
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	public void Wrapper_and_condition_rules_compose_in_the_delegated_body()
	{
		var source = LayoutRuleSamples.Source.Replace("var value=new Value(number);", "if (number == RequiredCategoryIdentifier && number != ExcludedSegmentIdentifier && ct.IsCancellationRequested) { return new Value(number); } var value=new Value(number);", StringComparison.Ordinal);
		var rules = new LayoutRuleSet([new WrapperLayoutRule("wrappers", ["TraceScope.RunAsync", "Outcome.CaptureAsync"]), new LogicalConditionLayoutRule("conditions")]);
		var options = TestOptions.Parse(LayoutRuleSamples.Config + "\nmax_line_length = 85");
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(source, options, layoutRules: rules);
		first.Success.Should().BeTrue(first.Message);
		first.LayoutApplications.Count.Should().Be(2);
		first.Text.Should().Contain("        if (number == RequiredCategoryIdentifier &&\n            number != ExcludedSegmentIdentifier &&\n            ct.IsCancellationRequested\n        )");
		formatter.Format(first.Text, options, layoutRules: rules).Text.Should().Be(first.Text);
	}

	[Test]
	public void Boundary_comments_and_directives_fail_closed()
	{
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(ConditionLayoutSamples.Config);
		foreach (var source in new[]
		{
			ConditionLayoutSamples.Source.Replace(" && ", " // before operator\n&& ", StringComparison.Ordinal),
			ConditionLayoutSamples.Source.Replace(" && ", "\n#if FLAG\n&& extra\n#endif\n&& ", StringComparison.Ordinal),
		})
		{
			var result = formatter.Format(source, options, layoutRules: Rules());
			result.Status.Should().Be(FormatStatus.VerificationFailed);
			result.Text.Should().BeNull();
			result.Changed.Should().BeFalse();
		}
	}

	[Test]
	public void Interior_operand_comments_still_use_the_normal_trivia_printer()
	{
		var source = ConditionLayoutSamples.Source.Replace("candidate.CategoryId ==", "candidate.CategoryId /* category */ ==", StringComparison.Ordinal);
		var options = TestOptions.Parse(ConditionLayoutSamples.Config);
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().Contain("/* category */");
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	public void Many_headers_allocate_distinct_anchors_and_reset_for_the_next_file()
	{
		var conditions = string.Join("\n", Enumerable.Repeat("if (firstVeryLongCondition && secondVeryLongCondition) { Call(); }", 20));
		var source = "class C { void M() {\n" + conditions + "\n} }";
		var options = TestOptions.Parse(ConditionLayoutSamples.Config + "\nmax_line_length = 40");
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.LayoutApplications.Count.Should().Be(20);
		first.Text!.Split("\n            secondVeryLongCondition\n        )", StringSplitOptions.None).Length.Should().Be(21);
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
		var ordinary = formatter.Format("class C { }", options);
		ordinary.Success.Should().BeTrue(ordinary.Message);
		ordinary.LayoutApplications.Should().BeEmpty();
	}

	[Test]
	public void Else_if_continuations_follow_the_real_first_operand_column()
	{
		const string source = "class C { void M() { if (ok) { Call(); } else if (firstLongCondition && secondLongCondition && thirdLongCondition) { Call(); } } }";
		var options = TestOptions.Parse(ConditionLayoutSamples.Config + "\nmax_line_length = 55");
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().Contain("else if (firstLongCondition &&\n                 secondLongCondition &&\n                 thirdLongCondition\n        )");
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	public void Logical_or_uses_the_same_header_contract()
	{
		var source = ConditionLayoutSamples.Source.Replace("&&", "||", StringComparison.Ordinal);
		var expected = ConditionLayoutSamples.Expected.Replace("&&", "||", StringComparison.Ordinal);
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(ConditionLayoutSamples.Config);
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text!.TrimEnd().Should().Be(expected);
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	public void Configured_keyword_and_operator_spacing_is_not_replaced_by_alignment()
	{
		var options = TestOptions.Parse(ConditionLayoutSamples.Config + "\ncsharp_space_after_keywords_in_control_flow_statements = false\ncsharp_space_around_binary_operators = none");
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(ConditionLayoutSamples.Source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().Contain("        if(candidate.CategoryId==RequiredCategoryIdentifier&&\n           candidate.SegmentId==RequiredSegmentIdentifier&&\n           candidate.State==\"Ready\"\n        )");
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	public void Excessive_logical_chains_fail_with_a_bounded_refusal()
	{
		var source = "class C { void M() { if (" + string.Join(" && ", Enumerable.Repeat("value", 257)) + ") { Call(); } } }";
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(source, TestOptions.Parse(ConditionLayoutSamples.Config), layoutRules: Rules());
		result.Status.Should().Be(FormatStatus.VerificationFailed);
		result.Text.Should().BeNull();
		result.Message.Should().Contain("budget");
	}

	[Test]
	public void The_selected_recipe_overrides_leading_logical_operator_preferences()
	{
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(ConditionLayoutSamples.Config + "\ncsharp_wrap_before_binary_opsign = true");
		var result = formatter.Format(ConditionLayoutSamples.Source, options, layoutRules: Rules());
		result.Success.Should().BeTrue(result.Message);
		result.Text!.TrimEnd().Should().Be(ConditionLayoutSamples.Expected);
	}

	[Test]
	[Arguments(" /* reason */ ")]
	[Arguments(" // reason\n")]
	public void Comments_after_logical_operators_are_preserved_without_extra_spacing(string trivia)
	{
		var source = ConditionLayoutSamples.Source.Replace("&& ", "&&" + trivia, StringComparison.Ordinal);
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(ConditionLayoutSamples.Config);
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().Contain(trivia.Trim());
		first.Text.Should().Contain("\n            candidate.SegmentId");
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}
}
