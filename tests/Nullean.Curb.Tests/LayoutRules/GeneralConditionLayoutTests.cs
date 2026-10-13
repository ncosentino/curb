using System.Text;
using AwesomeAssertions;
using Nullean.Curb.Cli;
using Nullean.Curb.LayoutRules;
using Nullean.Curb.Tests.Formatting;

namespace Nullean.Curb.Tests.LayoutRules;

public class GeneralConditionLayoutTests
{
	private static LayoutRuleSet Rules() =>
		new(LayoutRuleCompiler.Compile(Encoding.UTF8.GetBytes(GeneralConditionLayoutSamples.Policy)).Select(definition => definition.Rule));

	private static void AssertLayout(string source, string expected, string? settings = null, LayoutRuleSet? rules = null) =>
		LayoutRuleAssertions.AssertLayout(source, expected, TestOptions.Parse(GeneralConditionLayoutSamples.Config + "\n" + settings), rules ?? Rules());

	[Test]
	public void The_schema_compiles_and_indexes_a_general_condition_rule() =>
		Rules().GeneralConditions.Should().ContainSingle().Which.Recipe.Should().Be("attached-condition");

	[Test]
	[Arguments("\"if-statement\"", "\"while-statement\"")]
	[Arguments("\"if-long\"", "\"always\"")]
	[Arguments("\"with-condition\"", "\"own-line\"")]
	[Arguments("\"one-indent\"", "\"align-first-operand\"")]
	[Arguments("\"closeParen\": \"with-condition\"", "\"closeParen\": \"with-condition\", \"unknown\": true")]
	public void Unsupported_policy_values_fail_explicitly(string before, string after)
	{
		Action compile = () => LayoutRuleCompiler.Compile(Encoding.UTF8.GetBytes(
			GeneralConditionLayoutSamples.Policy.Replace(before, after, StringComparison.Ordinal)));
		compile.Should().Throw<LayoutRuleConfigurationException>();
	}

	[Test]
	[Arguments("")]
	[Arguments("!")]
	[Arguments("await ")]
	[Arguments("!await ")]
	[Arguments("(")]
	[Arguments("!(")]
	public void Calls_prefer_internal_argument_breaks(string prefix)
	{
		var parenthesized = prefix.EndsWith('(');
		var condition = prefix + GeneralConditionLayoutSamples.Call + (parenthesized ? ")" : "");
		var expected = GeneralConditionLayoutSamples.CallExpected.Replace("if (HasCalibration(", "if (" + prefix + "HasCalibration(", StringComparison.Ordinal);
		if (parenthesized)
			expected = expected.Replace("        ))", "        )))", StringComparison.Ordinal);
		AssertLayout(GeneralConditionLayoutSamples.Source(condition), expected);
	}

	[Test]
	public void Bare_property_patterns_keep_the_open_and_designation_attached() =>
		AssertLayout(GeneralConditionLayoutSamples.Source(GeneralConditionLayoutSamples.Property), GeneralConditionLayoutSamples.PropertyExpected);

	[Test]
	[Arguments("SensorFrame")]
	[Arguments("not SensorFrame")]
	public void Typed_property_patterns_keep_the_type_attached(string type)
	{
		var condition = GeneralConditionLayoutSamples.Property.Replace("reading is {", "reading is " + type + " {", StringComparison.Ordinal);
		var expected = GeneralConditionLayoutSamples.PropertyExpected.Replace("reading is", "reading is " + type, StringComparison.Ordinal);
		AssertLayout(GeneralConditionLayoutSamples.Source(condition), expected);
	}

	[Test]
	[Arguments("is null")]
	[Arguments("is not null")]
	[Arguments("is SensorFrame frame")]
	[Arguments("is { } frame")]
	[Arguments("== expected")]
	[Arguments("< 0")]
	public void Invocation_suffixes_share_the_header_measurement(string suffix) =>
		AssertLayout(GeneralConditionLayoutSamples.Source(GeneralConditionLayoutSamples.Call + " " + suffix),
			GeneralConditionLayoutSamples.CallExpected.Replace("        ))", "        ) " + suffix + ")", StringComparison.Ordinal));

