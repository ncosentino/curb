using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullean.Curb.Documents;
using Nullean.Curb.LayoutRules;
using Nullean.Curb.Options;

namespace Nullean.Curb.Printing.CSharp;

internal static partial class Printers
{
	private static bool TryPrintLogicalLambdaLayout(ArgumentListSyntax node, PrintContext context)
	{
		if (context.LayoutRules?.LogicalLambdas is not { Count: > 0 } rules
			|| node.Parent is not InvocationExpressionSyntax invocation
			|| node.Arguments.Count != 1
			|| node.Arguments[0].Expression is not LambdaExpressionSyntax lambda
			|| lambda.ExpressionBody is not BinaryExpressionSyntax body
			|| body.Kind() is not (SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression))
			return false;
		if (context.Suppressed is { } suppressed)
		{
			foreach (var span in suppressed)
			{
				if (span.IntersectsWith(node.Span))
					return false;
			}
		}
		if (rules.Count > 1)
			throw new LayoutRuleException($"Layout rules '{rules[0].Id}' and '{rules[1].Id}' claim the same logical lambda argument.");
		var rule = rules[0];
		if (context.Options.ReflowDisabled || context.Options.KeepExistingLinebreaks)
			throw new LayoutRuleException($"Layout rule '{rule.Id}' requires a finite width and deterministic layout.");
		if (context.Options.SpaceAroundBinaryOperators == BinaryOperatorSpacing.Ignore)
			throw new LayoutRuleException($"Layout rule '{rule.Id}' cannot canonicalize ignored binary spacing.");
		var argument = node.Arguments[0];
		if (argument.NameColon is not null || argument.RefKindKeyword.RawKind != 0)
			throw new LayoutRuleException($"Layout rule '{rule.Id}' encountered an unsupported named or ref lambda argument.");
		if (node.ContainsDirectives || HasAnyTrivia(node.OpenParenToken)
			|| HasTrailingLogicalLambdaContent(invocation.Expression.GetLastToken())
			|| TokenPrinter.HasLeadingContent(node.CloseParenToken)
			|| TokenPrinter.HasLeadingContent(body.GetFirstToken())
			|| HasTrailingLogicalLambdaContent(body.GetLastToken())
			|| TokenPrinter.HasAnyContent(lambda.ArrowToken))
			throw new LayoutRuleException($"Layout rule '{rule.Id}' encountered unsupported lambda-boundary trivia.");
		foreach (var token in lambda.DescendantTokens())
		{
			if (token.SpanStart >= body.SpanStart)
				break;
			if (TokenPrinter.HasAnyContent(token))
				throw new LayoutRuleException($"Layout rule '{rule.Id}' encountered unsupported lambda-header trivia.");
		}
		var operands = new List<ExpressionSyntax>();
		var operators = new List<SyntaxToken>();
		Flatten(body, operands, operators, nodeBudget: 511);
		for (var i = 0; i < operators.Count; i++)
		{
			if (TokenPrinter.HasLeadingContent(operators[i]) || HasTrailingLogicalLambdaContent(operands[i].GetLastToken()))
				throw new LayoutRuleException($"Layout rule '{rule.Id}' cannot move a logical break around content trivia.");
		}
		var arena = context.Arena;
		var group = arena.NextGroupId();
		var header = arena.NextGroupId();
		var indentAnchor = arena.NextAnchorId();
		arena.LineIndentAnchor(indentAnchor);
		var previousRoot = context.LogicalLambdaRoot;
		var previousIndent = context.IndentedCondition;
		context.LogicalLambdaRoot = body;
		context.IndentedCondition = null;
		try
		{
			using (arena.IndentToAnchor(indentAnchor))
			using (arena.Group(group))
			{
				using (arena.Group(header))
				{
					TokenPrinter.Print(node.OpenParenToken, context);
					using (arena.Indent())
					{
						Spacing.InsideCallParensBreakable(context);
						if (lambda is SimpleLambdaExpressionSyntax simple)
						{
							PrintModifiers(simple.Modifiers, context);
							Node.Print(simple.Parameter, context);
						}
						else if (lambda is ParenthesizedLambdaExpressionSyntax parenthesized)
						{
							PrintAttributeLists(parenthesized.AttributeLists, context);
							PrintModifiers(parenthesized.Modifiers, context);
							if (parenthesized.ReturnType is not null)
							{
								Node.Print(parenthesized.ReturnType, context);
								arena.Synthetic(SyntheticText.Space);
							}
							Node.Print(parenthesized.ParameterList, context);
						}
						arena.Synthetic(SyntheticText.Space);
						TokenPrinter.Print(lambda.ArrowToken, context);
					}
				}
				using (arena.IndentIfBroken(header))
				using (arena.Indent())
				{
					arena.Line();
					Node.Print(operands[0], context);
					for (var i = 1; i < operands.Count; i++)
					{
						Spacing.BeforeOperator(context);
						TokenPrinter.Print(operators[i - 1], context);
						if (TokenPrinter.HasAnyContent(operators[i - 1]))
							arena.Trim();
						if (context.Options.SpaceAroundBinaryOperators == BinaryOperatorSpacing.BeforeAndAfter)
							arena.Line();
						else
							arena.SoftLine();
						Node.Print(operands[i], context);
					}
					Spacing.InsideCallParens(context);
					TokenPrinter.Print(node.CloseParenToken, context);
				}
			}
		}
		finally
		{
			context.LogicalLambdaRoot = previousRoot;
			context.IndentedCondition = previousIndent;
		}
		context.ArgumentListGroup = group;
		context.AppliedLayout(rule, invocation.Span);
		return true;
	}

	private static bool HasTrailingLogicalLambdaContent(SyntaxToken token)
	{
		foreach (var trivia in token.TrailingTrivia)
		{
			if (trivia.Kind() is not (SyntaxKind.WhitespaceTrivia or SyntaxKind.EndOfLineTrivia))
				return true;
		}
		return false;
	}
}
