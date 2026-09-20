namespace Nullean.Curb.LayoutRules;

/// <summary>Places multiline raw-string openers on their own line without altering literal contents.</summary>
public sealed class MultilineRawStringLayoutRule : LayoutRule
{
	/// <summary>Creates a canonical raw-string boundary policy.</summary>
	/// <param name="id">A unique, bounded rule identifier.</param>
	/// <exception cref="ArgumentException">The identifier is invalid.</exception>
	public MultilineRawStringLayoutRule(string id) : base(id, "standalone-raw-string") { }
}
