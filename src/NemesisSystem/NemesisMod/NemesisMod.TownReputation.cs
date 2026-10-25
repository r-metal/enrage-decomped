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
	private readonly Dictionary<string, (TownRepParts v, int at)> _townRepCache = new Dictionary<string, (TownRepParts, int)>();

	private readonly Dictionary<string, (int blip, string name, int tier)> _townRepBlips = new Dictionary<string, (int, string, int)>();

	private int _nextTownRepBlips;

	internal static IEnumerable<string> RepTowns => TownCoreRadius.Keys;

	internal TownRepParts TownRep(string town)
	{
		if (string.IsNullOrEmpty(town))
		{
			return default(TownRepParts);
		}
		if (_townRepCache.TryGetValue(town, out (TownRepParts, int) value) && UiDraw.RealMs - value.Item2 < 1000)
		{
			return value.Item1;
		}
		TownRepParts townRepParts = TownRepNow(town);
		_townRepCache[town] = (townRepParts, UiDraw.RealMs);
		return townRepParts;
	}

	internal int TownReputation(string town)
	{
		return TownRep(town).Rep;
	}

	private TownRepParts TownRepNow(string town)
	{
		TownRepParts result = default(TownRepParts);
		List<Acquaintance> list = People.Where((Acquaintance a) => !a.Dead && a.Town == town).ToList();
		result.Known = list.Count;
		float num = 0f;
		float num2 = 0f;
		foreach (Acquaintance item in list)
		{
			float num3 = (float)item.Like + (float)item.Favours * 12f;
			float num4 = ((item.SawFace == false) ? 0f : ((float)Math.Max(item.Grudge, 0))) * (item.GrudgeSettled ? 0.5f : 1f) + (float)item.Fear * (SawFirstHand(item) ? 0.6f : 0.3f);
			num += num3;
			num2 += num4;
			if (num3 - num4 >= 10f)
			{
				result.Liked++;
			}
			else if (num4 - num3 >= 10f)
			{
				result.Disliked++;
			}
		}
		if (Chronicle.TownWord != null && Chronicle.TownWord.TryGetValue(town, out var value))
		{
			num2 += value.Word;
		}
		num2 += Roster.Where((NemesisRecord n) => n.IsAlive && !n.PresumedDead && n.KillLocation == town && n.Origin != GrudgeOrigin.GangMember && !Gangs.Orders(n)).Sum((NemesisRecord n) => (float)n.Hate * 0.8f);
		Vector3 centre;
		float radius;
		float num5 = (TownCore(town, out centre, out radius) ? Math.Max(1f, radius / 130f * (radius / 130f)) : 1f);
		result.Goodwill = (int)Math.Round(100f * (1f - (float)Math.Exp((0f - num) / (120f * num5))));
		result.Illwill = (int)Math.Round(100f * (1f - (float)Math.Exp((0f - num2) / (100f * num5))));
		result.Law = LawPart();
		result.Deeds = ((Chronicle.TownWord != null && Chronicle.TownWord.TryGetValue(town, out var value2)) ? ((int)Math.Round(value2.Good)) : 0);
		result.Rep = Math.Max(-100, Math.Min(100, result.Goodwill - result.Illwill + result.Law + result.Deeds));
		return result;
	}

	private int LawPart()
	{
		int num;
		try
		{
			num = BountyDollars;
		}
		catch
		{
			num = 0;
		}
		return -Math.Min(35, num / 6);
	}

	internal int OverallReputation()
	{
		double num = 0.0;
		double num2 = 0.0;
		foreach (string repTown in RepTowns)
		{
			TownRepParts townRepParts = TownRep(repTown);
			if (townRepParts.Known != 0 || townRepParts.Illwill != 0)
			{
				double num3 = 1 + townRepParts.Known;
				num += (double)townRepParts.Rep * num3;
				num2 += num3;
			}
		}
		if (!(num2 <= 0.0))
		{
			return (int)Math.Round(num / num2);
		}
		return LawPart();
	}

	internal static string RepWord(int r)
	{
		if (r > -60)
		{
			if (r > -20)
			{
				if (r < 60)
				{
					if (r < 20)
					{
						return "Known";
					}
					return "Liked";
				}
				return "Admired";
			}
			return "Disliked";
		}
		return "Hated";
	}

	internal static (int r, int g, int b) RepColor(int r)
	{
		if (r > -60)
		{
			if (r > -20)
			{
				if (r < 20)
				{
					return Theme.Dim;
				}
				return Theme.Good;
			}
			return Theme.Warn;
		}
		return Theme.Blood;
	}

	internal float GoodRepLikeMult(string town)
	{
		int num = TownReputation(town);
		if (num > 0)
		{
			return 1f + (float)num / 200f;
		}
		return 1f;
	}

	internal float GoodRepGrudgeMult(string town)
	{
		int num = TownReputation(town);
		if (num > 0)
		{
			return 1f - (float)num / 300f;
		}
		return 1f;
	}

	internal string TownRepLine(string town)
	{
		TownRepParts townRepParts = TownRep(town);
		return $"{town} {townRepParts.Rep:+0;-0;0} {RepWord(townRepParts.Rep)} (goodwill {townRepParts.Goodwill}, ill will {townRepParts.Illwill}, law {townRepParts.Law}, deeds {townRepParts.Deeds}; {townRepParts.Liked} like you, {townRepParts.Disliked} don't, {townRepParts.Known} known)";
	}

	private static int RepTier(int r)
	{
		if (r < 20)
		{
			if (r > -70)
			{
				if (r > -40)
				{
					if (r > -10)
					{
						return 0;
					}
					return -1;
				}
				return -2;
			}
			return -3;
		}
		return 2;
	}

	private static string RepModifier(int tier)
	{
		if (tier < 2)
		{
			if (tier != -1)
			{
				if (tier != -2)
				{
					if (tier > -3)
					{
						return null;
					}
					return "BLIP_MODIFIER_HOSTILITY_COLOR_3";
				}
				return "BLIP_MODIFIER_HOSTILITY_COLOR_2";
			}
			return "BLIP_MODIFIER_HOSTILITY_COLOR_1";
		}
		return "BLIP_MODIFIER_FRIENDLY";
	}

	private void UpdateTownRepBlips()
	{
		if (Game.GameTime < _nextTownRepBlips || !MapSettled())
		{
			return;
		}
		_nextTownRepBlips = Game.GameTime + 10000;
		if (!Config.TownRepMarkers || _mapHidden)
		{
			ClearTownRepBlips();
			return;
		}
		foreach (string repTown in RepTowns)
		{
			TownRepParts townRepParts = TownRep(repTown);
			string text = null;
			try
			{
				District d;
				Gang gang = TownHolder(repTown, out d);
				(Gang, Gang)? tuple = ((d != null) ? WarIn(d) : (((Gang, Gang)?)null));
				if (gang != null && tuple.HasValue)
				{
					text = ShortOutfit(gang) + " town, at war";
				}
			}
			catch
			{
			}
			bool flag = townRepParts.Known > 0 || townRepParts.Illwill > 0;
			if (!flag && text == null)
			{
				DropTownRepBlip(repTown);
			}
			else
			{
				if (!TownCore(repTown, out var centre, out var _))
				{
					continue;
				}
				string text2 = UiDraw.TheFix((text == null) ? $"{repTown} - reputation {townRepParts.Rep:+0;-0;0} ({RepWord(townRepParts.Rep)})" : (flag ? $"{repTown} - {text}, rep {townRepParts.Rep:+0;-0;0}" : (repTown + " - " + text)));
				int num = RepTier(townRepParts.Rep);
				if (_townRepBlips.TryGetValue(repTown, out (int, string, int) value) && !MAP.DOES_BLIP_EXIST(value.Item1))
				{
					_townRepBlips.Remove(repTown);
				}
				if (_townRepBlips.TryGetValue(repTown, out (int, string, int) value2) && value2.Item2 == text2 && value2.Item3 == num)
				{
					continue;
				}
				if (_townRepBlips.TryGetValue(repTown, out value2) && value2.Item3 == num)
				{
					Function.Call(11290983183885202434uL, value2.Item1, new InputArgument(UiDraw.ForeverVarString(text2)));
					_townRepBlips[repTown] = (value2.Item1, text2, num);
					continue;
				}
				DropTownRepBlip(repTown);
				Crumbs.Native("TownRep blip add");
				int num2 = MAP.BLIP_ADD_FOR_COORDS(3911271093u, centre.X, centre.Y, centre.Z);
				Crumbs.Native("TownRep blip sprite");
				try
				{
					MAP.SET_BLIP_SPRITE(num2, MISC.GET_HASH_KEY("blip_town"), p2: true);
				}
				catch
				{
				}
				string text3 = RepModifier(num);
				Crumbs.Native("TownRep blip modifier");
				if (text3 != null)
				{
					try
					{
						Function.Call<bool>(7362600660482055727uL, new InputArgument[2]
						{
							num2,
							MISC.GET_HASH_KEY(text3)
						});
					}
					catch
					{
					}
				}
				Function.Call(11290983183885202434uL, num2, new InputArgument(UiDraw.ForeverVarString(text2)));
				_townRepBlips[repTown] = (num2, text2, num);
				GrudgeLog("REPUTATION map: " + TownRepLine(repTown));
			}
		}
	}

	private unsafe void DropTownRepBlip(string town)
	{
		if (!_townRepBlips.TryGetValue(town, out (int, string, int) value))
		{
			return;
		}
		var (blip, _, _) = value;
		Crumbs.Native("TownRep blip remove");
		try
		{
			if (MAP.DOES_BLIP_EXIST(blip))
			{
				MAP.REMOVE_BLIP(&blip);
			}
		}
		catch
		{
		}
		_townRepBlips.Remove(town);
	}

	internal void ClearTownRepBlips()
	{
		foreach (string item in _townRepBlips.Keys.ToList())
		{
			DropTownRepBlip(item);
		}
	}

	internal void ForgetTownRepBlips()
	{
		bool flag = false;
		try
		{
			flag = CAM.IS_SCREEN_FADED_OUT();
		}
		catch
		{
		}
		if (!flag)
		{
			ClearTownRepBlips();
		}
		else
		{
			_townRepBlips.Clear();
		}
		_nextTownRepBlips = 0;
	}
}
