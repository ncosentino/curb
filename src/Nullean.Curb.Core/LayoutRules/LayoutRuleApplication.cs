using Microsoft.CodeAnalysis.Text;

namespace Nullean.Curb.LayoutRules;

/// <summary>A custom layout applied to an original source span.</summary>
/// <param name="RuleId">The configured rule identifier.</param>
/// <param name="Span">The original owner or header span, for explanations rather than suppression.</param>
/// <param name="Recipe">The renderer that owns the reported layout boundaries.</param>
public readonly record struct LayoutRuleApplication(string RuleId, TextSpan Span, string Recipe = "vertical-wrapper-chain");
