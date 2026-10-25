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
	private static readonly (string key, string label)[] CrimeKinds = new(string, string)[12]
	{
		("kill", "killings"),
		("gore", "butchered"),
		("shot", "shootings"),
		("cut", "knifings"),
		("punch", "beatings"),
		("ko", "knocked cold"),
		("rob", "robberies"),
		("stickup", "stick-ups"),
		("hogtie", "kidnappings"),
		("horse", "horses stolen"),
		("wagon", "wagons stolen"),
		("horsekill", "horses killed")
	};

	private CrimeProfile _crimeCache;

	private NemesisRecord _crimeCacheFor;

	private int _crimeCacheAt = -100000;

	private bool _crimeCacheDebug;

	internal void CountYourCrime(string crime)
	{
		if (crime != null)
		{
			Dictionary<string, int> dictionary = Chronicle.YourCrimes ?? (Chronicle.YourCrimes = new Dictionary<string, int>());
			dictionary[crime] = ((!dictionary.TryGetValue(crime, out var value)) ? 1 : (value + 1));
			SaveWorld();
		}
	}

	private static string CrimeBadge(string text)
	{
		string text2 = (text ?? "").ToLowerInvariant();
		if (text2.Contains("horse"))
		{
			return "HORSE THIEF";
		}
		if (text2.Contains("train") || text2.Contains("stage") || text2.Contains("mail") || text2.Contains("coach"))
		{
			return "HOLDUP";
		}
		if (text2.Contains("bank"))
		{
			return "BANK JOB";
		}
		if (text2.Contains("rob") || text2.Contains("stole") || text2.Contains("theft") || text2.Contains("thie"))
		{
			return "ROBBERY";
		}
		if (text2.Contains("kill") || text2.Contains("murder") || text2.Contains("shot"))
		{
			return "KILLING";
		}
		if (text2.Contains("sisika") || text2.Contains("jail") || text2.Contains("prison") || text2.Contains("escape") || text2.Contains("broke out"))
		{
			return "PRISON";
		}
		if (text2.Contains("card") || text2.Contains("cheat"))
		{
			return "CARDS";
		}
		if (text2.Contains("fire") || text2.Contains("burn"))
		{
			return "ARSON";
		}
		if (text2.Contains("cattle") || text2.Contains("rustl"))
		{
			return "RUSTLING";
		}
		return "CRIME";
	}

	internal static (int r, int g, int b) OutcomeColor(string o)
	{
		switch (o)
		{
		default:
			return Theme.Dim;
		case "DISTINGUISHED":
		case "WON":
			return Theme.Good;
		case "WOUNDED":
		case "LOST":
			return Theme.Warn;
		case "KILLED":
			return Theme.Blood;
		}
	}

	internal CrimeProfile CrimeSheet(NemesisRecord n)
	{
		if (_crimeCache != null && _crimeCacheFor == n && _crimeCacheDebug == _debugOn && UiDraw.RealMs - _crimeCacheAt < 500)
		{
			return _crimeCache;
		}
		_crimeCache = BuildCrimeSheet(n);
		_crimeCacheFor = n;
		_crimeCacheAt = UiDraw.RealMs;
		_crimeCacheDebug = _debugOn;
		return _crimeCache;
	}

	private CrimeProfile BuildCrimeSheet(NemesisRecord n)
	{
		if (n.Id == YouId)
		{
			return YourCrimeSheet();
		}
		CrimeProfile crimeProfile = new CrimeProfile();
		bool flag = n.TemperKnown || _debugOn;
		List<PastEntry> list = (from e in n.Past ?? new List<PastEntry>()
			where PastShown(e) && e.Kind == "crime" && (e.Known || (e.Public && n.TemperKnown) || _debugOn)
			orderby e.Year descending
			select e).ToList();
		foreach (PastEntry item in list.Take(4))
		{
			crimeProfile.Crimes.Add((item.WhenText, item.Text, CrimeBadge(item.Text)));
		}
		crimeProfile.CrimesTitle = "KNOWN CRIMES";
		crimeProfile.CrimesNote = ((list.Count != 0) ? ((list.Count > 4) ? $"and {list.Count - 4} more" : ((!flag) ? "There may be more - make him talk" : null)) : (flag ? "Nothing you know of" : "Unknown - make him talk"));
		List<WorldEvent> list2 = (from e in Chronicle.Events
			where e.People != null && e.People.Any((WorldPart p) => p.Id == n.Id) && e.Kind != "LIFE" && e.Kind != "RANKS"
			orderby e.GameMin descending
			select e).ToList();
		int num = list2.Count((WorldEvent e) => e.People.Any((WorldPart p) => p.Id != n.Id && p.Outcome == "KILLED") && e.People.First((WorldPart p) => p.Id == n.Id).Outcome != "KILLED");
		foreach (WorldEvent item2 in list2.Take(4))
		{
			string text = (item2.People.First((WorldPart p) => p.Id == n.Id).Outcome ?? "").ToUpperInvariant();
			crimeProfile.Events.Add((KindLabel(item2.Kind), KindColor(item2.Kind), item2.Title ?? "", text, OutcomeColor(text)));
		}
		crimeProfile.EventsNote = ((list2.Count == 0) ? "Nothing on record" : ((list2.Count > 4) ? $"and {list2.Count - 4} more" : null));
		Gang gang = ((n.Gang != null) ? Gangs.ById(n.Gang) : null);
		if (gang != null)
		{
			crimeProfile.Side = gang.Name.ToUpperInvariant();
			crimeProfile.SideCol = gang.Accent;
			crimeProfile.SideFilled = true;
		}
		else if (n.Affiliation != null && flag)
		{
			Gang gang2 = Gangs.ById(n.Affiliation);
			crimeProfile.Side = "IN WITH THE " + (gang2?.Name ?? n.Affiliation).ToUpperInvariant();
			crimeProfile.SideCol = gang2?.Accent ?? Theme.Warn;
		}
		else
		{
			crimeProfile.Side = "NO GANG";
			crimeProfile.SideCol = Theme.Dim;
		}
		int num2 = 5 * list.Count + 15 * n.TimesKilledPlayer + 10 * n.TimesRobbedPlayer + 8 * num + 10 * Math.Max(0, n.Rank - 1) + ((n.Gang != null) ? 10 : 0);
		if (n.Story)
		{
			crimeProfile.BannerLabel = "RIDES WITH YOU";
			crimeProfile.BannerValue = "";
			crimeProfile.BannerWord = "the law knows the gang, not the man";
			crimeProfile.BannerCol = Theme.Dim;
		}
		else
		{
			crimeProfile.BannerLabel = ((num2 >= 60) ? "WANTED" : ((num2 > 0) ? "SMALL TIME" : "NOBODY'S PAYING"));
			crimeProfile.BannerValue = ((num2 > 0) ? $"${num2}" : "-");
			crimeProfile.BannerWord = ((num2 > 0) ? "word on the trail" : "no price on him");
			crimeProfile.BannerCol = ((num2 >= 60) ? Theme.Blood : ((num2 > 0) ? Theme.Gold : Theme.Dim));
		}
		if (!n.Story && !n.Person)
		{
			crimeProfile.TilesTitle = "AGAINST YOU";
			crimeProfile.Tiles.Add((n.TimesKilledPlayer.ToString(), "KILLED YOU", (n.TimesKilledPlayer > 0) ? Theme.Blood : Theme.Dim));
			crimeProfile.Tiles.Add((n.TimesRobbedPlayer.ToString(), "ROBBED YOU", (n.TimesRobbedPlayer > 0) ? Theme.Warn : Theme.Dim));
			crimeProfile.Tiles.Add((n.TimesAmbushed.ToString(), "CAME FOR YOU", (n.TimesAmbushed > 0) ? Theme.Text : Theme.Dim));
		}
		return crimeProfile;
	}

	private void FixJohnRecord()
	{
		WorldState c = Chronicle;
		if (c.JohnRecordFixed || c.EraTurnAt <= 0)
		{
			return;
		}
		c.JohnRecordFixed = true;
		if (!(c.Past ?? new List<WorldEvent>()).Any((WorldEvent e) => e.Kind == "ERA" && e.Title == "Eight years later"))
		{
			SaveWorld();
			return;
		}
		HashSet<string> since = new HashSet<string>(from id in c.Events.Where((WorldEvent e) => e.GameMin >= c.EraTurnAt && e.People != null).SelectMany((WorldEvent e) => from p in e.People
				where p.Outcome == "KILLED"
				select p.Id)
			where id != null
			select id);
		int num = 0;
		foreach (NemesisRecord item in Roster.Where((NemesisRecord r) => r.KilledByPlayer && !since.Contains(r.Id)))
		{
			item.KilledByPlayer = false;
			num++;
		}
		GrudgeLog($"EPILOGUE John's record: {num} of Arthur's kills taken off it");
		SaveRoster();
		SaveWorld();
	}

	private CrimeProfile YourCrimeSheet()
	{
		CrimeProfile crimeProfile = new CrimeProfile
		{
			You = true
		};
		int num = 0;
		try
		{
			num = BountyDollars;
		}
		catch
		{
		}
		string regionName = RegionName;
		crimeProfile.BannerLabel = ((num > 0) ? "WANTED" : "NO BOUNTY");
		crimeProfile.BannerValue = ((num > 0) ? $"${num}" : "-");
		crimeProfile.BannerWord = ((regionName != "?") ? ("in " + regionName) : "where you are");
		crimeProfile.BannerCol = ((num > 0) ? Theme.Blood : Theme.Dim);
		crimeProfile.Rep = OverallReputation();
		Dictionary<string, int> d = Chronicle.YourCrimes ?? new Dictionary<string, int>();
		crimeProfile.TilesTitle = "ON RECORD";
		int value;
		foreach (var item in CrimeKinds.Where(((string key, string label) k) => d.TryGetValue(k.key, out value) && value > 0))
		{
			crimeProfile.Tiles.Add((d[item.key].ToString(), item.label.ToUpperInvariant(), (item.key == "kill" || item.key == "gore") ? Theme.Blood : Theme.Warn));
		}
		FixJohnRecord();
		List<NemesisRecord> list = (from r in Roster
			where !r.IsAlive && r.KilledByPlayer
			orderby r.Rank descending
			select r).ToList();
		crimeProfile.Dead = (from r in list.Take(8)
			select r.Name).ToList();
		crimeProfile.DeadMore = Math.Max(0, list.Count - 8);
		int num2 = People.Count((Acquaintance a) => a.Witnessed != null && a.Witnessed.Any((string w) => w.StartsWith("saw you")));
		int num3 = People.Count((Acquaintance a) => a.Reported);
		int num4 = People.Count((Acquaintance a) => a.KeptQuiet);
		crimeProfile.Witnesses.Add((num2.ToString(), "SAW SOMETHING", (num2 > 0) ? Theme.Text : Theme.Dim));
		crimeProfile.Witnesses.Add((num3.ToString(), "TOLD THE LAW", (num3 > 0) ? Theme.Blood : Theme.Dim));
		crimeProfile.Witnesses.Add((num4.ToString(), "KEPT QUIET", (num4 > 0) ? Theme.Good : Theme.Dim));
		crimeProfile.Towns = (from x in (from t in RepTowns
				select (t: t, p: TownRep(t)) into x
				where x.p.Known > 0 || x.p.Illwill > 0
				orderby Math.Abs(x.p.Rep) descending
				select x).Take(5)
			select (t: x.t, Rep: x.p.Rep)).ToList();
		Gang[] all = Gangs.All;
		foreach (Gang gang in all)
		{
			int num6 = NotorietyOf(gang);
			if (num6 > 0)
			{
				crimeProfile.Factions.Add((gang.Name, num6, gang.Accent));
			}
		}
		crimeProfile.Factions = crimeProfile.Factions.OrderByDescending(((string name, int value, (int r, int g, int b) col) f) => f.value).ToList();
		List<WorldEvent> list2 = (from e in Chronicle.Events
			where e.Kind == "YOU" && (e.Tag == "mission" || e.Tag == "stranger")
			orderby e.GameMin descending
			select e).ToList();
		foreach (WorldEvent item2 in list2.Take(4))
		{
			crimeProfile.Events.Add(((item2.Tag == "mission") ? "THE GANG" : "STRANGER", (item2.Tag == "mission") ? VdlAccent : (150, 170, 200), item2.Title ?? "", (item2.When ?? "").Split(new string[1] { "  " }, StringSplitOptions.None)[0], Theme.Dim));
		}
		crimeProfile.EventsNote = ((list2.Count > 4) ? $"and {list2.Count - 4} more in the World tab" : null);
		return crimeProfile;
	}
}
