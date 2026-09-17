using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullean.Curb.Documents;
using Nullean.Curb.LayoutRules;

namespace Nullean.Curb.Printing.CSharp;

internal static partial class Printers
{
	private readonly record struct WrapperStage(AwaitExpressionSyntax? Await, InvocationExpressionSyntax Invocation, ParenthesizedLambdaExpressionSyntax Callback);

	private static bool TryPrintLayoutMethod(MethodDeclarationSyntax method, PrintContext context)
	{
		var expression = method.ExpressionBody?.Expression;
		var converted = false;
		if (expression is null && method.Body is { } body
			&& TryGetExpressionBody(body, context.Options.ExpressionBodiedMethods, context, out var value, out var throws)
			&& !throws)
		{
			expression = value;
			converted = true;
		}
		var invocation = (expression is AwaitExpressionSyntax rootAwait ? rootAwait.Expression : expression) as InvocationExpressionSyntax;
		if (invocation is null)
			return false;

		if (context.Suppressed is { } suppressed)
		{
			foreach (var span in suppressed)
			{
				if (span.IntersectsWith(method.FullSpan))
					return false;
			}
		}

		WrapperLayoutRule? selected = null;
		List<WrapperStage>? selectedStages = null;
		BlockSyntax? terminal = null;
		foreach (var rule in context.LayoutRules!.Candidates(invocation.Expression))
		{
			if (!TryCaptureWrappers(expression!, rule, out var stages, out var block))
				continue;
			if (selected is not null)
				throw new LayoutRuleException($"Layout rules '{selected.Id}' and '{rule.Id}' claim the same method.");
			selected = rule;
			selectedStages = stages;
			terminal = block;
		}
		if (selected is null || selectedStages is null || terminal is null)
			return false;

		if (HasAnyTrivia(method.ParameterList.CloseParenToken)
			|| (method.ParameterList.Parameters.Count > 0 && HasAnyTrivia(method.ParameterList.Parameters[^1].GetLastToken()))
			|| (method.ExpressionBody is { } arrow && HasAnyTrivia(arrow.ArrowToken)))
			throw new LayoutRuleException($"Layout rule '{selected.Id}' cannot join a declaration boundary carrying content trivia.");
		foreach (var stage in selectedStages)
		{
			if ((stage.Await is { } awaited && HasAnyTrivia(awaited.AwaitKeyword))
				|| HasAnyTrivia(stage.Invocation.Expression, context)
				|| HasAnyTrivia(stage.Invocation.ArgumentList.OpenParenToken)
				|| HasAnyTrivia(stage.Invocation.ArgumentList.CloseParenToken)
				|| HasAnyTrivia(stage.Callback.ArrowToken)
				|| HasAnyTrivia(stage.Callback.ParameterList, context))
				throw new LayoutRuleException($"Layout rule '{selected.Id}' encountered unsupported wrapper-boundary trivia.");
			foreach (var modifier in stage.Callback.Modifiers)
			{
				if (HasAnyTrivia(modifier))
					throw new LayoutRuleException($"Layout rule '{selected.Id}' encountered callback-modifier trivia.");
			}
			var arguments = stage.Invocation.ArgumentList.Arguments;
			if (arguments[^1].NameColon is { } callbackName && HasAnyTrivia(callbackName, context))
				throw new LayoutRuleException($"Layout rule '{selected.Id}' encountered named-callback trivia.");
			for (var i = 0; i < arguments.Count - 1; i++)
			{
				if (HasAnyTrivia(arguments[i], context) || HasAnyTrivia(arguments.GetSeparator(i)))
					throw new LayoutRuleException($"Layout rule '{selected.Id}' encountered argument-boundary trivia.");
			}
		}
		foreach (var trivia in terminal.CloseBraceToken.TrailingTrivia)
		{
			if (trivia.Kind() is not (SyntaxKind.WhitespaceTrivia or SyntaxKind.EndOfLineTrivia))
				throw new LayoutRuleException($"Layout rule '{selected.Id}' cannot compact closing delimiters around content trivia.");
		}

		PrintMethodHeader(method, context, customLayout: true);
		var arena = context.Arena;
		arena.Synthetic(SyntheticText.Space);
		if (converted)
		{
			BeginExpressionBodyRewrite(method.Body!, context);
			arena.Synthetic(SyntheticText.Arrow);
		}
		else
			TokenPrinter.Print(method.ExpressionBody!.ArrowToken, context);

		for (var i = 0; i < selectedStages.Count; i++)
		{
			var stage = selectedStages[i];
			if (stage.Await is { } awaited)
			{
				arena.Synthetic(SyntheticText.Space);
				TokenPrinter.Print(awaited.AwaitKeyword, context);
			}
			arena.HardLine(DocFlags.Reindent);
			using (arena.Group())
			{
				PrintLayoutCallee(stage.Invocation.Expression, context);
				Spacing.BeforeCallParens(context);
				TokenPrinter.Print(stage.Invocation.ArgumentList.OpenParenToken, context);
				using (arena.Indent())
				{
					Spacing.InsideCallParensBreakable(context);
					var arguments = stage.Invocation.ArgumentList.Arguments;
					for (var argumentIndex = 0; argumentIndex < arguments.Count - 1; argumentIndex++)
					{
						Node.Print(arguments[argumentIndex], context);
						Spacing.BeforeComma(context);
						TokenPrinter.Print(arguments.GetSeparator(argumentIndex), context);
						arena.Line();
					}
					var callbackArgument = arguments[^1];
					if (callbackArgument.NameColon is { } nameColon)
					{
						TokenPrinter.Print(nameColon.Name.Identifier, context);
						TokenPrinter.Print(nameColon.ColonToken, context);
						arena.Synthetic(SyntheticText.Space);
					}
					PrintModifiers(stage.Callback.Modifiers, context, reorder: false);
					ParameterList(stage.Callback.ParameterList, context);
					arena.Synthetic(SyntheticText.Space);
					TokenPrinter.Print(stage.Callback.ArrowToken, context);
				}
			}
		}
		arena.HardLine(DocFlags.Reindent);
		Block(terminal, context, customLayout: true);
		for (var i = selectedStages.Count - 1; i >= 0; i--)
		{
			Spacing.InsideCallParens(context);
			TokenPrinter.Print(selectedStages[i].Invocation.ArgumentList.CloseParenToken, context);
		}
		if (converted)
		{
			TokenPrinter.Print(SemicolonOf(method.Body!.Statements[0]), context);
			context.Dropped(method.Body.CloseBraceToken.Span);
		}
		else
			TokenPrinter.PrintIfPresent(method.SemicolonToken, context);
		context.AppliedLayout(selected.Id, method.Span);
		return true;
	}

