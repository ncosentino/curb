using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nullean.Curb.LayoutRules;

/// <summary>A data-only vertical layout for configured invocation/lambda chains.</summary>
public sealed class WrapperLayoutRule : LayoutRule
{
	private readonly ExpressionSyntax[] _callees;
	private readonly IdentifierNameSyntax[] _postfixes;

	/// <summary>Creates an immutable rule whose callee spellings match syntax, not resolved symbols.</summary>
	/// <param name="id">A bounded, unique identifier used in explanations and failures.</param>
	/// <param name="calleeSyntax">Identifier/member-access spellings; invocation type arguments are preserved but not used for matching.</param>
	/// <exception cref="ArgumentException">An identifier or callee spelling is invalid or exceeds the rule budget.</exception>
	public WrapperLayoutRule(string id, IEnumerable<string> calleeSyntax) : this(id, calleeSyntax, null)
	{
	}

	/// <summary>Creates a vertical wrapper rule that also permits explicitly selected postfix invocations.</summary>
	/// <param name="id">A bounded, unique identifier used in explanations and failures.</param>
	/// <param name="calleeSyntax">Identifier/member-access spellings of wrapper callees.</param>
	/// <param name="postfixCalleeSyntax">Optional simple method names allowed after each wrapper invocation. Arguments and type arguments are preserved.</param>
	/// <exception cref="ArgumentException">An identifier or callee spelling is invalid or exceeds the rule budget.</exception>
	public WrapperLayoutRule(string id, IEnumerable<string> calleeSyntax, IEnumerable<string>? postfixCalleeSyntax) : base(id, "vertical-wrapper-chain")
	{
		ArgumentNullException.ThrowIfNull(calleeSyntax);
		var spellings = calleeSyntax.ToArray();
		if (spellings.Length is < 1 or > 64)
			throw new ArgumentException("A layout rule must name 1 to 64 callees.", nameof(calleeSyntax));
		_callees = new ExpressionSyntax[spellings.Length];
		for (var i = 0; i < spellings.Length; i++)
		{
			var spelling = spellings[i];
			if (string.IsNullOrWhiteSpace(spelling) || spelling.Length > 256)
				throw new ArgumentException("A callee spelling is empty or exceeds 256 characters.", nameof(calleeSyntax));
			var expression = SyntaxFactory.ParseExpression(spelling, options: new CSharpParseOptions(LanguageVersion.Preview), consumeFullText: true);
			if (expression.ContainsDiagnostics || !IsSupported(expression, 0))
				throw new ArgumentException("A callee must be an identifier or simple member-access path.", nameof(calleeSyntax));
			_callees[i] = expression;
		}
		CalleeSyntax = Array.AsReadOnly(spellings);
		var postfixSpellings = postfixCalleeSyntax?.ToArray() ?? [];
		if (postfixSpellings.Length > 64)
			throw new ArgumentException("A layout rule cannot exceed 64 postfix callees.", nameof(postfixCalleeSyntax));
		_postfixes = new IdentifierNameSyntax[postfixSpellings.Length];
		for (var i = 0; i < postfixSpellings.Length; i++)
		{
			var spelling = postfixSpellings[i];
			if (string.IsNullOrWhiteSpace(spelling) || spelling.Length > 256
				|| SyntaxFactory.ParseExpression(spelling, options: new CSharpParseOptions(LanguageVersion.Preview), consumeFullText: true)
					is not IdentifierNameSyntax { ContainsDiagnostics: false } name)
				throw new ArgumentException("A postfix callee must be a simple identifier of at most 256 characters.", nameof(postfixCalleeSyntax));
			_postfixes[i] = name;
		}
		PostfixCalleeSyntax = Array.AsReadOnly(postfixSpellings);
	}

	/// <summary>The immutable configured callee spellings.</summary>
	public IReadOnlyList<string> CalleeSyntax { get; }

	/// <summary>The immutable allowed postfix method names; an empty list retains the original match contract.</summary>
	public IReadOnlyList<string> PostfixCalleeSyntax { get; }

	internal IEnumerable<string> LeafNames
	{
		get
		{
			foreach (var callee in _callees)
				yield return LeafName(callee)!;
			foreach (var postfix in _postfixes)
				yield return postfix.Identifier.ValueText;
		}
	}

	internal bool MatchesPostfix(SimpleNameSyntax name)
	{
		foreach (var postfix in _postfixes)
		{
			if (postfix.Identifier.ValueText == name.Identifier.ValueText)
				return true;
		}
		return false;
	}

	internal bool Matches(ExpressionSyntax expression)
	{
		foreach (var callee in _callees)
		{
			if (SamePath(callee, expression))
				return true;
		}
		return false;
	}

	internal static string? LeafName(ExpressionSyntax expression) => expression switch
	{
		SimpleNameSyntax name => name.Identifier.ValueText,
		MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
		AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
		_ => null,
	};

	private static bool IsSupported(ExpressionSyntax expression, int depth) =>
		depth < 16 && expression switch
		{
			IdentifierNameSyntax => true,
			MemberAccessExpressionSyntax member when member.RawKind == (int)SyntaxKind.SimpleMemberAccessExpression =>
				member.Name is IdentifierNameSyntax && IsSupported(member.Expression, depth + 1),
			AliasQualifiedNameSyntax alias => alias.Name is IdentifierNameSyntax,
			_ => false,
		};

	private static bool SamePath(ExpressionSyntax expected, ExpressionSyntax actual) => (expected, actual) switch
	{
		(IdentifierNameSyntax left, SimpleNameSyntax right) => left.Identifier.ValueText == right.Identifier.ValueText,
		(MemberAccessExpressionSyntax left, MemberAccessExpressionSyntax right) =>
			left.RawKind == right.RawKind
			&& left.Name.Identifier.ValueText == right.Name.Identifier.ValueText
			&& SamePath(left.Expression, right.Expression),
		(AliasQualifiedNameSyntax left, AliasQualifiedNameSyntax right) =>
			left.Alias.Identifier.ValueText == right.Alias.Identifier.ValueText
			&& left.Name.Identifier.ValueText == right.Name.Identifier.ValueText,
		_ => false,
	};
}
