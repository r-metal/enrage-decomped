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
	private readonly Dictionary<int, Acquaintance> _acqOf = new Dictionary<int, Acquaintance>();

	private readonly List<Ped> _regulars = new List<Ped>();

	private bool _peopleDirty;

	private int _nextPeopleSave;

	private int _nextPeopleSync;

	private List<Acquaintance> _peopleListCache;

	private int _peopleListAt = -100000;

	private bool _peopleSoft;

	private int _peopleSoftSavedAt;

	private readonly Dictionary<string, NemesisRecord> _personPages = new Dictionary<string, NemesisRecord>();

	private string SeatNoteExtra = "";

	private readonly Dictionary<int, int> _seatTaken = new Dictionary<int, int>();

	private string PeoplePath => Path.Combine(_saveDir, "people.json");

	internal List<Acquaintance> PeopleForList()
	{
		if (_peopleListCache != null && UiDraw.RealMs - _peopleListAt < 250)
		{
			return _peopleListCache;
		}
		_peopleListAt = UiDraw.RealMs;
		return _peopleListCache = PeopleForListNow();
	}

	private List<Acquaintance> PeopleForListNow()
	{
		HashSet<string> onRoster = new HashSet<string>(Roster.Select((NemesisRecord r) => r.Name));
		List<Acquaintance> list = People.Where((Acquaintance a) => a != null && !string.IsNullOrEmpty(a.Name) && !onRoster.Contains(a.Name) && (a.Met || a.Grudge > 0 || a.Fear > 0 || a.Like > 0 || a.Favours > 0 || a.LastDealt > DateTime.MinValue || (a.Witnessed != null && a.Witnessed.Count > 0))).ToList();
		HashSet<string> hashSet = new HashSet<string>(list.Select((Acquaintance a) => a.Name));
		foreach (KeyValuePair<int, Named> item in _named)
		{
			Named value = item.Value;
			if (!value.Story && !string.IsNullOrEmpty(value.Name) && !hashSet.Contains(value.Name) && !onRoster.Contains(value.Name) && RecordAt(item.Key) == null && _provoked.TryGetValue(item.Key, out var value2) && !value2.Settled)
			{
				Acquaintance acquaintance = new Acquaintance
				{
					LifeRules = 2,
					Model = (ENTITY.DOES_ENTITY_EXIST(item.Key) ? ENTITY.GET_ENTITY_MODEL(item.Key) : 0u),
					Name = value.Name,
					Title = value.Title,
					Met = value.Met,
					Town = _currentTown,
					LastSeen = DateTime.Now,
					LastGameMin = GameMinutesNow(),
					TimesSeen = 1
				};
				if (_provoked.TryGetValue(item.Key, out var value3) && !value3.Settled)
				{
					acquaintance.Grudge = (int)value3.Score;
					acquaintance.Temper = value3.Temper.ToString();
				}
				hashSet.Add(value.Name);
				list.Add(acquaintance);
			}
		}
		return list;
	}

	private List<Acquaintance> People
	{
		get
		{
			if (_people != null)
			{
				return _people;
			}
			_people = SafeFile.Read<List<Acquaintance>>(PeoplePath, GrudgeLog);
			return _people ?? (_people = new List<Acquaintance>());
		}
	}

	private void SavePeople(bool force = false, bool later = false)
	{
		if ((_peopleDirty || !_peopleSoft || (!force && Game.GameTime - _peopleSoftSavedAt < 60000)) && (!_peopleDirty || (!force && Game.GameTime < _nextPeopleSave)))
		{
			return;
		}
		_nextPeopleSave = Game.GameTime + 5000;
		try
		{
			string text = JsonConvert.SerializeObject(People);
			if (force && !later && SafeFile.WaitWrites())
			{
				SafeFile.Write(PeoplePath, text, keepBak: true);
			}
			else
			{
				SafeFile.WriteLater(PeoplePath, text, keepBak: true, delegate(Exception err)
				{
					if (err != null)
					{
						_peopleDirty = true;
					}
				});
			}
			_peopleDirty = false;
			_peopleSoft = false;
			_peopleSoftSavedAt = Game.GameTime;
		}
		catch (Exception ex)
		{
			GrudgeLog("PEOPLE save failed: " + ex.Message);
		}
	}

	internal NemesisRecord PersonRecord(Acquaintance a)
	{
		if (a == null)
		{
			return null;
		}
		string key = (a.Id ?? a.Name) + "|" + a.Model;
		if (!_personPages.TryGetValue(key, out var value))
		{
			Gang gang = Gangs.ForModel(a.Model);
			value = new NemesisRecord
			{
				Name = "",
				ModelHash = a.Model,
				Gang = gang?.Id
			};
			value.Id = $"stranger:{a.Name}|{a.Model:X8}";
			RollNewLife(value, null, mind: true, (a.LifeRules < 2) ? 1 : 2);
			if (a.LifeRules >= 2 && !string.IsNullOrEmpty(a.Temper) && Enum.TryParse<Personality>(a.Temper, out var result) && value.Personality != result)
			{
				Personality? personality = value.Personality;
				value.Personality = result;
				BackstoryResult backstoryResult = Backstory.Roll(value, Seeds.For(value, "life-fit"), GameYear(), GameMonth(), derive: false, keepBorn: true);
				value.LifeFx = (from kv in backstoryResult.Tilt
					where kv.Value != 0
					select $"t:{kv.Key.ToString().ToLowerInvariant()}:{kv.Value}").ToList();
				Mind.Roll(value, Seeds.For(value, "mind"), GameMinutesNow());
				value.Quirks = new List<string>();
				value.QuirksRolled = false;
				GrudgeLog($"HISTORY {a.Name}: known as {result} (the roll said {personality}) - his life written to fit");
			}
			value.Id = "person:" + (a.Id ?? a.Name);
			value.KinId = null;
			value.FriendIds = new List<string>();
			_personPages[key] = value;
		}
		value.Name = a.Name;
		value.Title = a.Title ?? "";
		value.Person = true;
		value.PersonAcq = a;
		if (!string.IsNullOrEmpty(a.Temper) && Enum.TryParse<Personality>(a.Temper, out var result2))
		{
			value.Personality = result2;
		}
		value.PersonalityKnown = a.Met || a.Grudge > 0;
		if (value.Past != null)
		{
			foreach (PastEntry item in value.Past)
			{
				item.Known = a.Met;
			}
		}
		value.KillLocation = a.Town;
		return value;
	}

	internal void NoteDealt(int handle)
	{
		if (handle != 0 && _acqOf.TryGetValue(handle, out var value))
		{
			value.LastDealt = DateTime.Now;
			_peopleDirty = true;
		}
	}

	internal void RememberPerson(int handle, bool met)
	{
		if (!Config.RememberPeople || handle == 0 || RecordAt(handle) != null || OutfitFighter(handle) || !_named.TryGetValue(handle, out var n) || n.Story)
		{
			return;
		}
		Ped ped = Entity.FromHandle(handle) as Ped;
		if (ped == null || !ped.Exists() || ped.IsDead || !ped.IsHuman || Kids.IsChild(ped))
		{
			return;
		}
		if (!_acqOf.TryGetValue(handle, out var a))
		{
			a = People.FirstOrDefault((Acquaintance x) => x.Name == n.Name && x.Model == (uint)ped.Model.Hash);
			if (a == null)
			{
				a = new Acquaintance
				{
					LifeRules = 2,
					Id = Guid.NewGuid().ToString("N").Substring(0, 10),
					Name = n.Name,
					Title = n.Title,
					Model = (uint)ped.Model.Hash,
					Town = (_currentTown ?? TownAt(ped.Position) ?? GetZoneName(ped.Position)),
					FirstSeen = DateTime.Now,
					LastSeen = DateTime.Now,
					LastGameMin = GameMinutesNow(),
					Met = (met || n.Met),
					Affil = n.Affil
				};
				People.Add(a);
				WaryFromWord(a, TemperOf(ped), ped);
				try
				{
					int num = ((a.Town != null) ? TownReputation(a.Town) : 0);
					if (num >= 20 && !IsLawman(ped))
					{
						a.Like = Math.Max(a.Like, Math.Min(12, num / 6));
						GrudgeLog($"PEOPLE {a.Name} has heard good things about you in {a.Town} (reputation {num:+0}) - likes you {a.Like}");
					}
				}
				catch
				{
				}
				if (a.Fear > 0 && IsLawman(ped))
				{
					a.Fear = Math.Min(5, a.Fear / 4);
				}
				GrudgeLog("PEOPLE remembered " + a.Name + " (" + a.Town + ")" + ((a.Fear > 0) ? $" - wary already, fear {a.Fear} (the town talks)" : ""));
				while (People.Count > Config.PeopleCap)
				{
					Acquaintance acquaintance = (from x in People
						where x != a && !_acqOf.ContainsValue(x)
						orderby x.Met, x.Grudge > 0, x.Fear > 0 || x.Like > 0 || x.Favours > 0 || (x.Witnessed != null && x.Witnessed.Count > 0), x.Fear + x.Like, x.LastSeen
						select x).FirstOrDefault();
					if (acquaintance == null)
					{
						break;
					}
					People.Remove(acquaintance);
				}
			}
			_acqOf[handle] = a;
		}
		a.Met |= met || n.Met;
		if (met)
		{
			a.LastDealt = DateTime.Now;
		}
		if (string.IsNullOrEmpty(a.Temper))
		{
			try
			{
				a.Temper = TemperOf(ped);
			}
			catch
			{
			}
		}
		a.LastSeen = DateTime.Now;
		a.LastGameMin = GameMinutesNow();
		_peopleDirty = true;
	}

	internal void ForgetPerson(int handle, string name)
	{
		Acquaintance value;
		Acquaintance acquaintance = (_acqOf.TryGetValue(handle, out value) ? value : ((name != null) ? People.FirstOrDefault((Acquaintance x) => x.Name == name) : null));
		NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord r) => r.Name == name);
		Named value2;
		string text = acquaintance?.Affil ?? (_named.TryGetValue(handle, out value2) ? value2.Affil : null);
		if (nemesisRecord != null && text != null && nemesisRecord.Affiliation == null && nemesisRecord.Gang == null)
		{
			nemesisRecord.Affiliation = text;
			History(nemesisRecord, "secretly runs with the " + (Gangs.ById(text)?.Name ?? text), Theme.Dim);
		}
		if (acquaintance != null && nemesisRecord != null)
		{
			nemesisRecord.FormerAcqId = acquaintance.Id;
			if (acquaintance.Circle != null && acquaintance.Circle.Count > 0)
			{
				nemesisRecord.CircleAcq = acquaintance.Circle.ToList();
				foreach (string item2 in acquaintance.Circle)
				{
					NemesisRecord item = ResolveCircle(item2).r;
					if (item != null && item.IsAlive)
					{
						MakeFriends(nemesisRecord, item, "they rode together before");
					}
				}
			}
		}
		if (acquaintance != null && nemesisRecord != null && (acquaintance.Like > 0 || acquaintance.Favours > 0))
		{
			int feltHate = nemesisRecord.FeltHate;
			int num = Math.Min(10, acquaintance.Like / 8 + acquaintance.Favours * 3);
			if (num > 0)
			{
				Gang gang = Gangs.ById(nemesisRecord.Gang ?? "");
				if (gang == null || !gang.OrdersNotFeelings)
				{
					nemesisRecord.Hate = Math.Max(5, feltHate - num);
				}
			}
			History(nemesisRecord, Pron.Her(string.Format("he used to like you (like {0}{1}) - hate {2} to {3}", acquaintance.Like, (acquaintance.Favours > 0) ? $", {acquaintance.Favours} favours" : "", feltHate, nemesisRecord.Hate), Pron.FemaleAt(handle)), Theme.Dim);
		}
		if (acquaintance != null && nemesisRecord != null && acquaintance.Fear > nemesisRecord.Fear)
		{
			GrudgeLog(Pron.Her(string.Format("PEOPLE {0} {1} - keeps his fear of you {2} -> {3}", name, nemesisRecord.IsAlive ? "becomes a nemesis" : "goes on the roster as a dead man", nemesisRecord.Fear, acquaintance.Fear), Pron.FemaleAt(handle)));
			nemesisRecord.Fear = acquaintance.Fear;
		}
		if (_acqOf.TryGetValue(handle, out var value3))
		{
			People.Remove(value3);
			_acqOf.Remove(handle);
			_peopleDirty = true;
			return;
		}
		uint fm = (ENTITY.DOES_ENTITY_EXIST(handle) ? ENTITY.GET_ENTITY_MODEL(handle) : 0u);
		if (name != null && People.RemoveAll((Acquaintance x) => x.Name == name && (fm == 0 || x.Model == fm)) > 0)
		{
			_peopleDirty = true;
		}
	}

	private void UpdatePeople(Ped player)
	{
		if (Game.GameTime >= _nextPeopleSync)
		{
			_nextPeopleSync = Game.GameTime + 2000;
			try
			{
				FadeWitnessFear();
			}
			catch
			{
			}
			try
			{
				RetireDeadNames();
			}
			catch
			{
			}
			foreach (KeyValuePair<int, Acquaintance> item in _acqOf.ToList())
			{
				Acquaintance value = item.Value;
				if (!ENTITY.DOES_ENTITY_EXIST(item.Key))
				{
					_acqOf.Remove(item.Key);
					continue;
				}
				if (_provoked.TryGetValue(item.Key, out var value2) && value2.Record == null && (value.Grudge != (int)value2.Score || string.IsNullOrEmpty(value.Temper) || value.GrudgeSettled != value2.Settled || value.SawFace != value2.SawFace))
				{
					value.Grudge = (int)value2.Score;
					if (string.IsNullOrEmpty(value.Temper))
					{
						value.Temper = value2.Temper.ToString();
					}
					value.GrudgeSettled = value2.Settled;
					value.SawFace = value2.SawFace;
					_peopleDirty = true;
				}
				Ped ped = new Ped(item.Key);
				if (!ped.IsDead || LooksKnockedOut(ped))
				{
					continue;
				}
				value.Dead = true;
				_acqOf.Remove(item.Key);
				_peopleDirty = true;
				GrudgeLog("PEOPLE " + value.Name + " died");
				int num = PED.GET_PED_SOURCE_OF_DEATH(ped.Handle);
				if (num == player.Handle || IsCompanionKiller(num) || (num == 0 && ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(ped.Handle, player.Handle, p2: true, p3: true)))
				{
					try
					{
						QueuePeopleNews(value.Name, value.Town, value.Circle, ped.Position);
					}
					catch (Exception ex)
					{
						GrudgeLog("NEWS failed: " + ex.Message);
					}
				}
			}
			for (int num2 = _regulars.Count - 1; num2 >= 0; num2--)
			{
				Ped ped2 = _regulars[num2];
				if (ped2 == null || !ped2.Exists())
				{
					_regulars.RemoveAt(num2);
				}
				else if (ped2.Position.DistanceTo(player.Position) > 220f && SafeToDelete(ped2) && !_worldNemeses.ContainsKey(ped2.Handle))
				{
					_acqOf.Remove(ped2.Handle);
					_named.Remove(ped2.Handle);
					ped2.Delete();
					_regulars.RemoveAt(num2);
				}
			}
		}
		SavePeople();
	}
}
