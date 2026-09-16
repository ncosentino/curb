using AwesomeAssertions;

namespace Nullean.Curb.Tests.Formatting.Expressions;

public class SwitchIndentationTests : FormattingTest
{
	[Test]
	public Task Indented_switch_arms_use_tab_columns() => Formats(
		"""
		class C
		{
			int M(int x)
			{
				return x switch
				{
					1 => 1,
					_ => 0,
				};
			}
		}
		""",
		"""
		class C
			{
			int M(int x)
				{
				return x switch
					{
						1 => 1,
						_ => 0,
						};
				}
			}
		""",
		"csharp_indent_braces = true\nindent_style = tab");

	[Test]
	[Arguments(true, 40)]
	[Arguments(false, 40)]
	[Arguments(true, 120)]
	[Arguments(false, 120)]
	public void Nested_switches_preserve_tokens_and_settle(bool indentBraces, int width)
	{
		const string source = "class C { int M(int x, int y) => x switch { 1 => y switch { 2 => 3, _ => 4 }, _ => 0 }; }";
		var options = TestOptions.Parse($"max_line_length = {width}\ncsharp_indent_braces = {indentBraces}");
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(source, options, verifyRoundTrip: true, forceRoundTrip: true);
		first.Success.Should().BeTrue(first.Message);
		var second = formatter.Format(first.Text!, options, verifyRoundTrip: true, forceRoundTrip: true);
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(first.Text);
	}
}