	private static bool TryCaptureWrappers(ExpressionSyntax expression, WrapperLayoutRule rule, out List<WrapperStage> stages, out BlockSyntax? block)
	{
		stages = [];
		block = null;
		while (true)
		{
			var awaited = expression as AwaitExpressionSyntax;
			if ((awaited?.Expression ?? expression) is not InvocationExpressionSyntax invocation || !rule.Matches(invocation.Expression))
				return false;
			var arguments = invocation.ArgumentList.Arguments;
			if (arguments.Count == 0
				|| arguments[^1].Expression is not ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 0, AttributeLists.Count: 0, ReturnType: null } callback
				|| arguments[^1].RefKindKeyword.RawKind != 0)
				return false;
			if (stages.Count == 16)
				throw new LayoutRuleException($"Layout rule '{rule.Id}' exceeds the wrapper-chain budget.");
			stages.Add(new WrapperStage(awaited, invocation, callback));
			if (callback.Block is { } terminal)
			{
				block = terminal;
				return true;
			}
			if (callback.ExpressionBody is not { } next)
				return false;
			expression = next;
		}
	}

	private static void PrintLayoutCallee(ExpressionSyntax expression, PrintContext context)
	{
		switch (expression)
		{
			case SimpleNameSyntax name:
				PrintLayoutName(name, context);
				break;
			case MemberAccessExpressionSyntax member:
				PrintLayoutCallee(member.Expression, context);
				Spacing.BeforeDot(context);
				TokenPrinter.Print(member.OperatorToken, context);
				Spacing.AfterDot(context);
				PrintLayoutName(member.Name, context);
				break;
			case AliasQualifiedNameSyntax alias:
				TokenPrinter.Print(alias.Alias.Identifier, context);
				TokenPrinter.Print(alias.ColonColonToken, context);
				PrintLayoutName(alias.Name, context);
				break;
			default:
				throw new LayoutRuleException("A matched callee has an unsupported syntax shape.");
		}
	}

	private static void PrintLayoutName(SimpleNameSyntax name, PrintContext context)
	{
		TokenPrinter.Print(name.Identifier, context);
		if (name is GenericNameSyntax generic)
			Node.Print(generic.TypeArgumentList, context);
	}
}
