using AwesomeAssertions;
using Nullean.Curb;

namespace Nullean.Curb.Tests;

public class ParsingTests
{
	[Test]
	[Arguments("#error intentional guard\nclass C { }")]
	[Arguments("#define ENABLED\n#if ENABLED\n#error intentional guard\n#endif\nclass C { }")]
	public void Intentional_error_directives_remain_in_the_parsed_tree(string text)
	{
		CSharpSource.TryParse(text, out var source, out var errors).Should().BeTrue();
		errors.Should().BeEmpty();
		source.Root.GetDiagnostics().Should().Contain(diagnostic => diagnostic.Id == "CS1029");
	}

	[Test]
	[Arguments("#error intentional guard\nclass C { void M( }")]
	[Arguments("#error intentional guard\n#endif\nclass C { }")]
	public void An_error_directive_does_not_hide_malformed_syntax(string text)
	{
		CSharpSource.TryParse(text, out _, out var errors).Should().BeFalse();
		errors.Should().NotBeEmpty();
		errors.Should().NotContain(diagnostic => diagnostic.Id == "CS1029");
	}

	[Test]
	public async Task Parses_valid_source()
	{
		var parsed = CSharpSource.TryParse("class C { void M() { } }", out var source, out var errors);

		parsed.Should().BeTrue();
		errors.Should().BeEmpty();
		source.Root.DescendantTokens().Should().NotBeEmpty();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Refuses_source_with_syntax_errors()
	{
		// A formatter must never re-print from a recovered tree: that is how code gets destroyed.
		var parsed = CSharpSource.TryParse("class C { void M( { }", out _, out var errors);

		parsed.Should().BeFalse();
		errors.Should().NotBeEmpty();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Accepts_syntax_newer_than_our_printers()
	{
		// Parsed at LanguageVersion.Preview on purpose — never reject code merely for being new.
		const string source = """
			var point = (X: 1, Y: 2);
			int[] numbers = [1, 2, 3];
			""";

		CSharpSource.TryParse(source, out _, out var errors).Should().BeTrue();
		errors.Should().BeEmpty();
		await Task.CompletedTask;
	}
}
