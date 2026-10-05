using Stampeded.Core.AzureDevOps;
using Stampeded.Core.Bitbucket;
using Stampeded.Core.GitHub;

namespace Stampeded;

/// <summary>
/// Most-recently-opened repository paths, one per line under the user data directory.
/// </summary>
public static class RecentRepos
{
	const int Capacity = 10;

	static string FilePath => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "stampeded", "recent-repos.txt");

	public static IReadOnlyList<string> Load()
	{
		try
		{
			if (!File.Exists(FilePath))
				return [];

			var seen = new HashSet<string>(StringComparer.Ordinal);
			var rows = new List<string>();
			foreach (string line in File.ReadAllLines(FilePath))
			{
				string entry = NormalizeEntry(line);
				if (entry.Length == 0 || !Directory.Exists(entry))
					continue;
				if (seen.Add(CanonicalKey(entry)))
					rows.Add(entry);
			}
			return rows;
		}
		catch (IOException)
		{
			return [];
		}
	}

	public static void Record(string repoPath)
	{
		repoPath = NormalizeEntry(repoPath);
		if (repoPath.Length == 0)
			return;
		string key = CanonicalKey(repoPath);
		var list = Load().ToList();
		list.RemoveAll(p => CanonicalKey(p) == key);
		list.Insert(0, repoPath);
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
			File.WriteAllLines(FilePath, list.Take(Capacity));
		}
		catch (IOException)
		{
			// Recents are a convenience; never fail an open over them.
		}
	}

	static string NormalizeEntry(string entry)
	{
		entry = entry.Trim();
		if (entry.Length == 0 || IsRepoUrl(entry))
			return entry.TrimEnd('/');
		try
		{
			return Path.TrimEndingDirectorySeparator(Path.GetFullPath(entry));
		}
		catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
		{
			return entry;
		}
	}

	static string CanonicalKey(string entry)
	{
		if (GitHubUrl.TryParse(entry, out string owner, out string repo, out _))
			return $"url:github:{owner.ToUpperInvariant()}/{repo.ToUpperInvariant()}";
		if (AzureDevOpsUrl.TryParse(entry, out string org, out string project, out string azRepo, out _))
			return $"url:azdo:{org.ToUpperInvariant()}/{project.ToUpperInvariant()}/{azRepo.ToUpperInvariant()}";
		if (BitbucketUrl.TryParse(entry, out string bitbucketBase, out string bitbucketProject, out string bitbucketRepo, out _))
			return $"url:bitbucket:{NormalizeUrlBase(bitbucketBase)}/{bitbucketProject.ToUpperInvariant()}/{bitbucketRepo.ToUpperInvariant()}";
		if (TryGenericUrlKey(entry, out string urlKey))
			return "url:" + urlKey;

		string normalized = NormalizeEntry(entry);
		return OperatingSystem.IsWindows()
			? "path:" + normalized.ToUpperInvariant()
			: "path:" + normalized;
	}

	static bool IsRepoUrl(string entry)
		=> GitHubUrl.TryParse(entry, out _, out _, out _)
			|| AzureDevOpsUrl.TryParse(entry, out _, out _, out _, out _)
			|| BitbucketUrl.TryParse(entry, out _, out _, out _, out _)
			|| TryGenericUrlKey(entry, out _);

	static bool TryGenericUrlKey(string entry, out string key)
	{
		key = "";
		if (TryScpLikeUrlKey(entry, out key))
			return true;
		if (!Uri.TryCreate(entry, UriKind.Absolute, out var uri)
			|| uri.Host.Length == 0
			|| uri.Scheme is not ("http" or "https" or "ssh" or "git"))
		{
			return false;
		}

		string path = Uri.UnescapeDataString(uri.AbsolutePath).TrimEnd('/');
		if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
			path = path[..^4];
		key = $"{uri.Scheme.ToUpperInvariant()}://{NormalizeHostPort(uri)}{path}";
		return true;
	}

	static bool TryScpLikeUrlKey(string entry, out string key)
	{
		key = "";
		int colon = entry.IndexOf(':');
		if (colon <= 0 || entry[..colon].Contains('/') || colon + 1 >= entry.Length)
			return false;
		string host = entry[..colon];
		int at = host.LastIndexOf('@');
		if (at >= 0)
			host = host[(at + 1)..];
		if (host.Length == 0 || !host.Contains('.'))
			return false;
		string path = entry[(colon + 1)..].Trim('/');
		if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
			path = path[..^4];
		key = $"SSH://{host.ToUpperInvariant()}/{path}";
		return true;
	}

	static string NormalizeUrlBase(string url)
	{
		if (!Uri.TryCreate(url.TrimEnd('/'), UriKind.Absolute, out var uri))
			return url.TrimEnd('/').ToUpperInvariant();
		return $"{uri.Scheme.ToUpperInvariant()}://{NormalizeHostPort(uri)}{uri.AbsolutePath.TrimEnd('/')}";
	}

	static string NormalizeHostPort(Uri uri)
	{
		string host = uri.Host.ToUpperInvariant();
		return uri.IsDefaultPort ? host : $"{host}:{uri.Port}";
	}
}
