using Nullean.Curb.LayoutRules;

namespace Nullean.Curb.Cli;

internal readonly record struct ResolvedLayoutRules(LayoutRuleSet? Rules, UInt128 Fingerprint, string? Path, string? BaseDirectory);
