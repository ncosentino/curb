using Nullean.Curb.LayoutRules;

namespace Nullean.Curb.Cli;

internal sealed record LayoutRuleDefinition(WrapperLayoutRule Rule, string[] Files);
