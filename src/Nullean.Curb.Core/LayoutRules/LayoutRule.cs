namespace Nullean.Curb.LayoutRules;

/// <summary>The closed, data-only family of supported syntax layout rules.</summary>
public abstract class LayoutRule
{
	private protected LayoutRule(string id, string recipe)
	{
		if (string.IsNullOrWhiteSpace(id) || id.Length > 80)
			throw new ArgumentException("A layout rule ID must contain 1 to 80 characters.", nameof(id));
		foreach (var character in id)
		{
			if (!char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_' or '.'))
				throw new ArgumentException("A layout rule ID contains an unsupported character.", nameof(id));
		}
		Id = id;
		Recipe = recipe;
	}

	/// <summary>The configured identifier reported for matches and conflicts.</summary>
	public string Id { get; }

	/// <summary>The supported rendering recipe used by this rule.</summary>
	public string Recipe { get; }
}
