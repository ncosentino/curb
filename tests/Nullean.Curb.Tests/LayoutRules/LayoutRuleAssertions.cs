using AwesomeAssertions;
using Nullean.Curb.LayoutRules;

namespace Nullean.Curb.Tests.LayoutRules;

internal static class LayoutRuleAssertions
{
	internal static void AssertLayout(string source, string expected, FormatOptions options, LayoutRuleSet? rules, bool expectApplications = true)
	{
		using var formatter = new CSharpFormatter();
		var first = formatter.Format(source, options, verifyRoundTrip: true, forceRoundTrip: true, layoutRules: rules);
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().NotBeNull();
		first.Text.TrimEnd('\r', '\n').Should().Be(expected);
		if (expectApplications)
			first.LayoutApplications.Should().NotBeEmpty();
		else
			first.LayoutApplications.Should().BeEmpty();
		var second = formatter.Format(first.Text, options, verifyRoundTrip: true, forceRoundTrip: true, layoutRules: rules);
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(first.Text);
		formatter.RoundTripsChecked.Should().Be(2);
	}
}
