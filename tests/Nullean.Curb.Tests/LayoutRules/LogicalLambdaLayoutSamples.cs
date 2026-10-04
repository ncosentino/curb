namespace Nullean.Curb.Tests.LayoutRules;

internal static class LogicalLambdaLayoutSamples
{
	internal const string Policy = """
		{
		  "schemaVersion": 1,
		  "rules": [{
		    "id": "logical-lambdas",
		    "files": ["**/*.cs"],
		    "match": {
		      "kind": "logical-lambda-argument",
		      "owner": "sole-invocation-argument"
		    },
		    "layout": {
		      "recipe": "hanging-logical-lambda",
		      "wrap": "if-long",
		      "header": "inline-if-fits",
		      "continuation": "one-indent",
		      "operators": "trailing",
		      "closeParen": "with-final-operand"
		    }
		  }]
		}
		""";

	internal const string Config = "max_line_length = 100\ncsharp_keep_existing_linebreaks = false\nend_of_line = lf";

	internal const string Source = """
		class C
		{
		    bool M()
		    {
		        return entries.Any(entry => entry.IsEnabled && (entry.MatchesPrimaryCategory || entry.MatchesSecondaryCategory) && entry.HasRequiredPermission);
		    }
		}
		""";

	internal const string Expected = """
		class C
		{
		    bool M()
		    {
		        return entries.Any(entry =>
		            entry.IsEnabled &&
		            (entry.MatchesPrimaryCategory || entry.MatchesSecondaryCategory) &&
		            entry.HasRequiredPermission);
		    }
		}
		""";
}
