using System.IO.Abstractions.TestingHelpers;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Nullean.Curb.Cli;
using Nullean.Curb.LayoutRules;

namespace Nullean.Curb.Tests.LayoutRules;

public class ConditionConfigurationTests
{
	[Test]
	public void Both_rule_kinds_compile_in_the_existing_schema()
	{
		using var wrappers = JsonDocument.Parse(LayoutRuleSamples.Policy);
		using var conditions = JsonDocument.Parse(ConditionLayoutSamples.Policy);
		var policy = "{\"schemaVersion\":1,\"rules\":[" + wrappers.RootElement.GetProperty("rules")[0].GetRawText()
			+ "," + conditions.RootElement.GetProperty("rules")[0].GetRawText() + "]}";
		var definitions = LayoutRuleCompiler.Compile(Encoding.UTF8.GetBytes(policy));
		definitions.Length.Should().Be(2);
		definitions[0].Rule.Should().BeOfType<WrapperLayoutRule>();
		definitions[1].Rule.Should().BeOfType<LogicalConditionLayoutRule>();
	}

	[Test]
	[Arguments("\"if-statement\"", "\"while-statement\"")]
	[Arguments("\"if-long\"", "\"always\"")]
	[Arguments("\"trailing\"", "\"leading\"")]
	[Arguments("\"kind\": \"logical-condition\",", "")]
	[Arguments("\"closeParen\": \"own-line-when-broken\"", "\"closeParen\": \"own-line-when-broken\", \"unexpected\": true")]
	public void Unsupported_condition_settings_are_rejected(string before, string after)
	{
		var policy = ConditionLayoutSamples.Policy.Replace(before, after, StringComparison.Ordinal);
		Action compile = () => LayoutRuleCompiler.Compile(Encoding.UTF8.GetBytes(policy));
		compile.Should().Throw<LayoutRuleConfigurationException>();
	}

	[Test]
	public void A_condition_policy_change_invalidates_cache_without_source_changes()
	{
		var fs = new MockFileSystem();
		fs.AddFile("/repo/.editorconfig", new MockFileData("root=true\n[*.cs]\n" + ConditionLayoutSamples.Config + "\ncurb_layout_rules=policy.json\n"));
		fs.AddFile("/repo/policy.json", new MockFileData(ConditionLayoutSamples.Policy));
		fs.AddFile("/repo/Source.cs", new MockFileData(ConditionLayoutSamples.Source));
		FormattingRun.Execute(fs, "/repo", write: true, cachePath: "/repo/cache").ExitCode.Should().Be(0);
		FormattingRun.Execute(fs, "/repo", write: false, cachePath: "/repo/cache").ExitCode.Should().Be(0);
		FormattingRun.Execute(fs, "/repo", write: false, cachePath: "/repo/cache").Cached.Should().Be(1);
		var time = fs.File.GetLastWriteTimeUtc("/repo/policy.json");
		fs.File.WriteAllText("/repo/policy.json", LayoutRuleSamples.Policy);
		fs.File.SetLastWriteTimeUtc("/repo/policy.json", time);
		var changed = FormattingRun.Execute(fs, "/repo", write: false, cachePath: "/repo/cache");
		changed.Cached.Should().Be(0);
		changed.ExitCode.Should().Be(1);
	}
}
