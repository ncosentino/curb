using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nullean.Curb.LayoutRules;

/// <summary>Immutable, indexed layout rules selected for one source file.</summary>
public sealed class LayoutRuleSet
{
	private readonly Dictionary<string, WrapperLayoutRule[]> _byCallee;

	/// <summary>Creates a rule set without IO, assembly loading or semantic analysis.</summary>
	/// <param name="rules">At most 256 rules with unique identifiers.</param>
	/// <exception cref="ArgumentException">The set exceeds its budget or contains duplicate identifiers.</exception>
	public LayoutRuleSet(IEnumerable<WrapperLayoutRule> rules)
	{
		ArgumentNullException.ThrowIfNull(rules);
		var entries = rules.ToArray();
		if (entries.Length > 256)
			throw new ArgumentException("A rule set cannot exceed 256 rules.", nameof(rules));
		var ids = new HashSet<string>(StringComparer.Ordinal);
		var index = new Dictionary<string, List<WrapperLayoutRule>>(StringComparer.Ordinal);
		foreach (var rule in entries)
		{
			ArgumentNullException.ThrowIfNull(rule);
			if (!ids.Add(rule.Id))
				throw new ArgumentException("Layout rule IDs must be unique.", nameof(rules));
			foreach (var name in rule.LeafNames.Distinct(StringComparer.Ordinal))
			{
				if (!index.TryGetValue(name, out var candidates))
					index[name] = candidates = [];
				candidates.Add(rule);
			}
		}
		_byCallee = index.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
		Rules = Array.AsReadOnly(entries);
	}

	/// <summary>The selected rules, in configuration order.</summary>
	public IReadOnlyList<WrapperLayoutRule> Rules { get; }

	internal IReadOnlyList<WrapperLayoutRule> Candidates(ExpressionSyntax callee) =>
		WrapperLayoutRule.LeafName(callee) is { } name && _byCallee.TryGetValue(name, out var rules) ? rules : [];
}
