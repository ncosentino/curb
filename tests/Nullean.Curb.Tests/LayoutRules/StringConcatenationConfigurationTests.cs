using System.Text;
using AwesomeAssertions;
using Nullean.Curb.Cli;
using Nullean.Curb.LayoutRules;

namespace Nullean.Curb.Tests.LayoutRules;

public class StringConcatenationConfigurationTests
{
	[Test]
	public void The_rule_compiles_as_data_in_schema_one()
	{
		var definitions = LayoutRuleCompiler.Compile(Encoding.UTF8.GetBytes(StringConcatenationLayoutSamples.Policy));
		definitions.Should().ContainSingle().Which.Rule.Should().BeOfType<StringConcatenationLayoutRule>()
			.Which.Recipe.Should().Be("argument-string-concatenation");
	}

	[Test]
	[Arguments("\"argument\"", "\"expression\"")]
	[Arguments("\"argument-string-concatenation\"", "\"hanging-string-concatenation\"")]
	[Arguments("\"if-long\"", "\"always\"")]
	[Arguments("\"trailing\"", "\"leading\"")]
	[Arguments("\"argument-indent\"", "\"align-first-operand\"")]
	public void Unsupported_values_are_rejected(string before, string after)
	{
		var policy = StringConcatenationLayoutSamples.Policy.Replace(before, after, StringComparison.Ordinal);
		Action compile = () => LayoutRuleCompiler.Compile(Encoding.UTF8.GetBytes(policy));
		compile.Should().Throw<LayoutRuleConfigurationException>();
	}

	[Test]
	public void A_missing_layout_property_is_rejected()
	{
		var policy = StringConcatenationLayoutSamples.Policy.Replace(
			",\n      \"continuation\": \"argument-indent\"", string.Empty, StringComparison.Ordinal);
		policy.Should().NotContain("continuation");
		Action compile = () => LayoutRuleCompiler.Compile(Encoding.UTF8.GetBytes(policy));
		compile.Should().Throw<LayoutRuleConfigurationException>();
	}
}
