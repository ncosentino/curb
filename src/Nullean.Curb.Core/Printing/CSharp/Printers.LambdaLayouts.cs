using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullean.Curb.Documents;
using Nullean.Curb.LayoutRules;
using Nullean.Curb.Options;

namespace Nullean.Curb.Printing.CSharp;

internal static partial class Printers
{
	private readonly record struct LambdaLayoutCapture(InvocationExpressionSyntax Invocation, LambdaExpressionSyntax Lambda, ExpressionSyntax Body, LayoutRule Rule);

	private static bool TryCaptureLambdaLayout(ArgumentListSyntax node, PrintContext context, out LambdaLayoutCapture capture)
	{
		capture = default;
		if (context.LayoutRules is not { } layoutRules
			|| (layoutRules.LogicalLambdas.Count == 0 && layoutRules.ExpressionLambdas.Count == 0)
			|| node.Parent is not InvocationExpressionSyntax invocation
			|| node.Arguments.Count != 1
			|| node.Arguments[0].Expression is not LambdaExpressionSyntax lambda
			|| lambda.ExpressionBody is not { } body)
			return false;
		IReadOnlyList<LayoutRule> rules = IsLogicalLambdaBody(body) ? layoutRules.LogicalLambdas : layoutRules.ExpressionLambdas;
		if (rules.Count == 0)
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
			throw new LayoutRuleException($"Layout rules '{rules[0].Id}' and '{rules[1].Id}' claim the same {(IsLogicalLambdaBody(body) ? "logical" : "expression")} lambda argument.");
		capture = new LambdaLayoutCapture(invocation, lambda, body, rules[0]);
		return true;
	}

	private static bool IsLogicalLambdaBody(ExpressionSyntax body) =>
		body.Kind() is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression;

