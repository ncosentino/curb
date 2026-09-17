using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nullean.Curb.LayoutRules;

/// <summary>A data-only vertical layout for configured invocation/lambda chains.</summary>
public sealed class WrapperLayoutRule
{
	private readonly ExpressionSyntax[] _callees;

	/// <summary>Creates an immutable rule whose callee spellings match syntax, not resolved symbols.</summary>
	/// <param name="id">A bounded, unique identifier used in explanations and failures.</param>
	/// <param name="calleeSyntax">Identifier/member-access spellings; invocation type arguments are preserved but not used for matching.</param>
	/// <exception cref="ArgumentException">An identifier or callee spelling is invalid or exceeds the rule budget.</exception>
	public WrapperLayoutRule(string id, IEnumerable<string> calleeSyntax)
	{
		ArgumentNullException.ThrowIfNull(calleeSyntax);
		if (string.IsNullOrWhiteSpace(id) || id.Length > 80)
			throw new ArgumentException("A layout rule ID must contain 1 to 80 characters.", nameof(id));
		foreach (var character in id)
		{
			if (!char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_' or '.'))
				throw new ArgumentException("A layout rule ID contains an unsupported character.", nameof(id));
		}
		Id = id;
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
	}

	/// <summary>The identifier reported when this rule matches or conflicts.</summary>
	public string Id { get; }

	/// <summary>The immutable configured callee spellings.</summary>
	public IReadOnlyList<string> CalleeSyntax { get; }

	internal IEnumerable<string> LeafNames
	{
		get
		{
			foreach (var callee in _callees)
				yield return LeafName(callee)!;
		}
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
