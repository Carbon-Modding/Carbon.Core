using System.Security.Cryptography;
using System.Text;

namespace Carbon.Pooling;

public class HookStringPool
{
	public static Dictionary<string, uint> HookNamePoolString = new();
	public static Dictionary<uint, string> HookNamePoolInt = new();

	public static uint GetOrAdd(string name)
	{
		if (HookNamePoolString.TryGetValue(name, out var hash))
		{
			return hash;
		}

		hash = ManifestHash(name);
		HookNamePoolString[name] = hash;
		HookNamePoolInt[hash] = name;
		return hash;
	}

	public static string GetOrAdd(uint name)
	{
		return HookNamePoolInt.TryGetValue(name, out var hash) ? hash : string.Empty;
	}

	private static uint ManifestHash(string str)
	{
		return string.IsNullOrEmpty(str) ? 0 : BitConverter.ToUInt32(new MD5CryptoServiceProvider().ComputeHash(Encoding.UTF8.GetBytes(str)), 0);
	}
}
