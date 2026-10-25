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
	private static readonly Dictionary<string, int> OldStartMen = new Dictionary<string, int>
	{
		["odriscoll"] = 120,
		["lemoyneraiders"] = 100,
		["bronte"] = 80,
		["klan"] = 50,
		["wapiti"] = 60,
		["murfree"] = 70,
		["skinners"] = 60,
		["army"] = 400
	};

	private const double WorldMenPer = 60.0;

	private const double WorldDayCap = 0.08;

	private readonly Dictionary<string, (long day, int startMen, int lost, bool said)> _worldLost = new Dictionary<string, (long, int, int, bool)>();

	private const int HireCost = 2;

	private const int HireReserve = 150;

	private static readonly string[] IconKindNames = new string[10] { "camp sites", "supplies", "funerals", "treasuries", "laid-up spots", "bars", "officers' habits", "plans coming", "named men out now", "calm zones" };

	private readonly Dictionary<IconKind, List<int>> _iconBlips = new Dictionary<IconKind, List<int>>();

	private Dictionary<string, int> MenMap => Chronicle.Men ?? (Chronicle.Men = new Dictionary<string, int>());

	internal int MenStart(Gang g)
	{
		if (g != null)
		{
			if (!StartMen.TryGetValue(g.Id, out var value))
			{
				return 60;
			}
			return value;
		}
		return 1;
	}

	internal int MenOf(Gang g)
	{
		if (g == null || g.StoryOnly)
		{
			return 0;
		}
		if (!Chronicle.MenRescaled)
		{
			RescaleMen();
		}
		if (!Chronicle.MenRescaled2)
		{
			RescaleMen2();
		}
		if (!MenMap.TryGetValue(g.Id, out var value))
		{
			value = MenStart(g);
			MenMap[g.Id] = value;
		}
		return value;
	}

	private void RescaleMen()
	{
		Chronicle.MenRescaled = true;
		foreach (KeyValuePair<string, int> oldStartMan in OldStartMen)
		{
			if (MenMap.TryGetValue(oldStartMan.Key, out var value))
			{
				int num = MidStartMen[oldStartMan.Key];
				int num2 = Math.Max((int)Math.Round((double)value * (double)num / (double)oldStartMan.Value), (int)((double)num * 0.4));
				MenMap[oldStartMan.Key] = num2;
				GrudgeLog($"MEN {Gangs.ById(oldStartMan.Key)?.Name ?? oldStartMan.Key}: {value} -> {num2} (the bigger starting numbers - {oldStartMan.Value} -> {num})");
			}
		}
		SaveWorld();
	}

	private void RescaleMen2()
	{
		Chronicle.MenRescaled2 = true;
		foreach (KeyValuePair<string, int> midStartMan in MidStartMen)
		{
			if (MenMap.TryGetValue(midStartMan.Key, out var value))
			{
				int num = StartMen[midStartMan.Key];
				int num2 = (int)Math.Round((double)value * (double)num / (double)midStartMan.Value);
				MenMap[midStartMan.Key] = num2;
				GrudgeLog($"MEN {Gangs.ById(midStartMan.Key)?.Name ?? midStartMan.Key}: {value} -> {num2} (hundreds and thousands - {midStartMan.Value} -> {num})");
			}
		}
		SaveWorld();
	}

	internal static int NamedWorth(int rank)
	{
		if (rank > 0)
		{
			return Math.Min(10, 5 + (int)Math.Round((double)(rank - 1) * 1.25));
		}
		return 1;
	}

	private bool DeclaredWar(string a, string b)
	{
		if (a != null && b != null && a != b)
		{
			return Wars.Any((WarRecord w) => w.Result == null && ((w.A == a && w.B == b) || (w.A == b && w.B == a)));
		}
		return false;
	}

	private int WorldLoss(Gang g, int rolled, long atMin, double mult = 1.0, int fightMen = -1)
	{
		if (g == null || rolled <= 0)
		{
			return 0;
		}
		int num = MenOf(g);
		int num2 = ((fightMen > 0) ? Math.Min(num, fightMen) : num);
		int num3 = (int)Math.Round((double)rolled * mult * Math.Max(1.0, (double)num2 / 60.0));
		double num4 = 0.02 + _rng.NextDouble() * 0.05;
		int num5 = Math.Max(1, (int)Math.Ceiling((double)num * num4));
		if (num3 > num5)
		{
			GrudgeLog($"MEN {g.Name}: a fight's losses {num3} -> {num5} (at most {num4 * 100.0:0.#}% of {num} this time)");
			num3 = num5;
		}
		long num6 = ((atMin > 0) ? atMin : GameMinutesNow()) / 1440;
		if (!_worldLost.TryGetValue(g.Id, out (long, int, int, bool) value) || value.Item1 != num6)
		{
			value = (num6, num, 0, false);
		}
		int num7 = Math.Max(0, Math.Max(1, (int)Math.Ceiling((double)value.Item2 * 0.08)) - value.Item3);
		if (num3 > num7)
		{
			if (num7 == 0 && !value.Item4)
			{
				GrudgeLog($"MEN {g.Name}: that day's losses are at the cap ({value.Item3}, 8% of {value.Item2})");
				value.Item4 = true;
			}
			num3 = num7;
		}
		_worldLost[g.Id] = (value.Item1, value.Item2, value.Item3 + num3, value.Item4);
		return num3;
	}

	internal int FieldCap(Gang g)
	{
		if (g != null && !g.OrdersNotFeelings)
		{
			return Math.Max(0, (int)((long)MenOf(g) * 200L / (3L * (long)Math.Max(1, MenStart(g)))));
		}
		return 999;
	}

	internal double TargetWeight(Gang g)
	{
		if (g == null || g.OrdersNotFeelings)
		{
			return 1.0;
		}
		double num = MenShare(g);
		if (!(num < 0.1))
		{
			if (!(num < 0.25))
			{
				return 1.0;
			}
			return 0.5;
		}
		return 0.15;
	}

	internal double MenShare(Gang g)
	{
		if (g != null)
		{
			if (!IsDestroyed(g))
			{
				return (double)MenOf(g) / (double)Math.Max(1, MenStart(g));
			}
			return 0.0;
		}
		return 1.0;
	}

	internal float MenPower(Gang g)
	{
		double num = MenShare(g);
		return (float)((num < 0.2) ? (0.2 + 2.0 * num) : (0.6 + 0.4 * Math.Min(1.25, num)));
	}

	internal void LoseMen(Gang g, int n, string why)
	{
		if (g == null || g.StoryOnly || n <= 0 || IsDestroyed(g))
		{
			return;
		}
		int num = MenOf(g);
		int num2 = Math.Max(0, num - n);
		if (num2 != num)
		{
			MenMap[g.Id] = num2;
			GrudgeLog($"MEN {g.Name}: {num} -> {num2} ({why})");
			SaveWorld();
			try
			{
				SuppliesForLosses(g, num - num2);
			}
			catch
			{
			}
			if ((double)num >= (double)MenStart(g) * 0.4 && (double)num2 < (double)MenStart(g) * 0.4)
			{
				Feed($"MEN the {g.Name} are down to {num2} men - their crews ride short", Theme.Warn);
			}
		}
	}

	private void DailyHiring(int days)
	{
		foreach (Gang item in Gangs.All.Where((Gang x) => !x.StoryOnly && !IsDestroyed(x)))
		{
			int num = MenOf(item);
			int num2 = MenStart(item);
			int num3 = num2 * 3 / 2;
			if (num >= num2)
			{
				continue;
			}
			int num4 = Math.Min(days, 30);
			int val = Math.Min(num2 - num, num3 - num);
			int num5;
			if (item.OrdersNotFeelings)
			{
				num5 = Math.Min(val, (int)Math.Round((double)num2 * 0.015 * (double)num4));
			}
			else
			{
				int num6 = (((double)num < (double)num2 * 0.9) ? Math.Min(val, (int)Math.Round((double)num2 * 0.01 * (double)num4)) : 0);
				if (_rng.NextDouble() > RecruitChance(item))
				{
					if (num6 <= 0)
					{
						GrudgeLog($"MEN {item.Name}: nobody signs on today (recruits {RecruitChance(item) * 100.0:0}%)");
						continue;
					}
					MenMap[item.Id] = num + num6;
					GrudgeLog($"MEN {item.Name}: {num} -> {num + num6} ({num6} local men throw in - the outfit's down to {num * 100 / Math.Max(1, num2)}% of its strength)");
					continue;
				}
				num5 = Math.Min(val, (int)Math.Round((double)num2 * 0.025 * (0.33 + 0.67 * Math.Min(1.2, TerritoryShare(item))) * (double)num4));
				int num7 = (HasPurse(item) ? (PurseOf(item) - 150) : 0);
				int num8 = ((!HasPurse(item)) ? num5 : ((num7 > 0) ? (((PurseOf(item) >= 600) ? num7 : (num7 / 2)) / 2) : 0));
				bool flag = LowSupplies(item);
				if (flag)
				{
					GrudgeLog($"MEN {item.Name}: no new hands - no supplies to feed or arm them ({SuppliesOf(item)}); locals only");
					num8 = 0;
				}
				int num9 = 0;
				if ((HasPurse(item) || flag) && num8 < num5)
				{
					num9 = Math.Min(num5 - num8, (int)Math.Round((double)num2 * 0.004 * (1.0 + _rng.NextDouble()) * (double)num4));
				}
				num5 = Math.Min(num5, num8);
				if (num5 + num9 < num6)
				{
					num9 = num6 - num5;
				}
				if (num5 > 0 && HasPurse(item))
				{
					AddPurse(item, -2 * num5, $"{num5} new hands hired");
				}
				if (num9 > 0)
				{
					GrudgeLog($"MEN {item.Name}: {num9} local men throw in for a share - no wages (purse ${PurseOf(item)})");
					num5 += num9;
				}
			}
			if (num5 > 0)
			{
				MenMap[item.Id] = num + num5;
				GrudgeLog($"MEN {item.Name}: {num} -> {num + num5} (hired over {num4} day(s))");
			}
		}
		SaveWorld();
	}

	internal int Strength(Gang g)
	{
		if (g == null || g.StoryOnly)
		{
			return 0;
		}
		if (IsDestroyed(g))
		{
			return 0;
		}
		var (num, num2, num3, num4) = StrengthParts(g);
		return (int)Math.Round(100.0 * (0.3 * num + 0.3 * num2 + 0.2 * num3 + 0.2 * num4));
	}

	internal (double ground, double men, double money, double supplies) StrengthParts(Gang g)
	{
		double item = Math.Min(1.25, TerritoryShare(g)) / 1.25;
		double item2 = Math.Min(1.25, MenShare(g)) / 1.25;
		double item3 = (HasPurse(g) ? Math.Min(1.0, (double)PurseOf(g) / (g.OrdersNotFeelings ? 6000.0 : 1200.0)) : 1.0);
		return (ground: item, men: item2, money: item3, supplies: (double)SuppliesOf(g) / 100.0);
	}

	internal static string StrengthWord(int s)
	{
		if (s < 75)
		{
			if (s < 50)
			{
				if (s < 30)
				{
					if (s < 15)
					{
						return "Breaking";
					}
					return "Weak";
				}
				return "Stretched";
			}
			return "Holding";
		}
		return "Strong";
	}

	private void LoseMenInEvent(WorldEvent ev)
	{
		if (ev == null)
		{
			return;
		}
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		foreach (WorldPart p in ev.People.Where((WorldPart worldPart) => worldPart.Outcome == "KILLED" && worldPart.Gang != null))
		{
			dictionary[p.Gang] = (dictionary.TryGetValue(p.Gang, out var value) ? value : 0) + NamedWorth(Roster.FirstOrDefault((NemesisRecord n) => n.Id == p.Id)?.Rank ?? 1);
		}
		bool flag = ev.Gangs != null && ev.Gangs.Count >= 2 && DeclaredWar(ev.Gangs[0], ev.Gangs[1]);
		int fightMen = ((ev.Gangs == null || ev.Gangs.Count == 0) ? (-1) : (from id in ev.Gangs
			select Gangs.ById(id) into x
			where x != null && !x.StoryOnly
			select MenOf(x)).DefaultIfEmpty(-1).Min());
		foreach (KeyValuePair<string, int> item in ev.UnnamedDead)
		{
			dictionary[item.Key] = (dictionary.TryGetValue(item.Key, out var value2) ? value2 : 0) + WorldLoss(Gangs.ById(item.Key), item.Value, ev.GameMin, flag ? 1.0 : 0.5, fightMen);
		}
		foreach (KeyValuePair<string, int> item2 in dictionary)
		{
			LoseMen(Gangs.ById(item2.Key), item2.Value, ev.Title ?? ev.Kind);
		}
	}

	internal string MenLine()
	{
		return "men: " + string.Join(", ", from g in Gangs.All
			where !g.StoryOnly && !IsDestroyed(g)
			select $"{g.Id} {MenOf(g)}/{MenStart(g)} (strength {Strength(g)})");
	}
}
