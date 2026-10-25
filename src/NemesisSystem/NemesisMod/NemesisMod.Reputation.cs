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
	private static readonly string[] BuiltInBadBlood = new string[8] { "{aw} men ran off a {bw} horse string near {p}", "a {bw} man was found hanged outside {p} - the {a} are blamed", "{aw} and {bw} men shot it out over a card game in {p}", "the {a} took a {bw} supply wagon on the road by {p}", "a {aw} rider insulted a {bw} officer in front of his men near {p}", "the {a} burned a {bw} lean-to by {p}", "a {bw} man killed a {aw} boy in a drunken fight in {p}", "the {a} moved in on ground the {b} call theirs near {p}" };

	private static readonly string[] BuiltInDeals = new string[5] { "{aw} and {bw} bosses met in {p} and split the roads between them", "the {a} paid the {b} for passage through {d}", "a {aw} man married into a {bw} family near {p}", "the {a} sent back a {bw} prisoner unharmed", "{aw} and {bw} men drank together after a hunt near {p}" };

	private static string[] BadBlood = BuiltInBadBlood;

	private static string[] Deals = BuiltInDeals;

	private static readonly Dictionary<string, string[]> NervousLines = new Dictionary<string, string[]>
	{
		["Coward"] = new string[3] { "I-I don't want no trouble, mister.", "W-what can I do for you? Anything.", "Please, I ain't done nothing." },
		["Hothead"] = new string[2] { "I know who you are. Don't push it.", "Heard about you. Keep walking." },
		["Proud"] = new string[2] { "I know what you are. Good day.", "...We all know who you are." },
		["Cold"] = new string[2] { "...Afternoon.", "Mm." },
		[""] = new string[4] { "Oh - uh, hello.", "Morning. I, uh... morning.", "Didn't mean to stare, mister.", "What do you want with me?" }
	};

	private readonly List<(int at, string text)> _repNotes = new List<(int, string)>();

	private long _gmCache;

	private int _gmFrame = -1;

	private WorldEvent Incident(List<Gang> outfits, long now)
	{
		List<(Gang, Gang, District)> list = new List<(Gang, Gang, District)>();
		District[] districts = Districts;
		foreach (District district in districts)
		{
			foreach (Gang outfit in outfits)
			{
				foreach (Gang outfit2 in outfits)
				{
					if (string.CompareOrdinal(outfit.Id, outfit2.Id) < 0 && HoldOf(district, outfit.Id) >= 5 && HoldOf(district, outfit2.Id) >= 5)
					{
						list.Add((outfit, outfit2, district));
					}
				}
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		var (gang, gang2, district2) = list[_rng.Next(list.Count)];
		if (_rng.Next(2) == 0)
		{
			Gang gang3 = gang2;
			gang2 = gang;
			gang = gang3;
		}
		int num = Relation(gang.Id, gang2.Id);
		bool flag = gang.OrdersNotFeelings || gang2.OrdersNotFeelings || _rng.Next(100) < ((num <= Config.WarLine) ? 80 : ((num < 0) ? 65 : 45));
		string[] array = (flag ? BadBlood : Deals);
		string name = district2.Name;
		string input = array[_rng.Next(array.Length)].Replace("{aw}", OutfitWord(gang)).Replace("{bw}", OutfitWord(gang2)).Replace("{a}", gang.Name)
			.Replace("{b}", gang2.Name)
			.Replace("{p}", name)
			.Replace("{d}", district2.Name);
		input = Regex.Replace(input, "(^|\\s)([aA]) (?=[AEIOaeio])", "$1$2n ");
		input = char.ToUpperInvariant(input[0]) + input.Substring(1);
		int delta = (flag ? (-(10 + _rng.Next(13))) : (8 + _rng.Next(11)));
		int num2 = num;
		ShiftRelation(gang.Id, gang2.Id, delta, char.ToLowerInvariant(input[0]) + input.Substring(1));
		WorldEvent worldEvent = NewEvent("TURMOIL", now);
		worldEvent.Gangs.Add(gang.Id);
		worldEvent.Gangs.Add(gang2.Id);
		worldEvent.Title = input;
		worldEvent.Place = "in " + district2.Name;
		worldEvent.HasPos = true;
		worldEvent.X = district2.X;
		worldEvent.Y = district2.Y;
		worldEvent.Z = 100f;
		int num3 = Relation(gang.Id, gang2.Id);
		worldEvent.Lines.Add(flag ? $"Bad blood: the {gang.Name} and the {gang2.Name} go from {RelationWord(num2)} to {RelationWord(num3)} ({num2} -> {num3})." : $"Cooler heads: the {gang.Name} and the {gang2.Name} go from {RelationWord(num2)} to {RelationWord(num3)} ({num2} -> {num3}).");
		if (flag && num3 <= Config.WarLine && num2 > Config.WarLine)
		{
			worldEvent.Lines.Add("There's talk of war in " + district2.Name + ".");
		}
		return worldEvent;
	}

	private NemesisRecord ShadowOf(Ped p)
	{
		if (_provoked.TryGetValue(p.Handle, out var value) && value.Shadow != null)
		{
			return value.Shadow;
		}
		if (!_witnessShadow.TryGetValue(p.Handle, out var value2))
		{
			value2 = ShadowFor(p, mind: false);
			Personality? personality = KnownTemper(p);
			if (personality.HasValue)
			{
				value2.Personality = personality;
			}
			_witnessShadow[p.Handle] = value2;
		}
		return value2;
	}

	private float RepAt(Vector3 at)
	{
		return TownWord(_currentTown ?? TownAt(at));
	}

	private static int StartFear(float rep, string temper)
	{
		if (!(rep < 20f))
		{
			return (int)Math.Min(30f, rep / 5f * TemperFearGain(temper));
		}
		return 0;
	}

	internal static int HeardChance(float rep)
	{
		if (!(rep < 15f))
		{
			return (int)Math.Round(75.0 * Math.Pow(Math.Min(1.0, (double)(rep - 15f) / 85.0), 1.2));
		}
		return 0;
	}

	private static float RepUneaseMult(float rep)
	{
		return 1f + rep / 200f;
	}

	private static float RepGrudgeMult(float rep, string temper)
	{
		return Math.Max(0.5f, 1f - rep / 100f * 0.35f * Math.Min(1.5f, TemperFearGain(temper)));
	}

	private static float RepLikeMult(float rep)
	{
		return Math.Max(0.5f, 1f - rep / 100f * 0.4f);
	}

	private static float LikeGain(NemesisRecord sh)
	{
		if (sh == null)
		{
			return 1f;
		}
		return ((sh.Disposition == Disposition.Friendly) ? 1.5f : ((sh.Disposition == Disposition.Surly) ? 0.5f : 1f)) * ((sh.Sociability == Sociability.Outgoing) ? 1.3f : 0.8f) * Tempers.Of(sh.Personality).LikeGain;
	}

	private int LikeDelta(int handle, int baseDelta)
	{
		if (baseDelta <= 0)
		{
			return baseDelta;
		}
		Ped ped = Entity.FromHandle(handle) as Ped;
		if (ped == null || !ped.Exists())
		{
			return baseDelta;
		}
		float num = LikeGain(ShadowOf(ped)) * RepLikeMult(RepAt(ped.Position));
		return Math.Max(1, (int)Math.Round((float)baseDelta * num));
	}

	private void NervousGreet(int handle)
	{
		if (!_acqOf.TryGetValue(handle, out var value) || !_named.TryGetValue(handle, out var value2))
		{
			return;
		}
		Ped ped = Entity.FromHandle(handle) as Ped;
		if (!(ped == null) && ped.Exists())
		{
			float num = RepAt(ped.Position);
			if ((value.Fear >= 15 || !(num < 40f)) && _rng.Next(100) < 70)
			{
				string text = value.Temper ?? "";
				string[] value3;
				string[] array = (NervousLines.TryGetValue(text, out value3) ? value3 : NervousLines[""]);
				ShowSubtitle(value2.Name + ": \"" + array[_rng.Next(array.Length)] + "\"");
				RepNote(string.Format("NERVOUS {0} ({1}, fear {2}, infamy {3:0}) greeted you back", value2.Name, (text == "") ? "?" : text.ToLowerInvariant(), value.Fear, num));
			}
		}
	}

	internal void RepNote(string text)
	{
		_repNotes.Add((UiDraw.RealMs, text));
		if (_repNotes.Count > 30)
		{
			_repNotes.RemoveAt(0);
		}
	}

	internal IEnumerable<string> ReputationLines()
	{
		List<Acquaintance> alive = People.Where((Acquaintance a) => !a.Dead).ToList();
		int afraid = alive.Count((Acquaintance a) => a.Fear > 0);
		int terrified = alive.Count((Acquaintance a) => a.Fear >= 50);
		int liked = alive.Count((Acquaintance a) => a.Like > 0);
		string noto = string.Join(", ", Gangs.All.Select((Gang g) => $"{g.Name} {NotorietyOf(g)} ({Notoriety.LevelName(NotorietyOf(g))})"));
		int num = OverallReputation();
		yield return string.Format("REPUTATION {0:+0;-0;0} ({1}) - bounty ${2} (law part {3}){4}", num, RepWord(num), BountyDollars, LawPart(), LAW.IS_LAW_INCIDENT_ACTIVE(Game.Player.Handle) ? " - the law is after you" : "");
		foreach (var item in (from t in RepTowns
			select (t: t, p: TownRep(t)) into x
			where x.p.Known > 0 || x.p.Illwill > 0
			orderby x.p.Known descending
			select x).Take(6))
		{
			yield return "  " + TownRepLine(item.t);
		}
		yield return "notoriety: " + noto;
		IEnumerable<string> first = (from a in alive
			where a.Town != null && (a.Fear > 0 || a.Like > 0)
			select a.Town).Distinct();
		IEnumerable<string> enumerable = Chronicle.TownWord?.Keys;
		List<(string, float, int, int)> list = (from t in first.Concat(enumerable ?? Enumerable.Empty<string>()).Distinct()
			select (t: t, w: TownWord(t), af: alive.Count((Acquaintance a) => a.Town == t && a.Fear > 0), lk: alive.Count((Acquaintance a) => a.Town == t && a.Like > 0)) into x
			orderby x.w descending
			select x).Take(6).ToList();
		yield return "infamy (fear talk): " + ((list.Count == 0) ? "nobody talks about you" : string.Join(";  ", list.Select<(string, float, int, int), string>(((string t, float w, int af, int lk) x) => $"{x.t} {x.w:0} ({x.af} afraid, {x.lk} like you)")));
		yield return $"people who know you: {alive.Count} - {afraid} afraid (avg {((afraid > 0) ? alive.Where((Acquaintance a) => a.Fear > 0).Average((Acquaintance a) => a.Fear) : 0.0):0}), {terrified} at 50+, {liked} like you, " + $"{People.Count((Acquaintance a) => a.Reported)} told the law, {People.Count((Acquaintance a) => a.KeptQuiet)} kept quiet, {People.Count((Acquaintance a) => a.Dead)} dead";
		List<NemesisRecord> list2 = Roster.Where((NemesisRecord r) => r.IsAlive && !r.PresumedDead).ToList();
		yield return $"nemeses: {list2.Count} alive - {list2.Count((NemesisRecord r) => r.HateTier == HateTier.Murderous)} murderous, {list2.Count((NemesisRecord r) => r.HateTier == HateTier.Vengeful)} vengeful, " + $"{list2.Count((NemesisRecord r) => r.HateTier == HateTier.Resentful)} resentful ({list2.Count((NemesisRecord r) => r.Gang == null)} loners)";
	}

	internal IEnumerable<string> PeopleNearLines(Ped player)
	{
		Vector3 me = player.Position;
		List<(int, Named, float)> list = (from kv in _named
			where !kv.Value.Story && ENTITY.DOES_ENTITY_EXIST(kv.Key) && !ENTITY.IS_ENTITY_DEAD(kv.Key)
			select (h: kv.Key, n: kv.Value, d: new Ped(kv.Key).Position.DistanceTo(me)) into t
			where t.d < 40f
			orderby t.d
			select t).Take(12).ToList();
		if (list.Count == 0)
		{
			yield return "nobody named near you";
			yield break;
		}
		foreach (var item4 in list)
		{
			int item = item4.Item1;
			Named item2 = item4.Item2;
			float item3 = item4.Item3;
			NemesisRecord nemesisRecord = RecordAt(item);
			if (nemesisRecord != null)
			{
				yield return $"{nemesisRecord.Name} {item3:0}m - NEMESIS {nemesisRecord.Personality}, hate {nemesisRecord.Hate}, fear {EffectiveFear(nemesisRecord)}";
				continue;
			}
			_acqOf.TryGetValue(item, out var value);
			_provoked.TryGetValue(item, out var value2);
			float num = UneaseOf(item);
			string text = ((value2 != null) ? value2.Temper.ToString() : (value?.Temper ?? "?"));
			string text2 = string.Format("fear {0}{1}, likes {2}, grudge {3:0}", value?.Fear ?? 0, (num >= 1f) ? $" | uneasy {num:0}" : "", value?.Like ?? 0, value2?.Score ?? ((float)(value?.Grudge ?? 0)));
			string text3 = value?.Witnessed?.LastOrDefault();
			yield return string.Format("{0}{1} {2:0}m - {3}, {4}{5}", item2.Met ? "*" : "", item2.Name, item3, text, text2, (text3 != null) ? (" - " + text3) : "");
		}
	}

	internal IEnumerable<string> RecentRepLines()
	{
		if (_repNotes.Count == 0)
		{
			yield return "nothing yet this session";
			yield break;
		}
		foreach (var item3 in Enumerable.Reverse(_repNotes).Take(14))
		{
			int item = item3.at;
			string item2 = item3.text;
			int num = Math.Max(0, (UiDraw.RealMs - item) / 1000);
			yield return ((num < 60) ? (num + "s") : (num / 60 + "m")) + "  " + item2;
		}
	}
}
