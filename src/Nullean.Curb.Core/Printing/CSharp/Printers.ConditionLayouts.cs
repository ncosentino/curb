using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Nullean.Curb.Documents;
using Nullean.Curb.LayoutRules;
using Nullean.Curb.Options;

namespace Nullean.Curb.Printing.CSharp;

internal static partial class Printers
{
	private static bool TryPrintConditionLayout(IfStatementSyntax node, PrintContext context)
	{
		if (context.LayoutRules?.Conditions is not { Count: > 0 } rules
			|| node.Condition is not BinaryExpressionSyntax condition
			|| condition.Kind() is not (SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression))
			return false;

		var header = TextSpan.FromBounds(node.IfKeyword.SpanStart, node.CloseParenToken.Span.End);
		if (context.Suppressed is { } suppressed)
		{
			foreach (var span in suppressed)
			{
				if (span.IntersectsWith(header))
					return false;
			}
		}
		if (rules.Count > 1)
			throw new LayoutRuleException($"Layout rules '{rules[0].Id}' and '{rules[1].Id}' claim the same condition header.");
		var rule = rules[0];
		if (context.Options.ReflowDisabled || context.Options.KeepExistingLinebreaks)
			throw new LayoutRuleException($"Layout rule '{rule.Id}' requires a finite width and deterministic layout.");
		if (context.Options.SpaceAroundBinaryOperators == BinaryOperatorSpacing.Ignore)
			throw new LayoutRuleException($"Layout rule '{rule.Id}' cannot canonicalize a condition with ignored binary spacing.");
		if (condition.ContainsDirectives || HasAnyTrivia(node.OpenParenToken)
			|| TokenPrinter.HasLeadingContent(condition.GetFirstToken())
			|| TokenPrinter.HasLeadingContent(node.CloseParenToken))
			throw new LayoutRuleException($"Layout rule '{rule.Id}' encountered unsupported condition-boundary trivia.");
		foreach (var trivia in node.IfKeyword.TrailingTrivia)
		{
			if (trivia.Kind() is not (SyntaxKind.WhitespaceTrivia or SyntaxKind.EndOfLineTrivia))
				throw new LayoutRuleException($"Layout rule '{rule.Id}' encountered control-keyword trivia.");
		}

		var operands = new List<ExpressionSyntax>();
		var operators = new List<SyntaxToken>();
		Flatten(condition, operands, operators, nodeBudget: 511);
		for (var i = 0; i < operators.Count; i++)
		{
			if (TokenPrinter.HasLeadingContent(operators[i]) || EndsWithLineComment(operands[i].GetLastToken()))
				throw new LayoutRuleException($"Layout rule '{rule.Id}' cannot move a logical break around content trivia.");
		}

		var arena = context.Arena;
		TokenPrinter.Print(node.IfKeyword, context);
		Spacing.AfterControlFlowKeyword(context);
		TokenPrinter.Print(node.OpenParenToken, context);
		var group = arena.NextGroupId();
		var anchor = arena.NextAnchorId();
		var previousRoot = context.LogicalConditionRoot;
		var previousIndent = context.IndentedCondition;
		context.LogicalConditionRoot = condition;
		context.IndentedCondition = null;
		try
		{
			using (arena.Group(group))
			{
				Spacing.InsideControlFlowParens(context);
				arena.Anchor(anchor);
				Node.Print(operands[0], context);
				for (var i = 1; i < operands.Count; i++)
				{
					Spacing.BeforeOperator(context);
					TokenPrinter.Print(operators[i - 1], context);
					var hasComment = TokenPrinter.HasAnyContent(operators[i - 1]);
					if (hasComment)
						arena.Trim();
					arena.AlignedBreakOpportunity(anchor, hasComment || context.Options.SpaceAroundBinaryOperators == BinaryOperatorSpacing.BeforeAndAfter);
					Node.Print(operands[i], context);
				}
				using (var choice = arena.IfBreak(group))
				{
					using (choice.Branch())
						Spacing.InsideControlFlowParens(context);
					using (choice.Branch())
					{ }
				}
				arena.LineIfBroken(group, DocFlags.Reindent);
				TokenPrinter.Print(node.CloseParenToken, context);
			}
		}
		finally
		{
			context.LogicalConditionRoot = previousRoot;
			context.IndentedCondition = previousIndent;
		}
		context.AppliedLayout(rule, header);
		return true;
	}
}
