using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullean.Curb.LayoutRules;

namespace Nullean.Curb.Printing.CSharp;

internal static partial class Printers
{
	/// <summary>
	/// The rule that owns a link of a <c>+</c> chain, when the chain contains a string operand and
	/// its outermost link is itself a call argument.
	/// </summary>
	/// <remarks>
	/// Only the continuation indent is owned. Whether and where the chain breaks stays with the
	/// ordinary per-operator groups, so a chain that fits is unaffected and a wrapped chain keeps
	/// packing operands onto each line. Every link of the chain resolves to the same root, so the
	/// whole chain agrees on one indent.
	/// </remarks>
	internal static StringConcatenationLayoutRule? OwnedStringConcatenation(
		BinaryExpressionSyntax node,
		PrintContext context,
		out BinaryExpressionSyntax root)
	{
		root = node;
		if (context.LayoutRules?.StringConcatenations is not { Count: > 0 } rules
			|| !node.IsKind(SyntaxKind.AddExpression))
			return null;

		while (root.Parent is BinaryExpressionSyntax parent && parent.IsKind(SyntaxKind.AddExpression))
			root = parent;

		if (root.Parent is not ArgumentSyntax || !HasStringOperand(root))
			return null;

		if (context.Suppressed is { } suppressed)
		{
			foreach (var span in suppressed)
			{
				if (span.IntersectsWith(root.FullSpan))
					return null;
			}
		}

		if (rules.Count > 1)
			throw new LayoutRuleException($"Layout rules '{rules[0].Id}' and '{rules[1].Id}' claim the same string concatenation.");

		return rules[0];
	}

	private static bool HasStringOperand(BinaryExpressionSyntax root)
	{
		ExpressionSyntax current = root;
		while (current is BinaryExpressionSyntax link && link.IsKind(SyntaxKind.AddExpression))
		{
			if (IsStringOperand(link.Right))
				return true;
			current = link.Left;
		}

		return IsStringOperand(current);
	}

	private static bool IsStringOperand(ExpressionSyntax operand) =>
		operand is InterpolatedStringExpressionSyntax
		|| operand.Kind() is SyntaxKind.StringLiteralExpression or SyntaxKind.Utf8StringLiteralExpression;
}
