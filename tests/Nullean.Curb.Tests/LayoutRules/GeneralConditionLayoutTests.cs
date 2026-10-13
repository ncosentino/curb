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
}