	[Test]
	public void Pattern_alternatives_gain_internal_break_opportunities()
	{
		const string condition = "reading.Kind is CalibrationKind.PrimaryReference or CalibrationKind.SecondaryReference";
		const string expected = """
			class C
			{
			    void M()
			    {
			        if (reading.Kind is CalibrationKind.PrimaryReference or
			            CalibrationKind.SecondaryReference)
			        {
			            Use();
			        }
			    }
			}
			""";
		AssertLayout(GeneralConditionLayoutSamples.Source(condition), expected, "max_line_length = 70");
	}

	[Test]
	public void Ternary_conditions_retain_branch_wrapping()
	{
		const string expected = """
			class C
			{
			    void M()
			    {
			        if (usePrimary
			            ? HasCalibration(primaryReading)
			            : HasCalibration(secondaryReading))
			        {
			            Use();
			        }
			    }
			}
			""";
		AssertLayout(GeneralConditionLayoutSamples.Source("usePrimary ? HasCalibration(primaryReading) : HasCalibration(secondaryReading)"),
			expected, "max_line_length = 55");
	}

	[Test]
	public void Negated_logical_groups_keep_explicit_parentheses()
	{
		const string expected = """
			class C
			{
			    void M()
			    {
			        if (!(primaryReading.IsCalibrated ||
			            secondaryReading.IsCalibrated))
			        {
			            Use();
			        }
			    }
			}
			""";
		AssertLayout(GeneralConditionLayoutSamples.Source("!(primaryReading.IsCalibrated || secondaryReading.IsCalibrated)"), expected);
	}

	[Test]
	[Arguments(false, "\n")]
	[Arguments(false, "\r\n")]
	[Arguments(true, "\n")]
	[Arguments(true, "\r\n")]
	public void Output_columns_and_endings_are_stable(bool tabs, string ending)
	{
		var expected = (tabs ? GeneralConditionLayoutSamples.CallExpected.Replace("    ", "\t", StringComparison.Ordinal)
			: GeneralConditionLayoutSamples.CallExpected).ReplaceLineEndings(ending);
		AssertLayout(GeneralConditionLayoutSamples.Source(GeneralConditionLayoutSamples.Call).ReplaceLineEndings(ending), expected,
			$"indent_style = {(tabs ? "tab" : "space")}\nend_of_line = {(ending == "\n" ? "lf" : "crlf")}");
	}

	[Test]
	public void Expanded_and_flat_sources_converge_to_the_same_output()
	{
		AssertLayout(GeneralConditionLayoutSamples.Source(GeneralConditionLayoutSamples.Call), GeneralConditionLayoutSamples.CallExpected);
		AssertLayout(GeneralConditionLayoutSamples.CallExpected.Replace("if (HasCalibration(", "if (\n            HasCalibration(", StringComparison.Ordinal)
			.Replace("        ))", "            )\n        )", StringComparison.Ordinal), GeneralConditionLayoutSamples.CallExpected);
	}

	[Test]
	public void Else_if_prefixes_are_measured_and_rendered()
	{
		var source = GeneralConditionLayoutSamples.Source(GeneralConditionLayoutSamples.Call)
			.Replace("if (", "if (ready) { Use(); } else if (", StringComparison.Ordinal);
		var expected = GeneralConditionLayoutSamples.CallExpected.Replace("        if (",
			"        if (ready)\n        {\n            Use();\n        }\n        else if (", StringComparison.Ordinal);
		AssertLayout(source, expected);
	}

	[Test]
	public void Default_property_pattern_goldens_remain_unchanged() =>
		LayoutRuleAssertions.AssertLayout(GeneralConditionLayoutSamples.Source(GeneralConditionLayoutSamples.Property),
			GeneralConditionLayoutSamples.PropertyDefault, TestOptions.Parse(GeneralConditionLayoutSamples.Config), null, expectApplications: false);

