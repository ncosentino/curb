using AwesomeAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Nullean.Curb.LayoutRules;
using Nullean.Curb.Tests.Formatting;

namespace Nullean.Curb.Tests.LayoutRules;

public class StringConcatenationLayoutTests
{
	private static LayoutRuleSet Rules() => new([new StringConcatenationLayoutRule("argument-strings")]);

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void Joined_hanging_and_canonical_inputs_converge_to_the_argument_indent(bool hanging)
	{
		Check(hanging ? StringConcatenationLayoutSamples.Hanging : StringConcatenationLayoutSamples.Joined,
			StringConcatenationLayoutSamples.Canonical, StringConcatenationLayoutSamples.Config, applications: 1);
		Check(StringConcatenationLayoutSamples.Canonical, StringConcatenationLayoutSamples.Canonical,
			StringConcatenationLayoutSamples.Config, applications: 1);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void The_continuation_indent_is_owned_in_preservation_and_deterministic_modes(bool preserve)
	{
		Check(StringConcatenationLayoutSamples.Hanging, StringConcatenationLayoutSamples.Canonical,
			StringConcatenationLayoutSamples.Config.Replace("csharp_keep_existing_linebreaks = false",
				$"csharp_keep_existing_linebreaks = {preserve}", StringComparison.Ordinal), applications: 1);
	}

	[Test]
	public void A_concatenation_that_fits_stays_on_one_line()
	{
		const string source = """
			public static class Repro
			{
			    public static void Report(Logger logger, string name, int count)
			    {
			        logger.Warn("short " + name + " done", count);
			    }
			}
			""";
		Check(source, source, StringConcatenationLayoutSamples.Config, applications: 1);
	}

	[Test]
	public void Operands_keep_packing_onto_each_continuation_line()
	{
		const string source = """
			public static class Repro
			{
			    public static void Report(Logger logger, string name, int count)
			    {
			        logger.Warn("This is a deliberately long message describing " + name + " which keeps going past the line limit so that " + "the chain must break somewhere, and it continues with " + count + " more words to make three lines worth", count);
			    }
			}
			""";
		const string expected = """
			public static class Repro
			{
			    public static void Report(Logger logger, string name, int count)
			    {
			        logger.Warn(
			            "This is a deliberately long message describing " + name +
			            " which keeps going past the line limit so that " +
			            "the chain must break somewhere, and it continues with " + count + " more words to make three lines worth",
			            count
			        );
			    }
			}
			""";
		Check(source, expected, StringConcatenationLayoutSamples.Config, applications: 1);
	}

	[Test]
	[Arguments("BaseAddress", "\"/session/\"")]
	[Arguments("\"/session/\"", "BaseAddress")]
	[Arguments("BaseAddress", "$\"/session/{Id}/reset\"")]
	public void Any_string_or_interpolated_operand_selects_the_chain(string first, string second)
	{
		var source = """
			public static class Repro
			{
			    public static void Report(Logger logger)
			    {
			        logger.Warn(FIRST + SECOND + " and a long enough trailing literal to force the argument list onto several lines", 1);
			    }
			}
			""".Replace("FIRST", first, StringComparison.Ordinal).Replace("SECOND", second, StringComparison.Ordinal);
		var expected = """
			public static class Repro
			{
			    public static void Report(Logger logger)
			    {
			        logger.Warn(
			            FIRST + SECOND +
			            " and a long enough trailing literal to force the argument list onto several lines",
			            1
			        );
			    }
			}
			""".Replace("FIRST", first, StringComparison.Ordinal).Replace("SECOND", second, StringComparison.Ordinal);
		Check(source, expected, StringConcatenationLayoutSamples.Config, applications: 1);
	}

	[Test]
	public void A_named_argument_continues_at_the_argument_indent()
	{
		var source = StringConcatenationLayoutSamples.Joined.Replace("Warn(\"", "Warn(message: \"", StringComparison.Ordinal);
		var expected = StringConcatenationLayoutSamples.Canonical
			.Replace("            \"Scheduler", "            message: \"Scheduler", StringComparison.Ordinal);
		Check(source, expected, StringConcatenationLayoutSamples.Config, applications: 1);
	}

	[Test]
	[Arguments("logger.Warn(Count + Count + Count + Count + Count + Count + Count + Count + Count + Count + Count + Count + Count + Count, 1);")]
	[Arguments("logger.Warn(value => \"A lambda body concatenation stays under ordinary formatting because \" + value + \" is not an argument.\");")]
	[Arguments("var message = \"An assignment concatenation stays under ordinary formatting because it is not an argument \" + Count + \" at all.\";")]
	[Arguments("logger.Warn(Enabled && Count > 100000000 && Count < 200000000 && Count != 150000000 && Count != 160000000 && Ready, 1);")]
	public void Unowned_shapes_keep_ordinary_formatting(string statement)
	{
		var source = """
			public static class Repro
			{
			    public static void Report(Logger logger)
			    {
			        STATEMENT
			    }
			}
			""".Replace("STATEMENT", statement, StringComparison.Ordinal);
		var options = TestOptions.Parse(StringConcatenationLayoutSamples.Config);
		using var formatter = new CSharpFormatter();
		var ordinary = formatter.Format(source, options);
		ordinary.Success.Should().BeTrue(ordinary.Message);
		var ruled = formatter.Format(source, options, layoutRules: Rules());
		ruled.Success.Should().BeTrue(ruled.Message);
		ruled.Text.Should().Be(ordinary.Text);
		ruled.LayoutApplications.Should().BeNullOrEmpty();
	}

	[Test]
	[Arguments("\n", "lf")]
	[Arguments("\r\n", "crlf")]
	public void Line_endings_follow_the_configured_file_ending(string ending, string configured)
	{
		Check(StringConcatenationLayoutSamples.Hanging.ReplaceLineEndings(ending),
			StringConcatenationLayoutSamples.Canonical.ReplaceLineEndings(ending),
			StringConcatenationLayoutSamples.Config.Replace("end_of_line = lf", "end_of_line = " + configured, StringComparison.Ordinal),
			applications: 1);
	}

	[Test]
	public void Tab_indentation_places_the_continuation_at_the_argument_tab_stop()
	{
		Check(StringConcatenationLayoutSamples.Hanging.Replace("    ", "\t", StringComparison.Ordinal),
			StringConcatenationLayoutSamples.Canonical.Replace("    ", "\t", StringComparison.Ordinal),
			StringConcatenationLayoutSamples.Config + "\nindent_style = tab\nindent_size = 4\ntab_width = 4", applications: 1);
	}

	[Test]
	public void A_multiline_raw_operand_composes_with_the_raw_string_rule()
	{
		const string source = """"
			public static class Repro
			{
			    public static void Report(Logger logger, string name)
			    {
			        logger.Warn("A prefix long enough that the concatenation cannot stay beside the call " + name + """
			            raw payload
			            """, 1);
			    }
			}
			"""";
		var options = TestOptions.Parse(StringConcatenationLayoutSamples.Config);
		var rules = new LayoutRuleSet([new StringConcatenationLayoutRule("argument-strings"), new MultilineRawStringLayoutRule("raw-openers")]);
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(source, options, layoutRules: rules);
		first.Success.Should().BeTrue(first.Message);
		AssertTokens(source, first.Text!);
		first.LayoutApplications.Should().HaveCount(2);
		var second = formatter.Format(first.Text!, options, layoutRules: rules);
		second.Success.Should().BeTrue(second.Message);
		second.Changed.Should().BeFalse();
	}

	[Test]
	public void Duplicate_string_concatenation_rules_refuse_before_output()
	{
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(StringConcatenationLayoutSamples.Joined, TestOptions.Parse(StringConcatenationLayoutSamples.Config),
			layoutRules: new LayoutRuleSet([new StringConcatenationLayoutRule("one"), new StringConcatenationLayoutRule("two")]));
		result.Status.Should().Be(FormatStatus.VerificationFailed);
		result.Text.Should().BeNull();
		result.Message.Should().Contain("same string concatenation");
	}

	private static void Check(string source, string expected, string configuration, int applications)
	{
		var options = TestOptions.Parse(configuration);
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text!.TrimEnd('\r', '\n').Should().Be(expected);
		first.LayoutApplications.Should().HaveCount(applications)
			.And.AllSatisfy(application => application.Recipe.Should().Be("argument-string-concatenation"));
		AssertTokens(source, first.Text);
		var second = formatter.Format(first.Text, options, layoutRules: Rules());
		second.Success.Should().BeTrue(second.Message);
		second.Changed.Should().BeFalse();
		second.Text.Should().Be(first.Text);
	}

	private static void AssertTokens(string source, string output)
	{
		var before = CSharpSyntaxTree.ParseText(source).GetRoot().DescendantTokens().Select(token => (token.RawKind, token.Text, token.ValueText));
		var after = CSharpSyntaxTree.ParseText(output).GetRoot().DescendantTokens().Select(token => (token.RawKind, token.Text, token.ValueText));
		after.Should().Equal(before);
	}
}