	private static bool TryPrintLambdaArgumentLayout(ArgumentListSyntax node, PrintContext context)
	{
		if (!TryCaptureLambdaLayout(node, context, out var capture))
			return false;
		var (invocation, lambda, body, rule) = capture;
		if (context.Options.ReflowDisabled || context.Options.KeepExistingLinebreaks)
			throw new LayoutRuleException($"Layout rule '{rule.Id}' requires a finite width and deterministic layout.");
		if (context.Options.SpaceAroundBinaryOperators == BinaryOperatorSpacing.Ignore)
			throw new LayoutRuleException($"Layout rule '{rule.Id}' cannot canonicalize ignored binary spacing.");
		var argument = node.Arguments[0];
		if (argument.NameColon is not null || argument.RefKindKeyword.RawKind != 0)
			throw new LayoutRuleException($"Layout rule '{rule.Id}' encountered an unsupported named or ref lambda argument.");
		if (node.ContainsDirectives || HasAnyTrivia(node.OpenParenToken)
			|| HasTrailingLambdaContent(invocation.Expression.GetLastToken())
			|| TokenPrinter.HasLeadingContent(node.CloseParenToken)
			|| TokenPrinter.HasLeadingContent(body.GetFirstToken())
			|| HasTrailingLambdaContent(body.GetLastToken())
			|| TokenPrinter.HasAnyContent(lambda.ArrowToken))
			throw new LayoutRuleException($"Layout rule '{rule.Id}' encountered unsupported lambda-boundary trivia.");
		foreach (var token in lambda.DescendantTokens())
		{
			if (token.SpanStart >= body.SpanStart)
				break;
			if (TokenPrinter.HasAnyContent(token))
				throw new LayoutRuleException($"Layout rule '{rule.Id}' encountered unsupported lambda-header trivia.");
		}
		List<ExpressionSyntax>? operands = null;
		List<SyntaxToken>? operators = null;
		if (rule is LogicalLambdaLayoutRule && body is BinaryExpressionSyntax logicalBody)
		{
			operands = [];
			operators = [];
			Flatten(logicalBody, operands, operators, nodeBudget: 511);
			for (var i = 0; i < operators.Count; i++)
			{
				if (TokenPrinter.HasLeadingContent(operators[i]) || HasTrailingLambdaContent(operands[i].GetLastToken()))
					throw new LayoutRuleException($"Layout rule '{rule.Id}' cannot move a logical break around content trivia.");
			}
		}
		var arena = context.Arena;
		var group = arena.NextGroupId();
		var header = arena.NextGroupId();
		var indentAnchor = arena.NextAnchorId();
		arena.LineIndentAnchor(indentAnchor);
		var previousRoot = context.LogicalLambdaRoot;
		var previousIndent = context.IndentedCondition;
		context.LogicalLambdaRoot = operands is not null ? body : null;
		context.IndentedCondition = null;
		try
		{
			using (arena.Group(group))
			using (arena.IndentToAnchor(indentAnchor))
			{
				PrintLambdaLayoutHeader(node, lambda, header, context);
				using (arena.IndentIfBroken(header))
				{
					if (operands is not null && operators is not null)
					{
						using (arena.Indent())
						{
							arena.Line();
							PrintLogicalLambdaOperands(operands, operators, context);
							PrintLambdaLayoutClose(node, context);
						}
					}
					else if (LambdaBodyHasOwnBreaks(body))
					{
						// Measure the body prefix up to its first break without flattening its internal groups.
						var bodyHeader = arena.NextGroupId();
						using (arena.Group(bodyHeader))
						using (arena.Indent())
							arena.Line();
						using (arena.IndentIfBroken(bodyHeader))
							Node.Print(body, context);
						PrintLambdaLayoutClose(node, context);
					}
					else
					{
						using (arena.Indent())
						{
							arena.Line();
							Node.Print(body, context);
							PrintLambdaLayoutClose(node, context);
						}
					}
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

	private static void PrintLambdaLayoutHeader(ArgumentListSyntax node, LambdaExpressionSyntax lambda, ushort header, PrintContext context)
	{
		var arena = context.Arena;
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
					PrintInlineAttributeLists(parenthesized.AttributeLists, context);
					PrintModifiers(parenthesized.Modifiers, context);
					if (parenthesized.ReturnType is not null)
					{
						Node.Print(parenthesized.ReturnType, context);
						arena.Synthetic(SyntheticText.Space);
					}
					ParameterList(parenthesized.ParameterList, context, widthDriven: true);
				}
				arena.Synthetic(SyntheticText.Space);
				TokenPrinter.Print(lambda.ArrowToken, context);
			}
		}
	}

	private static void PrintLogicalLambdaOperands(List<ExpressionSyntax> operands, List<SyntaxToken> operators, PrintContext context)
	{
		var arena = context.Arena;
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
	}

	private static void PrintLambdaLayoutClose(ArgumentListSyntax node, PrintContext context)
	{
		Spacing.InsideCallParens(context);
		TokenPrinter.Print(node.CloseParenToken, context);
	}

	private static bool LambdaBodyHasOwnBreaks(ExpressionSyntax body) =>
		body switch
		{
			AwaitExpressionSyntax awaited => LambdaBodyHasOwnBreaks(awaited.Expression),
			InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0 } => false,
			MemberAccessExpressionSyntax => false,
			ObjectCreationExpressionSyntax { ArgumentList: null or { Arguments.Count: 0 }, Initializer: null } => false,
			ImplicitObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 0, Initializer: null } => false,
			_ => BreaksWithoutHelp(body),
		};

	private static bool TryPrintLambdaCondition(IfStatementSyntax node, PrintContext context)
	{
		if (context.LayoutRules is not { } rules
			|| (rules.LogicalLambdas.Count == 0 && rules.ExpressionLambdas.Count == 0))
			return false;
		ExpressionSyntax expression = node.Condition;
		var budget = 511;
		while (expression is ParenthesizedExpressionSyntax or PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression })
		{
			if (--budget == 0)
				throw new LayoutRuleException("A selected lambda condition exceeds the header budget.");
			expression = expression is ParenthesizedExpressionSyntax parenthesized ? parenthesized.Expression : ((PrefixUnaryExpressionSyntax)expression).Operand;
		}
		if (expression is not InvocationExpressionSyntax invocation
			|| !TryCaptureLambdaLayout(invocation.ArgumentList, context, out var capture))
			return false;
		if (node.Condition.ContainsDirectives || HasAnyTrivia(node.OpenParenToken)
			|| HasTrailingLambdaContent(node.IfKeyword)
			|| HasTrailingLambdaContent(node.Condition.GetLastToken())
			|| TokenPrinter.HasLeadingContent(node.CloseParenToken))
			throw new LayoutRuleException($"Layout rule '{capture.Rule.Id}' encountered unsupported condition-boundary trivia.");
		TokenPrinter.Print(node.IfKeyword, context);
		Spacing.AfterControlFlowKeyword(context);
		TokenPrinter.Print(node.OpenParenToken, context);
		Spacing.InsideControlFlowParens(context);
		Node.Print(node.Condition, context);
		Spacing.InsideControlFlowParens(context);
		TokenPrinter.Print(node.CloseParenToken, context);
		return true;
	}

	private static bool HasTrailingLambdaContent(SyntaxToken token)
	{
		foreach (var trivia in token.TrailingTrivia)
		{
			if (trivia.Kind() is not (SyntaxKind.WhitespaceTrivia or SyntaxKind.EndOfLineTrivia))
				return true;
		}
		return false;
	}
}
