using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NemesisSystem.BulletImpact;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RDR2;
using RDR2.Math;
using RDR2.Native;
using RDR2.UI;

namespace NemesisSystem;

public partial class NemesisMod
{
	private static readonly Dictionary<string, int> LoreRegard = new Dictionary<string, int>
	{
		["dutch"] = 55,
		["hosea"] = 70,
		["john"] = 20,
		["abigail"] = 15,
		["jack"] = 60,
		["charles"] = 45,
		["lenny"] = 55,
		["javier"] = 35,
		["sean"] = 40,
		["bill"] = 15,
		["micah"] = -55,
		["karen"] = 30,
		["sadie"] = 30,
		["tilly"] = 35,
		["marybeth"] = 50,
		["molly"] = -15,
		["grimshaw"] = 20,
		["pearson"] = 25,
		["strauss"] = 5,
		["uncle"] = 30,
		["swanson"] = 35,
		["trelawny"] = 30,
		["kieran"] = -10
	};

	private static readonly Dictionary<string, int[]> ChapterRegard = new Dictionary<string, int[]>
	{
		["dutch"] = new int[7] { 60, 55, 50, 40, 15, -40, -40 },
		["hosea"] = new int[7] { 70, 70, 72, 75, 75, 75, 75 },
		["john"] = new int[7] { 15, 20, 25, 30, 40, 60, 60 },
		["abigail"] = new int[7] { 10, 15, 20, 25, 35, 60, 60 },
		["jack"] = new int[7] { 55, 60, 60, 65, 65, 70, 70 },
		["charles"] = new int[7] { 40, 45, 50, 55, 65, 75, 75 },
		["lenny"] = new int[7] { 50, 55, 60, 65, 65, 65, 65 },
		["javier"] = new int[7] { 35, 35, 35, 35, 25, -30, -30 },
		["sean"] = new int[7] { 40, 40, 45, 45, 45, 45, 45 },
		["bill"] = new int[7] { 15, 15, 10, 5, 0, -35, -35 },
		["micah"] = new int[7] { -50, -55, -60, -65, -70, -90, -90 },
		["karen"] = new int[7] { 30, 30, 30, 25, 15, 20, 20 },
		["sadie"] = new int[7] { 20, 30, 40, 50, 65, 75, 75 },
		["tilly"] = new int[7] { 30, 35, 40, 45, 55, 65, 65 },
		["marybeth"] = new int[7] { 45, 50, 55, 55, 60, 70, 70 },
		["molly"] = new int[7] { -10, -15, -20, -30, -40, -40, -40 },
		["grimshaw"] = new int[7] { 15, 20, 25, 30, 35, 40, 40 },
		["pearson"] = new int[7] { 20, 25, 25, 25, 25, 10, 10 },
		["strauss"] = new int[7] { 5, 5, 0, -10, -25, -30, -30 },
		["uncle"] = new int[7] { 30, 30, 30, 30, 35, 40, 40 },
		["swanson"] = new int[7] { 30, 35, 40, 45, 50, 55, 55 },
		["trelawny"] = new int[7] { 30, 30, 35, 35, 40, 45, 45 },
		["kieran"] = new int[7] { -20, -10, 10, 25, 25, 25, 25 }
	};

	private readonly Dictionary<string, int> _regardAt = new Dictionary<string, int>();

	internal int RegardChapter
	{
		get
		{
			if (Chronicle.StoryChapter < 1)
			{
				return 2;
			}
			return Math.Min(7, Chronicle.StoryChapter);
		}
	}

	internal int RegardBase(string key)
	{
		if (!ChapterRegard.TryGetValue(key, out var value))
		{
			if (!LoreRegard.TryGetValue(key, out var value2))
			{
				return 0;
			}
			return value2;
		}
		return value[RegardChapter - 1];
	}

	private Dictionary<string, int> EarnedMap
	{
		get
		{
			WorldState chronicle = Chronicle;
			if (chronicle.StoryRegardEarned != null)
			{
				return chronicle.StoryRegardEarned;
			}
			chronicle.StoryRegardEarned = new Dictionary<string, int>();
			if (chronicle.StoryRegard != null)
			{
				foreach (KeyValuePair<string, int> item in chronicle.StoryRegard)
				{
					chronicle.StoryRegardEarned[item.Key] = Math.Max(-80, Math.Min(80, item.Value - (LoreRegard.TryGetValue(item.Key, out var value) ? value : 0)));
				}
			}
			SaveWorld();
			return chronicle.StoryRegardEarned;
		}
	}

	internal int RegardEarned(string key)
	{
		if (!EarnedMap.TryGetValue(key, out var value))
		{
			return 0;
		}
		return value;
	}

	internal int RegardOf(string key)
	{
		return Math.Max(-100, Math.Min(100, RegardBase(key) + RegardEarned(key)));
	}

	internal int RegardOfRecord(NemesisRecord r)
	{
		if (r == null || r.Id == null || !r.Id.StartsWith("vdl_"))
		{
			return 0;
		}
		return RegardOf(r.Id.Substring(4));
	}

	internal static string RegardWord(int v)
	{
		if (v < 60)
		{
			if (v < 30)
			{
				if (v < 10)
				{
					if (v <= -10)
					{
						if (v <= -30)
						{
							if (v <= -60)
							{
								return "can't stand you";
							}
							return "dislikes you";
						}
						return "cool on you";
					}
					return "on the fence about you";
				}
				return "warm to you";
			}
			return "likes you";
		}
		return "would ride through hell for you";
	}

	internal void AddRegard(string key, int amount, string why)
	{
		if (string.IsNullOrEmpty(key) || amount == 0)
		{
			return;
		}
		int num = RegardOf(key);
		EarnedMap[key] = Math.Max(-80, Math.Min(80, RegardEarned(key) + amount));
		int num2 = RegardOf(key);
		if (num2 != num)
		{
			SaveWorld();
			string text = StoryRoster.FirstOrDefault((NemesisRecord r) => r.Id == "vdl_" + key)?.Name ?? key;
			GrudgeLog($"REGARD {text} {num} -> {num2} ({why})");
			Feed($"REGARD {text}: {num} -> {num2} - {why}", (amount > 0) ? Theme.Good : Theme.Warn);
		}
	}

	private bool RegardOnce(string key, string kind, int ms)
	{
		string key2 = key + "|" + kind;
		if (_regardAt.TryGetValue(key2, out var value) && Game.GameTime - value < ms)
		{
			return false;
		}
		_regardAt[key2] = Game.GameTime;
		return true;
	}
}
