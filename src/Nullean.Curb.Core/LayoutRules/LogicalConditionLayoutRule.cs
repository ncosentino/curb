namespace Nullean.Curb.LayoutRules;

/// <summary>An opt-in, width-driven layout for logical chains in if headers.</summary>
public sealed class LogicalConditionLayoutRule : LayoutRule
{
	/// <summary>Creates a rule that keeps the first operand beside the opening parenthesis.</summary>
	/// <param name="id">A unique, bounded rule identifier.</param>
	/// <exception cref="ArgumentException">The identifier is invalid.</exception>
	public LogicalConditionLayoutRule(string id) : base(id, "hanging-logical-condition") { }
}
