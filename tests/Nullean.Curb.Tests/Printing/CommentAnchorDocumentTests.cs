using AwesomeAssertions;
using Nullean.Curb.Documents;
using Nullean.Curb.Printing;

namespace Nullean.Curb.Tests.Printing;

public class CommentAnchorDocumentTests
{
	[Test]
	[Arguments(false, false, false)]
	[Arguments(true, false, false)]
	[Arguments(true, false, true)]
	[Arguments(false, true, false)]
	[Arguments(true, true, false)]
	[Arguments(true, true, true)]
	public void Only_line_local_anchors_reset_across_output_blank_lines(bool reset, bool blank, bool tabs)
	{
		const string source = "abcdeXYZ";
		var arena = new DocArena();
		arena.SourceText(0, 5);
		arena.Anchor(0, resetOnBlankLine: reset);
		arena.SourceText(5, 1);
		arena.HardLine();
		if (blank)
			arena.HardLine();
		using (arena.Indent())
		{
			arena.AlignedLine(0);
			arena.SourceText(6, 1);
			arena.HardLine();
			arena.AlignedLine(0);
			arena.SourceText(7, 1);
		}
		var options = new FormatOptions { IndentSize = 4, TabWidth = 4, UseTabs = tabs, EndOfLine = EndOfLine.CrLf, InsertFinalNewLine = false };
		var padding = reset && blank ? (tabs ? "\t" : "    ") : (tabs ? "\t " : "     ");
		DocValidator.Validate(arena, source.Length);
		var expected = "abcdeX\r\n" + (blank ? "\r\n" : "") + padding + "Y\r\n" + padding + "Z";
		DocLayout.Render(arena, source, options).Should().Be(expected);
	}
}
