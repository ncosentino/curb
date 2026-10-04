using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Nullean.Curb.LayoutRules;

namespace Nullean.Curb.Printing.CSharp;

internal static partial class Printers
{
	internal static bool TryPrintRawStringLayout(SyntaxNode node, PrintContext context)
	{
		if (context.LayoutRules?.RawStrings is not { Count: > 0 } rules)
			return false;
		var utf8 = false;
		switch (node)
		{
			case LiteralExpressionSyntax literal when literal.Token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken):
				break;
			case LiteralExpressionSyntax literal when literal.Token.IsKind(SyntaxKind.Utf8MultiLineRawStringLiteralToken):
				utf8 = true;
				break;
			case InterpolatedStringExpressionSyntax interpolated when interpolated.StringStartToken.IsKind(SyntaxKind.InterpolatedMultiLineRawStringStartToken):
				break;
			default:
				return false;
		}
		if (context.Suppressed is { } suppressed)
		{
			foreach (var span in suppressed)
			{
				if (span.IntersectsWith(node.FullSpan))
					return false;
			}
		}
		if (rules.Count > 1)
			throw new LayoutRuleException($"Layout rules '{rules[0].Id}' and '{rules[1].Id}' claim the same raw-string boundary.");
		var rule = rules[0];
		if (StartsOwnedLogicalOperand(node, context))
			throw new LayoutRuleException($"Layout rule '{rule.Id}' overlaps a custom logical-operand boundary.");

		var closingStart = node.Span.End - (utf8 ? 2 : 0);
		var closingEnd = closingStart;
		while (closingStart > node.SpanStart && context.Text[closingStart - 1] == '"')
			closingStart--;
		if (closingEnd - closingStart < 3)
			throw new LayoutRuleException($"Layout rule '{rule.Id}' could not locate a raw-string closing delimiter.");
		var line = context.Text.Lines.GetLineFromPosition(closingStart);
		for (var i = line.Start; i < closingStart; i++)
		{
			if (!char.IsWhiteSpace(context.Text[i]))
				throw new LayoutRuleException($"Layout rule '{rule.Id}' encountered unsupported closing-delimiter indentation.");
		}
		var indentation = TextSpan.FromBounds(line.Start, closingStart);
		VerbatimContent(node, context, indentation);
		context.PreviousToken = node.GetLastToken();
		context.AppliedLayout(rule, node.Span);
		return true;
	}

	private static bool StartsOwnedLogicalOperand(SyntaxNode node, PrintContext context)
	{
		if (!context.IsInLogicalConditionHeader(node) && !context.IsInLogicalLambdaBody(node))
			return false;
		for (var current = node; current.Parent is { } parent; current = parent)
		{
			if (parent is BinaryExpressionSyntax binary
				&& binary.Kind() is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression
				&& current.SpanStart == node.SpanStart)
				return true;
			if (ReferenceEquals(parent, context.LogicalConditionRoot) || ReferenceEquals(parent, context.LogicalLambdaRoot))
				break;
		}
		return false;
	}
}
