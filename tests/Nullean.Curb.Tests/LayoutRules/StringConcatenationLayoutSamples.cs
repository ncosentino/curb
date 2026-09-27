namespace Nullean.Curb.Tests.LayoutRules;

internal static class StringConcatenationLayoutSamples
{
	internal const string Policy = """
		{
		  "schemaVersion": 1,
		  "rules": [{
		    "id": "argument-strings",
		    "files": ["**/*.cs"],
		    "match": {
		      "kind": "string-concatenation",
		      "owner": "argument"
		    },
		    "layout": {
		      "recipe": "argument-string-concatenation",
		      "wrap": "if-long",
		      "operators": "trailing",
		      "continuation": "argument-indent"
		    }
		  }]
		}
		""";

	internal const string Config =
		"max_line_length = 120\ncsharp_keep_existing_linebreaks = false\nend_of_line = lf\ncsharp_wrap_arguments_style = chop_if_long";

	internal const string Joined = """
		public static class Repro
		{
		    public static void Report(Logger logger, int count, string keys)
		    {
		        logger.Warn("Scheduler configuration supplied {UnboundCount} setting(s) that never reached the " + "scheduler and are therefore ignored: {UnboundKeys}.", count, keys);
		    }
		}
		""";

	internal const string Hanging = """
		public static class Repro
		{
		    public static void Report(Logger logger, int count, string keys)
		    {
		        logger.Warn(
		            "Scheduler configuration supplied {UnboundCount} setting(s) that never reached the " +
		                "scheduler and are therefore ignored: {UnboundKeys}.",
		            count,
		            keys
		        );
		    }
		}
		""";

	internal const string Canonical = """
		public static class Repro
		{
		    public static void Report(Logger logger, int count, string keys)
		    {
		        logger.Warn(
		            "Scheduler configuration supplied {UnboundCount} setting(s) that never reached the " +
		            "scheduler and are therefore ignored: {UnboundKeys}.",
		            count,
		            keys
		        );
		    }
		}
		""";
}
