using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace Nullean.Curb.Tests.Guidance;

public partial class GuidanceContractTests
{
	private static readonly string RepositoryRoot = FindRepositoryRoot();

	[Test]
	public void Roots_owners_and_review_routing_are_valid()
	{
		using var document = ReadJson(RepositoryRoot, ".github/guidance.json");
		var contract = document.RootElement;
		var issues = new List<string>();
		var agents = contract.GetProperty("agents");
		CheckBudget(RepositoryRoot, Text(agents, "path"), Number(agents, "maxLines"), Number(agents, "maxBytes"), issues);
		CheckRedirect(RepositoryRoot, Text(agents, "claude"), "@AGENTS.md", issues);
		CheckRedirect(RepositoryRoot, Text(agents, "copilot"), "Follow [AGENTS.md](../AGENTS.md) for repository instructions.", issues);

		foreach (var section in new[] { "docs", "review", "validation" })
		{
			foreach (var property in contract.GetProperty(section).EnumerateObject())
			{
				if (property.Name is "guidanceTarget" or "guidanceTestClass")
					continue;
				CheckOwner(RepositoryRoot, property.Value.GetString()!, issues);
			}
		}
		foreach (var path in Strings(contract.GetProperty("representativePaths")))
			CheckOwner(RepositoryRoot, path, issues);

		var review = contract.GetProperty("review");
		var reviewText = File.ReadAllText(Resolve(RepositoryRoot, Text(review, "skill")));
		foreach (var name in new[] { Text(review, "resolver"), Text(review, "inventory") })
			reviewText.Should().Contain(Path.GetFileName(name));
		reviewText.Should().Contain("ncosentino/curb");

		foreach (var file in Directory.EnumerateFiles(Resolve(RepositoryRoot, ".github/skills"), "SKILL.md", SearchOption.AllDirectories))
		{
			var metadata = Frontmatter(File.ReadAllText(file));
			metadata.Should().ContainKey("name");
			metadata["name"].Should().Be(new DirectoryInfo(Path.GetDirectoryName(file)!).Name);
			metadata.Should().ContainKey("description");
			metadata["description"].Should().NotBeNullOrWhiteSpace();
		}
		issues.Should().BeEmpty(string.Join(Environment.NewLine, issues));
	}

	[Test]
	public void Shared_imports_match_the_exact_allowlist()
	{
		var issues = CheckImports(RepositoryRoot);
		issues.Should().BeEmpty(string.Join(Environment.NewLine, issues));
	}

	[Test]
	public void Documentation_links_navigation_and_decisions_are_valid()
	{
		var issues = new List<string>();
		var docsRoot = Resolve(RepositoryRoot, "docs");
		var pages = Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories).ToArray();
		var navigation = NavigationPages(RepositoryRoot, issues);
		foreach (var file in pages)
		{
			var relative = Relative(RepositoryRoot, file);
			if (!navigation.Contains(relative))
				issues.Add($"Page is absent from navigation: {relative}");
		}

		var guidancePages = Directory.EnumerateFiles(Resolve(RepositoryRoot, ".github"), "*.md", SearchOption.AllDirectories)
			.Concat(Directory.EnumerateFiles(Resolve(RepositoryRoot, ".claude/skills"), "*.md", SearchOption.AllDirectories));
		foreach (var file in pages.Concat(guidancePages).Concat([Resolve(RepositoryRoot, "AGENTS.md"), Resolve(RepositoryRoot, "README.md")]))
			CheckLinks(RepositoryRoot, file, issues);

