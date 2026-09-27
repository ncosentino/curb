namespace Nullean.Curb.LayoutRules;

/// <summary>
/// Keeps the continuation lines of a wrapped string concatenation argument at the argument's
/// indentation instead of a hanging continuation indent.
/// </summary>
public sealed class StringConcatenationLayoutRule : LayoutRule
{
	/// <summary>Creates a string-concatenation argument policy.</summary>
	/// <param name="id">A unique, bounded rule identifier.</param>
	/// <exception cref="ArgumentException">The identifier is invalid.</exception>
	public StringConcatenationLayoutRule(string id) : base(id, "argument-string-concatenation") { }
}
