using AwesomeAssertions;
using Nullean.Curb.Verification;

namespace Nullean.Curb.Tests.Verification;

public class UsingDirectiveVerificationTests
{
	private static bool Verify(string source, string output)
	{
		CSharpSource.TryParse(source, out var parsed, out _).Should().BeTrue();
		return TokenStreamComparer.Verify(parsed.Root, source, output, output, out _, usingsReordered: true);
	}

	[Test]
	public void Layout_trivia_can_change_without_changing_directive_content()
	{
		const string source = "global using unsafe Alias =\r\n    int*;\r\nusing static\n System.Math;\nusing Pair =\n (int Left, int Right);";
		const string output = "global using unsafe Alias=int*;\nusing Pair=(int Left,int Right);\nusing static System.Math;";
		Verify(source, output).Should().BeTrue();
	}

	[Test]
	public void Interior_comments_remain_with_their_directive()
	{
		const string source = "using B = /* keep */\n System.Text.StringBuilder;\nusing A = System.String;";
		const string output = "using A=System.String;\nusing B=/* keep */System.Text.StringBuilder;";
		Verify(source, output).Should().BeTrue();
		Verify(source, output.Replace("/* keep */", "/* peek */", StringComparison.Ordinal)).Should().BeFalse();
		Verify(source, output.Replace("/* keep */", "", StringComparison.Ordinal)).Should().BeFalse();
	}

	[Test]
	[Arguments("using Alias = System.String;", "using Renamed = System.String;")]
	[Arguments("using Alias = A.BC;", "using Alias = AB.C;")]
	[Arguments("global using Alias = System.String;", "using Alias = System.String;")]
	[Arguments("using unsafe Alias = int*;", "using Alias = int*;")]
	[Arguments("using static X;", "using staticX;")]
	[Arguments("using @Alias = System.String;", "using Alias = System.String;")]
	[Arguments("using Alias = System.String; using Other = System.Int32;", "using Alias = System.String;")]
	[Arguments("using Alias = System.String; using Other = System.Int32;", "using Alias = System.String; using Alias = System.String;")]
	public void A_reordered_region_does_not_excuse_token_changes(string source, string output) =>
		Verify(source, output).Should().BeFalse();

	[Test]
	public void Tokens_outside_the_using_region_stay_protected()
	{
		const string source = "using B = System.Int32; using A = System.String; class C { int Value; }";
		const string output = "using A=System.String; using B=System.Int32; class C { int Changed; }";
		Verify(source, output).Should().BeFalse();
	}

	[Test]
	public void Invalid_complex_element_commas_are_rejected_even_with_a_declared_comma_delta()
	{
		const string source = "class C { object Values = new D { { 1, 2 } }; }";
		const string output = "class C { object Values = new D { { 1, 2, }, }; }";
		CSharpSource.TryParse(source, out var parsed, out _).Should().BeTrue();
		TokenStreamComparer.Verify(parsed.Root, source, output, output, out _, trailingCommas: true).Should().BeFalse();
	}
}
