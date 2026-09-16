using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nullean.Curb.Cleanup.Rules;

internal sealed class ClarityParentheses : ICleanupRule
{
	public string RuleId => "IDE0048";
	public bool NeedsSpan => false;

	public bool TryFix(CleanupContext context, in CleanupDiagnostic diagnostic, TextSpan span, ICollection<PlannedFix> into, out string? refusal)
	{
		var token = context.Root.FindToken(span.Start);
		if (token.SpanStart != span.Start || token.Parent is not BinaryExpressionSyntax expression
			|| expression.OperatorToken != token || (diagnostic.HasSpan && span.End != token.Span.End))
		{
			refusal = "the diagnostic does not identify a complete binary operator token";
			return false;
		}
		if (context.HasConditionalDirectives)
		{
			refusal = "the verdict covers only one conditional-compilation configuration";
			return false;
		}

		var precedence = Precedence(expression.Kind());
		if (precedence == 0)
		{
			refusal = "the binary expression is outside the supported precedence groups";
			return false;
		}
		while (expression.Parent is BinaryExpressionSyntax parent && Precedence(parent.Kind()) == precedence)
			expression = parent;
		if (expression.Parent is not BinaryExpressionSyntax enclosing || Precedence(enclosing.Kind()) == 0)
		{
			refusal = "the expression has no unparenthesized binary precedence boundary";
			return false;
		}

		var root = ParenthesesSyntax.ExpressionRoot(expression);
		if (!ParenthesesSyntax.WithinBudget(root))
		{
			refusal = "the expression exceeds the syntax-verification budget or contains directives";
			return false;
		}
		var text = context.Text.ToString(root.Span);
		text = text.Insert(expression.Span.End - root.SpanStart, ")").Insert(expression.SpanStart - root.SpanStart, "(");
		var expected = root.ReplaceNode(expression, SyntaxFactory.ParenthesizedExpression(expression));
		if (!ParenthesesSyntax.Matches(text, expected))
		{
			refusal = "the parentheses would change the intended syntax tree";
			return false;
		}

		into.Add(new PlannedFix(new TextSpan(expression.SpanStart, 0), "(", [], ["("]));
		into.Add(new PlannedFix(new TextSpan(expression.Span.End, 0), ")", [], [")"]));
		refusal = null;
		return true;
	}

	private static int Precedence(SyntaxKind kind) => kind switch
	{
		SyntaxKind.CoalesceExpression => 1,
		SyntaxKind.LogicalOrExpression => 2,
		SyntaxKind.LogicalAndExpression => 3,
		SyntaxKind.BitwiseOrExpression => 4,
		SyntaxKind.ExclusiveOrExpression => 5,
		SyntaxKind.BitwiseAndExpression => 6,
		SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression => 7,
		SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
			or SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression
			or SyntaxKind.IsExpression or SyntaxKind.AsExpression => 8,
		SyntaxKind.LeftShiftExpression or SyntaxKind.RightShiftExpression or SyntaxKind.UnsignedRightShiftExpression => 9,
		SyntaxKind.AddExpression or SyntaxKind.SubtractExpression => 10,
		SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression => 11,
		_ => 0,
	};
}