		using var contract = ReadJson(RepositoryRoot, ".github/guidance.json");
		var adrIndexPath = Resolve(RepositoryRoot, Text(contract.RootElement.GetProperty("docs"), "adrIndex"));
		var adrIndex = File.ReadAllText(adrIndexPath);
		foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(adrIndexPath)!, "*.md"))
		{
			if (file == adrIndexPath)
				continue;
			var metadata = Frontmatter(File.ReadAllText(file));
			var name = Path.GetFileName(file);
			if (!AdrName().IsMatch(name))
				issues.Add($"Invalid ADR name: {name}");
			if (!metadata.TryGetValue("status", out var status) || status is not ("Proposed" or "Accepted" or "Rejected" or "Superseded"))
				issues.Add($"Invalid ADR status: {name}");
			if (!metadata.TryGetValue("date", out var date) || !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
				issues.Add($"Invalid ADR date: {name}");
			foreach (var key in new[] { "supersedes", "superseded_by" })
			{
				if (!metadata.TryGetValue(key, out var related))
					issues.Add($"Missing ADR lifecycle field {key}: {name}");
				else if (related.Length > 0)
					CheckOwner(RepositoryRoot, "docs/adr/" + related, issues);
			}
			if (!adrIndex.Contains(name, StringComparison.Ordinal))
				issues.Add($"ADR is absent from its index: {name}");
			if (status == "Superseded" && metadata.GetValueOrDefault("superseded_by", "").Length == 0)
				issues.Add($"Superseded ADR has no replacement: {name}");
		}
		foreach (var file in Directory.EnumerateFiles(Resolve(RepositoryRoot, "docs/reference"), "*.md", SearchOption.AllDirectories))
		{
			var text = File.ReadAllText(file).TrimStart('\uFEFF');
			var body = MetadataBlock().Replace(text, "", 1).TrimStart();
			if (!body.StartsWith("<!-- generated by Nullean.Curb.OptionDocs -->", StringComparison.Ordinal))
				issues.Add($"Reference page lacks its generator marker: {Relative(RepositoryRoot, file)}");
		}
		issues.Should().BeEmpty(string.Join(Environment.NewLine, issues));
	}

	[Test]
	public async Task Resolver_covers_shared_and_local_rules_within_budget(CancellationToken cancellationToken)
	{
		using var contractDocument = ReadJson(RepositoryRoot, ".github/guidance.json");
		var contract = contractDocument.RootElement;
		var tracked = await Run("git", RepositoryRoot, ["ls-files", "--cached", "--others", "--exclude-standard"], cancellationToken);
		tracked.ExitCode.Should().Be(0, tracked.Error);
		var paths = tracked.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Concat(Strings(contract.GetProperty("representativePaths"))).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
		using var fixture = new TemporaryFiles();
		var pathsFile = fixture.WritePaths(paths);
		var result = await PowerShell(
			"build/guidance/Get-ApplicableInstructions.ps1",
			["-ProjectRoot", RepositoryRoot, "-PathsFile", pathsFile, "-Json"], cancellationToken);
		result.ExitCode.Should().Be(0, result.Error);
		using var matches = JsonDocument.Parse(result.Output);
		var rows = matches.RootElement.EnumerateArray().ToArray();
		rows.Should().NotBeEmpty();
		rows.Select(row => Text(row, "InstructionPath")).Should().Contain(path => path.StartsWith(".github/instructions/genesis/", StringComparison.Ordinal));
		rows.Select(row => Text(row, "InstructionPath")).Should().Contain(path => path.StartsWith(".github/instructions/curb/", StringComparison.Ordinal));
		var limits = contract.GetProperty("instructions");
		foreach (var group in rows.GroupBy(row => Text(row, "Path")))
		{
			group.Sum(row => Number(row, "Lines")).Should().BeLessThanOrEqualTo(Number(limits, "maxMatchedLines"), group.Key);
			group.Sum(row => Number(row, "Bytes")).Should().BeLessThanOrEqualTo(Number(limits, "maxMatchedBytes"), group.Key);
		}
		var matchedFiles = rows.Select(row => Text(row, "InstructionPath")).ToHashSet(StringComparer.Ordinal);
		foreach (var file in Directory.EnumerateFiles(Resolve(RepositoryRoot, Text(limits, "root")), "*.instructions.md", SearchOption.AllDirectories))
		{
			var relative = Relative(RepositoryRoot, file);
			matchedFiles.Should().Contain(relative, "each rule must match a real or declared representative population");
			relative.Should().MatchRegex(@"^\.github/instructions/(?:genesis|curb)/");
			var issues = new List<string>();
			CheckBudget(RepositoryRoot, relative, Number(limits, "maxFileLines"), Number(limits, "maxFileBytes"), issues);
			issues.Should().BeEmpty(string.Join(Environment.NewLine, issues));
		}
	}

	[Test]
	public async Task Inventory_discovers_the_actual_build_and_test_stack(CancellationToken cancellationToken)
	{
		var result = await PowerShell("build/guidance/Get-ValidationInventory.ps1", ["-ProjectRoot", RepositoryRoot, "-Json"], cancellationToken);
		result.ExitCode.Should().Be(0, result.Error);
		using var document = JsonDocument.Parse(result.Output);
		var inventory = document.RootElement;
		Strings(inventory.GetProperty("dotnetProjects")).Should().Contain("build/scripts/scripts.fsproj");
		Strings(inventory.GetProperty("dotnetProjects")).Should().Contain("tests/Nullean.Curb.Tests/Nullean.Curb.Tests.csproj");
		Strings(inventory.GetProperty("buildSources")).Should().Contain("build/scripts/Targets.fs");
		Strings(inventory.GetProperty("buildTargets")).Should().Contain("guidance");
		Strings(inventory.GetProperty("skills")).Should().Contain(".claude/skills/commit/SKILL.md");
		Strings(inventory.GetProperty("skills")).Should().Contain(".github/skills/review-changes/SKILL.md");
	}

	[Test]
	public void Oversized_roots_and_wrong_redirects_are_rejected()
	{
		using var fixture = new TemporaryFiles();
		fixture.Write("AGENTS.md", new string('x', 3073));
		fixture.Write("CLAUDE.md", "Different instructions\n");
		var issues = new List<string>();
		CheckBudget(fixture.Root, "AGENTS.md", 60, 3072, issues);
		CheckRedirect(fixture.Root, "CLAUDE.md", "@AGENTS.md", issues);
		issues.Should().HaveCount(2);
		issues.Should().Contain(issue => issue.StartsWith("Byte budget exceeded", StringComparison.Ordinal));
		issues.Should().Contain(issue => issue.StartsWith("Invalid redirect", StringComparison.Ordinal));
		fixture.Write("AGENTS.md", string.Concat(Enumerable.Repeat("line\n", 61)));
		issues.Clear();
		CheckBudget(fixture.Root, "AGENTS.md", 60, 3072, issues);
		issues.Should().ContainSingle().Which.Should().StartWith("Line budget exceeded");
	}

	[Test]
	public void Frontmatter_handles_both_line_endings_and_navigation_checks_listing_shape()
	{
		const string source = "---\nname: example\nstatus: Accepted\ndate: 2026-09-15\nsupersedes: \"\"\nsuperseded_by: \"\"\n---\n";
		Frontmatter(source).Should().BeEquivalentTo(Frontmatter(source.Replace("\n", "\r\n", StringComparison.Ordinal)));
		Frontmatter(source).Should().HaveCount(5);
		using var fixture = new TemporaryFiles();
		fixture.Write("docs/reference/page.md", "# Page\n");
		fixture.Write("docs/_docset.yml", "toc:\n  - listing: reference\n    glob: \"*.txt\"\n");
		var issues = new List<string>();
		NavigationPages(fixture.Root, issues).Should().BeEmpty();
		issues.Should().ContainSingle().Which.Should().StartWith("Unsupported navigation glob");
	}

	[Test]
	public void Import_metadata_is_closed_and_nonempty()
	{
		using var fixture = new TemporaryFiles();
		fixture.Write(".github/instructions/imports.json", """{"schemaVersion":1,"files":[],"unexpected":true}""");
		var issues = CheckImports(fixture.Root);
		issues.Should().Contain(issue => issue.StartsWith("Unexpected import metadata", StringComparison.Ordinal));
		issues.Should().Contain("The shared import allowlist is empty.");
	}

	[Test]
	public void Changed_missing_and_unapproved_imports_are_rejected()
	{
		using var fixture = new TemporaryFiles();
		const string approved = ".github/instructions/genesis/approved.instructions.md";
		fixture.Write(approved, "approved bytes\n");
		fixture.WriteImportManifest(approved);
		CheckImports(fixture.Root).Should().BeEmpty();
		fixture.Write(approved, "changed bytes\n");
		CheckImports(fixture.Root).Should().Contain(issue => issue.StartsWith("Changed import", StringComparison.Ordinal));
		fixture.Write(".github/instructions/genesis/unapproved.instructions.md", "unreviewed rule\n");
		CheckImports(fixture.Root).Should().Contain(issue => issue.StartsWith("Unexpected import", StringComparison.Ordinal));
		File.Delete(Resolve(fixture.Root, approved));
		CheckImports(fixture.Root).Should().Contain(issue => issue.StartsWith("Missing import", StringComparison.Ordinal));
	}

	[Test]
	public void Missing_owners_links_and_escaping_paths_are_rejected()
	{
		using var fixture = new TemporaryFiles();
		var file = fixture.Write("docs/index.md", "[Missing](absent.md)\n[Escape](../../outside.md)\n");
		var issues = new List<string>();
		CheckOwner(fixture.Root, "docs/absent.md", issues);
		CheckLinks(fixture.Root, file, issues);
		issues.Should().HaveCount(3);
		issues.Should().Contain(issue => issue.StartsWith("Missing owner", StringComparison.Ordinal));
		issues.Should().Contain(issue => issue.StartsWith("Missing link", StringComparison.Ordinal));
		issues.Should().Contain(issue => issue.StartsWith("Escaping link", StringComparison.Ordinal));
	}

	[Test]
	public async Task Invalid_globs_and_lookup_paths_fail_explicitly(CancellationToken cancellationToken)
	{
		using var fixture = new TemporaryFiles();
		var root = fixture.Write("instructions/rule.instructions.md", "---\napplyTo: \"src/{*.cs\"\n---\n");
		var result = await PowerShell("build/guidance/Get-ApplicableInstructions.ps1",
			["-ProjectRoot", fixture.Root, "-InstructionsRoot", Path.GetDirectoryName(root)!, "-Path", "src/File.cs", "-Json"], cancellationToken);
		result.ExitCode.Should().NotBe(0);
		result.Error.Should().Contain("unmatched");
		fixture.Write("instructions/rule.instructions.md", "---\napplyTo: \"**/*.cs\"\n---\n");
		result = await PowerShell("build/guidance/Get-ApplicableInstructions.ps1",
			["-ProjectRoot", fixture.Root, "-InstructionsRoot", Path.GetDirectoryName(root)!, "-Path", "../outside.cs", "-Json"], cancellationToken);
		result.ExitCode.Should().NotBe(0);
		result.Error.Should().Contain("inside the repository");
		result = await PowerShell("build/guidance/Get-ApplicableInstructions.ps1",
			["-ProjectRoot", fixture.Root, "-InstructionsRoot", Path.GetDirectoryName(root)!, "-Path", @"\\server\share\File.cs", "-Json"], cancellationToken);
		result.ExitCode.Should().NotBe(0);
		result.Error.Should().Contain("repository-relative");
	}

	private static List<string> CheckImports(string root)
	{
		var issues = new List<string>();
		using var document = ReadJson(root, ".github/instructions/imports.json");
		foreach (var property in document.RootElement.EnumerateObject())
		{
			if (property.Name is not ("schemaVersion" or "files"))
				issues.Add($"Unexpected import metadata: {property.Name}");
		}
		if (Number(document.RootElement, "schemaVersion") != 1)
			issues.Add("Unsupported import manifest version.");
		var entries = document.RootElement.GetProperty("files").EnumerateArray().ToArray();
		if (entries.Length == 0)
			issues.Add("The shared import allowlist is empty.");
		var allowed = new HashSet<string>(StringComparer.Ordinal);
		foreach (var entry in entries)
		{
			if (entry.EnumerateObject().Any(property => property.Name is not ("path" or "sha256")))
				issues.Add("Unexpected import entry metadata.");
			var relative = Text(entry, "path");
			if (!relative.StartsWith(".github/instructions/genesis/", StringComparison.Ordinal) || !allowed.Add(relative))
			{
				issues.Add($"Invalid import path: {relative}");
				continue;
			}
			var file = Resolve(root, relative);
			if (!File.Exists(file))
				issues.Add($"Missing import: {relative}");
			else if (!string.Equals(Hash(file), Text(entry, "sha256"), StringComparison.Ordinal))
				issues.Add($"Changed import: {relative}");
		}
		var sharedRoot = Resolve(root, ".github/instructions/genesis");
		if (Directory.Exists(sharedRoot))
		{
			foreach (var file in Directory.EnumerateFiles(sharedRoot, "*", SearchOption.AllDirectories))
			{
				if (!allowed.Contains(Relative(root, file)))
					issues.Add($"Unexpected import: {Relative(root, file)}");
			}
		}
		return issues;
	}

	private static void CheckBudget(string root, string relative, int maxLines, int maxBytes, List<string> issues)
	{
		var path = Resolve(root, relative);
		if (File.ReadAllLines(path).Length > maxLines)
			issues.Add($"Line budget exceeded: {relative}");
		if (Encoding.UTF8.GetByteCount(File.ReadAllText(path)) > maxBytes)
			issues.Add($"Byte budget exceeded: {relative}");
	}

	private static void CheckRedirect(string root, string relative, string expected, List<string> issues)
	{
		if (!string.Equals(File.ReadAllText(Resolve(root, relative)).Trim(), expected, StringComparison.Ordinal))
			issues.Add($"Invalid redirect: {relative}");
	}

	private static void CheckOwner(string root, string relative, List<string> issues)
	{
		var path = Resolve(root, relative);
		if (!File.Exists(path) && !Directory.Exists(path))
			issues.Add($"Missing owner: {relative}");
	}

	private static HashSet<string> NavigationPages(string root, List<string> issues)
	{
		var pages = new HashSet<string>(StringComparer.Ordinal);
		var folders = new List<(int Indent, string Name)>();
		var inToc = false;
		var lines = File.ReadAllLines(Resolve(root, "docs/_docset.yml"));
		for (var index = 0; index < lines.Length; index++)
		{
			var line = lines[index];
			if (line == "toc:")
				inToc = true;
			if (!inToc)
				continue;
			var match = TocEntry().Match(line);
			if (!match.Success)
				continue;
			var indent = match.Groups["indent"].Length;
			while (folders.Count > 0 && indent <= folders[^1].Indent)
				folders.RemoveAt(folders.Count - 1);
			var name = match.Groups["name"].Value.Trim().Trim('"', '\'');
			var parent = folders.Count == 0 ? "docs" : "docs/" + string.Join('/', folders.Select(folder => folder.Name));
			var relative = parent + "/" + name;
			var kind = match.Groups["kind"].Value;
			if (kind == "folder")
				folders.Add((indent, name));
			else if (kind == "listing")
			{
				var glob = index + 1 < lines.Length ? NavigationGlob().Match(lines[index + 1]) : Match.Empty;
				if (!glob.Success || glob.Groups["pattern"].Value.Trim().Trim('"', '\'') != "**/*.md")
				{
					issues.Add($"Unsupported navigation glob: {relative}");
					continue;
				}
				var directory = Resolve(root, relative);
				if (!Directory.Exists(directory))
					issues.Add($"Missing navigation listing: {relative}");
				else
				{
					foreach (var file in Directory.EnumerateFiles(directory, "*.md", SearchOption.AllDirectories))
						pages.Add(Relative(root, file));
				}
			}
			else
			{
				CheckOwner(root, relative, issues);
				pages.Add(relative);
			}
		}
		return pages;
	}

	private static void CheckLinks(string root, string file, List<string> issues)
	{
		foreach (var line in ProseLines(File.ReadAllText(file)))
		{
			foreach (Match match in InlineLink().Matches(line).Concat(ReferenceLink().Matches(line)))
			{
				var target = match.Groups["target"].Value.Trim('<', '>');
				if (ExternalLink().IsMatch(target) || target.StartsWith('/') || target.Contains("{{", StringComparison.Ordinal))
					continue;
				var pieces = target.Split('#', 2);
				var relative = Uri.UnescapeDataString(pieces[0].Split('?', 2)[0]);
				var destination = relative.Length == 0
					? file
					: Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, relative.Replace('/', Path.DirectorySeparatorChar)));
				if (!Inside(root, destination))
					issues.Add($"Escaping link: {Relative(root, file)} -> {target}");
				else if (!File.Exists(destination) && !Directory.Exists(destination))
					issues.Add($"Missing link: {Relative(root, file)} -> {target}");
				else if (pieces.Length == 2 && pieces[1].Length > 0 && destination.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
					&& !HeadingIds(File.ReadAllText(destination)).Contains(Uri.UnescapeDataString(pieces[1])))
					issues.Add($"Missing anchor: {Relative(root, file)} -> {target}");
			}
		}
	}

	private static IEnumerable<string> ProseLines(string text)
	{
		char fence = '\0';
		var length = 0;
		foreach (var line in text.Split('\n'))
		{
			var match = Fence().Match(line);
			if (match.Success)
			{
				var marker = match.Groups["marker"].Value;
				if (fence == '\0')
				{
					fence = marker[0];
					length = marker.Length;
				}
				else if (marker[0] == fence && marker.Length >= length)
					fence = '\0';
				continue;
			}
			if (fence == '\0')
				yield return line;
		}
	}

	private static HashSet<string> HeadingIds(string text)
	{
		var ids = new HashSet<string>(StringComparer.Ordinal);
		foreach (var line in ProseLines(text))
		{
			var match = Heading().Match(line);
			if (!match.Success)
				continue;
			var title = match.Groups["title"].Value;
			title = InlineLink().Replace(title, item => item.Groups["label"].Value);
			char[] characters =
			[
				.. title.ToLowerInvariant().Where(character => char.IsLetterOrDigit(character) || char.IsWhiteSpace(character) || character is '-' or '_'),
			];
			var slug = HeadingWhitespace().Replace(new string(characters).Trim(), "-");
			var unique = slug;
			for (var suffix = 1; !ids.Add(unique); suffix++)
				unique = slug + "-" + suffix.ToString(CultureInfo.InvariantCulture);
		}
		return ids;
	}

	private static Dictionary<string, string> Frontmatter(string text)
	{
		var match = MetadataBlock().Match(text.Replace("\r\n", "\n", StringComparison.Ordinal));
		if (!match.Success)
			throw new InvalidOperationException("YAML frontmatter is missing.");
		return MetadataEntry().Matches(match.Groups["metadata"].Value)
			.ToDictionary(item => item.Groups["key"].Value, item => item.Groups["value"].Value.Trim().Trim('"', '\''), StringComparer.Ordinal);
	}

	private static string FindRepositoryRoot()
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
		{
			if (File.Exists(Path.Combine(directory.FullName, "curb.slnx")))
				return directory.FullName;
		}
		throw new InvalidOperationException("Cannot locate the Curb repository for guidance tests.");
	}

	private static string Resolve(string root, string relative)
	{
		if (Path.IsPathRooted(relative) || relative.Contains(':') || relative.Replace('\\', '/').Split('/').Any(part => part is "" or "." or ".."))
			throw new InvalidOperationException("An owner must be a repository-relative path.");
		return Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
	}

	private static bool Inside(string root, string path) =>
		path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
			OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

	private static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
	private static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
	private static JsonDocument ReadJson(string root, string relative) => JsonDocument.Parse(File.ReadAllText(Resolve(root, relative)));
	private static string Text(JsonElement value, string key) =>
		value.GetProperty(key).GetString() ?? throw new InvalidOperationException($"Missing string: {key}");
	private static int Number(JsonElement value, string key) => value.GetProperty(key).GetInt32();
	private static IEnumerable<string> Strings(JsonElement array) =>
		array.EnumerateArray().Select(value => value.GetString() ?? throw new InvalidOperationException("Expected string."));

	private static Task<ProcessResult> PowerShell(string relative, string[] arguments, CancellationToken cancellationToken) =>
		Run("pwsh", RepositoryRoot, ["-NoLogo", "-NoProfile", "-NonInteractive", "-File", Resolve(RepositoryRoot, relative), .. arguments], cancellationToken);

	private static async Task<ProcessResult> Run(string command, string directory, string[] arguments, CancellationToken cancellationToken)
	{
		using var process = new Process
		{
			StartInfo = new ProcessStartInfo(command)
			{
				WorkingDirectory = directory,
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			},
		};
		foreach (var argument in arguments)
			process.StartInfo.ArgumentList.Add(argument);
		if (!process.Start())
			throw new InvalidOperationException($"Could not start {command}.");
		var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
		var error = process.StandardError.ReadToEndAsync(cancellationToken);
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(TimeSpan.FromSeconds(60));
		try
		{
			await process.WaitForExitAsync(timeout.Token);
		}
		catch (OperationCanceledException)
		{
			if (!process.HasExited)
				process.Kill(entireProcessTree: true);
			throw;
		}
		return new ProcessResult(process.ExitCode, await output, await error);
	}

	private sealed record ProcessResult(int ExitCode, string Output, string Error);

	private sealed class TemporaryFiles : IDisposable
	{
		public string Root { get; } = Path.Combine(Path.GetTempPath(), "curb-guidance-tests-" + Guid.NewGuid().ToString("N"));

		public string Write(string relative, string text)
		{
			var file = Resolve(Root, relative);
			Directory.CreateDirectory(Path.GetDirectoryName(file)!);
			File.WriteAllText(file, text, new UTF8Encoding(false));
			return file;
		}

		public string WritePaths(IEnumerable<string> paths)
		{
			Directory.CreateDirectory(Root);
			var path = Path.Combine(Root, "paths.json");
			using var stream = File.Create(path);
			using var writer = new Utf8JsonWriter(stream);
			writer.WriteStartArray();
			foreach (var value in paths)
				writer.WriteStringValue(value);
			writer.WriteEndArray();
			return path;
		}

		public void WriteImportManifest(string relative)
		{
			var path = Resolve(Root, ".github/instructions/imports.json");
			using var stream = File.Create(path);
			using var writer = new Utf8JsonWriter(stream);
			writer.WriteStartObject();
			writer.WriteNumber("schemaVersion", 1);
			writer.WriteStartArray("files");
			writer.WriteStartObject();
			writer.WriteString("path", relative);
			writer.WriteString("sha256", Hash(Resolve(Root, relative)));
			writer.WriteEndObject();
			writer.WriteEndArray();
			writer.WriteEndObject();
		}

		public void Dispose()
		{
			if (Directory.Exists(Root))
				Directory.Delete(Root, recursive: true);
		}
	}

	[GeneratedRegex(@"(?m)^(?<indent>\s*)-\s+(?<kind>file|folder|listing):\s*(?<name>.+?)\s*$", RegexOptions.None, 1000)]
	private static partial Regex TocEntry();
	[GeneratedRegex(@"^\s*glob:\s*(?<pattern>.+?)\s*$", RegexOptions.None, 1000)]
	private static partial Regex NavigationGlob();
	[GeneratedRegex(@"!?\[(?<label>[^\]\r\n]*)\]\((?<target><[^>\r\n]+>|[^\s)]+)(?:\s+""[^""]*"")?\)", RegexOptions.None, 1000)]
	private static partial Regex InlineLink();
	[GeneratedRegex(@"^\s*\[[^\]]+\]:\s*(?<target>\S+)", RegexOptions.None, 1000)]
	private static partial Regex ReferenceLink();
	[GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.-]*:", RegexOptions.None, 1000)]
	private static partial Regex ExternalLink();
	[GeneratedRegex(@"^\s*(?<marker>`{3,}|~{3,})", RegexOptions.None, 1000)]
	private static partial Regex Fence();
	[GeneratedRegex(@"^#{1,6}\s+(?<title>.+?)\s*#*\s*$", RegexOptions.None, 1000)]
	private static partial Regex Heading();
	[GeneratedRegex(@"\s+", RegexOptions.None, 1000)]
	private static partial Regex HeadingWhitespace();
	[GeneratedRegex(@"\A(?:\uFEFF)?---\r?\n(?<metadata>.*?)\r?\n---", RegexOptions.Singleline, 1000)]
	private static partial Regex MetadataBlock();
	[GeneratedRegex(@"(?m)^(?<key>[A-Za-z_][A-Za-z0-9_]*):[ \t]*(?<value>[^\r\n]*)$", RegexOptions.None, 1000)]
	private static partial Regex MetadataEntry();
	[GeneratedRegex(@"^\d{4}-[a-z0-9-]+\.md$", RegexOptions.None, 1000)]
	private static partial Regex AdrName();
}
