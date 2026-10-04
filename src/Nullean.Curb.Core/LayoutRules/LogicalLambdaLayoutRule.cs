namespace Nullean.Curb.LayoutRules;

/// <summary>An opt-in logical predicate layout for a sole invocation lambda argument.</summary>
public sealed class LogicalLambdaLayoutRule : LayoutRule
{
	/// <summary>Creates a deterministic, width-driven hanging logical-lambda rule.</summary>
	/// <param name="id">A unique, bounded rule identifier.</param>
	/// <exception cref="ArgumentException">The identifier is invalid.</exception>
	public LogicalLambdaLayoutRule(string id) : base(id, "hanging-logical-lambda") { }
}
