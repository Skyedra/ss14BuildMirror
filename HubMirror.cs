using System.Net;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

public class HubMirror
{
	public static string hubServersLocalPath = "hub/api/servers";
	public static string hubServersUpstream = "https://central.spacestation14.io/hub/api/servers";

	private const int MINIMUM_SERVERS_TO_CONSIDER_GOOD_MANIFEST = 3;

	public static void Mirror()
	{
		// Download manifest
		string hubServersString = "";
		using (var client = new WebClient())
		{
			hubServersString = client.DownloadString(hubServersUpstream);
		}

		// Now sanity check returned result before saving it

		// Convert manifest to JSON
		JArray json = JArray.Parse(hubServersString);

		int serversProcessed = 0;

		foreach (JObject serverEntry in json)
		{
			if (serverEntry.ContainsKey("address"))
			{
				string address = (string) serverEntry["address"];

				if (!string.IsNullOrEmpty(address))
					serversProcessed++;
			}
		}

		// All files iterated OK
		Console.WriteLine("[INFO] Processed " + serversProcessed + " servers.");

		// Just in case something weird happened with JSON, don't replace our existing JSON
		// unless it looked like things generally succeeded.
		if (serversProcessed >= MINIMUM_SERVERS_TO_CONSIDER_GOOD_MANIFEST)
		{
			string directory = hubServersLocalPath.Substring(0, hubServersLocalPath.LastIndexOf('/'));
			Directory.CreateDirectory(directory);
			File.WriteAllText(hubServersLocalPath, json.ToString(Newtonsoft.Json.Formatting.Indented));
			Console.WriteLine("[INFO] Wrote out servers.");
		} else {
			Console.WriteLine("[WARN] Not enough servers, sus.");
		}
	}
}