	[Test]
	[Arguments("while")]
	[Arguments("lock")]
	public void Other_control_flow_headers_keep_default_behavior(string keyword)
	{
		var source = GeneralConditionLayoutSamples.Source(GeneralConditionLayoutSamples.Call).Replace("if (", keyword + " (", StringComparison.Ordinal);
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(GeneralConditionLayoutSamples.Config);
		var ordinary = formatter.Format(source, options, verifyRoundTrip: true, forceRoundTrip: true);
		var selected = formatter.Format(source, options, verifyRoundTrip: true, forceRoundTrip: true, layoutRules: Rules());
		ordinary.Success.Should().BeTrue(ordinary.Message);
		selected.Success.Should().BeTrue(selected.Message);
		selected.Text.Should().Be(ordinary.Text);
		selected.LayoutApplications.Should().BeEmpty();
	}

	[Test]
	[Arguments(26, false)]
	[Arguments(25, false)]
	[Arguments(24, true)]
	public void Complete_header_width_includes_both_closing_delimiters(int width, bool broken)
	{
		var header = broken ? "if (Check(\n            value\n        ))" : "if (Check(value))";
		var expected = $$"""
			class C
			{
			    void M()
			    {
			        {{header}}
			        {
			            Use();
			        }
			    }
			}
			""";
		AssertLayout(GeneralConditionLayoutSamples.Source("Check(value)"), expected, $"max_line_length = {width}");
	}

	[Test]
	public void Named_ref_in_and_out_arguments_use_the_normal_argument_printer()
	{
		const string expected = """
			class C
			{
			    void M()
			    {
			        if (Check(
			            primary: primaryReading,
			            ref secondaryReading,
			            in backupReading,
			            out SensorFrame? selectedReading
			        ))
			        {
			            Use();
			        }
			    }
			}
			""";
		AssertLayout(GeneralConditionLayoutSamples.Source(
			"Check(primary: primaryReading, ref secondaryReading, in backupReading, out SensorFrame? selectedReading)"), expected);
	}

	[Test]
	public void Generic_calls_and_postfixes_keep_their_original_ownership()
	{
		var source = GeneralConditionLayoutSamples.Source("Check<SensorFrame>(primaryReading, secondaryReading, backupReading).ConfigureAwait(false)");
		var expected = GeneralConditionLayoutSamples.CallExpected.Replace("HasCalibration(", "Check<SensorFrame>(", StringComparison.Ordinal)
			.Replace("        ))", "        ).ConfigureAwait(false))", StringComparison.Ordinal);
		AssertLayout(source, expected);
	}

	[Test]
	public void Pattern_negation_parentheses_and_alternatives_preserve_grouping()
	{
		const string expected = """
			class C
			{
			    void M()
			    {
			        if (reading.Kind is not (CalibrationKind.PrimaryReference or
			            CalibrationKind.SecondaryReference))
			        {
			            Use();
			        }
			    }
			}
			""";
		AssertLayout(GeneralConditionLayoutSamples.Source(
			"reading.Kind is not (CalibrationKind.PrimaryReference or CalibrationKind.SecondaryReference)"),
			expected, "max_line_length = 70");
	}

	[Test]
	public void Pattern_conjunctions_use_the_same_internal_break_policy()
	{
		const string expected = """
			class C
			{
			    void M()
			    {
			        if (reading is not PrimarySensorFrame and
			            not SecondarySensorFrame and
			            not ReferenceSensorFrame)
			        {
			            Use();
			        }
			    }
			}
			""";
		AssertLayout(GeneralConditionLayoutSamples.Source(
			"reading is not PrimarySensorFrame and not SecondarySensorFrame and not ReferenceSensorFrame"),
			expected, "max_line_length = 60");
	}

