namespace Nullean.Curb.Tests.LayoutRules;

internal static class GeneralConditionLayoutSamples
{
	internal const string Config = """
		max_line_length = 50
		csharp_keep_existing_linebreaks = false
		csharp_wrap_arguments_style = chop_if_long
		""";

	internal const string Policy = """
		{
		  "schemaVersion": 1,
		  "rules": [{
		    "id": "condition-headers",
		    "files": ["**/*.cs"],
		    "match": { "kind": "condition", "owner": "if-statement" },
		    "layout": {
		      "recipe": "attached-condition",
		      "wrap": "if-long",
		      "header": "inline-if-fits",
		      "continuation": "one-indent",
		      "closeParen": "with-condition"
		    }
		  }]
		}
		""";

	internal const string Call = "HasCalibration(primaryReading, secondaryReading, backupReading)";

	internal const string CallExpected = """
		class C
		{
		    void M()
		    {
		        if (HasCalibration(
		            primaryReading,
		            secondaryReading,
		            backupReading
		        ))
		        {
		            Use();
		        }
		    }
		}
		""";

	internal const string Property = "reading is { Temperature: > 20, Humidity: < 80 } accepted";

	internal const string PropertyExpected = """
		class C
		{
		    void M()
		    {
		        if (reading is
		        {
		            Temperature: > 20,
		            Humidity: < 80
		        } accepted)
		        {
		            Use();
		        }
		    }
		}
		""";

	internal const string PropertyDefault = """
		class C
		{
		    void M()
		    {
		        if (
		            reading is
		            {
		                Temperature: > 20,
		                Humidity: < 80
		            } accepted
		        )
		        {
		            Use();
		        }
		    }
		}
		""";

	internal static string Source(string condition) => $"class C {{ void M() {{ if ({condition}) {{ Use(); }} }} }}";
}
