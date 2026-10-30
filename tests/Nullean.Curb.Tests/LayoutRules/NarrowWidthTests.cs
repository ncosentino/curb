using AwesomeAssertions;
using Nullean.Curb.Tests.Formatting;

namespace Nullean.Curb.Tests.LayoutRules;

public class NarrowWidthTests
{
	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void Field_types_offer_breaks_at_narrow_width(bool preserve) =>
		AssertWidth("class Widget\n{\n    private readonly Func<IReadOnlyList<Item>, string> _format;\n}", preserve,
			"""
			class Widget
			{
			    private readonly Func<
			        IReadOnlyList<Item>,
			        string
			    > _format;
			}
			""");

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void Tuple_return_types_offer_breaks_at_narrow_width(bool preserve) =>
		AssertWidth("class Widget\n{\n    public (ExecutionOutcome Outcome, IReadOnlyList<string> Calls) Run()\n    {\n        return default;\n    }\n}", preserve,
			"""
			class Widget
			{
			    public (
			        ExecutionOutcome Outcome,
			        IReadOnlyList<string> Calls
			    ) Run()
			    {
			        return default;
			    }
			}
			""");

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void Nested_initializer_types_offer_breaks_at_narrow_width(bool preserve) =>
		AssertWidth("class Widget\n{\n    private readonly IReadOnlyDictionary<string, DisplayStyle> _styles = new System.Collections.ObjectModel.ReadOnlyDictionary<string, DisplayStyle>(new Dictionary<string, DisplayStyle>(StringComparer.Ordinal) { [\"body\"] = new(\"Serif\", \"Black\") });\n}", preserve);

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void Expression_lambda_arguments_stay_within_narrow_width(bool preserve) =>
		AssertWidth("class Widget\n{\n    void Run()\n    {\n        var result = items.Select(item => $\"{item.Name}: \" + item.Amount.ToString(\"0.00\", CultureInfo.InvariantCulture));\n    }\n}", preserve);

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void Nested_generic_parameter_types_can_break_inside_one_parameter(bool preserve) =>
		AssertWidth(
			"""
			class Widget
			{
			    class Items
			    {
			        class Item
			        {
			            void Run(
			                IEnumerable<Func<Widget, string?>> validators
			            )
			            {
			            }
			        }
			    }
			}
			""", preserve);

	private static void AssertWidth(string source, bool preserve, string? expected = null)
	{
		using var formatter = new CSharpFormatter();
		var options = TestOptions.Parse("max_line_length = 60\ncsharp_keep_existing_linebreaks = " + preserve.ToString().ToLowerInvariant());
		var first = formatter.Format(source, options, verifyRoundTrip: true, forceRoundTrip: true);
		first.Success.Should().BeTrue(first.Message);
		first.Text.Should().NotBeNull();
		if (expected is not null)
			first.Text.TrimEnd('\r', '\n').Should().Be(expected);

		first.Text.Split('\n').Should().OnlyContain(line => line.TrimEnd('\r').Length <= 60);
		var second = formatter.Format(first.Text, options, verifyRoundTrip: true, forceRoundTrip: true);
		second.Success.Should().BeTrue(second.Message);
		second.Text.Should().Be(first.Text);
		formatter.RoundTripsChecked.Should().Be(2);
	}
}