	[Test]
	public void Positional_patterns_delegate_internal_subpattern_wrapping()
	{
		const string expected = """
			class C
			{
			    void M()
			    {
			        if (reading is SensorFrame(
			            primaryReading,
			            secondaryReading
			        ))
			        {
			            Use();
			        }
			    }
			}
			""";
		AssertLayout(GeneralConditionLayoutSamples.Source("reading is SensorFrame(primaryReading, secondaryReading)"), expected);
	}

	[Test]
	public void Nested_property_patterns_delegate_their_own_brace_indentation()
	{
		const string expected = """
			class C
			{
			    void M()
			    {
			        if (reading is
			        {
			            Payload:
			            {
			                Temperature: > 20,
			                Humidity: < 80
			            }
			        } accepted)
			        {
			            Use();
			        }
			    }
			}
			""";
		AssertLayout(GeneralConditionLayoutSamples.Source(
			"reading is { Payload: { Temperature: > 20, Humidity: < 80 } } accepted"), expected);
	}

	[Test]
	public void Block_callbacks_can_contain_independently_selected_headers()
	{
		const string source = "class C { void M() { if (Readings.Any(reading => { if (HasCalibration(primaryReading, secondaryReading, backupReading)) { return true; } return false; })) { Use(); } } }";
		const string expected = """
			class C
			{
			    void M()
			    {
			        if (Readings.Any(reading =>
			        {
			            if (HasCalibration(
			                primaryReading,
			                secondaryReading,
			                backupReading
			            ))
			            {
			                return true;
			            }
			            return false;
			        }))
			        {
			            Use();
			        }
			    }
			}
			""";
		AssertLayout(source, expected);
	}

	[Test]
	[Arguments("true")]
	[Arguments("ready")]
	[Arguments("reading.IsCalibrated")]
	[Arguments("flags[index]")]
	[Arguments("(bool)reading")]
	[Arguments("checked(count > 0)")]
	[Arguments("unchecked(count > 0)")]
	[Arguments("(ready = Check(value))")]
	[Arguments("reading?.IsCalibrated ?? false")]
	[Arguments("reading is [1, .., 3]")]
	[Arguments("Check($\"sensor-{index}\")")]
	[Arguments("Check(@\"sensor\\reference\")")]
	public void Fitting_grammar_variants_stay_inline(string condition)
	{
		var expected = $$"""
			class C
			{
			    void M()
			    {
			        if ({{condition}})
			        {
			            Use();
			        }
			    }
			}
			""";
		AssertLayout(GeneralConditionLayoutSamples.Source(condition), expected, "max_line_length = 100");
	}

	[Test]
	[Arguments("max_line_length = off")]
	[Arguments("csharp_keep_existing_linebreaks = true")]
	[Arguments("csharp_space_around_binary_operators = ignore")]
	public void Inapplicable_modes_refuse_without_output(string setting)
	{
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(GeneralConditionLayoutSamples.Source(GeneralConditionLayoutSamples.Call),
			TestOptions.Parse(GeneralConditionLayoutSamples.Config + "\n" + setting), layoutRules: Rules());
		result.Status.Should().Be(FormatStatus.VerificationFailed);
		result.Text.Should().BeNull();
		result.Changed.Should().BeFalse();
	}

	[Test]
	[Arguments("if (", "if /* seam */ (")]
	[Arguments("if (", "if (/* seam */ ")]
	[Arguments("backupReading))", "backupReading) /* seam */ )")]
	[Arguments("backupReading))", "backupReading) // seam\n)")]
	public void Moved_boundary_content_fails_without_output(string before, string after)
	{
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(GeneralConditionLayoutSamples.Source(GeneralConditionLayoutSamples.Call).Replace(before, after, StringComparison.Ordinal),
			TestOptions.Parse(GeneralConditionLayoutSamples.Config), layoutRules: Rules());
		result.Status.Should().Be(FormatStatus.VerificationFailed);
		result.Text.Should().BeNull();
		result.Changed.Should().BeFalse();
		result.Message.Should().Contain("condition-boundary trivia");
	}

