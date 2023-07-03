using System.Net;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

public class BuildMirror
{
	public static string buildStoragePath = "builds/robust/builds/";
	public static string mirrorBuildUrl = "https://cdn.blepstation.com/builds/robust/builds/";
	public static string upstreamBaseUrlToReplace = "https://cdn.centcomm.spacestation14.com/builds/robust/builds/";
	public static string manifestLocalPath = "manifest/manifest.json";
	public static string manifestUpstream = "https://central.spacestation14.io/builds/robust/manifest.json";

	/// <summary>
	/// If set, the version string must start with a number that is this number or larger.
	/// Set to 0 to pull all versions (will use lots of disk space)
	/// </summary>
	public static int minimumVersionToPull = 132;


	private const int MINIMUM_FILES_TO_CONSIDER_GOOD_MANIFEST = 2;

	public static void Main()
	{
		// Download manifest
		string buildManifest = "";
		using (var client = new WebClient())
		{
			buildManifest = client.DownloadString(manifestUpstream);
		}

		// Convert manifest to JSON
		JObject json = JObject.Parse(buildManifest);

		int filesProcessed = 0;

		foreach (var versionKVP in json)
		{
			string version = (string) versionKVP.Key;
			bool insecure = (bool) versionKVP.Value["insecure"];

			// Check version doesn't contain strange characters / don't allow path escape
			if (ContainsInvalidCharacters(version))
			{
				Console.WriteLine("[WARN] Skipping version because invalid characters: " + version);
				continue;
			}

			if (VersionIsOld(version))
			{
				Console.WriteLine("[INFO] Skipping old version: " + version);
				continue;
			}

			if (versionKVP.Value.Type == JTokenType.Object)
			{
				var platforms = (JObject) versionKVP.Value["platforms"];

				if (platforms != null)
				{
					foreach (var platformKVP in platforms)
					{
						string platformName = platformKVP.Key;
						string sig = (string) platformKVP.Value["sig"];
						string sha256 = (string) platformKVP.Value["sha256"];
						string url = (string) platformKVP.Value["url"];

						// Check platform name doesn't contain strange characters / don't allow path escape
						if (ContainsInvalidCharacters(platformName))
						{
							Console.WriteLine("[WARN] Skipping platform because invalid characters: " + platformName);
							continue;
						}

						// Does file exist locally?
						if (CheckFileAndDownloadIfNeeded(url, sha256, version, platformName))
						{
							// Update manifest URL to use our path
							url = url.Replace(upstreamBaseUrlToReplace, mirrorBuildUrl);
							platformKVP.Value["url"] = url;
							filesProcessed++;
						} else {
							// Problem.  Bail out
							Console.WriteLine("[ERR] Bailing out early due to download problem.");
							return;
						}
					}
				}
			}
		}

		// All files iterated OK
		Console.WriteLine("[INFO] Processed " + filesProcessed + " files.");

		// Just in case something weird happened with JSON, don't replace our existing JSON
		// unless it looked like things generally succeeded.
		if (filesProcessed >= MINIMUM_FILES_TO_CONSIDER_GOOD_MANIFEST)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(manifestLocalPath));
			File.WriteAllText(manifestLocalPath, json.ToString(Newtonsoft.Json.Formatting.Indented));
			Console.WriteLine("[INFO] Wrote out manifest.");
		} else {
			Console.WriteLine("[WARN] Not enough files, sus.");
		}
	}

	private static bool VersionIsOld(string version)
	{
		if (minimumVersionToPull <= 0)
			return false;

		try 
		{
			int firstNumber = int.Parse(version.Substring(0, version.IndexOf('.')));
			return firstNumber < minimumVersionToPull;
		} catch (Exception) { }
		return true; // weirdly named version, skip it
	}

	private static bool ContainsInvalidCharacters(string checkString)
	{
		if (!checkString.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.'))
			return true;
		if (checkString.Contains(".."))
			return true;
		return false;
	}

	private static bool CheckFileAndDownloadIfNeeded(string url, string sha256, string version, string platform)
	{
		string filename = Path.GetFileName(url);

		// Safety check to prevent path escape
		if (filename.StartsWith('/') || filename.Contains(".."))
		{
			Console.WriteLine("[WARN] Skipping download because invalid file name characters: " + filename);
			return false;
		}

		string filePath = buildStoragePath + version + "/" + filename;

		if (File.Exists(filePath))
		{
			// Check SHA match
			if (SHA256CheckSum(filePath) == sha256)
			{
				Console.WriteLine("[INFO] file already ok: " + filePath);
				return true;
			}
		}

		Console.WriteLine("[DBG] Downloading file: " + url);

		using (var client = new WebClient())
		{
			Directory.CreateDirectory(Path.GetDirectoryName(filePath));
			client.DownloadFile(url, filePath);
		}	

		if (File.Exists(filePath))
		{
			// Check SHA match
			string checksum = SHA256CheckSum(filePath);
			if (checksum == sha256)
			{
				Console.WriteLine("[INFO] file downloaded + verified ok: " + filePath);
				return true;
			}

			// Broken file, delete it
			File.Delete(filePath);
		}

		Console.WriteLine("[WARN] file download / verification failure: " + filePath);
		return false;
	}

	private static string SHA256CheckSum(string filePath)
	{
		using (SHA256 SHA256 = SHA256Managed.Create())
		{
			using (FileStream fileStream = File.OpenRead(filePath))
				return Convert.ToHexString(SHA256.ComputeHash(fileStream));
				//return Convert.ToBase64String(SHA256.ComputeHash(fileStream));
		}
	}
}