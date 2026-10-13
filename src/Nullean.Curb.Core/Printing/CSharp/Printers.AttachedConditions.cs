using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Nullean.Curb.Documents;
using Nullean.Curb.LayoutRules;
using Nullean.Curb.Options;

namespace Nullean.Curb.Printing.CSharp;

internal static partial class Printers
{
	private static bool TryPrintAttachedCondition(IfStatementSyntax node, PrintContext context)
	{
		if (context.LayoutRules?.GeneralConditions is not { Count: > 0 } rules)
			return false;
		var header = TextSpan.FromBounds(node.IfKeyword.SpanStart, node.CloseParenToken.Span.End);
		if (context.IntersectsSuppression(header))
			return false;
		if (rules.Count > 1)
			throw new LayoutRuleException($"Layout rules '{rules[0].Id}' and '{rules[1].Id}' claim the same condition header.");
		var rule = rules[0];
		if (context.Options.ReflowDisabled || context.Options.KeepExistingLinebreaks)
			throw new LayoutRuleException($"Layout rule '{rule.Id}' requires a finite width and deterministic layout.");
		if (context.Options.SpaceAroundBinaryOperators == BinaryOperatorSpacing.Ignore)
			throw new LayoutRuleException($"Layout rule '{rule.Id}' cannot canonicalize ignored binary spacing.");
		if (node.Condition.ContainsDirectives || HasAnyTrivia(node.OpenParenToken)
			|| HasTrailingLambdaContent(node.IfKeyword)
			|| TokenPrinter.HasLeadingContent(node.Condition.GetFirstToken())
			|| HasTrailingLambdaContent(node.Condition.GetLastToken())
			|| TokenPrinter.HasLeadingContent(node.CloseParenToken))
			throw new LayoutRuleException($"Layout rule '{rule.Id}' encountered unsupported condition-boundary trivia.");

		ExpressionSyntax root = node.Condition;
		var budget = 511;
		while (root is ParenthesizedExpressionSyntax or PrefixUnaryExpressionSyntax or AwaitExpressionSyntax)
		{
			if (--budget == 0)
				throw new LayoutRuleException($"Layout rule '{rule.Id}' exceeds the condition-header budget.");
			root = root switch
			{
				ParenthesizedExpressionSyntax parenthesized => parenthesized.Expression,
				PrefixUnaryExpressionSyntax prefix => prefix.Operand,
				AwaitExpressionSyntax awaited => awaited.Expression,
				_ => root,
			};
		}

		var arena = context.Arena;
		var opening = arena.NextGroupId();
		var previousRoot = context.AttachedConditionRoot;
		var previousIndent = context.IndentedCondition;
		context.AttachedConditionRoot = node.Condition;
		context.IndentedCondition = null;
		try
		{
			TokenPrinter.Print(node.IfKeyword, context);
			Spacing.AfterControlFlowKeyword(context);
			TokenPrinter.Print(node.OpenParenToken, context);
			// Measure only the first segment; internal expression groups keep their own decisions.
			using (arena.Group(opening))
			using (arena.Indent())
				Spacing.InsideControlFlowParensBreakable(context);
			using (arena.IndentIfBroken(opening))
			{
				Node.Print(node.Condition, context);
				Spacing.InsideControlFlowParens(context);
				// Keep the delimiter within the expression's lookahead, but restore statement indentation.
				using (arena.IndentIfBroken(opening, -1))
				{
					arena.LineIfBroken(opening, DocFlags.Reindent);
					TokenPrinter.Print(node.CloseParenToken, context);
				}
			}
		}
		finally
		{
			context.AttachedConditionRoot = previousRoot;
			context.IndentedCondition = previousIndent;
		}
		context.AppliedLayout(rule, header);
		return true;
	}

	private static void PrintAttachedPatternGap(PatternSyntax pattern, PrintContext context)
	{
		var arena = context.Arena;
		var gap = arena.NextGroupId();
		using (arena.Group(gap))
		using (arena.Indent())
			arena.Line();
		using (arena.IndentIfBroken(gap))
			Node.Print(pattern, context);
	}

	private static void PrintAttachedPatternChain(BinaryPatternSyntax node, PrintContext context)
	{
		var operands = new List<PatternSyntax>();
		var operators = new List<SyntaxToken>();
		var budget = 511;
		FlattenPattern(node, operands, operators, ref budget);
		for (var i = 0; i < operators.Count; i++)
		{
			if (TokenPrinter.HasAnyContent(operators[i]) || HasTrailingLambdaContent(operands[i].GetLastToken()))
				throw new LayoutRuleException("A selected condition cannot move pattern operators around content trivia.");
		}
		var arena = context.Arena;
		using (arena.Group())
		{
			Node.Print(operands[0], context);
			using (arena.Indent())
			{
				for (var i = 1; i < operands.Count; i++)
				{
					arena.Synthetic(SyntheticText.Space);
					TokenPrinter.Print(operators[i - 1], context);
					arena.Line();
					Node.Print(operands[i], context);
				}
			}
		}
	}

	private static void FlattenPattern(PatternSyntax node, List<PatternSyntax> operands, List<SyntaxToken> operators, ref int budget)
	{
		if (--budget < 0)
			throw new LayoutRuleException("A selected condition pattern exceeds the 511-node budget.");
		if (node is BinaryPatternSyntax binary)
		{
			FlattenPattern(binary.Left, operands, operators, ref budget);
			operators.Add(binary.OperatorToken);
			FlattenPattern(binary.Right, operands, operators, ref budget);
		}
		else
			operands.Add(node);
	}

	private static void PrintAttachedPositionalPattern(PositionalPatternClauseSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		TokenPrinter.Print(node.OpenParenToken, context);
		using (arena.Group())
		{
			using (arena.Indent())
			{
				arena.SoftLine();
				PrintSeparated(node.Subpatterns, context);
			}
			arena.SoftLine();
		}
		TokenPrinter.Print(node.CloseParenToken, context);
	}
}
