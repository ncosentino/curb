using AwesomeAssertions;

namespace Nullean.Curb.Tests.Formatting.Trivia;

public class TrailingCommentAnchorTests : FormattingTest
{
	[Test]
	public Task A_separated_attribute_comment_uses_member_indentation() => Formats(
		"""
		public class C
		{
		    [System.Obsolete] // trailing
		    // Section
		    [System.CLSCompliant(false)]
		    public int Value;
		}
		""",
		"""
		public class C
		{
		    [System.Obsolete] // trailing

		    // Section
		    [System.CLSCompliant(false)]
		    public int Value;
		}
		""");

	[Test]
	public Task A_contiguous_aligned_comment_run_keeps_its_anchor() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(); // trailing
		                // first continuation
		                // second continuation
		        Call();
		    }
		}
		""");

	[Test]
	[Arguments("\n")]
	[Arguments("\r\n")]
	public void A_section_comment_does_not_inherit_a_separated_trailing_anchor(string ending)
	{
		const string source = """
            using System;

            [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
            public sealed class ExampleAttribute(string value) : Attribute
            {
            }

            public class Sample
            {
                [Example("first")]          // first
                [Example("second")]         // second
                // Next group
                [Example("third")]
                public int Value;
            }
            """;
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse($"max_line_length = 120\ncsharp_keep_existing_linebreaks = false\nend_of_line = {(ending == "\n" ? "lf" : "crlf")}");
		var first = formatter.Format(source.ReplaceLineEndings(ending), options, verifyRoundTrip: true, forceRoundTrip: true);
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().Contain(ending + "    // Next group" + ending);
		var second = formatter.Format(first.Text, options, verifyRoundTrip: true, forceRoundTrip: true);
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(first.Text);
	}

	[Test]
	[Arguments("\n", false, false)]
	[Arguments("\r\n", false, false)]
	[Arguments("\n", true, false)]
	[Arguments("\r\n", true, true)]
	public void The_reported_attribute_matrix_is_a_first_pass_fixed_point(string ending, bool tabs, bool preserve)
	{
		const string source = """
			using System;

			namespace FormatterRepro;

			[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
			public sealed class ExampleAttribute(params string[] values) : Attribute
			{
			}

			public sealed class Sample
			{
			    [Example("(https://example.test)", "https://example.test")]             // delimiter comment
			    [Example("[https://example.test]", "https://example.test")]             // bracket comment
			    [Example("{https://example.test}", "https://example.test")]             // brace comment
			    [Example("<https://example.test>", "https://example.test")]             // angle comment
			    [Example("\"https://example.test\"", "https://example.test")]           // double-quoted
			    [Example("'https://example.test'", "https://example.test")]             // single-quoted
			    [Example("[label](https://example.test)", "https://example.test")] // markup comment
			    // Next group
			    // Continuation of the section
			    [Example("first", "second", "third")]
			    public void Execute()
			    {
			    }
			}
			""";
		var input = (tabs ? source.Replace("    ", "\t", StringComparison.Ordinal) : source).ReplaceLineEndings(ending);
		var options = TestOptions.Parse($"max_line_length = 120\ncsharp_keep_existing_linebreaks = {preserve}\nindent_style = {(tabs ? "tab" : "space")}\nend_of_line = {(ending == "\n" ? "lf" : "crlf")}");
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(input, options, verifyRoundTrip: true, forceRoundTrip: true);
		first.Success.Should().BeTrue(first.Message);
		var indent = tabs ? "\t" : "    ";
		first.Text.Should().Contain(ending + indent + "// Next group" + ending + indent + "// Continuation of the section" + ending);
		var second = formatter.Format(first.Text, options, verifyRoundTrip: true, forceRoundTrip: true);
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(first.Text);
	}
}
