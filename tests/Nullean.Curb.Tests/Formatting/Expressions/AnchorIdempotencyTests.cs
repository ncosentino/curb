using AwesomeAssertions;

namespace Nullean.Curb.Tests.Formatting.Expressions;

public class AnchorIdempotencyTests : FormattingTest
{
	private const string Input = """
		class C
		{
		    void M()
		    {
		        Call(longArgumentName, new Options { First = 1, Second = 2, Third = 3 });
		    }
		}
		""";

	private const string Broken = """
		class C
		{
		    void M()
		    {
		        Call(
		            longArgumentName,
		            new Options
		            {
		                First = 1,
		                Second = 2,
		                Third = 3
		            }
		        );
		    }
		}
		""";

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public Task A_reflowed_argument_anchors_to_its_output_line(bool tabs) =>
		Formats(
			tabs ? Input.Replace("    ", "\t") : Input,
			tabs ? Broken.Replace("    ", "\t") : Broken,
			"max_line_length = 40\ncsharp_keep_existing_linebreaks = true\n"
			+ (tabs ? "indent_style = tab\n" : "indent_style = space\n"));

	[Test]
	public Task Deterministic_layout_keeps_the_same_anchor() =>
		Formats(Input, Broken, "max_line_length = 40\ncsharp_keep_existing_linebreaks = false");

	[Test]
	public Task An_already_broken_argument_keeps_its_anchor() =>
		Unchanged(Broken, "max_line_length = 40\ncsharp_keep_existing_linebreaks = true");

	[Test]
	public Task An_initializer_that_fits_does_not_gain_a_break() => Formats(
		Input,
		"""
		class C
		{
		    void M()
		    {
		        Call(
		            longArgumentName,
		            new Options { First = 1, Second = 2, Third = 3 }
		        );
		    }
		}
		""",
		"max_line_length = 60\ncsharp_keep_existing_linebreaks = true");

	[Test]
	public Task A_preserved_inline_argument_keeps_the_statement_anchor() => Unchanged(
		"""
		class C
		{
		    void M()
		    {
		        Call(1, new Options
		        {
		            First = 1,
		            Second = 2
		        });
		    }
		}
		""",
		"max_line_length = off\ncsharp_keep_existing_linebreaks = true");

	[Test]
	public Task A_nested_creation_argument_uses_the_outer_output_anchor() => Formats(
		"""
		class C
		{
		    void M()
		    {
		        Call(longArgumentName, Wrap(new Options { First = 1, Second = 2, Third = 3 }));
		    }
		}
		""",
		"""
		class C
		{
		    void M()
		    {
		        Call(
		            longArgumentName,
		            Wrap(new Options
		            {
		                First = 1,
		                Second = 2,
		                Third = 3
		            })
		        );
		    }
		}
		""",
		"max_line_length = 40\ncsharp_keep_existing_linebreaks = true");

	[Test]
	[Arguments(true, true)]
	[Arguments(true, false)]
	[Arguments(false, true)]
	[Arguments(false, false)]
	public void Braces_and_initializer_member_options_preserve_the_fixed_point(bool indentBraces, bool oneMemberPerLine)
	{
		var options = TestOptions.Parse(
			$"max_line_length = 40\ncsharp_keep_existing_linebreaks = true\ncsharp_indent_braces = {indentBraces}\n"
			+ $"csharp_new_line_before_members_in_object_initializers = {oneMemberPerLine}");
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(Input, options, verifyRoundTrip: true, forceRoundTrip: true);
		first.Success.Should().BeTrue(first.Message);
		var second = formatter.Format(first.Text!, options, verifyRoundTrip: true, forceRoundTrip: true);
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(first.Text);
		second.Changed.Should().BeFalse();
	}
}
