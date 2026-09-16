using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nullean.Curb.Cleanup.Rules;

internal sealed class UnnecessaryParentheses : ICleanupRule
{
	public string RuleId => "IDE0047";
	public bool NeedsSpan => false;

	public bool TryFix(CleanupContext context, in CleanupDiagnostic diagnostic, TextSpan span, ICollection<PlannedFix> into, out string? refusal)
	{
		var token = context.Root.FindToken(span.Start);
		if (token.SpanStart != span.Start || token.Parent is not ParenthesizedExpressionSyntax node
			|| token != node.OpenParenToken)
		{
			refusal = "the position is not an expression's opening parenthesis";
			return false;
		}

		var expectedEnd = Math.Min(node.Span.End, context.Text.Lines.GetLineFromPosition(span.Start).End);
		if (diagnostic.HasSpan && span.End != expectedEnd)
		{
			refusal = "the reported span does not match the expression's diagnostic location";
			return false;
		}
		if (context.HasConditionalDirectives)
		{
			refusal = "the verdict covers only one conditional-compilation configuration";
			return false;
		}

		while (node.Parent is ParenthesizedExpressionSyntax outer)
			node = outer;

		var chain = new List<ParenthesizedExpressionSyntax>();
		var inner = node;
		while (true)
		{
			chain.Add(inner);
			if (chain.Count > 128)
			{
				refusal = "the parentheses exceed the rewrite nesting budget";
				return false;
			}
			if (inner.Expression is not ParenthesizedExpressionSyntax nested)
				break;
			inner = nested;
		}

		if (node.Parent is ConstantPatternSyntax
			or ArgumentSyntax { Parent: TupleExpressionSyntax }
			or AnonymousObjectMemberDeclaratorSyntax { NameEquals: null }
			|| inner.Expression.DescendantNodesAndSelf().Any(child =>
				child is StackAllocArrayCreationExpressionSyntax or ImplicitStackAllocArrayCreationExpressionSyntax))
		{
			refusal = "removal can affect an inferred name, type or constant-pattern binding";
			return false;
		}

		var count = chain.Count;
		if (!CanRemove(context, node, inner.Expression, chain, count))
		{
			count--;
			if (count == 0 || !CanRemove(context, node, inner, chain, count))
			{
				refusal = "parentheses affect syntax, association or the expression exceeds the verification budget";
				return false;
			}
		}

		for (var index = 0; index < count; index++)
		{
			var pair = chain[index];
			into.Add(PlannedFix.Delete(pair.OpenParenToken.Span, [pair.OpenParenToken.Span]));
			into.Add(PlannedFix.Delete(pair.CloseParenToken.Span, [pair.CloseParenToken.Span]));
		}
		refusal = null;
		return true;
	}

	private static bool CanRemove(
		CleanupContext context,
		ParenthesizedExpressionSyntax outer,
		ExpressionSyntax replacement,
		List<ParenthesizedExpressionSyntax> chain,
		int count)
	{
		var root = ParenthesesSyntax.ExpressionRoot(outer);
		if (!ParenthesesSyntax.WithinBudget(root))
			return false;

		var removed = new List<TextSpan>(count * 2);
		for (var index = 0; index < count; index++)
		{
			removed.Add(chain[index].OpenParenToken.Span);
			removed.Add(chain[index].CloseParenToken.Span);
		}
		removed.Sort(static (left, right) => right.Start.CompareTo(left.Start));
		var text = context.Text.ToString(root.Span);
		foreach (var token in removed)
			text = text.Remove(token.Start - root.SpanStart, token.Length);
		var expected = root == outer ? replacement : root.ReplaceNode(outer, replacement);
		return ParenthesesSyntax.Matches(text, expected);
	}
}
