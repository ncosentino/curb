namespace Nullean.Curb.LayoutRules;

/// <summary>An opt-in layout for if headers not claimed by specialized condition recipes.</summary>
public sealed class ConditionLayoutRule : LayoutRule
{
	/// <summary>Creates a width-driven rule that prefers breaks within the condition.</summary>
	/// <param name="id">A unique, bounded rule identifier.</param>
	/// <exception cref="ArgumentException">The identifier is invalid.</exception>
	public ConditionLayoutRule(string id) : base(id, "attached-condition") { }
}
