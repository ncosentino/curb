using Microsoft.CodeAnalysis.Text;

namespace Nullean.Curb.LayoutRules;

/// <summary>A custom layout applied to an original source span.</summary>
/// <param name="RuleId">The configured rule identifier.</param>
/// <param name="Span">The original method span, for explanations rather than suppression.</param>
public readonly record struct LayoutRuleApplication(string RuleId, TextSpan Span);
