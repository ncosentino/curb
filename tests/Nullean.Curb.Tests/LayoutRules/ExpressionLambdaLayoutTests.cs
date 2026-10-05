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
		first.Text.Should().NotBeNull();
		first.Text.TrimEnd('\r', '\n').Should().Be(expected);
		first.LayoutApplications.Should().NotBeEmpty();
		var second = formatter.Format(first.Text, options, verifyRoundTrip: true, forceRoundTrip: true, layoutRules: rules);
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(first.Text);
		formatter.RoundTripsChecked.Should().Be(2);
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
	[Arguments(29, false)]
	[Arguments(28, true)]
	public void The_width_boundary_includes_the_enclosing_if_delimiter(int width, bool broken)
	{
		var header = broken ? "if (xs.Any(x =>\n            x.A))" : "if (xs.Any(x => x.A))";
		var expected = $$"""
			class C
			{
			    void M()
			    {
			        {{header}}
			        {
			            Call();
			        }
			    }
			}
			""";
		AssertLayout("class C { void M() { if (xs.Any(x => x.A)) { Call(); } } }", expected, $"max_line_length = {width}");
	}

	[Test]
	[Arguments(39, false)]
	[Arguments(38, true)]
	public void Zero_argument_body_calls_do_not_hide_the_statement_delimiter(int width, bool broken)
	{
		var statement = broken ? "return xs.Select(x =>\n            x.Get());" : "return xs.Select(x => x.Get());";
		var expected = $$"""
			class C
			{
			    object M()
			    {
			        {{statement}}
			    }
			}
			""";
		AssertLayout("class C { object M() { return xs.Select(x => x.Get()); } }", expected, $"max_line_length = {width}");
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
	[Arguments("csharp_wrap_arguments_style = chop_always")]
	[Arguments("csharp_max_invocation_arguments_on_line = 1")]
	public void Constructor_arguments_retain_their_forced_wrapping_policy(string setting)
	{
		const string source = "class C { object M() { return entries.Select(entry => new Pair(entry.Id, entry.Name)); } }";
		const string expected = """
			class C
			{
			    object M()
			    {
			        return entries.Select(entry => new Pair(
			            entry.Id,
			            entry.Name
			        ));
			    }
			}
			""";
		AssertLayout(source, expected, setting);
	}

	[Test]
	public void Attributed_typed_headers_share_the_existing_width_driven_printer()
	{
		const string source = "class C { object M() { return entries.Select([Marker] static object (Entry entry) => entry.Id); } }";
		const string expected = """
			class C
			{
			    object M()
			    {
			        return entries.Select([Marker] static object (Entry entry) => entry.Id);
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
		var second = formatter.Format(ordinary.Text!, options);
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(ordinary.Text);
	}

	[Test]
	public void Only_an_oversized_callback_header_breaks_after_the_call_opener()
	{
		const string source = "class C { object M() { return quiteLongCollectionName.Select(longCallbackParameter => longCallbackParameter.Id); } }";
		const string expected = """
			class C
			{
			    object M()
			    {
			        return quiteLongCollectionName.Select(
			            longCallbackParameter =>
			                longCallbackParameter.Id);
			    }
			}
			""";
		AssertLayout(source, expected, "max_line_length = 50");
	}

	[Test]
	public void An_oversized_constructor_introducer_can_break_after_the_arrow()
	{
		const string source = "class C { object M() { return entries.Select(entry => new LongProjectionResultTypeName(entry.Id)); } }";
		const string expected = """
			class C
			{
			    object M()
			    {
			        return entries.Select(entry =>
			            new LongProjectionResultTypeName(
			                entry.Id
			            ));
			    }
			}
			""";
		AssertLayout(source, expected, "max_line_length = 50");
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void Negated_and_parenthesized_if_conditions_keep_the_selected_callback_attached(bool parenthesized)
	{
		var source = ExpressionLambdaLayoutSamples.PatternSource.Replace("if (entries.Any(", parenthesized ? "if ((entries.Any(" : "if (!entries.Any(", StringComparison.Ordinal);
		var expected = ExpressionLambdaLayoutSamples.PatternExpected.Replace("if (entries.Any(", parenthesized ? "if ((entries.Any(" : "if (!entries.Any(", StringComparison.Ordinal);
		if (parenthesized)
		{
			source = source.Replace("NotApplicable)))", "NotApplicable))))", StringComparison.Ordinal);
			expected = expected.Replace("NotApplicable)))", "NotApplicable))))", StringComparison.Ordinal);
		}
		AssertLayout(source, expected);
	}

	[Test]
	public void Else_if_uses_the_actual_output_line_indent()
	{
		var source = ExpressionLambdaLayoutSamples.PatternSource.Replace("if (entries.Any", "if (ok) { Call(); } else if (entries.Any", StringComparison.Ordinal);
		var expected = ExpressionLambdaLayoutSamples.PatternExpected.Replace("if (entries.Any", "if (ok)\n        {\n            Call();\n        }\n        else if (entries.Any", StringComparison.Ordinal);
		AssertLayout(source, expected);
	}

	[Test]
	public void Nested_projection_callbacks_share_header_handling_without_extra_body_indents()
	{
		const string source = "class C { object M() { return entries.Select(entry => nestedEntries.Select(nested => new SearchResult(nested.Id, Map(nested.CurrentValue), Map(nested.PreviousValue)))); } }";
		const string expected = """
			class C
			{
			    object M()
			    {
			        return entries.Select(entry => nestedEntries.Select(nested => new SearchResult(
			            nested.Id,
			            Map(nested.CurrentValue),
			            Map(nested.PreviousValue)
			        )));
			    }
			}
			""";
		AssertLayout(source, expected);
	}

	[Test]
	public void Implicit_construction_uses_the_same_header_and_argument_layout() =>
		AssertLayout(ExpressionLambdaLayoutSamples.ProjectionSource.Replace("new SearchResult(", "new(", StringComparison.Ordinal),
			ExpressionLambdaLayoutSamples.ProjectionExpected.Replace("new SearchResult(", "new(", StringComparison.Ordinal),
			"max_line_length = 80");

	[Test]
	public void Interior_body_comments_use_normal_trivia_printing()
	{
		var source = ExpressionLambdaLayoutSamples.ProjectionSource.Replace("Map(entry.CurrentValue)", "Map(entry /* interior */ .CurrentValue)", StringComparison.Ordinal);
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(ExpressionLambdaLayoutSamples.Config);
		var first = formatter.Format(source, options, verifyRoundTrip: true, forceRoundTrip: true, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().Contain("/* interior */");
		first.LayoutApplications.Should().ContainSingle();
		var second = formatter.Format(first.Text, options, verifyRoundTrip: true, forceRoundTrip: true, layoutRules: Rules());
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(first.Text);
	}

	[Test]
	public void Raw_string_rules_compose_in_delegated_invocation_bodies()
	{
		const string source = """"
			class C
			{
			    object M()
			    {
			        return entries.Select(entry => Consume("""
			            content
			            """));
			    }
			}
			"""";
		var rules = new LayoutRuleSet(Rules().Rules.Concat([new MultilineRawStringLayoutRule("raw")]));
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(ExpressionLambdaLayoutSamples.Config);
		var first = formatter.Format(source, options, verifyRoundTrip: true, forceRoundTrip: true, layoutRules: rules);
		first.Success.Should().BeTrue(first.Message);
		first.LayoutApplications.Count.Should().Be(2);
		first.Text.Should().Contain("content");
		var second = formatter.Format(first.Text, options, verifyRoundTrip: true, forceRoundTrip: true, layoutRules: rules);
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(first.Text);
	}

	[Test]
	public void Wrapper_delegated_bodies_use_the_shared_callback_headers()
	{
		var source = LayoutRuleSamples.Source.Replace("var value=new Value(number);",
			"var projected = entries.Select(entry => new SearchResult(entry.Id, Map(entry.CurrentValue), Map(entry.PreviousValue))); var value=new Value(number);", StringComparison.Ordinal);
		var rules = new LayoutRuleSet(Rules().Rules.Concat([new WrapperLayoutRule("wrappers", ["TraceScope.RunAsync", "Outcome.CaptureAsync"])]));
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LayoutRuleSamples.Config + "\nmax_line_length = 100");
		var first = formatter.Format(source, options, verifyRoundTrip: true, forceRoundTrip: true, layoutRules: rules);
		first.Success.Should().BeTrue(first.Message);
		first.LayoutApplications.Count.Should().Be(2);
		first.Text.Should().Contain("Select(entry => new SearchResult(");
		var second = formatter.Format(first.Text, options, verifyRoundTrip: true, forceRoundTrip: true, layoutRules: rules);
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(first.Text);
	}

	[Test]
	public void Existing_logical_rules_and_expression_rules_have_disjoint_owners()
	{
		const string source = "class C { object M() { return entries.Where(entry => entry.IsEnabled && entry.HasRequiredPermission && entry.IsAvailable).Select(entry => new SearchResult(entry.Id, Map(entry.CurrentValue), Map(entry.PreviousValue))); } }";
		var rules = new LayoutRuleSet(Rules().Rules.Concat([new LogicalLambdaLayoutRule("logical")]));
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(ExpressionLambdaLayoutSamples.Config);
		var first = formatter.Format(source, options, layoutRules: rules);
		first.Success.Should().BeTrue(first.Message);
		first.LayoutApplications.Select(application => application.Recipe).Should().BeEquivalentTo(["hanging-logical-lambda", "attached-expression-lambda"]);
		first.Text.Should().Contain("Where(entry =>\n");
		first.Text.Should().Contain("Select(entry => new SearchResult(");
		var second = formatter.Format(first.Text, options, layoutRules: rules);
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(first.Text);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void Logical_condition_alignment_composes_with_pattern_callbacks(bool elseIf)
	{
		var source = ExpressionLambdaLayoutSamples.PatternSource.Replace("if (entries.Any", "if (entries is null || entries.Any", StringComparison.Ordinal);
		if (elseIf)
			source = source.Replace("if (entries is null", "if (ok) { Call(); } else if (entries is null", StringComparison.Ordinal);
		var rules = new LayoutRuleSet(Rules().Rules.Concat([new LogicalConditionLayoutRule("conditions")]));
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(ExpressionLambdaLayoutSamples.Config);
		var first = formatter.Format(source, options, layoutRules: rules);
		first.Success.Should().BeTrue(first.Message);
		var callIndent = new string(' ', elseIf ? 17 : 12);
		first.Text.Should().Contain(callIndent + "entries.Any(entry =>\n" + callIndent
			+ "    entry.Status is not (ResultStatus.Unavailable or ResultStatus.NotApplicable))\n        )");
		first.LayoutApplications.Count.Should().Be(2);
		var second = formatter.Format(first.Text, options, layoutRules: rules);
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(first.Text);
	}

	[Test]
	public void Other_control_flow_headers_retain_their_existing_parenthesis_policy()
	{
		var source = ExpressionLambdaLayoutSamples.PatternSource.Replace("if (entries.Any", "while (entries.Any", StringComparison.Ordinal);
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(source, TestOptions.Parse(ExpressionLambdaLayoutSamples.Config), layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().Contain("while (\n");
		first.Text.Should().Contain("entries.Any(entry =>");
		var second = formatter.Format(first.Text, TestOptions.Parse(ExpressionLambdaLayoutSamples.Config), layoutRules: Rules());
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(first.Text);
	}

	[Test]
	[Arguments("max_line_length = off")]
	[Arguments("csharp_keep_existing_linebreaks = true")]
	[Arguments("csharp_space_around_binary_operators = ignore")]
	public void Unsupported_modes_fail_without_output(string setting)
	{
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(ExpressionLambdaLayoutSamples.PatternSource,
			TestOptions.Parse(ExpressionLambdaLayoutSamples.Config + "\n" + setting), layoutRules: Rules());
		result.Status.Should().Be(FormatStatus.VerificationFailed);
		result.Text.Should().BeNull();
		result.Changed.Should().BeFalse();
		result.Message.Should().Contain("Layout rule");
	}

	[Test]
	[Arguments("entry =>", "entry /* seam */ =>")]
	[Arguments("entry =>", "entry => /* seam */")]
	[Arguments("Any(entry", "Any(/* seam */ entry")]
	[Arguments("if (entries", "if (/* seam */ entries")]
	[Arguments("NotApplicable)))", "NotApplicable) // close seam\n))")]
	[Arguments("entry.Status", "\n#if FLAG\nentry.OtherStatus\n#else\nentry.Status\n#endif\n")]
	public void Unsafe_moved_boundaries_fail_without_output(string before, string after)
	{
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(ExpressionLambdaLayoutSamples.PatternSource.Replace(before, after, StringComparison.Ordinal),
			TestOptions.Parse(ExpressionLambdaLayoutSamples.Config), layoutRules: Rules());
		result.Status.Should().Be(FormatStatus.VerificationFailed);
		result.Text.Should().BeNull();
		result.Changed.Should().BeFalse();
		result.Message.Should().Contain("trivia");
	}

	[Test]
	public void Duplicate_rules_and_named_arguments_fail_explicitly()
	{
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(ExpressionLambdaLayoutSamples.Config);
		var duplicate = formatter.Format(ExpressionLambdaLayoutSamples.PatternSource, options,
			layoutRules: new LayoutRuleSet([new ExpressionLambdaLayoutRule("one"), new ExpressionLambdaLayoutRule("two")]));
		duplicate.Status.Should().Be(FormatStatus.VerificationFailed);
		duplicate.Text.Should().BeNull();
		duplicate.Message.Should().Contain("same expression lambda");
		var named = formatter.Format("class C { object M() { return entries.Select(selector: entry => entry.Id); } }", options, layoutRules: Rules());
		named.Status.Should().Be(FormatStatus.VerificationFailed);
		named.Text.Should().BeNull();
		named.Message.Should().Contain("named or ref");
	}

	[Test]
	[Arguments("\"sole-invocation-argument\"", "\"argument\"")]
	[Arguments("\"nonlogical-expression\"", "\"any\"")]
	[Arguments("\"inline-if-fits\"", "\"always-break\"")]
	[Arguments("\"one-indent\"", "\"two-indents\"")]
	[Arguments("\"with-body\"", "\"own-line\"")]
	[Arguments("\"wrap\": \"if-long\",", "\"wrap\": \"if-long\", \"unknown\": true,")]
	public void Unsupported_schema_values_fail_explicitly(string before, string after)
	{
		Action compile = () => LayoutRuleCompiler.Compile(Encoding.UTF8.GetBytes(
			ExpressionLambdaLayoutSamples.Policy.Replace(before, after, StringComparison.Ordinal)));
		compile.Should().Throw<LayoutRuleConfigurationException>();
	}

	[Test]
	public void Source_suppression_precedes_callback_and_condition_layouts()
	{
		var source = ExpressionLambdaLayoutSamples.PatternSource.Replace("        if (entries", "#pragma warning disable IDE0055\n        if (entries", StringComparison.Ordinal)
			.Replace("        return true;", "#pragma warning restore IDE0055\n        return true;", StringComparison.Ordinal);
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
	[Arguments(false, "\n")]
	[Arguments(true, "\n")]
	[Arguments(false, "\r\n")]
	[Arguments(true, "\r\n")]
	public void Output_indentation_and_line_endings_are_byte_stable(bool tabs, string ending)
	{
		var expected = (tabs ? ExpressionLambdaLayoutSamples.PatternExpected.Replace("    ", "\t", StringComparison.Ordinal)
			: ExpressionLambdaLayoutSamples.PatternExpected).ReplaceLineEndings(ending);
		AssertLayout(ExpressionLambdaLayoutSamples.PatternSource.ReplaceLineEndings(ending), expected,
			$"indent_style = {(tabs ? "tab" : "space")}\nend_of_line = {(ending == "\n" ? "lf" : "crlf")}");
	}
}
