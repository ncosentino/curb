using AwesomeAssertions;
using Nullean.Curb.LayoutRules;
using Nullean.Curb.Tests.Formatting;

namespace Nullean.Curb.Tests.LayoutRules;

public class LayoutRuleTests
{
	private static LayoutRuleSet Rules(string id = "wrappers") =>
		new([new WrapperLayoutRule(id, ["TraceScope.RunAsync", "Outcome.CaptureAsync", "Outcome.Capture"])]);

	[Test]
	[Arguments(false, "\n")]
	[Arguments(true, "\n")]
	[Arguments(false, "\r\n")]
	[Arguments(true, "\r\n")]
	public void A_matching_method_is_canonical_and_its_body_is_formatted(bool tabs, string ending)
	{
		var source = LayoutRuleSamples.Source.ReplaceLineEndings(ending);
		var expected = (tabs ? LayoutRuleSamples.Expected.Replace("    ", "\t", StringComparison.Ordinal) : LayoutRuleSamples.Expected).ReplaceLineEndings(ending) + ending;
		var options = TestOptions.Parse(LayoutRuleSamples.Config + $"\nindent_style = {(tabs ? "tab" : "space")}\nend_of_line = {(ending == "\n" ? "lf" : "crlf")}");
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().Be(expected);
		first.LayoutApplications.Should().ContainSingle().Which.RuleId.Should().Be("wrappers");
		formatter.RoundTripsChecked.Should().Be(1);
		var second = formatter.Format(first.Text, options, layoutRules: Rules());
		second.Success.Should().BeTrue(second.Message);
		second.Changed.Should().BeFalse();
		second.Text.Should().Be(first.Text);
	}

	[Test]
	public void Unmatched_names_keep_the_default_output()
	{
		var options = TestOptions.Parse(LayoutRuleSamples.Config);
		using var formatter = new CSharpFormatter();
		var ordinary = formatter.Format(LayoutRuleSamples.Source, options);
		var unmatched = formatter.Format(LayoutRuleSamples.Source, options, layoutRules: new LayoutRuleSet([new WrapperLayoutRule("other", ["Other.Run"])]));
		unmatched.Success.Should().BeTrue(unmatched.Message);
		unmatched.Text.Should().Be(ordinary.Text);
		unmatched.LayoutApplications.Should().BeEmpty();
	}

	[Test]
	public void Conflicting_rules_fail_without_output()
	{
		var rules = new LayoutRuleSet([new WrapperLayoutRule("one", ["TraceScope.RunAsync", "Outcome.CaptureAsync"]), new WrapperLayoutRule("two", ["TraceScope.RunAsync", "Outcome.CaptureAsync"])]);
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(LayoutRuleSamples.Source, TestOptions.Parse(LayoutRuleSamples.Config), layoutRules: rules);
		result.Status.Should().Be(FormatStatus.VerificationFailed);
		result.Text.Should().BeNull();
		result.Changed.Should().BeFalse();
		result.Message.Should().Contain("claim the same method");
	}

