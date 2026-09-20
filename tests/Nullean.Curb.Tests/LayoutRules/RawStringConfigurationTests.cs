using System.IO.Abstractions.TestingHelpers;
using System.Text;
using AwesomeAssertions;
using Nullean.Curb.Cli;
using Nullean.Curb.LayoutRules;

namespace Nullean.Curb.Tests.LayoutRules;

public class RawStringConfigurationTests
{
	[Test]
	public void The_rule_compiles_as_data_in_schema_one()
	{
		var definitions = LayoutRuleCompiler.Compile(Encoding.UTF8.GetBytes(RawStringLayoutSamples.Policy));
		definitions.Should().ContainSingle().Which.Rule.Should().BeOfType<MultilineRawStringLayoutRule>();
	}

	[Test]
	[Arguments("\"expression\"", "\"method\"")]
	[Arguments("\"preserve-closing\"", "\"reindent\"")]
	[Arguments("\"own-line\"", "\"preserve\"")]
	[Arguments("\"preserve\"", "\"normalize\"")]
	public void Unsupported_literal_mutations_are_rejected(string before, string after)
	{
		var policy = RawStringLayoutSamples.Policy.Replace(before, after, StringComparison.Ordinal);
		Action compile = () => LayoutRuleCompiler.Compile(Encoding.UTF8.GetBytes(policy));
		compile.Should().Throw<LayoutRuleConfigurationException>();
	}

	[Test]
	public void A_policy_only_change_invalidates_an_already_proven_raw_literal_cache_entry()
	{
		var fs = new MockFileSystem();
		fs.AddFile("/repo/.editorconfig", new MockFileData("root=true\n[*.cs]\n" + RawStringLayoutSamples.Config + "\ncurb_layout_rules=policy.json\n"));
		fs.AddFile("/repo/policy.json", new MockFileData(RawStringLayoutSamples.Policy));
		fs.AddFile("/repo/Source.cs", new MockFileData(RawStringLayoutSamples.Attached));
		FormattingRun.Execute(fs, "/repo", write: true, cachePath: "/repo/cache").ExitCode.Should().Be(0);
		fs.File.ReadAllText("/repo/Source.cs").TrimEnd().Should().Be(RawStringLayoutSamples.Detached);
		FormattingRun.Execute(fs, "/repo", write: false, cachePath: "/repo/cache").ExitCode.Should().Be(0);
		FormattingRun.Execute(fs, "/repo", write: false, cachePath: "/repo/cache").Cached.Should().Be(1);
		var time = fs.File.GetLastWriteTimeUtc("/repo/policy.json");
		fs.File.WriteAllText("/repo/policy.json", RawStringLayoutSamples.Policy.Replace("**/*.cs", "Excluded/*.cs", StringComparison.Ordinal));
		fs.File.SetLastWriteTimeUtc("/repo/policy.json", time);
		var check = FormattingRun.Execute(fs, "/repo", write: false, cachePath: "/repo/cache");
		check.Cached.Should().Be(0);
		check.ExitCode.Should().Be(1);
	}
}
