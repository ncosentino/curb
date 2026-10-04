using System.Text;
using AwesomeAssertions;
using Nullean.Curb.Cli;
using Nullean.Curb.LayoutRules;
using Nullean.Curb.Tests.Formatting;

namespace Nullean.Curb.Tests.LayoutRules;

public class WrapperPostfixTests
{
	private static LayoutRuleSet Rules() =>
		new(LayoutRuleCompiler.Compile(Encoding.UTF8.GetBytes(Policy())).Select(definition => definition.Rule));

	private static string Policy() =>
		LayoutRuleSamples.Policy.Replace("\"awaitTokens\": \"preserve\"", "\"awaitTokens\": \"preserve\",\n      \"postfixCalleeSyntax\": [\"ConfigureAwait\", \"WithPolicy\"]", StringComparison.Ordinal);

	[Test]
	[Arguments(false, false)]
	[Arguments(false, true)]
	[Arguments(true, false)]
	[Arguments(true, true)]
	public void Configured_suffixes_keep_the_wrapper_layout_and_original_attachment(bool nested, bool preserve)
	{
		var source = nested
			? LayoutRuleSamples.Source.Replace("return value; }))", "return value; }).ConfigureAwait(false)).ConfigureAwait(true)", StringComparison.Ordinal)
			: LayoutRuleSamples.Source.Replace("return value; }))", "return value; })).ConfigureAwait(false)", StringComparison.Ordinal);
		var expected = nested
			? LayoutRuleSamples.Expected.Replace("}));", "}).ConfigureAwait(false)).ConfigureAwait(true);", StringComparison.Ordinal)
			: LayoutRuleSamples.Expected.Replace("}));", "})).ConfigureAwait(false);", StringComparison.Ordinal);
		if (preserve)
			source = source.Replace("var value=new Value(number); return value;", "var value=new Value(number);\nreturn value;", StringComparison.Ordinal);
		var options = TestOptions.Parse(LayoutRuleSamples.Config + $"\ncsharp_keep_existing_linebreaks = {preserve}");
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(source, options, forceRoundTrip: true, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text!.TrimEnd().Should().Be(expected);
		first.LayoutApplications.Should().ContainSingle().Which.Recipe.Should().Be("vertical-wrapper-chain");
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	public void A_suffix_not_explicitly_configured_retains_ordinary_formatting()
	{
		var source = LayoutRuleSamples.Source.Replace("return value; }))", "return value; })).Other(false)", StringComparison.Ordinal);
		var options = TestOptions.Parse(LayoutRuleSamples.Config);
		using var formatter = new CSharpFormatter();
		var ordinary = formatter.Format(source, options);
		var selected = formatter.Format(source, options, layoutRules: Rules());
		selected.Success.Should().BeTrue(selected.Message);
		selected.Text.Should().Be(ordinary.Text);
		selected.LayoutApplications.Should().BeEmpty();
	}

	[Test]
	public void Omitted_suffix_configuration_preserves_the_original_match_contract()
	{
		var source = LayoutRuleSamples.Source.Replace("return value; }))", "return value; })).ConfigureAwait(false)", StringComparison.Ordinal);
		var options = TestOptions.Parse(LayoutRuleSamples.Config);
		using var formatter = new CSharpFormatter();
		var ordinary = formatter.Format(source, options);
		var selected = formatter.Format(source, options, layoutRules: new LayoutRuleSet([new WrapperLayoutRule("wrappers", ["TraceScope.RunAsync", "Outcome.CaptureAsync"])]));
		selected.Success.Should().BeTrue(selected.Message);
		selected.Text.Should().Be(ordinary.Text);
		selected.LayoutApplications.Should().BeEmpty();
	}

	[Test]
	public void Multiple_generic_and_named_suffix_arguments_are_preserved()
	{
		var source = LayoutRuleSamples.Source.Replace("return value; }))", "return value; })).WithPolicy<int>(mode: policy, count: 2).ConfigureAwait(false)", StringComparison.Ordinal);
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LayoutRuleSamples.Config);
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text!.TrimEnd().Should().Be(LayoutRuleSamples.Expected.Replace("}));", "})).WithPolicy<int>(mode: policy, count: 2).ConfigureAwait(false);", StringComparison.Ordinal));
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	public void Suffix_boundary_comments_fail_without_output()
	{
		var source = LayoutRuleSamples.Source.Replace("return value; }))", "return value; }))./* keep */ConfigureAwait(false)", StringComparison.Ordinal);
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(source, TestOptions.Parse(LayoutRuleSamples.Config), layoutRules: Rules());
		result.Status.Should().Be(FormatStatus.VerificationFailed);
		result.Text.Should().BeNull();
		result.Changed.Should().BeFalse();
		result.Message.Should().Contain("suffix");
	}

	[Test]
	public void Invalid_suffix_spellings_are_rejected()
	{
		Action compile = () => LayoutRuleCompiler.Compile(Encoding.UTF8.GetBytes(Policy().Replace("\"ConfigureAwait\"", "\"Task.ConfigureAwait\"", StringComparison.Ordinal)));
		compile.Should().Throw<LayoutRuleConfigurationException>();
	}

	[Test]
	public void An_oversized_selected_suffix_chain_is_refused()
	{
		var suffixes = string.Concat(Enumerable.Repeat(".ConfigureAwait(false)", 17));
		var source = LayoutRuleSamples.Source.Replace("return value; }))", "return value; }))" + suffixes, StringComparison.Ordinal);
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(source, TestOptions.Parse(LayoutRuleSamples.Config), layoutRules: Rules());
		result.Status.Should().Be(FormatStatus.VerificationFailed);
		result.Text.Should().BeNull();
		result.Message.Should().Contain("suffix-chain budget");
	}

	[Test]
	public void Suffix_budgets_do_not_refuse_unselected_receivers()
	{
		var source = "class C { Task M() => Other.RunAsync(async () => { Call(); })"
			+ string.Concat(Enumerable.Repeat(".ConfigureAwait(false)", 17)) + "; }";
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LayoutRuleSamples.Config);
		var ordinary = formatter.Format(source, options);
		var selected = formatter.Format(source, options, layoutRules: Rules());
		selected.Success.Should().BeTrue(selected.Message);
		selected.Text.Should().Be(ordinary.Text);
		selected.LayoutApplications.Should().BeEmpty();
	}

	[Test]
	public void Differently_wrapped_sources_converge_and_unselected_calls_are_unchanged()
	{
		var source = LayoutRuleSamples.Source.Replace("return value; }))", "return value; })).ConfigureAwait(false)", StringComparison.Ordinal);
		var options = TestOptions.Parse(LayoutRuleSamples.Config);
		using var formatter = new CSharpFormatter();
		var flat = formatter.Format(source, options, layoutRules: Rules());
		var wrapped = formatter.Format(source.Replace(".ConfigureAwait(false)", "\n.ConfigureAwait(\nfalse\n)", StringComparison.Ordinal), options, layoutRules: Rules());
		flat.Success.Should().BeTrue(flat.Message);
		wrapped.Success.Should().BeTrue(wrapped.Message);
		wrapped.Text.Should().Be(flat.Text);
		const string unrelated = "class C { Task M() => Other.RunAsync(async () => { Call(); }).ConfigureAwait(false); }";
		var ordinary = formatter.Format(unrelated, options);
		var selected = formatter.Format(unrelated, options, layoutRules: Rules());
		selected.Success.Should().BeTrue(selected.Message);
		selected.Text.Should().Be(ordinary.Text);
		selected.LayoutApplications.Should().BeEmpty();
	}
}
