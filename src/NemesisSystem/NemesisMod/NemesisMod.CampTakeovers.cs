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
	private int _nextCool = -1;

	private readonly HashSet<string> _cantHoldSaid = new HashSet<string>();

	private const string EmptyHow = "were gone";

	private const float CampSpacing = 500f;

	internal NemesisRecord BossOf(NemesisRecord n)
	{
		if (n?.BossId != null)
		{
			return Roster.FirstOrDefault((NemesisRecord b) => b.Id == n.BossId && b.IsAlive);
		}
		return null;
	}

	internal CampSite CampOf(NemesisRecord n)
	{
		return Camps.ById(n?.CampId);
	}

	private void CoolTempers()
	{
		if (_nextCool < 0)
		{
			_nextCool = Game.GameTime + 600000;
		}
		else
		{
			if (Game.GameTime < _nextCool)
			{
				return;
			}
			_nextCool = Game.GameTime + 600000;
			bool flag = false;
			foreach (NemesisRecord item in Roster.Where((NemesisRecord r) => r.IsAlive && r.Fear > 0))
			{
				item.Fear = Math.Max(0, item.Fear - Balance.FearDecayPer10Min);
				flag = true;
			}
			foreach (NemesisRecord item2 in Roster.Where((NemesisRecord r) => r.IsAlive && !r.PresumedDead && r.Gang == null && r.Personality.HasValue).ToList())
			{
				int num = Personalities.CoolPer10Min(item2.Personality.Value) + Traits.CoolPer10Min(item2);
				int num2 = ((num > 0 && !(Balance.CoolingScale <= 0f)) ? Math.Max(1, (int)Math.Round((float)num * Balance.CoolingScale)) : 0);
				if (num2 == 0 || item2 == ActiveRecord || _worldNemeses.ContainsValue(item2) || Mind.HasAgainstYou(item2, "Avenge"))
				{
					continue;
				}
				int hate = item2.Hate;
				int val = Math.Min(hate, SocialFloor(item2));
				if (item2.Personality == Personality.Coward)
				{
					val = Math.Max(val, Math.Min(hate, 10));
				}
				item2.Hate = Math.Max(val, hate - num2);
				if (item2.Hate != hate)
				{
					flag = true;
					if (item2.Personality == Personality.Drifter && item2.Hate < 15 && item2.TimesKilledPlayer == 0 && !item2.ScarredByYou)
					{
						RemoveRecord(item2, "let it go (drifter cooled off)");
						GrudgeNotice(item2.FullName + " has let it go. He won't come looking for you.");
					}
				}
			}
			if (flag)
			{
				SaveRoster();
			}
		}
	}

	private Dictionary<string, string> CampOwners => Chronicle.CampOwner ?? (Chronicle.CampOwner = new Dictionary<string, string>());

	internal string SiteOwner(CampSite s)
	{
		if (s != null)
		{
			if (!CampOwners.TryGetValue(s.Id, out var value))
			{
				return Camps.StartingOwner(s);
			}
			return value;
		}
		return null;
	}

	internal IEnumerable<CampSite> SitesHeldBy(string gangId)
	{
		return Camps.AllSites.Where((CampSite s) => SiteOwner(s) == gangId);
	}

	private static bool HoldsCamps(Gang g)
	{
		return Holdings.HoldsCamps(g);
	}

	private Dictionary<string, CampTakeover> Takeovers => Chronicle.CampTakeovers ?? (Chronicle.CampTakeovers = new Dictionary<string, CampTakeover>());

	internal bool TakeoverPending(CampSite s)
	{
		if (s != null)
		{
			return Takeovers.ContainsKey(s.Id);
		}
		return false;
	}

	internal bool HomeStronghold(CampSite s, string owner)
	{
		if (s != null && s.Hideout)
		{
			return Camps.StartingOwner(s) == owner;
		}
		return false;
	}

	internal bool BeginTakeover(CampSite site, Gang by, string how)
	{
		if (site == null || by == null || SiteOwner(site) == by.Id || TakeoverPending(site))
		{
			return false;
		}
		if (!by.OrdersNotFeelings)
		{
			int num = SitesHeldBy(by.Id).Count() + Takeovers.Values.Count((CampTakeover t) => t.Gang == by.Id);
			int num2 = Math.Max(2, MenOf(by) / 60);
			if (num >= num2 || MenShare(by) < 0.5)
			{
				if (_cantHoldSaid.Count > 300)
				{
					_cantHoldSaid.Clear();
				}
				if (_cantHoldSaid.Add(site.Id + "|" + by.Id + "|" + GameDayKey()))
				{
					GrudgeLog(string.Format("CAMPS {0}: the {1} can't hold it - {2} camps, {3} men{4}", site.Label, by.Name, num, MenOf(by), (MenShare(by) < 0.5) ? ", under half strength" : ""));
				}
				return false;
			}
		}
		long num3 = GameMinutesNow() + 2880 + _rng.Next(1441);
		Takeovers[site.Id] = new CampTakeover
		{
			Gang = by.Id,
			DueAt = num3,
			How = how,
			From = SiteOwner(site)
		};
		if (how != null && how.Contains("overran"))
		{
			AddGrievance(SiteOwner(site), by.Id, 12, "took " + site.Label);
		}
		foreach (NemesisRecord item in Roster.Where((NemesisRecord n) => n.CampId == site.Id))
		{
			item.CampId = null;
			item.CampKnown = false;
		}
		SaveWorld();
		SaveRoster();
		GrudgeLog($"CAMPS {site.Label} ({site.Id}): the {by.Name} will take it ({how}) - empty for {(double)(num3 - GameMinutesNow()) / 1440.0:0.0} game days");
		Feed("CAMP " + site.Label + " stands empty - the " + by.Name + " mean to move in (" + how + ")", by.Accent);
		return true;
	}

	private void UpdateTakeovers()
	{
		if (!Chronicle.HomeStrongholdsRestored)
		{
			Chronicle.HomeStrongholdsRestored = true;
			foreach (CampSite s in Camps.AllSites.Where((CampSite campSite2) => campSite2.Hideout))
			{
				string text = Camps.StartingOwner(s);
				string text2 = SiteOwner(s);
				Gang gang = Gangs.ById(text);
				if (gang == null || text2 == text || IsDestroyed(gang))
				{
					continue;
				}
				CampOwners[s.Id] = text;
				foreach (NemesisRecord item in Roster.Where((NemesisRecord n) => n.CampId == s.Id))
				{
					item.CampId = null;
					item.CampKnown = false;
				}
				GrudgeLog("CAMPS " + s.Label + ": back to the " + gang.Name + " (their home stronghold - the " + (Gangs.ById(text2)?.Name ?? text2) + " only had it because it stood empty)");
				Feed("CAMP " + s.Label + " is the " + OutfitPoss(gang) + " again", gang.Accent);
			}
			SaveWorld();
			SaveRoster();
		}
		long num = GameMinutesNow();
		foreach (KeyValuePair<string, CampTakeover> item2 in Takeovers.ToList())
		{
			CampSite campSite = Camps.ById(item2.Key);
			Gang gang2 = Gangs.ById(item2.Value.Gang);
			if (campSite == null || gang2 == null || IsDestroyed(gang2))
			{
				Takeovers.Remove(item2.Key);
				SaveWorld();
			}
			else if (num >= item2.Value.DueAt)
			{
				Takeovers.Remove(item2.Key);
				if (!gang2.OrdersNotFeelings && (SitesHeldBy(gang2.Id).Count() >= Math.Max(2, MenOf(gang2) / 60) || MenShare(gang2) < 0.5))
				{
					GrudgeLog($"CAMPS {campSite.Label}: the {gang2.Name} can't hold it after all - {SitesHeldBy(gang2.Id).Count()} camps, {MenOf(gang2)} men");
					SaveWorld();
				}
				else
				{
					TakeCamp(campSite, gang2, item2.Value.How + ", and settled in");
				}
			}
		}
	}

	internal void TakeCamp(CampSite site, Gang by, string how)
	{
		if (site == null || by == null || SiteOwner(site) == by.Id)
		{
			return;
		}
		string text = SiteOwner(site);
		CampOwners[site.Id] = by.Id;
		SaveWorld();
		foreach (NemesisRecord item in Roster.Where((NemesisRecord n) => n.CampId == site.Id))
		{
			item.CampId = null;
			item.CampKnown = false;
		}
		GrudgeLog("CAMPS " + site.Label + ": " + (Gangs.ById(text)?.Name ?? text) + " -> " + by.Name + " (" + how + ")");
		Feed("CAMP " + site.Label + " belongs to the " + by.Name + " now (" + how + ")", by.Accent);
		SaveRoster();
	}

	private void MoveIntoEmptySites()
	{
		FixTakeoverSpree();
		int day = GameDayKey();
		Dictionary<string, int> dictionary = Chronicle.EmptyTakeovers ?? (Chronicle.EmptyTakeovers = new Dictionary<string, int>());
		foreach (string item in dictionary.Keys.Where((string k) => !k.EndsWith("|" + day)).ToList())
		{
			dictionary.Remove(item);
		}
		foreach (CampSite s in Camps.AllSites)
		{
			if (IsCleared(s))
			{
				continue;
			}
			string owner = SiteOwner(s);
			if ((!CampOwners.ContainsKey(s.Id) && (Gangs.ById(Camps.StartingOwner(s))?.Era ?? 0) > Gangs.Era) || Roster.Any((NemesisRecord n) => n.CampId == s.Id && n.IsAlive))
			{
				continue;
			}
			District district = DistrictAt(new Vector3(s.X, s.Y, s.Z));
			if (district == null || HoldOf(district, owner) >= 5 || TakeoverPending(s) || (HomeStronghold(s, owner) && !IsDestroyed(Gangs.ById(owner))))
			{
				continue;
			}
			string text = district.Key + "|" + day;
			dictionary.TryGetValue(text, out var value);
			if (value < 1 + Math.Abs(Seeds.Stable("take/" + text)) % 2)
			{
				(Gang, int) tuple = (from kv in ControlMap[district.Key]
					where kv.Key != owner && kv.Value >= 20
					select (g: Gangs.ById(kv.Key), h: kv.Value) into t
					where HoldsCamps(t.g) && !IsDestroyed(t.g) && FreeOfficers(t.g) > 0
					orderby t.h descending
					select t).FirstOrDefault();
				if (tuple.Item1 != null && BeginTakeover(s, tuple.Item1, "the " + (Gangs.ById(owner)?.Name ?? "old owners") + " were gone"))
				{
					dictionary[text] = value + 1;
				}
			}
		}
	}

	private int FreeOfficers(Gang g)
	{
		int num = Roster.Count((NemesisRecord n) => n.Gang == g.Id && n.IsAlive && !n.PresumedDead && n.Rank >= 2 && n.CampId == null);
		int num2 = Takeovers.Values.Count((CampTakeover t) => t.Gang == g.Id && t.How != null && t.How.Contains("were gone"));
		return num - num2;
	}

	private void FixTakeoverSpree()
	{
		if (Chronicle.TakeoverSpreeFixed)
		{
			return;
		}
		Chronicle.TakeoverSpreeFixed = true;
		IEnumerable<IGrouping<string, (string Key, CampTakeover Value, CampSite site)>> enumerable = from kv in Takeovers
			where kv.Value.How != null && kv.Value.How.Contains("were gone")
			select (Key: kv.Key, Value: kv.Value, site: Camps.ById(kv.Key)) into t
			where t.site != null
			group t by (DistrictAt(new Vector3(t.site.X, t.site.Y, t.site.Z))?.Key ?? "?") + "|" + t.Value.Gang;
		int num = 0;
		foreach (IGrouping<string, (string, CampTakeover, CampSite)> item in enumerable)
		{
			foreach (var item2 in item.OrderBy<(string, CampTakeover, CampSite), long>(((string Key, CampTakeover Value, CampSite site) t) => t.Value.DueAt).Skip(2))
			{
				Takeovers.Remove(item2.Item1);
				num++;
			}
		}
		if (num > 0)
		{
			GrudgeLog($"CAMPS called off {num} empty-camp takeovers from the old rule (it handed out a whole district at once)");
			SaveWorld();
		}
	}

	private void AssignCamps()
	{
		bool flag = false;
		List<(NemesisRecord, string)> source = (from n in Roster
			where n.CampKnown && n.CampId != null && n.IsAlive && !n.PresumedDead
			select (n: n, CampId: n.CampId)).ToList();
		try
		{
			if (RetireSites())
			{
				flag = true;
			}
		}
		catch (Exception ex)
		{
			GrudgeLog("CAMPS retired-site clean-up failed: " + ex.Message);
		}
		try
		{
			UpdateTakeovers();
			MoveIntoEmptySites();
		}
		catch (Exception ex2)
		{
			GrudgeLog("CAMPS sandbox failed: " + ex2.Message);
		}
		List<(string id, Vector3 at)> manned = new List<(string, Vector3)>();
		int num = 0;
		if (!_knownSitesSeeded && (_knownSitesSeeded = true))
		{
			foreach (NemesisRecord item3 in Roster.Where((NemesisRecord n) => n.CampKnown && n.CampId != null).ToList())
			{
				KnowSite(item3.CampId);
			}
		}
		Gang[] all = Gangs.All;
		foreach (Gang gang in all)
		{
			flag |= AssignChain(gang);
			CampSite[] array = (from s in SitesHeldBy(gang.Id)
				where !IsCleared(s)
				select s).ToArray();
			if (array.Length == 0)
			{
				continue;
			}
			List<NemesisRecord> source2 = Roster.Where((NemesisRecord n) => n.Gang == gang.Id && n.IsAlive && !n.Paralyzed && !n.PresumedDead).ToList();
			List<NemesisRecord> lts = (from n in source2
				where n.Rank >= 4
				orderby n.Rank descending, n.Id
				select n).ToList();
			List<NemesisRecord> caps = (from n in source2
				where n.Rank >= 2 && n.Rank < 4
				orderby n.Id
				select n).ToList();
			List<NemesisRecord> source3 = source2.Where((NemesisRecord n) => n.Rank < 2).ToList();
			HashSet<string> used = new HashSet<string>();
			string text = SuccessorId(gang);
			foreach (NemesisRecord o in lts.Concat(caps))
			{
				CampSite campSite = Camps.ById(o.CampId);
				if (campSite == null || used.Contains(campSite.Id) || IsCleared(campSite) || SiteOwner(campSite) != gang.Id)
				{
					o.CampId = null;
					o.CampKnown = false;
					flag = flag || campSite != null;
					campSite = null;
				}
				if (campSite != null && !campSite.Hideout && !_liveCamps.ContainsKey(campSite.Id) && TooClose(campSite))
				{
					o.CampId = null;
					o.CampKnown = false;
					flag = true;
					num++;
					campSite = null;
				}
				if (o.Rank >= 4 && (campSite == null || !campSite.Hideout))
				{
					CampSite hide = array.FirstOrDefault((CampSite s) => s.Hideout && !used.Contains(s.Id) && !lts.Concat(caps).Any((NemesisRecord x) => x != o && x.CampId == s.Id));
					if (hide == null && o.Id == text && campSite == null)
					{
						hide = array.FirstOrDefault((CampSite s) => s.Hideout && !used.Contains(s.Id));
						if (hide != null)
						{
							foreach (NemesisRecord item4 in from x in lts.Concat(caps)
								where x != o && x.CampId == hide.Id
								select x)
							{
								item4.CampId = null;
								item4.CampKnown = false;
							}
						}
					}
					if (hide != null)
					{
						o.CampId = hide.Id;
						o.CampKnown = SiteKnown(hide.Id);
						o.CampUnsure = false;
						campSite = hide;
						flag = true;
					}
				}
				if (campSite == null)
				{
					List<CampSite> list = array.Where((CampSite s) => !s.Hideout && !used.Contains(s.Id) && !lts.Concat(caps).Any((NemesisRecord x) => x.CampId == s.Id) && !TooClose(s)).ToList();
					if (list.Count == 0)
					{
						list = array.Where((CampSite s) => !used.Contains(s.Id) && !lts.Concat(caps).Any((NemesisRecord x) => x != o && x.CampId == s.Id) && (s.Hideout || !TooClose(s))).ToList();
					}
					if (list.Count == 0 && o.Id == text)
					{
						NemesisRecord nemesisRecord = (from x in lts.Concat(caps)
							where x != o && x.CampId != null && !used.Contains(x.CampId) && Camps.ById(x.CampId) != null
							orderby x.Rank, x.Id
							select x).FirstOrDefault();
						if (nemesisRecord != null)
						{
							list.Add(Camps.ById(nemesisRecord.CampId));
							nemesisRecord.CampId = null;
							nemesisRecord.CampKnown = false;
						}
					}
					if (list.Count == 0)
					{
						continue;
					}
					campSite = list[_rng.Next(list.Count)];
					o.CampId = campSite.Id;
					o.CampKnown = SiteKnown(campSite.Id);
					o.CampUnsure = false;
					flag = true;
				}
				used.Add(campSite.Id);
				manned.Add((campSite.Id, SitePos(campSite)));
			}
			foreach (NemesisRecord item5 in source3.Where((NemesisRecord b) => b.CampId != null))
			{
				item5.CampId = null;
				item5.CampKnown = false;
				flag = true;
			}
			foreach (NemesisRecord item6 in Roster.Where((NemesisRecord n) => n.Gang == gang.Id && n.PresumedDead && n.CampId != null))
			{
				item6.CampId = null;
				item6.CampKnown = false;
				flag = true;
			}
		}
		if (num > 0)
		{
			GrudgeLog(string.Format("CAMPS {0} camp{1} let go - within {2:0} m of another manned camp", num, (num == 1) ? "" : "s", 500f));
		}
		List<(NemesisRecord, string)> list2 = source.Where<(NemesisRecord, string)>(((NemesisRecord n, string CampId) k) => k.n.IsAlive && k.n.CampId != k.CampId).ToList();
		foreach (var item7 in list2)
		{
			NemesisRecord item = item7.Item1;
			string item2 = item7.Item2;
			CampSite campSite2 = Camps.ById(item2);
			CampSite campSite3 = Camps.ById(item.CampId);
			string text2 = ((campSite3 == null) ? "where to, nobody says" : (item.CampKnown ? ("to " + campSite3.Label + " - a place you know") : "somewhere you don't know yet"));
			GrudgeLog("CAMPS " + item.Name + ": moved out of " + (campSite2?.Label ?? Camps.RetiredName(item2) ?? item2) + " (" + text2 + ")");
			if (list2.Count <= 2)
			{
				ReplyNotice(("Word is " + OfficerTitle(item) + " " + item.Name + " has moved out of " + (campSite2?.Label ?? Camps.RetiredName(item2) ?? "his camp") + ", " + text2 + ".").Replace("  ", " "));
			}
		}
		if (list2.Count > 2)
		{
			ReplyNotice(string.Format("Word is {0} camps you knew have changed hands or moved: {1}. Your map shows where they are now.", list2.Count, string.Join(", ", list2.Select<(NemesisRecord, string), string>(((NemesisRecord n, string CampId) m) => m.n.Name))));
		}
		if (flag)
		{
			SaveRoster();
		}
		static bool PairOk(string a, string b)
		{
			if (!a.StartsWith("placed_") && !b.StartsWith("placed_"))
			{
				return (uint)Seeds.Stable("campnear/" + ((string.CompareOrdinal(a, b) < 0) ? (a + "|" + b) : (b + "|" + a))) % 100u < 10;
			}
			return true;
		}
		bool TooClose(CampSite s)
		{
			Vector3 p = SitePos(s);
			return manned.Any(delegate((string id, Vector3 at) m)
			{
				float num3 = new Vector3(m.at.X - p.X, m.at.Y - p.Y, 0f).Length();
				return num3 < 500f && (num3 < 120f || !PairOk(s.Id, m.id));
			});
		}
	}

	private bool RetireSites()
	{
		bool flag = false;
		foreach (CampSite site in Camps.Retired.Concat(Camps.Removed))
		{
			bool flag2 = false;
			if (_liveCamps.TryGetValue(site.Id, out var value))
			{
				DespawnCamp(value, delete: true);
				_liveCamps.Remove(site.Id);
				flag2 = true;
			}
			foreach (NemesisRecord item in Roster.Where((NemesisRecord n) => n.CampId == site.Id))
			{
				item.CampId = null;
				item.CampKnown = false;
				flag2 = true;
			}
			if (CampOwners.Remove(site.Id))
			{
				flag2 = true;
			}
			Gang[] defined = Gangs.Defined;
			foreach (Gang gang in defined)
			{
				string text = (gang.Name.StartsWith("The ") ? ("the " + gang.Name.Substring(4)) : ("the " + gang.Name));
				if (Hold.SupplyCamps.TryGetValue(gang.Id, out var value2) && value2 == site.Id && Hold.SupplyKnownAt.TryGetValue(gang.Id, out var value3) && value3 == site.Id)
				{
					RetiredNews("Word is " + text + " have moved their supplies out of " + site.Name + ".");
				}
				if (Hold.Treasuries.TryGetValue(gang.Id, out var value4) && value4 == site.Id && Hold.TreasuryKnown(gang))
				{
					RetiredNews("Word is " + text + " have moved their money out of " + site.Name + ".");
				}
			}
			if (Hold.SupplyCamps.ContainsValue(site.Id) || Hold.Treasuries.ContainsValue(site.Id))
			{
				Hold.ForgetSite(site.Id);
				flag2 = true;
			}
			if (flag2)
			{
				flag = true;
				_nextCampAssign = 0;
				GrudgeLog("CAMPS " + site.Name + " isn't a camp any more (" + site.Id + ") - its men, supplies and money go to the outfit's other camps");
			}
		}
		if (flag)
		{
			SaveRoster();
			SaveWorld();
		}
		return flag;
	}

	private void RetiredNews(string text)
	{
		GrudgeLog("HOLDINGS news (big): " + text);
		if (Config.WorldNews >= 2)
		{
			ShowNotification(text);
		}
	}
}
