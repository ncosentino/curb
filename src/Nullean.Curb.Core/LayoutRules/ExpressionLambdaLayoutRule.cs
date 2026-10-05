namespace Nullean.Curb.LayoutRules;

/// <summary>An opt-in attached header layout for sole invocation lambdas with nonlogical expression bodies.</summary>
public sealed class ExpressionLambdaLayoutRule : LayoutRule
{
	/// <summary>Creates a deterministic, width-driven expression-lambda rule that delegates body formatting.</summary>
	/// <param name="id">A unique, bounded rule identifier.</param>
	/// <exception cref="ArgumentException">The identifier is invalid.</exception>
	public ExpressionLambdaLayoutRule(string id) : base(id, "attached-expression-lambda") { }
}
