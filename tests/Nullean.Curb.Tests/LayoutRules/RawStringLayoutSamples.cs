namespace Nullean.Curb.Tests.LayoutRules;

internal static class RawStringLayoutSamples
{
	internal const string Policy = """
		{
		  "schemaVersion": 1,
		  "rules": [{
		    "id": "raw-openers",
		    "files": ["**/*.cs"],
		    "match": {
		      "kind": "multiline-raw-string",
		      "owner": "expression"
		    },
		    "layout": {
		      "recipe": "standalone-raw-string",
		      "openingDelimiter": "own-line",
		      "indentation": "preserve-closing",
		      "contents": "preserve"
		    }
		  }]
		}
		""";

	internal const string Config = "max_line_length = 120\ncsharp_keep_existing_linebreaks = false\nend_of_line = lf";

	internal const string Attached = """"
		public static class Repro
		{
		    public static string Value()
		    {
		        var value = """
		            content
		            """;
		        return value;
		    }
		}
		"""";

	internal const string Detached = """"
		public static class Repro
		{
		    public static string Value()
		    {
		        var value =
		            """
		            content
		            """;
		        return value;
		    }
		}
		"""";
}