	[Test]
	public void A_spine_comment_is_refused_but_body_comments_survive()
	{
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LayoutRuleSamples.Config);
		var refused = formatter.Format(LayoutRuleSamples.Source.Replace("TraceScope.RunAsync", "TraceScope./* keep */RunAsync", StringComparison.Ordinal), options, layoutRules: Rules());
		refused.Status.Should().Be(FormatStatus.VerificationFailed);
		refused.Text.Should().BeNull();
		var accepted = formatter.Format(LayoutRuleSamples.Source.Replace("var value=", "/* body */ var value=", StringComparison.Ordinal), options, layoutRules: Rules());
		accepted.Success.Should().BeTrue(accepted.Message);
		accepted.Text.Should().Contain("/* body */");
	}

	[Test]
	public void Generic_calls_and_arguments_before_the_lambda_are_preserved()
	{
		var source = LayoutRuleSamples.Source.Replace("Outcome.CaptureAsync(async", "Outcome.CaptureAsync<Value>(context, async", StringComparison.Ordinal);
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(source, TestOptions.Parse(LayoutRuleSamples.Config), layoutRules: Rules());
		result.Success.Should().BeTrue(result.Message);
		result.Text.Should().Contain("    Outcome.CaptureAsync<Value>(context, async () =>");
		formatter.Format(result.Text, TestOptions.Parse(LayoutRuleSamples.Config), layoutRules: Rules()).Text.Should().Be(result.Text);
	}

	[Test]
	public void Existing_awaits_inside_the_chain_are_not_invented_or_removed()
	{
		var source = LayoutRuleSamples.Source.Replace("() => Outcome", "() => await Outcome", StringComparison.Ordinal);
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(source, TestOptions.Parse(LayoutRuleSamples.Config), layoutRules: Rules());
		result.Success.Should().BeTrue(result.Message);
		result.Text.Should().Contain("TraceScope.RunAsync(async () => await\n    Outcome.CaptureAsync");
	}

	[Test]
	public void Block_to_expression_conversion_composes_with_the_layout()
	{
		const string source = "public class C { public Task<Result<Value>> GetAsync() { return TraceScope.RunAsync(async () => Outcome.CaptureAsync(async () => { return new Value(); })); } }";
		var options = TestOptions.Parse(LayoutRuleSamples.Config + "\ncsharp_style_expression_bodied_methods = true");
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.LayoutApplications.Should().ContainSingle();
		var second = formatter.Format(first.Text!, options, layoutRules: Rules());
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(first.Text);
	}

	[Test]
	public void Nested_builtin_rewrites_keep_source_ordered_token_allowances()
	{
		const string source = "public class C { public Task<Result<int>> GetAsync() { return TraceScope.RunAsync(async () => Outcome.CaptureAsync(async () => { int Inner() { return 1; } return Inner(); })); } }";
		var options = TestOptions.Parse(LayoutRuleSamples.Config + "\ncsharp_style_expression_bodied_methods = true\ncsharp_style_expression_bodied_local_functions = true");
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().Contain("Inner() => 1;");
		var second = formatter.Format(first.Text, options, layoutRules: Rules());
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(first.Text);
	}

	[Test]
	[Arguments(true)]
	[Arguments(false)]
	public void Preservation_and_deterministic_modes_share_the_owned_wrapper_layout(bool preserve)
	{
		var options = TestOptions.Parse(LayoutRuleSamples.Config + $"\ncsharp_keep_existing_linebreaks = {preserve}");
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(LayoutRuleSamples.Expected, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		first.Text!.TrimEnd().Should().Be(LayoutRuleSamples.Expected);
		formatter.Format(first.Text, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	public void Body_indentation_is_owned_without_overriding_nested_formatting_options()
	{
		const string source = "class C { Task M() => TraceScope.RunAsync(async () => { if (ok) { Call(); } }); }";
		var options = TestOptions.Parse(LayoutRuleSamples.Config + "\ncsharp_indent_braces = true\ncsharp_indent_block_contents = false");
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(source, options, layoutRules: Rules());
		first.Success.Should().BeTrue(first.Message);
		formatter.Format(first.Text!, options, layoutRules: Rules()).Text.Should().Be(first.Text);
	}

	[Test]
	[Arguments("TraceScope.RunAsync(x => { Call(); })")]
	[Arguments("TraceScope.RunAsync(callback)")]
	[Arguments("OtherScope.RunAsync(async () => { Call(); })")]
	public void Nonmatching_callback_shapes_retain_ordinary_formatting(string expression)
	{
		var source = $"class C {{ Task M() => {expression}; }}";
		var options = TestOptions.Parse(LayoutRuleSamples.Config);
		using var formatter = new CSharpFormatter();
		var expected = formatter.Format(source, options);
		var actual = formatter.Format(source, options, layoutRules: Rules());
		actual.Success.Should().BeTrue(actual.Message);
		actual.Text.Should().Be(expected.Text);
		actual.LayoutApplications.Should().BeEmpty();
	}

	[Test]
	public void Existing_source_suppression_takes_precedence()
	{
		var source = "#pragma warning disable IDE0055\n" + LayoutRuleSamples.Source + "\n#pragma warning restore IDE0055\n";
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(source, TestOptions.Parse(LayoutRuleSamples.Config), layoutRules: Rules());
		result.Success.Should().BeTrue(result.Message);
		result.LayoutApplications.Should().BeEmpty();
		result.Text.Should().Contain(LayoutRuleSamples.Source);
	}

	[Test]
	public void Body_directives_and_disabled_text_are_preserved()
	{
		var source = LayoutRuleSamples.Source.Replace("var value=new Value(number); return value;", "\n#if NEVER\nnot active C#\n#endif\nvar value=new Value(number); return value;\n", StringComparison.Ordinal);
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LayoutRuleSamples.Config);
		var result = formatter.Format(source, options, layoutRules: Rules());
		result.Success.Should().BeTrue(result.Message);
		result.Text.Should().Contain("not active C#");
		formatter.Format(result.Text, options, layoutRules: Rules()).Text.Should().Be(result.Text);
	}

	[Test]
	public void A_long_wrapper_header_has_a_deterministic_secondary_layout()
	{
		var source = LayoutRuleSamples.Source.Replace("Outcome.CaptureAsync(async", "Outcome.CaptureAsync(longContextArgument, anotherContextArgument, async", StringComparison.Ordinal);
		var options = TestOptions.Parse(LayoutRuleSamples.Config + "\nmax_line_length = 60");
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(source, options, layoutRules: Rules());
		result.Success.Should().BeTrue(result.Message);
		result.Text.Should().Contain("Outcome.CaptureAsync(\n");
		formatter.Format(result.Text, options, layoutRules: Rules()).Text.Should().Be(result.Text);
	}

	[Test]
	public void Check_shaped_calls_reparse_and_reused_formatters_do_not_retain_rule_matches()
	{
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse(LayoutRuleSamples.Config);
		var custom = formatter.Format(LayoutRuleSamples.Source, options, produceText: false, layoutRules: Rules());
		custom.Success.Should().BeTrue(custom.Message);
		custom.Text.Should().BeNull();
		custom.LayoutApplications.Should().ContainSingle();
		formatter.RoundTripsChecked.Should().Be(1);
		var plain = formatter.Format("class C { }", options);
		plain.Success.Should().BeTrue(plain.Message);
		plain.LayoutApplications.Should().BeEmpty();
		formatter.RoundTripsChecked.Should().Be(1);
	}
}
