using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nullean.Curb.Cleanup.Rules;

internal static class ParenthesesSyntax
{
	private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

	public static ExpressionSyntax ExpressionRoot(ExpressionSyntax expression)
	{
		var root = expression;
		for (var parent = expression.Parent; parent is not null and not StatementSyntax and not MemberDeclarationSyntax; parent = parent.Parent)
		{
			if (parent is ExpressionSyntax enclosing)
				root = enclosing;
		}
		return root;
	}

	public static bool WithinBudget(ExpressionSyntax expression) =>
		!expression.ContainsDirectives
		&& expression.Span.Length <= 32768
		&& expression.DescendantNodes().Take(513).Count() <= 512;

	public static bool Matches(string text, ExpressionSyntax expected)
	{
		var parsed = SyntaxFactory.ParseExpression(text, options: ParseOptions, consumeFullText: true);
		return !parsed.ContainsDiagnostics && SyntaxFactory.AreEquivalent(expected, parsed);
	}
}
