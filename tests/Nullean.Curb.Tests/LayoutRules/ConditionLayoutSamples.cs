namespace Nullean.Curb.Tests.LayoutRules;

internal static class ConditionLayoutSamples
{
	internal const string Policy = """
		{
		  "schemaVersion": 1,
		  "rules": [{
		    "id": "logical-headers",
		    "files": ["**/*.cs"],
		    "match": {
		      "kind": "logical-condition",
		      "owner": "if-statement"
		    },
		    "layout": {
		      "recipe": "hanging-logical-condition",
		      "wrap": "if-long",
		      "firstOperand": "with-open",
		      "continuation": "align-first-operand",
		      "operators": "trailing",
		      "closeParen": "own-line-when-broken"
		    }
		  }]
		}
		""";

	internal const string Config = "max_line_length = 120\ncsharp_keep_existing_linebreaks = false\ncsharp_wrap_chained_binary_expressions = chop_if_long\ncsharp_wrap_before_binary_opsign = false\nend_of_line = lf";

	internal const string Source = """
		internal static class ConditionExample
		{
		    private const int RequiredCategoryIdentifier = 1;
		    private const int RequiredSegmentIdentifier = 2;

		    public static bool Matches(Candidate candidate)
		    {
		        if (candidate.CategoryId == RequiredCategoryIdentifier && candidate.SegmentId == RequiredSegmentIdentifier && candidate.State == "Ready")
		        {
		            return true;
		        }

		        return false;
		    }
		}

		internal sealed record Candidate(int CategoryId, int SegmentId, string State);
		""";

	internal const string Expected = """
		internal static class ConditionExample
		{
		    private const int RequiredCategoryIdentifier = 1;
		    private const int RequiredSegmentIdentifier = 2;

		    public static bool Matches(Candidate candidate)
		    {
		        if (candidate.CategoryId == RequiredCategoryIdentifier &&
		            candidate.SegmentId == RequiredSegmentIdentifier &&
		            candidate.State == "Ready"
		        )
		        {
		            return true;
		        }

		        return false;
		    }
		}

		internal sealed record Candidate(int CategoryId, int SegmentId, string State);
		""";
}
