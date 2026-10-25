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
	private sealed class Named
	{
		public string Name;

		public string Title;

		public int Importance;

		public int SeenAt;

		public int NamedAt;

		public bool Met;

		public bool Story;

		public string StoryTemper;

		public string StoryNote;

		public string Affil;
	}

	internal static Func<int, bool> LeaderCopyAt;

	private static string[] FemaleFirstFor(string[] maleBank)
	{
		if (maleBank != NameBank.IrishFirst)
		{
			if (maleBank != NameBank.SouthernFirst)
			{
				return NameBank.AmericanFemaleFirst;
			}
			return NameBank.SouthernFemaleFirst;
		}
		return NameBank.IrishFemaleFirst;
	}

	private (string, string) FreshName(uint model, bool female = false)
	{
		Gang gang = Gangs.ForModel(model);
		if (gang != null && gang.RankInName && !female)
		{
			return (SoldierName(gang), "");
		}
		bool flag = South && _rng.Next(100) < 60;
		string[] array = gang?.FirstNames ?? (flag ? NameBank.SouthernFirst : NameBank.AmericanFirst);
		string[] array2 = gang?.LastNames ?? (flag ? NameBank.SouthernLast : NameBank.AmericanLast);
		string[] array3 = gang?.Titles ?? NameBank.AmericanTitles;
		if (female && array != NameBank.WapitiFirst)
		{
			array = FemaleFirstFor(array);
			array3 = NameBank.FemaleTitles;
		}
		Dictionary<string, int> firstUse = new Dictionary<string, int>();
		Dictionary<string, int> lastUse = new Dictionary<string, int>();
		foreach (Named value5 in _named.Values)
		{
			Count(value5.Name);
		}
		foreach (NemesisRecord item in Roster)
		{
			if (item.IsAlive)
			{
				Count(item.Name);
			}
		}
		if (_keptNames != null)
		{
			foreach (string keptName in _keptNames)
			{
				Count(keptName);
			}
		}
		string text = null;
		string text2 = null;
		int num = int.MaxValue;
		for (int i = 0; i < 24; i++)
		{
			string text3 = array[_rng.Next(array.Length)];
			string text4 = array2[_rng.Next(array2.Length)];
			string name = text3 + " " + text4;
			text2 = text2 ?? name;
			if ((gang?.ExcludedNames != null && gang.ExcludedNames.Contains(name)) || Roster.Any((NemesisRecord r) => r.Name == name) || _named.Values.Any((Named n) => n.Name == name) || NameKept(name) || NameRetired(name))
			{
				continue;
			}
			int value;
			int value2;
			int num2 = (firstUse.TryGetValue(text3, out value) ? value : 0) * 2 + (lastUse.TryGetValue(text4, out value2) ? value2 : 0) * 2;
			if (num2 < num)
			{
				text = name;
				num = num2;
				if (num2 == 0)
				{
					break;
				}
			}
		}
		return (text ?? text2, HasNickname(text ?? text2, gang != null) ? array3[_rng.Next(array3.Length)] : "");
		void Count(string full)
		{
			if (!string.IsNullOrEmpty(full))
			{
				int num3 = full.IndexOf(' ');
				if (num3 > 0)
				{
					string key = full.Substring(0, num3);
					string key2 = full.Substring(num3 + 1);
					firstUse[key] = ((!firstUse.TryGetValue(key, out var value3)) ? 1 : (value3 + 1));
					lastUse[key2] = ((!lastUse.TryGetValue(key2, out var value4)) ? 1 : (value4 + 1));
				}
			}
		}
	}

	private string SoldierName(Gang g)
	{
		string[] array = g.LastNames ?? NameBank.AmericanLast;
		string text = null;
		for (int i = 0; i < 30; i++)
		{
			if (text != null)
			{
				break;
			}
			string text2 = array[_rng.Next(array.Length)];
			if (!Taken(text2))
			{
				text = text2;
			}
		}
		return "Pvt. " + (text ?? array[_rng.Next(array.Length)]);
		bool Taken(string sur)
		{
			string end = " " + sur;
			if (!Roster.Any((NemesisRecord r) => r.Gang == g.Id && r.Name != null && r.Name.EndsWith(end)) && !_named.Values.Any((Named n) => n.Name != null && n.Name.EndsWith(end) && Prefixes.Any((string p) => n.Name.StartsWith(p))) && !NameKept("Pvt." + end))
			{
				return NameRetired("Pvt." + end);
			}
			return true;
		}
	}

	private void FixSoldierNames()
	{
		Dictionary<string, string> map = new Dictionary<string, string>();
		int num = 0;
		int num2 = 0;
		foreach (Acquaintance person in People)
		{
			string text = Fix(person.Name, person.Model);
			if (text != person.Name)
			{
				person.Name = text;
				person.Title = "";
				num++;
			}
		}
		foreach (Print value2 in Prints.Values)
		{
			uint result = 0u;
			if (value2.Fp != null && value2.Fp.Length >= 8)
			{
				uint.TryParse(value2.Fp.Substring(0, 8), NumberStyles.HexNumber, null, out result);
			}
			string text2 = Fix(value2.Name, result);
			if (text2 != value2.Name)
			{
				value2.Name = text2;
				value2.Title = "";
				num2++;
			}
		}
		int num3 = _bulletImpact?.RenameVictims((string name, uint model) => Fix(name, model)) ?? 0;
		if (_keptNames != null)
		{
			foreach (KeyValuePair<string, string> item in map)
			{
				if (_keptNames.Remove(item.Key))
				{
					_keptNames.Add(item.Value);
				}
			}
		}
		if (num > 0)
		{
			_peopleDirty = true;
		}
		if (num2 > 0)
		{
			_printsDirty = true;
			_nextPrintsSave = 0;
		}
		if (map.Count > 0)
		{
			GrudgeLog($"NAMING soldier names fixed: {num} people you know, {num2} looks, {num3} X-ray entries");
		}
		string Fix(string full, uint model)
		{
			if (!string.IsNullOrEmpty(full))
			{
				Gang gang = Gangs.ForModel(model);
				if (gang != null && gang.RankInName)
				{
					if (Prefixes.Any((string p) => full.StartsWith(p)) || !full.Contains(' '))
					{
						return full;
					}
					if (map.TryGetValue(full, out var value))
					{
						return value;
					}
					string text3 = "Pvt. " + full.Substring(full.LastIndexOf(' ') + 1);
					map[full] = text3;
					GrudgeLog("NAMING soldier rename " + full + " -> " + text3);
					return text3;
				}
			}
			return full;
		}
	}

	private bool NameRetired(string name)
	{
		if (_deadNames == null || Game.GameTime - _deadNamesAt > 10000)
		{
			_deadNamesAt = Game.GameTime;
			IEnumerable<string> first = (from a in People
				where a.Name != null
				select a.Name).Concat(from p in Prints.Values
				where p.Dead && p.Name != null
				select p.Name);
			IEnumerable<string> retiredNames = Chronicle.RetiredNames;
			_deadNames = new HashSet<string>(first.Concat(retiredNames ?? Enumerable.Empty<string>()));
		}
		return _deadNames.Contains(name);
	}

	private void RetireDeadNames()
	{
		List<string> list = Chronicle.RetiredNames ?? (Chronicle.RetiredNames = new List<string>());
		bool flag = false;
		foreach (KeyValuePair<int, Named> item in _named)
		{
			Named value = item.Value;
			if (!value.Story && !string.IsNullOrEmpty(value.Name) && ENTITY.DOES_ENTITY_EXIST(item.Key) && ENTITY.IS_ENTITY_DEAD(item.Key) && !LooksKnockedOut(new Ped(item.Key)) && !list.Contains(value.Name))
			{
				list.Add(value.Name);
				_deadNames?.Add(value.Name);
				flag = true;
				GrudgeLog("NAMING " + value.Name + " died - the name is retired");
			}
		}
		if (flag)
		{
			if (list.Count > 3000)
			{
				list.RemoveRange(0, list.Count - 3000);
			}
			SaveWorld();
		}
	}

	private bool HasNickname(string name, bool gang)
	{
		if (!string.IsNullOrEmpty(name))
		{
			return (uint)Seeds.Stable("nick/" + name) % 100u < (uint)(gang ? Config.NicknameGangPercent : Config.NicknameLonerPercent);
		}
		return false;
	}

	private void ThinNicknames()
	{
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		foreach (NemesisRecord item in Roster)
		{
			if (!string.IsNullOrEmpty(item.Title) && !HasNickname(item.Name, item.Gang != null))
			{
				item.Title = "";
				num++;
			}
		}
		foreach (Acquaintance person in People)
		{
			if (!string.IsNullOrEmpty(person.Title) && !HasNickname(person.Name, Gangs.ForModel(person.Model) != null))
			{
				person.Title = "";
				num2++;
			}
		}
		foreach (Print value in Prints.Values)
		{
			uint result = 0u;
			if (value.Fp != null && value.Fp.Length >= 8)
			{
				uint.TryParse(value.Fp.Substring(0, 8), NumberStyles.HexNumber, null, out result);
			}
			if (!string.IsNullOrEmpty(value.Title) && !HasNickname(value.Name, Gangs.ForModel(result) != null))
			{
				value.Title = "";
				num3++;
			}
		}
		if (num > 0)
		{
			SaveRoster();
		}
		if (num2 > 0)
		{
			_peopleDirty = true;
		}
		if (num3 > 0)
		{
			_printsDirty = true;
			_nextPrintsSave = 0;
		}
		GrudgeLog($"NAMING nicknames thinned to {Config.NicknameGangPercent}% gang / {Config.NicknameLonerPercent}% others: dropped {num} on the roster, {num2} people you know, {num3} looks");
	}

	private static string ModelName(uint model)
	{
		return Models.Name(model);
	}

	private void MoveLaramieModels()
	{
		uint num = 440831189u;
		uint modelHash = 3204687205u;
		int num2 = 0;
		foreach (NemesisRecord item in Roster)
		{
			if (!(item.Gang != Gangs.LaramieGang.Id) && item.ModelHash == num && item.IsAlive && item.TimesAmbushed <= 0 && item.TimesAntagonized <= 0 && item.TimesInterrogated <= 0 && item.TimesKnockedOutByPlayer <= 0 && !item.LastInteraction.HasValue && item.LastCameAt <= 0 && !item.PersonalityKnown)
			{
				item.ModelHash = modelHash;
				item.OutfitHash = 0u;
				num2++;
			}
		}
		if (num2 != 0)
		{
			SaveRoster();
			GrudgeLog($"NAMING {num2} Laramie men moved to the gang's own model (g_m_m_uniranchers_01) - the ones you've faced keep their face");
		}
	}

	private void FixKinSurnames()
	{
		foreach (NemesisRecord r in Roster)
		{
			if (r.KinId == null || r.Name == null || !r.Name.Contains(' '))
			{
				continue;
			}
			NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord k) => k.Id == r.KinId);
			if (nemesisRecord == null || nemesisRecord.Gang != r.Gang)
			{
				continue;
			}
			Gang gang = Gangs.ById(r.Gang);
			if (gang != null && !gang.FamilyNames)
			{
				continue;
			}
			string text = FamilyNameOf(nemesisRecord);
			string text2 = NameBank.Surname(r.Name);
			bool flag = text.EndsWith(" " + text2);
			bool flag2 = IsLeaderCopy(nemesisRecord) && text2.EndsWith(" " + text);
			if (!(text == text2) && (flag || flag2))
			{
				string name = r.Name;
				string now = r.Name.Split(' ')[0] + " " + text;
				if (!Roster.Any((NemesisRecord n) => n != r && n.Name == now))
				{
					r.Name = now;
					GrudgeLog("NAMING " + name + " -> " + r.Name + " (" + (flag2 ? "the family name" : "the whole family name") + " of his " + (r.KinRelation ?? "kin") + " " + nemesisRecord.Name + ")");
					SaveRoster();
				}
			}
		}
	}

	private void RenameMobOnce()
	{
		int n = 0;
		foreach (WorldEvent item in Chronicle.Events.Concat(Chronicle.Past ?? new List<WorldEvent>()))
		{
			item.Title = R(item.Title);
			item.Place = R(item.Place);
			for (int i = 0; i < item.Lines.Count; i++)
			{
				item.Lines[i] = R(item.Lines[i]);
			}
			foreach (WorldPart person in item.People)
			{
				person.Note = R(person.Note);
				for (int j = 0; j < person.Effects.Count; j++)
				{
					person.Effects[j] = R(person.Effects[j]);
				}
			}
		}
		foreach (NemesisRecord item2 in Roster)
		{
			if (item2.History != null)
			{
				foreach (HistoryEntry item3 in item2.History)
				{
					item3.Text = R(item3.Text);
				}
			}
			if (item2.Past != null)
			{
				foreach (PastEntry item4 in item2.Past)
				{
					item4.Text = R(item4.Text);
				}
			}
			if (item2.Thoughts != null)
			{
				foreach (Thought thought in item2.Thoughts)
				{
					thought.Text = R(thought.Text);
				}
			}
			if (item2.Memories == null)
			{
				continue;
			}
			foreach (Memory memory in item2.Memories)
			{
				memory.Text = R(memory.Text);
			}
		}
		if (n > 0)
		{
			SaveWorld();
			SaveRoster();
			GrudgeLog($"NAMING the Saint Denis Mob is the Bronte Family now - {n} old lines renamed");
		}
		string R(string t)
		{
			if (t != null && t.Contains("Saint Denis Mob"))
			{
				n++;
				return t.Replace("Saint Denis Mob", "Bronte Family");
			}
			return t;
		}
	}

	private void FixTheKlanOnce()
	{
		int n = 0;
		foreach (WorldEvent item in Chronicle.Events.Concat(Chronicle.Past ?? new List<WorldEvent>()))
		{
			if (item == null)
			{
				continue;
			}
			item.Title = R(item.Title);
			item.Place = R(item.Place);
			if (item.Lines != null)
			{
				for (int i = 0; i < item.Lines.Count; i++)
				{
					item.Lines[i] = R(item.Lines[i]);
				}
			}
			if (item.People == null)
			{
				continue;
			}
			foreach (WorldPart person in item.People)
			{
				person.Note = R(person.Note);
				if (person.Effects != null)
				{
					for (int j = 0; j < person.Effects.Count; j++)
					{
						person.Effects[j] = R(person.Effects[j]);
					}
				}
			}
		}
		foreach (NemesisRecord item2 in Roster)
		{
			if (item2.History != null)
			{
				foreach (HistoryEntry item3 in item2.History)
				{
					item3.Text = R(item3.Text);
				}
			}
			if (item2.Past != null)
			{
				foreach (PastEntry item4 in item2.Past)
				{
					item4.Text = R(item4.Text);
				}
			}
			if (item2.Thoughts != null)
			{
				foreach (Thought thought in item2.Thoughts)
				{
					thought.Text = R(thought.Text);
				}
			}
			if (item2.Memories == null)
			{
				continue;
			}
			foreach (Memory memory in item2.Memories)
			{
				memory.Text = R(memory.Text);
			}
		}
		if (n > 0)
		{
			SaveWorld();
			SaveRoster();
			GrudgeLog($"NAMING 'the The Klan' fixed in {n} saved lines");
		}
		string R(string t)
		{
			if (t == null || t.IndexOf("he The Klan", StringComparison.Ordinal) < 0)
			{
				return t;
			}
			n++;
			return t.Replace("the The Klan", "the Klan").Replace("The The Klan", "The Klan");
		}
	}

	private void FixFemaleNames()
	{
		HashSet<string> male = new HashSet<string>(NameBank.AmericanFirst.Concat(NameBank.SouthernFirst).Concat(NameBank.IrishFirst));
		HashSet<string> femaleAll = new HashSet<string>(NameBank.AmericanFemaleFirst.Concat(NameBank.SouthernFemaleFirst).Concat(NameBank.IrishFemaleFirst));
		Dictionary<string, string> map = new Dictionary<string, string>();
		HashSet<string> taken = new HashSet<string>(from x in Roster.Select((NemesisRecord r) => r.Name).Concat(People.Select((Acquaintance a) => a.Name)).Concat(Prints.Values.Select((Print x) => x.Name))
			where x != null
			select x);
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		foreach (NemesisRecord item in Roster)
		{
			string text = Fix(item.Name, ModelName(item.ModelHash), item.ModelHash);
			if (text != item.Name)
			{
				item.Name = text;
				num++;
			}
		}
		foreach (Acquaintance person in People)
		{
			string text2 = Fix(person.Name, ModelName(person.Model), person.Model);
			if (text2 != person.Name)
			{
				person.Name = text2;
				person.Title = (HasNickname(text2, gang: false) ? FemaleTitle() : "");
				num2++;
			}
		}
		foreach (Print value2 in Prints.Values)
		{
			uint result = 0u;
			if (value2.Fp != null && value2.Fp.Length >= 8)
			{
				uint.TryParse(value2.Fp.Substring(0, 8), NumberStyles.HexNumber, null, out result);
			}
			string text3 = Fix(value2.Name, value2.Model, result);
			if (text3 != value2.Name)
			{
				value2.Name = text3;
				value2.Title = (HasNickname(text3, gang: false) ? FemaleTitle() : "");
				num3++;
			}
		}
		if (map.Count == 0)
		{
			return;
		}
		foreach (NemesisRecord item2 in Roster)
		{
			if (item2.History == null)
			{
				continue;
			}
			foreach (HistoryEntry item3 in item2.History)
			{
				if (item3.Text == null)
				{
					continue;
				}
				foreach (KeyValuePair<string, string> item4 in map)
				{
					if (item3.Text.Contains(item4.Key))
					{
						item3.Text = item3.Text.Replace(item4.Key, item4.Value);
					}
				}
			}
		}
		if (_keptNames != null)
		{
			foreach (KeyValuePair<string, string> item5 in map)
			{
				if (_keptNames.Remove(item5.Key))
				{
					_keptNames.Add(item5.Value);
				}
			}
		}
		if (num > 0)
		{
			SaveRoster();
		}
		if (num2 > 0)
		{
			_peopleDirty = true;
		}
		if (num3 > 0)
		{
			_printsDirty = true;
			_nextPrintsSave = 0;
		}
		GrudgeLog($"NAMING female names fixed: {num} on the roster, {num2} people you know, {num3} looks ({map.Count} names)");
		string FemaleTitle()
		{
			return NameBank.FemaleTitles[_rng.Next(NameBank.FemaleTitles.Length)];
		}
		string Fix(string full, string modelName, uint modelHash)
		{
			if (string.IsNullOrEmpty(full) || modelName == null || !modelName.Contains("_f_"))
			{
				return full;
			}
			if (map.TryGetValue(full, out var value))
			{
				return value;
			}
			int num4 = full.IndexOf(' ');
			if (num4 <= 0)
			{
				return full;
			}
			string text4 = full.Substring(0, num4);
			string text5 = full.Substring(num4 + 1);
			if (!male.Contains(text4) || femaleAll.Contains(text4))
			{
				return full;
			}
			string[] array = ((Gangs.ForModel(modelHash)?.FirstNames == NameBank.IrishFirst) ? NameBank.IrishFemaleFirst : ((NameBank.SouthernFirst.Contains(text4) && !NameBank.AmericanFirst.Contains(text4)) ? NameBank.SouthernFemaleFirst : NameBank.AmericanFemaleFirst));
			string text6 = null;
			for (int i = 0; i < 30; i++)
			{
				if (text6 != null)
				{
					break;
				}
				string text7 = array[_rng.Next(array.Length)] + " " + text5;
				if (!taken.Contains(text7))
				{
					text6 = text7;
				}
			}
			text6 = text6 ?? (array[_rng.Next(array.Length)] + " " + text5);
			taken.Add(text6);
			map[full] = text6;
			GrudgeLog("NAMING female rename " + full + " -> " + text6 + " (" + modelName + ")");
			return text6;
		}
	}

	internal static bool IsStoryPed(Ped p)
	{
		if (p == null || !p.Exists())
		{
			return false;
		}
		if (!GameNamed(Models.Name(p)))
		{
			return false;
		}
		if (LeaderCopyAt != null)
		{
			return !LeaderCopyAt(p.Handle);
		}
		return true;
	}

	internal static bool GameNamed(string ml)
	{
		if (string.IsNullOrEmpty(ml))
		{
			return false;
		}
		if (ml.StartsWith("cs_"))
		{
			return true;
		}
		if (!ml.StartsWith("u_"))
		{
			return false;
		}
		if (!StoryCast.TryGet(ml, out var _) && TownCast.NameFor(ml) == null)
		{
			return LoreModels.Contains(ml);
		}
		return true;
	}

	private static bool UniqueModel(int ped)
	{
		return Models.Name(ENTITY.GET_ENTITY_MODEL(ped)).StartsWith("u_");
	}
}
