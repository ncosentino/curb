using System.IO.Abstractions;
using System.Text;
using EditorConfig.Core;
using Nullean.Curb.EditorConfig;
using Nullean.Curb.LayoutRules;
using Nullean.Curb.Options;

namespace Nullean.Curb.Cli;

internal sealed class LayoutRuleLoader(IFileSystem fileSystem, CurbEditorConfig editorConfig)
{
	internal const string Key = OptionCatalog.LayoutRulesKey;
	private readonly Dictionary<(string Path, string Base), LoadedPack> _loaded = [];
	private readonly HashSet<string> _dependencies = new(StringComparer.Ordinal);
	internal IReadOnlyCollection<string> Dependencies => _dependencies;

	internal void WriteDependencies(string path)
	{
		var full = fileSystem.Path.GetFullPath(path);
		var directory = fileSystem.Path.GetDirectoryName(full);
		if (!string.IsNullOrEmpty(directory))
			fileSystem.Directory.CreateDirectory(directory);
		var temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			fileSystem.File.WriteAllLines(temporary, _dependencies.Order(StringComparer.Ordinal), new UTF8Encoding(false));
			fileSystem.File.Move(temporary, full, overwrite: true);
		}
		finally
		{
			if (fileSystem.File.Exists(temporary))
				fileSystem.File.Delete(temporary);
		}
	}

	private sealed record FilteredRule(WrapperLayoutRule Rule, GlobMatcher[] Filters);
	private sealed record LoadedPack(FilteredRule[] Rules, LayoutRuleSet All, UInt128 Fingerprint);

	internal ResolvedLayoutRules For(string sourcePath, FileConfiguration configuration, string? overridePath = null, string? overrideBase = null)
	{
		foreach (var key in configuration.Properties.Keys)
		{
			if (key.StartsWith("curb_", StringComparison.Ordinal) && !OptionCatalog.ConfigurationKeys.Contains(key))
				throw new LayoutRuleConfigurationException("An unsupported Curb configuration key was specified.");
		}
		string? selected = overridePath;
		string? origin = null;
		if (selected is null)
		{
			if (!configuration.Properties.TryGetValue(Key, out selected))
				return default;
			origin = editorConfig.OriginDirectoryFor(sourcePath, Key)
				?? throw new LayoutRuleConfigurationException("The declaring EditorConfig for the layout policy is unavailable.");
		}
		if (selected.Equals("none", StringComparison.OrdinalIgnoreCase) || selected.Equals("unset", StringComparison.OrdinalIgnoreCase))
			return default;
		if (string.IsNullOrWhiteSpace(selected) || selected.IndexOfAny(['\r', '\n', '\0']) >= 0
			|| selected.StartsWith(@"\\", StringComparison.Ordinal) || selected.StartsWith("//", StringComparison.Ordinal)
			|| (Uri.TryCreate(selected, UriKind.Absolute, out var uri) && !uri.IsFile))
			throw new LayoutRuleConfigurationException("The layout policy must be a local file path or none.");

		var path = fileSystem.Path.GetFullPath(origin is null ? selected : fileSystem.Path.Combine(origin, selected));
		if (origin is not null && !Within(origin, path))
			throw new LayoutRuleConfigurationException("An EditorConfig layout policy must stay inside its declaring configuration directory.");
		var baseDirectory = fileSystem.Path.GetFullPath(overrideBase ?? fileSystem.Path.GetDirectoryName(path)!);
		_dependencies.Add(path);

		if (!_loaded.TryGetValue((path, baseDirectory), out var pack))
		{
			try
			{
				RejectLinks(path);
				using var stream = fileSystem.File.OpenRead(path);
				using var buffer = new MemoryStream();
				var chunk = new byte[8192];
				int read;
				while ((read = stream.Read(chunk, 0, Math.Min(chunk.Length, LayoutRuleCompiler.MaximumBytes + 1 - checked((int)buffer.Length)))) > 0)
				{
					buffer.Write(chunk, 0, read);
					if (buffer.Length > LayoutRuleCompiler.MaximumBytes)
						throw new LayoutRuleConfigurationException("The layout rule file exceeds 1 MiB.");
				}
				var bytes = buffer.ToArray();
				var definitions = LayoutRuleCompiler.Compile(bytes);
				var rules = new FilteredRule[definitions.Length];
				for (var i = 0; i < rules.Length; i++)
				{
					var definition = definitions[i];
					var filters = definition.Files.Select(pattern => GlobMatcher.Create(pattern, new GlobMatcherOptions
					{
						Dot = true, AllowWindowsPaths = true, AllowWindowsPathsInPatterns = true,
						NoBrace = true, NoComment = true, NoNegate = true,
					})).ToArray();
					rules[i] = new FilteredRule(definition.Rule, filters);
				}
				var fingerprint = Fingerprint.OfContent(bytes);
				fingerprint = Fingerprint.Combine(fingerprint, Fingerprint.OfContent(Encoding.UTF8.GetBytes(baseDirectory)));
				pack = new LoadedPack(rules, new LayoutRuleSet(definitions.Select(definition => definition.Rule)), fingerprint);
				_loaded.Add((path, baseDirectory), pack);
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
			{
				throw new LayoutRuleConfigurationException("The selected layout policy cannot be read.");
			}
		}
		var relative = fileSystem.Path.GetRelativePath(baseDirectory, fileSystem.Path.GetFullPath(sourcePath));
		if (!Within(baseDirectory, fileSystem.Path.GetFullPath(sourcePath)))
			return new ResolvedLayoutRules(null, pack.Fingerprint, path, baseDirectory);
		var matched = new List<WrapperLayoutRule>();
		foreach (var entry in pack.Rules)
		{
			foreach (var filter in entry.Filters)
			{
				if (!filter.IsMatch(relative))
					continue;
				matched.Add(entry.Rule);
				break;
			}
		}
		var selectedRules = matched.Count == 0 ? null : matched.Count == pack.Rules.Length ? pack.All : new LayoutRuleSet(matched);
		return new ResolvedLayoutRules(selectedRules, pack.Fingerprint, path, baseDirectory);
	}

	private bool Within(string directory, string path)
	{
		var relative = fileSystem.Path.GetRelativePath(fileSystem.Path.GetFullPath(directory), path);
		return !fileSystem.Path.IsPathRooted(relative) && relative != ".."
			&& !relative.StartsWith(".." + fileSystem.Path.DirectorySeparatorChar, StringComparison.Ordinal)
			&& !relative.StartsWith(".." + fileSystem.Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
	}

	private void RejectLinks(string path)
	{
		if (fileSystem.FileInfo.New(path).LinkTarget is not null)
			throw new LayoutRuleConfigurationException("Symbolic layout policy files are not supported.");
		var directory = fileSystem.Path.GetDirectoryName(path);
		while (!string.IsNullOrEmpty(directory))
		{
			if (fileSystem.DirectoryInfo.New(directory).LinkTarget is not null)
				throw new LayoutRuleConfigurationException("Symbolic layout policy directories are not supported.");
			directory = fileSystem.Path.GetDirectoryName(directory);
		}
	}
}
