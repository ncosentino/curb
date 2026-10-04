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
	public void The_schema_compiles_to_a_typed_rule()
	{
		var definitions = LayoutRuleCompiler.Compile(Encoding.UTF8.GetBytes(LogicalLambdaLayoutSamples.Policy));
		definitions.Should().ContainSingle().Which.Rule.Should().BeOfType<LogicalLambdaLayoutRule>();
	}

	[Test]
	public void The_issue_shape_keeps_the_call_and_lambda_header_inline()
	{
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LogicalLambdaLayoutSamples.Config);
		var first = formatter.Format(LogicalLambdaLayoutSamples.Source, options, verifyRoundTrip: true, forceRoundTrip: true, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text!.TrimEnd().Should().Be(LogicalLambdaLayoutSamples.Expected);
		first.LayoutApplications.Should().ContainSingle().Which.Recipe.Should().Be("hanging-logical-lambda");
		formatter.RoundTripsChecked.Should().Be(1);
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
	[Arguments(39, false)]
	[Arguments(38, true)]
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

	[Test]
	public void Short_operand_calls_remain_intact_when_the_outer_predicate_breaks()
	{
		const string source = "class C { bool M() { return entries.Any(entry => Check(entry, true) && HasRequiredPermission(entry) && IsAvailable(entry)); } }";
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LogicalLambdaLayoutSamples.Config + "\nmax_line_length = 65");
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().Contain("return entries.Any(entry =>\n            Check(entry, true) &&\n            HasRequiredPermission(entry) &&\n            IsAvailable(entry));");
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	public void Logical_if_headers_and_lambda_arguments_have_independent_layouts()
	{
		const string source = "class C { bool M() { if (entries.Any(entry => entry.IsEnabled && entry.HasRequiredPermission && entry.IsAvailable) && outerPermissionIsGranted) { return true; } return false; } }";
		var rules = new LayoutRuleSet(Rules().Rules.Concat([new LogicalConditionLayoutRule("conditions")]));
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LogicalLambdaLayoutSamples.Config + "\nmax_line_length = 70");
		var first = formatter.Format(source, options, layoutRules: rules);
		first.Success.Should().BeTrue(first.Message);
		first.LayoutApplications.Count.Should().Be(2);
		first.Text.Should().Contain("if (entries.Any(entry =>");
		first.Text.Should().Contain("\n            outerPermissionIsGranted\n        )");
		formatter.Format(first.Text, options, layoutRules: rules).Text.Should().Be(first.Text);
	}

	[Test]
	public void Only_an_oversized_header_breaks_after_the_call_opener()
	{
		const string source = "class C { bool M() { return exceptionallyLongCollectionName.Any(exceptionallyLongParameterName => exceptionallyLongParameterName.A && exceptionallyLongParameterName.B); } }";
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LogicalLambdaLayoutSamples.Config + "\nmax_line_length = 70");
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().Contain("return exceptionallyLongCollectionName.Any(\n            exceptionallyLongParameterName =>\n                exceptionallyLongParameterName.A &&\n                exceptionallyLongParameterName.B);");
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	public void Oversized_operands_delegate_internal_argument_wrapping()
	{
		const string source = "class C { bool M() { return entries.Any(entry => VeryLongFunctionName(firstLongArgument, secondLongArgument, thirdLongArgument) && entry.HasRequiredPermission); } }";
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LogicalLambdaLayoutSamples.Config + "\nmax_line_length = 60");
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().Contain("return entries.Any(entry =>\n            VeryLongFunctionName(");
		first.Text.Should().Contain("\n            entry.HasRequiredPermission);");
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	public void Multiple_arguments_block_bodies_and_nonlogical_lambdas_are_not_selected()
	{
		const string source = "class C { void M() { Call(x => x.A && x.B, other); Call(x => x.A); Call(x => { return x.A && x.B; }); } }";
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LogicalLambdaLayoutSamples.Config);
		var ordinary = formatter.Format(source, options);
		ordinary.Success.Should().BeTrue(ordinary.Message);
		var selected = formatter.Format(source, options, layoutRules: Rules());
		selected.Success.Should().BeTrue(selected.Message);
		selected.Text.Should().Be(ordinary.Text);
		selected.LayoutApplications.Should().BeEmpty();
	}

	[Test]
	public void Nested_lambda_roots_do_not_leak_layout_context()
	{
		const string source = "class C { bool M() { return entries.Any(entry => Evaluate(other, () => firstCondition && secondCondition) && entry.HasRequiredPermission && entry.IsAvailable); } }";
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LogicalLambdaLayoutSamples.Config + "\nmax_line_length = 85\ncsharp_wrap_chained_binary_expressions = chop_always");
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().Contain("Evaluate(other, () => firstCondition && secondCondition)");
		first.LayoutApplications.Should().ContainSingle();
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	public void Duplicate_rules_and_excessive_chains_fail_closed()
	{
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LogicalLambdaLayoutSamples.Config);
		var duplicate = formatter.Format(LogicalLambdaLayoutSamples.Source, options,
			layoutRules: new LayoutRuleSet([new LogicalLambdaLayoutRule("one"), new LogicalLambdaLayoutRule("two")]));
		duplicate.Status.Should().Be(FormatStatus.VerificationFailed);
		duplicate.Text.Should().BeNull();
		duplicate.Message.Should().Contain("same logical lambda");
		var source = "class C { bool M() { return entries.Any(entry => " + string.Join(" && ", Enumerable.Repeat("entry.Enabled", 257)) + "); } }";
		var bounded = formatter.Format(source, options, layoutRules: Rules());
		bounded.Status.Should().Be(FormatStatus.VerificationFailed);
		bounded.Text.Should().BeNull();
		bounded.Message.Should().Contain("budget");
	}

	[Test]
	public void Source_suppression_precedes_the_selected_recipe()
	{
		const string source = "class C\n{\n    bool M()\n    {\n#pragma warning disable IDE0055\n        return entries.Any(entry => entry.IsEnabled && entry.HasRequiredPermission);\n#pragma warning restore IDE0055\n    }\n}";
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LogicalLambdaLayoutSamples.Config);
		var ordinary = formatter.Format(source, options);
		ordinary.Success.Should().BeTrue(ordinary.Message);
		var selected = formatter.Format(source, options, layoutRules: Rules());
		selected.Success.Should().BeTrue(selected.Message);
		selected.Text.Should().Be(ordinary.Text);
		selected.LayoutApplications.Should().BeEmpty();
	}

	[Test]
	public void Wrapper_delegated_bodies_compose_with_logical_lambda_arguments()
	{
		var source = LayoutRuleSamples.Source.Replace("var value=new Value(number);",
			"var found = entries.Any(entry => entry.IsEnabled && entry.HasRequiredPermission && entry.IsAvailable); var value=new Value(number);", StringComparison.Ordinal);
		var rules = new LayoutRuleSet(Rules().Rules.Concat([new WrapperLayoutRule("wrappers", ["TraceScope.RunAsync", "Outcome.CaptureAsync"])]));
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LayoutRuleSamples.Config + "\nmax_line_length = 75");
		var first = formatter.Format(source, options, layoutRules: rules);
		first.Success.Should().BeTrue(first.Message);
		first.LayoutApplications.Count.Should().Be(2);
		first.Text.Should().Contain("entries.Any(entry =>\n            entry.IsEnabled &&");
		formatter.Format(first.Text, options, layoutRules: rules).Text.Should().Be(first.Text);
	}

	[Test]
	public void Logical_or_preserves_the_same_trailing_operator_contract()
	{
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LogicalLambdaLayoutSamples.Config);
		var first = formatter.Format(LogicalLambdaLayoutSamples.Source.Replace("&&", "||", StringComparison.Ordinal),
			options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text!.TrimEnd().Should().Be(LogicalLambdaLayoutSamples.Expected.Replace("&&", "||", StringComparison.Ordinal));
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	public void Interior_comments_and_comments_after_operators_use_normal_trivia_printing()
	{
		var source = LogicalLambdaLayoutSamples.Source.Replace("entry.IsEnabled", "entry /* interior */ .IsEnabled", StringComparison.Ordinal)
			.Replace("&& ", "&& /* reason */ ", StringComparison.Ordinal);
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LogicalLambdaLayoutSamples.Config);
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().Contain("/* interior */");
		first.Text.Should().Contain("/* reason */");
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	public void Raw_string_rules_compose_inside_operand_arguments()
	{
		const string source = """"
			class C
			{
			    bool M()
			    {
			        return entries.Any(entry => Check("""
			            value
			            """) && entry.HasRequiredPermission && entry.IsAvailable);
			    }
			}
			"""";
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LogicalLambdaLayoutSamples.Config);
		var rules = new LayoutRuleSet(Rules().Rules.Concat([new MultilineRawStringLayoutRule("raw")]));
		var first = formatter.Format(source, options, layoutRules: rules);
		first.Success.Should().BeTrue(first.Message);
		first.LayoutApplications.Count.Should().Be(2);
		first.Text.Should().Contain("value");
		formatter.Format(first.Text, options, layoutRules: rules).Text.Should().Be(first.Text);
	}

	[Test]
	public void Raw_string_operand_opening_conflicts_fail_explicitly()
	{
		const string source = """"
			class C
			{
			    bool M()
			    {
			        return entries.Any(entry => """
			            value
			            """ == entry.Name && entry.HasRequiredPermission);
			    }
			}
			"""";
		using var formatter = new CSharpFormatter();
		var rules = new LayoutRuleSet(Rules().Rules.Concat([new MultilineRawStringLayoutRule("raw")]));
		var result = formatter.Format(source, TestOptions.Parse(LogicalLambdaLayoutSamples.Config), layoutRules: rules);
		result.Status.Should().Be(FormatStatus.VerificationFailed);
		result.Text.Should().BeNull();
		result.Message.Should().Contain("overlaps");
	}

	[Test]
	public void String_concatenation_rules_remain_scoped_to_nested_arguments()
	{
		const string source = "class C { bool M() { return entries.Any(entry => Check(\"first long message component \" + entry.Name + \" second long message component\") && entry.HasRequiredPermission); } }";
		using var formatter = new CSharpFormatter();
		var rules = new LayoutRuleSet(Rules().Rules.Concat([new StringConcatenationLayoutRule("strings")]));
		var options = TestOptions.Parse(LogicalLambdaLayoutSamples.Config + "\nmax_line_length = 65");
		var first = formatter.Format(source, options, layoutRules: rules);
		first.Success.Should().BeTrue(first.Message);
		first.LayoutApplications.Count.Should().Be(2);
		formatter.Format(first.Text, options, layoutRules: rules).Text.Should().Be(first.Text);
	}

	[Test]
	[Arguments(false, "\n")]
	[Arguments(true, "\n")]
	[Arguments(false, "\r\n")]
	[Arguments(true, "\r\n")]
	public void Output_indentation_is_stable_with_tabs_and_line_endings(bool tabs, string ending)
	{
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LogicalLambdaLayoutSamples.Config
			+ $"\nindent_style = {(tabs ? "tab" : "space")}\nend_of_line = {(ending == "\n" ? "lf" : "crlf")}");
		var first = formatter.Format(LogicalLambdaLayoutSamples.Source.ReplaceLineEndings(ending), options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		var expected = (tabs ? LogicalLambdaLayoutSamples.Expected.Replace("    ", "\t", StringComparison.Ordinal)
			: LogicalLambdaLayoutSamples.Expected).ReplaceLineEndings(ending);
		first.Text!.TrimEnd().Should().Be(expected);
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	[Arguments("chop_always", true)]
	[Arguments("wrap_if_long", false)]
	public void The_recipe_owns_logical_operator_breaks_independently_of_general_preferences(string style, bool leading)
	{
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LogicalLambdaLayoutSamples.Config
			+ $"\ncsharp_wrap_chained_binary_expressions = {style}\ncsharp_wrap_before_binary_opsign = {leading.ToString().ToLowerInvariant()}");
		var first = formatter.Format(LogicalLambdaLayoutSamples.Source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text!.TrimEnd().Should().Be(LogicalLambdaLayoutSamples.Expected);
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	public void Fluent_chain_trailers_still_select_the_logical_lambda_argument()
	{
		const string source = "class C { string M() { return entries.Where(Filter).Any(entry => entry.IsEnabled && entry.HasRequiredPermission && entry.IsAvailable).ToString(); } }";
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LogicalLambdaLayoutSamples.Config + "\nmax_line_length = 70");
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.LayoutApplications.Should().ContainSingle();
		first.Text.Should().Contain("Any(entry =>");
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	public void Logical_arguments_inside_an_operand_do_not_inherit_the_predicate_policy()
	{
		const string source = "class C { bool M() { return entries.Any(entry => Check(firstCondition && secondCondition) && entry.HasRequiredPermission && entry.IsAvailable); } }";
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LogicalLambdaLayoutSamples.Config + "\nmax_line_length = 70\ncsharp_wrap_chained_binary_expressions = chop_always");
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.LayoutApplications.Should().ContainSingle();
		first.Text.Should().Contain("Check(firstCondition && secondCondition)");
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}
}
