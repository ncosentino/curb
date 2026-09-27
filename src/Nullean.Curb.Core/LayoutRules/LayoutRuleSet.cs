using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nullean.Curb.LayoutRules;

/// <summary>Immutable, indexed layout rules selected for one source file.</summary>
public sealed class LayoutRuleSet
{
	private readonly Dictionary<string, WrapperLayoutRule[]> _byCallee;

	/// <summary>Creates a rule set without IO, assembly loading or semantic analysis.</summary>
	/// <param name="rules">At most 256 rules with unique identifiers.</param>
	/// <exception cref="ArgumentException">The set exceeds its budget or contains duplicate identifiers.</exception>
	public LayoutRuleSet(IEnumerable<LayoutRule> rules)
	{
		ArgumentNullException.ThrowIfNull(rules);
		var entries = rules.ToArray();
		if (entries.Length > 256)
			throw new ArgumentException("A rule set cannot exceed 256 rules.", nameof(rules));
		var ids = new HashSet<string>(StringComparer.Ordinal);
		var index = new Dictionary<string, List<WrapperLayoutRule>>(StringComparer.Ordinal);
		var conditions = new List<LogicalConditionLayoutRule>();
		var rawStrings = new List<MultilineRawStringLayoutRule>();
		var stringConcatenations = new List<StringConcatenationLayoutRule>();
		foreach (var rule in entries)
		{
			ArgumentNullException.ThrowIfNull(rule);
			if (!ids.Add(rule.Id))
				throw new ArgumentException("Layout rule IDs must be unique.", nameof(rules));
			if (rule is LogicalConditionLayoutRule condition)
			{
				conditions.Add(condition);
				continue;
			}
			if (rule is MultilineRawStringLayoutRule rawString)
			{
				rawStrings.Add(rawString);
				continue;
			}
			if (rule is StringConcatenationLayoutRule stringConcatenation)
			{
				stringConcatenations.Add(stringConcatenation);
				continue;
			}
			if (rule is not WrapperLayoutRule wrapper)
				throw new ArgumentException("The layout rule kind is unsupported.", nameof(rules));
			foreach (var name in wrapper.LeafNames.Distinct(StringComparer.Ordinal))
			{
				if (!index.TryGetValue(name, out var candidates))
					index[name] = candidates = [];
				candidates.Add(wrapper);
			}
		}
		_byCallee = index.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
		Rules = Array.AsReadOnly(entries);
		Conditions = conditions.AsReadOnly();
		RawStrings = rawStrings.AsReadOnly();
		StringConcatenations = stringConcatenations.AsReadOnly();
	}

	/// <summary>The selected rules, in configuration order.</summary>
	public IReadOnlyList<LayoutRule> Rules { get; }

	internal IReadOnlyList<LogicalConditionLayoutRule> Conditions { get; }

	internal IReadOnlyList<MultilineRawStringLayoutRule> RawStrings { get; }

	internal IReadOnlyList<StringConcatenationLayoutRule> StringConcatenations { get; }

	internal IReadOnlyList<WrapperLayoutRule> Candidates(ExpressionSyntax callee) =>
		WrapperLayoutRule.LeafName(callee) is { } name && _byCallee.TryGetValue(name, out var rules) ? rules : [];
}
