namespace Nullean.Curb.Tests.LayoutRules;

internal static class ExpressionLambdaLayoutSamples
{
	internal const string Policy = """
		{
		  "schemaVersion": 1,
		  "rules": [{
		    "id": "expression-lambdas",
		    "files": ["**/*.cs"],
		    "match": {
		      "kind": "expression-lambda-argument",
		      "owner": "sole-invocation-argument",
		      "body": "nonlogical-expression"
		    },
		    "layout": {
		      "recipe": "attached-expression-lambda",
		      "wrap": "if-long",
		      "header": "inline-if-fits",
		      "continuation": "one-indent",
		      "closeParen": "with-body"
		    }
		  }]
		}
		""";

	internal const string Config = """
		max_line_length = 100
		csharp_keep_existing_linebreaks = false
		csharp_wrap_arguments_style = chop_if_long
		csharp_wrap_parameters_style = chop_always
		end_of_line = lf
		""";

	internal const string PatternSource = """
		class C
		{
		    bool M()
		    {
		        if (entries.Any(entry => entry.Status is not (ResultStatus.Unavailable or ResultStatus.NotApplicable)))
		        {
		            return false;
		        }
		        return true;
		    }
		}
		""";

	internal const string PatternExpected = """
		class C
		{
		    bool M()
		    {
		        if (entries.Any(entry =>
		            entry.Status is not (ResultStatus.Unavailable or ResultStatus.NotApplicable)))
		        {
		            return false;
		        }
		        return true;
		    }
		}
		""";

	internal const string ProjectionSource = """
		class C
		{
		    object M()
		    {
		        return entries.Select(entry => new SearchResult(entry.Id, Map(entry.CurrentValue), Map(entry.PreviousValue)));
		    }
		}
		""";

	internal const string ProjectionExpected = """
		class C
		{
		    object M()
		    {
		        return entries.Select(entry => new SearchResult(
		            entry.Id,
		            Map(entry.CurrentValue),
		            Map(entry.PreviousValue)
		        ));
		    }
		}
		""";
}
