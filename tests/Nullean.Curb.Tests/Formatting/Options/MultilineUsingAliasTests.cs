using AwesomeAssertions;

namespace Nullean.Curb.Tests.Formatting.Options;

public class MultilineUsingAliasTests : FormattingTest
{
	[Test]
	public Task Sorting_accepts_a_wrapped_alias_target() => Formats(
		"""
        using Alias =
            System.Collections.Generic.List<string>;
        using System;

        namespace FormatterRepro;

        public sealed class Sample
        {
            public Alias Values { get; } = [];
        }
        """,
		"""
        using System;

        using Alias = System.Collections.Generic.List<string>;

        namespace FormatterRepro;

        public sealed class Sample
        {
            public Alias Values { get; } = [];
        }
        """,
		"max_line_length = 120\ncsharp_keep_existing_linebreaks = false\ndotnet_sort_system_directives_first = true\ndotnet_separate_import_directive_groups = true");

	[Test]
	[Arguments("\n")]
	[Arguments("\r\n")]
	public void Grouping_an_already_sorted_wrapped_alias_succeeds(string ending)
	{
		const string source = "using System;\nusing Alias =\n    System.Collections.Generic.List<string>;\n\npublic class C { }";
		var options = TestOptions.Parse("dotnet_sort_system_directives_first = true\ndotnet_separate_import_directive_groups = true");
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(source.ReplaceLineEndings(ending), options, verifyRoundTrip: true);
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().Contain("using Alias = System.Collections.Generic.List<string>;");
		formatter.RoundTripsChecked.Should().Be(1);
		var second = formatter.Format(first.Text, options, verifyRoundTrip: true);
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(first.Text);
	}
}
