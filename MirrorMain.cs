using System.Net;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

public class BuildMain
{
	public static void Main()
	{
		try
		{
			BuildMirror.Mirror();
		} catch (Exception e) {
			Console.WriteLine("BuildMirror EXCEPTION: " + e.Message);
		}

		try
		{
			HubMirror.Mirror();
		} catch (Exception e) {
			Console.WriteLine("HubMirror EXCEPTION: " + e.Message);
		}
	}
}