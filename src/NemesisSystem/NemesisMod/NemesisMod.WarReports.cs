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
	private static readonly HashSet<string> FightKinds = new HashSet<string> { "CLASH", "CAMP OVERRUN", "RAID", "WITNESSED" };

	private int _warHudPressedAt = -100000;

	private float _warHudAlpha;

	private static readonly Dictionary<string, Personality> DefaultLeaderTemper = new Dictionary<string, Personality>
	{
		["klan"] = Personality.Proud,
		["murfree"] = Personality.Hothead,
		["skinners"] = Personality.Cold,
		["wapiti"] = Personality.Loyal,
		["army"] = Personality.Cold
	};

	private int _warRollDay;

	internal string NameWar(WarRecord w, District d, Gang a, Gang b, string cause)
	{
		HashSet<string> used = new HashSet<string>(from x in Wars.Concat(Chronicle.PastWars ?? new List<WarRecord>())
			where x != w && x.Name != null
			select x.Name);
		int num = (Chronicle.PastWars ?? new List<WarRecord>()).Count((WarRecord x) => x.District == w.District);
		string text = d?.Name ?? w.District;
		if (text.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
		{
			text = text.Substring(4);
		}
		List<(string, int)> list = new List<(string, int)>();
		string text2 = Ordinals[Math.Min(num, Ordinals.Length - 1)];
		list.Add(("The " + text2 + text + " War", 3));
		string text3 = (cause ?? "").ToLowerInvariant() + " " + (w.GoalText ?? "").ToLowerInvariant();
		(string, string)[] causeWords = CauseWords;
		for (int num2 = 0; num2 < causeWords.Length; num2++)
		{
			var (value, text4) = causeWords[num2];
			if (text3.Contains(value))
			{
				list.Add(("The " + text4 + " War", 4));
				break;
			}
		}
		if (w.Goal == "camp" && w.GoalSite != null)
		{
			CampSite campSite = Camps.ById(w.GoalSite);
			if (campSite != null)
			{
				list.Add(("The War for " + campSite.Label.Replace("the ", "").Replace("The ", ""), 4));
			}
		}
		list.Add(("The " + Adjective(a) + "-" + Adjective(b) + " War", 2));
		if (Relation(a.Id, b.Id) <= -70)
		{
			list.Add(("The " + text + " Blood Feud", 3));
		}
		string leaderName = a.LeaderName;
		if (!string.IsNullOrEmpty(leaderName) && leaderName.Contains(" "))
		{
			list.Add((leaderName.Substring(leaderName.LastIndexOf(' ') + 1) + "'s War", 2));
		}
		int month = GameDate.FromMinutes(GameMinutesNow()).Month;
		string text5 = ((month <= 2 || month == 12) ? "Winter" : ((month <= 5) ? "Spring" : ((month <= 8) ? "Summer" : "Autumn")));
		list.Add(("The " + text5 + " War in " + text, 1));
		int cLOCK_HOURS = CLOCK.GET_CLOCK_HOURS();
		if (cLOCK_HOURS >= 21 || cLOCK_HOURS < 5)
		{
			list.Add(("The Night Riders' War", 1));
		}
		List<(string, int)> list2 = list.Where<(string, int)>(((string name, int weight) o) => !used.Contains(o.name)).ToList();
		if (list2.Count == 0)
		{
			return "The " + Ordinals[Math.Min(num + 1, Ordinals.Length - 1)] + text + " War";
		}
		int num3 = _rng.Next(list2.Sum<(string, int)>(((string name, int weight) o) => o.weight));
		foreach (var item in list2)
		{
			if ((num3 -= item.Item2) < 0)
			{
				return item.Item1;
			}
		}
		return list2[0].Item1;
	}

	internal string WarName(WarRecord w)
	{
		if (w == null)
		{
			return "the war";
		}
		if (string.IsNullOrEmpty(w.Name))
		{
			Gang gang = Gangs.ById(w.A);
			Gang gang2 = Gangs.ById(w.B);
			if (gang != null && gang2 != null)
			{
				w.Name = NameWar(w, DistrictByKey(w.District), gang, gang2, w.Cause);
				SaveWorld();
			}
		}
		if (w.Name != null && w.Name.Contains("The the "))
		{
			w.Name = w.Name.Replace("The the ", "The ");
			SaveWorld();
		}
		return w.Name ?? "the war";
	}

	internal void DebugStartWar(Ped player)
	{
		District d = CurrentDistrict;
		if (d == null)
		{
			ShowNotification("enRAGE: this isn't a district the mod knows - no war here.");
			return;
		}
		List<string> warZones = Chronicle.WarZones;
		if (warZones != null && warZones.Any((string t) => t.StartsWith(d.Key + ":")))
		{
			ShowNotification("enRAGE: there's already a war in " + d.Name + ".");
			return;
		}
		List<Gang> list = (from g in Gangs.All
			where !g.StoryOnly && g.TurfWars && !IsDestroyed(g) && FactionModels.ContainsKey(g.Id)
			orderby HoldOf(d, g.Id) descending
			select g).Take(2).ToList();
		if (list.Count < 2)
		{
			ShowNotification("enRAGE: not two outfits to fight over " + d.Name + ".");
			return;
		}
		Gang a = list[0];
		Gang b = list[1];
		Gang[] array = new Gang[2] { a, b };
		foreach (Gang gang in array)
		{
			if (HoldOf(d, gang.Id) < 20)
			{
				ShiftControl(d, gang, 20 - HoldOf(d, gang.Id), "a war started from the spawner (debug)");
			}
		}
		Truces.RemoveAll(delegate(string s)
		{
			string[] array2 = s.Split('|');
			return (array2[0] == a.Id && array2[1] == b.Id) || (array2[0] == b.Id && array2[1] == a.Id);
		});
		int num2 = Relation(a.Id, b.Id);
		if (num2 > -50)
		{
			ShiftRelation(a.Id, b.Id, -50 - num2, "a war started from the spawner (debug)", force: true);
		}
		Chronicle.WarEndedAt?.Remove(d.Key);
		DeclareWar(d, a, b, "it was started from the spawner (debug)");
		WarRecord w = Wars.FirstOrDefault((WarRecord x) => x.District == d.Key);
		ShowNotification("enRAGE: " + WarName(w) + " - the " + a.Name + " vs the " + b.Name + " in " + d.Name + ".");
	}

	internal void DebugDistantBattle(Ped player)
	{
		if (_skirmish.Count > 0)
		{
			ShowNotification("enRAGE: a fight is already going on.");
			return;
		}
		Vector3 me = player.Position;
		WarRecord warRecord = Wars.Where((WarRecord x) => x.Result == null && FactionModels.ContainsKey(x.A) && FactionModels.ContainsKey(x.B)).OrderBy(delegate(WarRecord x)
		{
			District district = DistrictByKey(x.District);
			return (district != null) ? new Vector3(district.X - me.X, district.Y - me.Y, 0f).Length() : 1E+09f;
		}).FirstOrDefault();
		if (warRecord == null)
		{
			ShowNotification("enRAGE: no war with men to send - start one first.");
			return;
		}
		Gang gang = Gangs.ById(warRecord.A);
		Gang gang2 = Gangs.ById(warRecord.B);
		if (TrySkirmish(player, town: false, test: false, rolled: true, (gang, gang2), distant: true))
		{
			_nextDistantAllowed = Game.GameTime + 240000;
			ShowNotification("enRAGE: a battle " + Away(me, _skirmishAt) + " - listen.");
			GrudgeLog("WAR distant battle (debug): the " + gang.Name + " and the " + gang2.Name + ", " + Away(me, _skirmishAt));
		}
		else
		{
			ShowNotification("enRAGE: no hidden, loaded ground 200-250 m away - try again elsewhere.");
		}
	}

	internal List<(WarRecord w, bool live)> WarList()
	{
		List<WarRecord> list = Wars.Where((WarRecord x) => x.Result == null || (Chronicle.WarZones?.Any((string t) => t.StartsWith(x.District + ":")) ?? false)).ToList();
		foreach (WarRecord item in list)
		{
			InitHeat(item);
		}
		List<(WarRecord x, bool)> list2 = (from x in list
			orderby x.Heat descending
			select (x: x, true)).ToList();
		list2.AddRange(from x in Chronicle.PastWars ?? new List<WarRecord>()
			orderby x.EndMin descending
			select (x: x, false));
		return list2;
	}

	internal District DistrictByKey(string key)
	{
		return Districts.FirstOrDefault((District x) => x.Key == key);
	}

	internal List<WorldEvent> WarTimeline(WarRecord w, int max)
	{
		long end = ((w.EndMin > 0) ? (w.EndMin + 60) : long.MaxValue);
		return (from e in Chronicle.Events
			where !e.IsPast && e.GameMin >= w.StartMin - 5 && e.GameMin <= end && e.Gangs.Contains(w.A) && e.Gangs.Contains(w.B) && (e.Kind == "WAR" || WarOfFight(w.A, w.B, e.HasPos ? DistrictAt(new Vector3(e.X, e.Y, e.Z)) : null, e.GameMin, liveOnly: false) == w)
			orderby e.GameMin descending
			select e).Take(max).ToList();
	}

	internal int WarCampsTaken(WarRecord w)
	{
		return WarTimeline(w, 200).Count((WorldEvent e) => e.Kind == "CAMP OVERRUN");
	}

	internal List<(WorldEvent e, int dead)> WarFights(WarRecord w)
	{
		return (from e in WarTimeline(w, 200)
			where FightKinds.Contains(e.Kind)
			select (e: e, e.People.Count((WorldPart p) => p.Outcome == "KILLED") + (e.UnnamedDead?.Values.Sum() ?? 0))).ToList();
	}

	internal List<(string name, Gang side, string rank, string when)> WarNamedDead(WarRecord w)
	{
		List<(string, Gang, string, string)> list = new List<(string, Gang, string, string)>();
		foreach (var item3 in WarFights(w))
		{
			WorldEvent item = item3.e;
			foreach (WorldPart item4 in item.People.Where((WorldPart p) => p.Outcome == "KILLED"))
			{
				NemesisRecord nemesisRecord = FindRecord(item4.Id);
				Gang item2 = Gangs.ById(item4.Gang ?? nemesisRecord?.Gang);
				string text = ((nemesisRecord != null) ? RosterMenu.RankText(nemesisRecord) : "");
				int num = text.IndexOf(" (");
				if (num > 0)
				{
					text = text.Substring(0, num);
				}
				list.Add((item4.Name ?? nemesisRecord?.Name ?? "a man", item2, text, GameDate.FromMinutes(item.GameMin).ToString("MMM d")));
			}
		}
		return list;
	}

	internal string WarLeader(Gang g)
	{
		if (g == null)
		{
			return null;
		}
		Personality? personality = LeaderTemper(g);
		if (personality.HasValue)
		{
			return string.Format("{0} ({1})", g.LeaderName ?? "their leader", personality.Value);
		}
		return "None - the seat is empty";
	}

	internal string WarAim(Gang g)
	{
		if (g != null && AgendaOf(g) != null)
		{
			return Cap1(AgendaText(g));
		}
		return null;
	}

	internal string WarPurse(Gang g)
	{
		if (g == null || !HasPurse(g))
		{
			return null;
		}
		int num = PurseOf(g);
		return $"${num:N0}  -  {PurseWord(num)}";
	}

	internal string WarCosts(Gang g)
	{
		if (g == null || !HasPurse(g))
		{
			return null;
		}
		int num = PurseOf(g);
		int num2 = WarsOf(g);
		List<string> list = new List<string>();
		if (num2 > 0)
		{
			list.Add(string.Format("${0} a day{1}", 15 * num2, (num2 > 1) ? $" ({num2} wars)" : ""));
		}
		if (num < 60)
		{
			list.Add("smaller crews, recruits fail");
		}
		else if (num >= 1200)
		{
			list.Add("bigger crews");
		}
		if (list.Count != 0)
		{
			return string.Join("  -  ", list);
		}
		return null;
	}

	private static string Cap1(string s)
	{
		if (!string.IsNullOrEmpty(s))
		{
			return char.ToUpperInvariant(s[0]) + s.Substring(1);
		}
		return s;
	}

	private void DrawWarHud(Ped player)
	{
		if (!Config.WarHud)
		{
			return;
		}
		if (Game.IsControlPressed(eInputType.RevealHud) || Game.IsDisabledControlPressed(eInputType.RevealHud))
		{
			_warHudPressedAt = Game.GameTime;
		}
		District d = CurrentDistrict;
		(Gang, Gang)? tuple = ((d != null) ? WarIn(d) : (((Gang, Gang)?)null));
		bool flag = tuple.HasValue && Game.GameTime - _warHudPressedAt < 3000 && !_menu.IsOpen && !_bulletImpact.ViewerOpen && _convo == null && _tg == null && !player.IsDead && !CAM.IS_SCREEN_FADED_OUT();
		_warHudAlpha = Math.Max(0f, Math.Min(1f, _warHudAlpha + (flag ? 5f : (-3f)) * UiDraw.RealDt));
		int a;
		float cx;
		if (!(_warHudAlpha <= 0.01f) && tuple.HasValue)
		{
			(Gang, Gang) value = tuple.Value;
			Gang item = value.Item1;
			Gang item2 = value.Item2;
			WarRecord warRecord = (from x in Chronicle.Wars?.Where((WarRecord x) => x.District == d.Key && x.Result == null)
				orderby x.StartMin descending
				select x).FirstOrDefault();
			string s = item.Name.ToUpperInvariant() + "  VS  " + item2.Name.ToUpperInvariant();
			string text = ((warRecord != null) ? $"Day {WarDay(warRecord)}  -  since {WarSince(warRecord)}" : "");
			string text2 = null;
			float warHudW = Config.WarHudW;
			float num = ((Config.WarHudX < 0f) ? ((1f - warHudW) / 2f) : Config.WarHudX);
			float warHudY = Config.WarHudY;
			float sc = 0.23f;
			float num2 = 0.3f;
			float num3 = 0.24f;
			float scale = 0.21f;
			while (Theme.CapsWidth(s, num2) > warHudW - 0.024f && num2 > 0.2f)
			{
				num2 -= 0.01f;
			}
			string text3 = text;
			while (Theme.TitleWidth(text3, num3) > warHudW - 0.024f && num3 > 0.18f)
			{
				num3 -= 0.01f;
			}
			string text4 = ((text2 != null) ? FitW(text2, scale, warHudW - 0.024f) : null);
			a = (int)(255f * _warHudAlpha);
			float h = 0.088f + ((text4 != null) ? 0.024f : 0f);
			Theme.Brush(num, warHudY, warHudW, h, (int)(235f * _warHudAlpha));
			cx = num + warHudW / 2f;
			Bold("AT WAR", sc, warHudY + 0.009f, Theme.NativeRed, caps: true, -0.0010416667f);
			UiDraw.Rect(cx - 0.022f, warHudY + 0.0275f, 0.044f, 0.0018518518f, Theme.NativeRed.r, Theme.NativeRed.g, Theme.NativeRed.b, (int)(200f * _warHudAlpha));
			Bold(s, num2, warHudY + 0.032f, (r: 246, g: 242, b: 232), caps: true);
			UiDraw.TextFont(text3, UiDraw.TitleFont, cx - Theme.TitleWidth(text3, num3) / 2f, warHudY + 0.058f, num3, 205, 200, 190, a);
			if (text4 != null)
			{
				UiDraw.TextFont(text4, UiDraw.TitleFont, cx - Theme.TitleWidth(text4, scale) / 2f, warHudY + 0.087f, scale, 170, 165, 155, a);
			}
		}
		void Bold(string text5, float scale2, float yy, (int r, int g, int b) c, bool caps, float nudge = 0f)
		{
			float num4 = (caps ? Theme.CapsWidth(text5, scale2) : Theme.TitleWidth(text5, scale2));
			float num5 = cx - num4 / 2f + nudge;
			UiDraw.TextFont(text5, UiDraw.TitleFont, num5, yy, scale2, c.r, c.g, c.b, a);
			UiDraw.TextFont(text5, UiDraw.TitleFont, num5 + 0.00052083336f, yy, scale2, c.r, c.g, c.b, a);
		}
	}
}
