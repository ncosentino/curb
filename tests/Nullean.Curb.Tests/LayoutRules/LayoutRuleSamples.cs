namespace Nullean.Curb.Tests.LayoutRules;

internal static class LayoutRuleSamples
{
	internal const string Policy = """
		{
		  "schemaVersion": 1,
		  "rules": [{
		    "id": "wrappers",
		    "files": ["**/*.cs"],
		    "match": {
		      "kind": "lambda-wrapper-chain",
		      "owner": "expression-bodied-method",
		      "calleeSyntax": ["TraceScope.RunAsync", "Outcome.CaptureAsync", "Outcome.Capture"],
		      "callbackArgument": "last",
		      "callbackParameters": "empty",
		      "terminalBody": "block",
		      "awaitTokens": "preserve"
		    },
		    "layout": {
		      "recipe": "vertical-wrapper-chain",
		      "anchor": "declaration",
		      "parameterClose": "with-last-parameter",
		      "arrowAndRootAwait": "with-header",
		      "wrapperIndent": 0,
		      "lambdaBraceIndent": 0,
		      "bodyIndent": 1,
		      "closeInvocations": "compact"
		    }
		  }]
		}
		""";

	internal const string Config = "max_line_length = 120\ncsharp_keep_existing_linebreaks = false\ncsharp_wrap_parameters_style = chop_always\ncsharp_wrap_before_first_method_call = true\ncsharp_wrap_before_declaration_rpar = true\nend_of_line = lf";

	internal const string Source = """
		public class Sample
		{
		    public async Task<Result<Value>> GetAsync(int number, CancellationToken ct) =>
		        await TraceScope.RunAsync(async () => Outcome.CaptureAsync(async () => { var value=new Value(number); return value; }));
		}
		""";

	internal const string Expected = """
		public class Sample
		{
		    public async Task<Result<Value>> GetAsync(
		        int number,
		        CancellationToken ct) => await
		    TraceScope.RunAsync(async () =>
		    Outcome.CaptureAsync(async () =>
		    {
		        var value = new Value(number);
		        return value;
		    }));
		}
		""";
}
