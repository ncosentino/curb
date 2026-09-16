using AwesomeAssertions;

namespace Nullean.Curb.Tests.Formatting.Declarations;

public class UnionDeclarationTests : FormattingTest
{
	[Test]
	public Task A_bodyless_union_formats_its_alternatives() => Formats(
		"public union Outcome( Started ,Orphaned );",
		"public union Outcome(Started, Orphaned);");

	[Test]
	public Task A_long_union_reflows_instead_of_passing_through() => Formats(
		"public union Command(ParseCommand, CheckCommand, VerifyCommand, RunCommand, StateCommand, Usage);",
		"""
		public union Command(
		    ParseCommand,
		    CheckCommand,
		    VerifyCommand,
		    RunCommand,
		    StateCommand,
		    Usage
		);
		""",
		"max_line_length = 40");

	[Test]
	public Task A_union_body_reaches_ordinary_member_printers() => Formats(
		"""
		public union Outcome(Started, Orphaned)
		{
		    public readonly int Code=>1 switch
		    {
		        1=>1,
		        _=>0,
		    };
		}
		""",
		"""
		public union Outcome(Started, Orphaned)
		{
		    public readonly int Code => 1 switch
		    {
		        1 => 1,
		        _ => 0,
		    };
		}
		""");

	[Test]
	public Task Preservation_keeps_broken_alternative_lines() => Unchanged(
		"""
		public union Outcome(
		    Started,
		    Orphaned);
		""");

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void Supported_unions_have_complete_printer_coverage(bool tabs)
	{
		const string source = "public union Outcome(Started, Orphaned) { public readonly int Code => 1 switch { 1 => 1, _ => 0 }; }";
		var options = TestOptions.Parse("max_line_length = 40\n" + (tabs ? "indent_style = tab" : "indent_style = space"));
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(source, options, verifyRoundTrip: true, forceRoundTrip: true);
		first.Success.Should().BeTrue(first.Message);
		first.Coverage.Should().Be(1);
		var second = formatter.Format(first.Text!, options, verifyRoundTrip: true, forceRoundTrip: true);
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(first.Text);
	}
}