	[Test]
	public void Duplicate_general_claims_fail_explicitly()
	{
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(GeneralConditionLayoutSamples.Source(GeneralConditionLayoutSamples.Call),
			TestOptions.Parse(GeneralConditionLayoutSamples.Config),
			layoutRules: new LayoutRuleSet([new ConditionLayoutRule("first"), new ConditionLayoutRule("second")]));
		result.Status.Should().Be(FormatStatus.VerificationFailed);
		result.Text.Should().BeNull();
		result.Message.Should().Contain("same condition header");
	}

	[Test]
	public void Suppression_wins_over_general_condition_selection()
	{
		const string source = """
			class C
			{
			    void M()
			    {
			#pragma warning disable IDE0055
			        if (HasCalibration(primaryReading, secondaryReading, backupReading)) { Use(); }
			#pragma warning restore IDE0055
			    }
			}
			""";
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(GeneralConditionLayoutSamples.Config);
		var ordinary = formatter.Format(source, options, verifyRoundTrip: true, forceRoundTrip: true);
		var selected = formatter.Format(source, options, verifyRoundTrip: true, forceRoundTrip: true, layoutRules: Rules());
		ordinary.Success.Should().BeTrue(ordinary.Message);
		selected.Success.Should().BeTrue(selected.Message);
		selected.Text.Should().Be(ordinary.Text);
		selected.LayoutApplications.Should().BeEmpty();
	}

	[Test]
	public void Existing_logical_condition_ownership_does_not_change()
	{
		var options = TestOptions.Parse(ConditionLayoutSamples.Config);
		using var formatter = new CSharpFormatter();
		var specialized = formatter.Format(ConditionLayoutSamples.Source, options,
			layoutRules: new LayoutRuleSet([new LogicalConditionLayoutRule("logical")]));
		var combined = formatter.Format(ConditionLayoutSamples.Source, options,
			layoutRules: new LayoutRuleSet([new ConditionLayoutRule("general"), new LogicalConditionLayoutRule("logical")]));
		specialized.Success.Should().BeTrue(specialized.Message);
		combined.Success.Should().BeTrue(combined.Message);
		combined.Text.Should().Be(specialized.Text);
		combined.LayoutApplications.Should().ContainSingle().Which.RuleId.Should().Be("logical");
	}

	[Test]
	public void Existing_callback_ownership_does_not_change()
	{
		var options = TestOptions.Parse(ExpressionLambdaLayoutSamples.Config);
		using var formatter = new CSharpFormatter();
		var specialized = formatter.Format(ExpressionLambdaLayoutSamples.PatternSource, options,
			layoutRules: new LayoutRuleSet([new ExpressionLambdaLayoutRule("callback")]));
		var combined = formatter.Format(ExpressionLambdaLayoutSamples.PatternSource, options,
			layoutRules: new LayoutRuleSet([new ConditionLayoutRule("general"), new ExpressionLambdaLayoutRule("callback")]));
		specialized.Success.Should().BeTrue(specialized.Message);
		combined.Success.Should().BeTrue(combined.Message);
		combined.Text.Should().Be(specialized.Text);
		combined.LayoutApplications.Should().ContainSingle().Which.RuleId.Should().Be("callback");
	}

	[Test]
	public void Unselected_patterns_outside_if_headers_do_not_change()
	{
		const string source = "class C { bool M(object reading) { return reading is { Temperature: > 20, Humidity: < 80 } accepted; } }";
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(GeneralConditionLayoutSamples.Config);
		var ordinary = formatter.Format(source, options);
		var selected = formatter.Format(source, options, layoutRules: Rules());
		ordinary.Success.Should().BeTrue(ordinary.Message);
		selected.Success.Should().BeTrue(selected.Message);
		selected.Text.Should().Be(ordinary.Text);
		selected.LayoutApplications.Should().BeEmpty();
	}
}
