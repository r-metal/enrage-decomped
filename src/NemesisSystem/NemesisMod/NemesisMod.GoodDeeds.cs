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
	private sealed class ReDeed
	{
		public string Kind;

		public string Note;

		public int MetAt;

		public int TalkMs;

		public int Kills;

		public bool Done;

		public bool Owned;

		public Vector3 MetPos;

		public float Weight = 1f;
	}

	private readonly Dictionary<string, int> _deedsToday = new Dictionary<string, int>();

	private readonly Dictionary<string, long> _deedLineAt = new Dictionary<string, long>();

	private const long DeedLineMin = 120L;

	private static readonly string[] ReHelpTokens = new string[12]
	{
		"strandedrider", "injuredrider", "snakebite", "lostman", "lostfriend", "lostdrunk", "trappedwoman", "crashedwagon", "beartrap", "poisoned",
		"frozentodeath", "washedashore"
	};

	private static readonly string[] ReRescueTokens = new string[7] { "kidnappedvictim", "hostagerescue", "footrobbery", "wagonthreat", "fleeingfamily", "animalattack", "animalmauling" };

	private static readonly string[] ReCharityTokens = new string[2] { "disabledbeggar", "fundraiser" };

	private const float DeedMovedM = 8f;

	private const float DeedLeftM = 60f;

	private const int DeedMinMs = 10000;

	private readonly Dictionary<int, ReDeed> _reDeeds = new Dictionary<int, ReDeed>();

	private readonly HashSet<int> _deedKillsSeen = new HashSet<int>();

	private int _deedCash = -1;

	private const long MercyTakeBackMin = 1440L;

	private readonly Dictionary<string, MercyNote> _mercyHandleNotes = new Dictionary<string, MercyNote>();

	private readonly Dictionary<string, int> _mercyDay = new Dictionary<string, int>();

	private readonly Dictionary<string, int> _goodwillToday = new Dictionary<string, int>();

	private void NoDeed(int h, string why)
	{
		if (_reDeeds.TryGetValue(h, out var value) && !value.Done)
		{
			value.Done = true;
			GrudgeLog("DEED roadside " + WhoIs(h) + ": " + why + " - no deed");
		}
	}

	private bool PlayedOut(ReDeed rd, Ped p)
	{
		if (p.Position.DistanceTo(rd.MetPos) > 8f)
		{
			return true;
		}
		if (!rd.Owned)
		{
			return false;
		}
		try
		{
			return ScriptOf(p.Handle) == null;
		}
		catch
		{
			return false;
		}
	}

	private string DeedTown(Vector3 at)
	{
		string result = null;
		float num = float.MaxValue;
		foreach (string repTown in RepTowns)
		{
			if (TownCore(repTown, out var centre, out var radius))
			{
				float num2 = new Vector3(at.X - centre.X, at.Y - centre.Y, 0f).Length() - radius;
				if (num2 < num)
				{
					num = num2;
					result = repTown;
				}
			}
		}
		if (!(num <= 1500f))
		{
			return null;
		}
		return result;
	}

	internal (string town, float add) GoodDeed(Vector3 at, string what, float weight = 1f, string kind = null)
	{
		try
		{
			string text = DeedTown(at);
			if (text == null)
			{
				GrudgeLog("DEED " + what + ": no town near enough to hear of it");
				return (town: null, add: 0f);
			}
			int num = GameDayKey();
			string key = text + "|" + num;
			string text2 = ((kind == null) ? null : (text + "|" + kind + "|" + num));
			_deedsToday.TryGetValue(key, out var value);
			int value2 = 0;
			if (text2 != null)
			{
				_deedsToday.TryGetValue(text2, out value2);
			}
			if (value >= 6 || (text2 != null && value2 >= 2))
			{
				GrudgeLog("DEED " + text + ": " + what + " - the town has heard enough for today");
				return (town: null, add: 0f);
			}
			if (_deedsToday.Count > 200)
			{
				_deedsToday.Clear();
			}
			_deedsToday[key] = value + 1;
			if (text2 != null)
			{
				_deedsToday[text2] = value2 + 1;
			}
			Dictionary<string, TownTalk> dictionary = Chronicle.TownWord ?? (Chronicle.TownWord = new Dictionary<string, TownTalk>());
			if (!dictionary.TryGetValue(text, out var value3))
			{
				value3 = (dictionary[text] = new TownTalk());
			}
			Vector3 centre;
			float radius;
			float num2 = (TownCore(text, out centre, out radius) ? Math.Max(1f, radius / 130f * (radius / 130f)) : 1f);
			float num3 = 5f * weight / (float)Math.Sqrt(num2);
			int num4 = TownReputation(text);
			value3.Good = Math.Min(30f, value3.Good + num3);
			_townRepCache.Remove(text);
			SaveWorld();
			int num5 = TownReputation(text);
			ShowNotification("Word gets around " + text + " - " + what + ".");
			GrudgeLog($"DEED {text}: {what} (+{num3:0.0} good word, now {value3.Good:0.0}; reputation {num4:+0;-0;0} -> {num5:+0;-0;0})");
			Feed($"GOOD WORD {text}: {what} - reputation {num4:+0;-0;0} -> {num5:+0;-0;0}", Theme.Good);
			string key2 = text + "|" + what;
			long num6 = GameMinutesNow();
			long value4;
			bool num7 = _deedLineAt.TryGetValue(key2, out value4) && num6 >= value4 && num6 - value4 < 120;
			_deedLineAt[key2] = num6;
			if (num7)
			{
				GrudgeLog("DEED " + text + ": " + what + " - the same fight, one World tab line");
			}
			else
			{
				try
				{
					ChronicleDeed("good", char.ToUpperInvariant(what[0]) + what.Substring(1), "word got around " + text, at, null);
				}
				catch
				{
				}
			}
			return (town: text, add: num3);
		}
		catch (Exception ex)
		{
			GrudgeLog("DEED failed: " + ex.Message);
			return (town: null, add: 0f);
		}
	}

	private Dictionary<string, MercyNote> MercyNotes => Chronicle.Mercy ?? (Chronicle.Mercy = new Dictionary<string, MercyNote>());

	private static bool HandleKey(string key, out int handle)
	{
		handle = 0;
		if (key != null && key.Length > 1 && key[0] == 'h')
		{
			return int.TryParse(key.Substring(1), out handle);
		}
		return false;
	}

	internal void NoteMercyDeed(string key, Gang g, int notorietyTaken, (string town, float add) d)
	{
		if (key == null || (notorietyTaken <= 0 && (d.town == null || d.add <= 0f)))
		{
			return;
		}
		long now = GameMinutesNow();
		foreach (string item in (from kv in MercyNotes
			where now - kv.Value.At > 1440 || now < kv.Value.At
			select kv.Key).ToList())
		{
			MercyNotes.Remove(item);
		}
		MercyNote obj = new MercyNote
		{
			Gang = g?.Id,
			Notoriety = Math.Max(0, notorietyTaken)
		};
		object town;
		if (d.add > 0f)
		{
			(town, _) = d;
		}
		else
		{
			town = null;
		}
		obj.Town = (string)town;
		obj.Good = Math.Max(0f, d.add);
		obj.At = now;
		MercyNote mercyNote = obj;
		if (HandleKey(key, out var handle))
		{
			try
			{
				mercyNote.Model = ENTITY.GET_ENTITY_MODEL(handle);
			}
			catch
			{
			}
			_mercyHandleNotes[key] = mercyNote;
		}
		else
		{
			MercyNotes[key] = mercyNote;
			SaveWorld();
		}
	}

	internal void MercyTakenBack(string key)
	{
		if (key == null)
		{
			return;
		}
		MercyNote value;
		if (HandleKey(key, out var handle))
		{
			if (!_mercyHandleNotes.TryGetValue(key, out value))
			{
				return;
			}
			_mercyHandleNotes.Remove(key);
			uint num = 0u;
			try
			{
				num = ENTITY.GET_ENTITY_MODEL(handle);
			}
			catch
			{
			}
			if (value.Model != 0 && num != 0 && num != value.Model)
			{
				return;
			}
		}
		else
		{
			if (!MercyNotes.TryGetValue(key, out value))
			{
				return;
			}
			MercyNotes.Remove(key);
			SaveWorld();
		}
		long num2 = GameMinutesNow();
		if (num2 - value.At > 1440 || num2 < value.At)
		{
			return;
		}
		if (value.Notoriety > 0)
		{
			Gang gang = Gangs.ById(value.Gang);
			if (gang != null)
			{
				int num3 = NotorietyOf(gang);
				int num4 = num3 + value.Notoriety;
				Config.Notoriety[gang.Id] = num4;
				Config.Save();
				GrudgeLog($"NOTORIETY {gang.Id} {num3} -> {num4} (you killed a man you'd let go - the mercy is taken back)");
				Feed($"NOTORIETY {gang.Name} {num3} -> {num4} (killed a man you'd spared)", Theme.Warn);
			}
		}
		if (value.Town != null && value.Good > 0f && Chronicle.TownWord != null && Chronicle.TownWord.TryGetValue(value.Town, out var value2))
		{
			value2.Good = Math.Max(0f, value2.Good - value.Good);
			_townRepCache.Remove(value.Town);
			GrudgeLog($"DEED {value.Town}: you killed a man you'd spared - the good word is taken back (-{value.Good:0.0}, now {value2.Good:0.0})");
		}
	}

	private void TrackRoadsideDeeds(Ped player, int target)
	{
		Vector3 position = player.Position;
		int num = -1;
		try
		{
			num = MONEY._MONEY_GET_CASH_BALANCE();
		}
		catch
		{
		}
		bool flag = _deedCash >= 0 && num >= 0 && num < _deedCash && _deedCash - num <= 5000;
		_deedCash = num;
		if (_reDeeds.Count == 0)
		{
			return;
		}
		List<Ped> list = null;
		foreach (KeyValuePair<int, ReDeed> item in _reDeeds.ToList())
		{
			int key = item.Key;
			ReDeed value = item.Value;
			if (value.Done)
			{
				continue;
			}
			if (!ENTITY.DOES_ENTITY_EXIST(key) || ENTITY.IS_ENTITY_DEAD(key))
			{
				value.Done = true;
				continue;
			}
			Ped ped = new Ped(key);
			float num2 = ped.Position.DistanceTo(position);
			if (PED.IS_PED_IN_COMBAT(key, player.Handle))
			{
				value.Done = true;
				GrudgeLog("DEED roadside " + WhoIs(key) + ": turned on you - no deed");
				continue;
			}
			if (ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(key, player.Handle, p2: true, p3: true))
			{
				NoDeed(key, "you hurt them");
				continue;
			}
			if (target == key && num2 < 8f)
			{
				value.TalkMs += 1000;
			}
			if (value.Kind == "rescue" && num2 < 60f)
			{
				list = list ?? World.GetAllPeds().ToList();
				foreach (Ped item2 in list)
				{
					if (!(item2 == null) && item2.Exists() && item2.IsDead && item2.Handle != key && !_deedKillsSeen.Contains(item2.Handle) && !(item2.Position.DistanceTo(ped.Position) > 35f) && PED.GET_PED_SOURCE_OF_DEATH(item2.Handle) == player.Handle)
					{
						_deedKillsSeen.Add(item2.Handle);
						value.Kills++;
					}
				}
			}
			string text = (Woman(ped) ? "woman" : "man");
			string text2 = null;
			bool flag2 = value.TalkMs >= 2000 && num2 < 60f && Game.GameTime - value.MetAt > 10000 && PlayedOut(value, ped);
			if (value.Kind == "help" && flag2)
			{
				text2 = "you helped a " + text + " in trouble (" + value.Note + ")";
			}
			else if (value.Kind == "rescue" && (value.Kills > 0 || flag2) && Game.GameTime - value.MetAt > 4000)
			{
				text2 = (value.Note.StartsWith("kidnapped") ? ("you saved a kidnapped " + text) : (value.Note.StartsWith("held hostage") ? "you freed a hostage" : ("you came to the aid of a " + text + " (" + value.Note + ")")));
			}
			else if (value.Kind == "escort" && value.TalkMs >= 2000 && Game.GameTime - value.MetAt > 90000 && num2 < 40f)
			{
				text2 = "you saw a " + text + " safely on " + ((text == "woman") ? "her" : "his") + " way";
			}
			else if (value.Kind == "charity" && flag && num2 < 4f)
			{
				text2 = "you gave a " + text + " some money";
			}
			if (text2 == null)
			{
				if ((value.Kind == "help" || value.Kind == "rescue") && num2 > 60f)
				{
					NoDeed(key, "you left before it played out");
				}
				else if (Game.GameTime - value.MetAt > 600000 || num2 > 300f)
				{
					value.Done = true;
				}
			}
			else
			{
				value.Done = true;
				GoodDeed(ped.Position, text2, value.Weight, (value.Kind == "charity") ? "charity" : null);
			}
		}
		if (_reDeeds.Count > 60)
		{
			foreach (int item3 in (from x in _reDeeds
				where x.Value.Done
				select x.Key).ToList())
			{
				_reDeeds.Remove(item3);
			}
		}
		if (_deedKillsSeen.Count > 400)
		{
			_deedKillsSeen.Clear();
		}
	}

	private void WatchRoadsideDeed(int h, string modelLower, string note)
	{
		if (_reDeeds.ContainsKey(h))
		{
			return;
		}
		string text = (ReHelpTokens.Any(modelLower.Contains) ? "help" : (ReRescueTokens.Any(modelLower.Contains) ? "rescue" : (modelLower.Contains("escort") ? "escort" : (ReCharityTokens.Any(modelLower.Contains) ? "charity" : null))));
		if (text != null)
		{
			bool owned = false;
			try
			{
				owned = ScriptOf(h) != null;
			}
			catch
			{
			}
			_reDeeds[h] = new ReDeed
			{
				Kind = text,
				Note = (note ?? "in trouble"),
				MetAt = Game.GameTime,
				Weight = ((text == "charity") ? 0.6f : 1f),
				MetPos = new Ped(h).Position,
				Owned = owned
			};
			GrudgeLog("DEED roadside: watching " + WhoIs(h) + " (" + text + ": " + note + ")");
		}
	}

	private void NoteTownDefence(Gang g, Vector3 where)
	{
		if (g != null && !g.OrdersNotFeelings && !g.StoryOnly && InsideAnyTown(where))
		{
			int num;
			try
			{
				num = BountyDollars;
			}
			catch
			{
				num = 1;
			}
			if (num <= 0 && PLAYER.GET_PLAYER_WANTED_LEVEL(Game.Player.Handle) <= 0 && _combatants.Values.Any((int t) => Game.GameTime - t < 15000))
			{
				GoodDeed(where, "you saw off the " + ShortOutfit(g) + " in town", 0.6f, "defence");
			}
		}
	}

	internal int LowerNotoriety(Gang g, int amount, string why, string kind = null, int cap = 0, bool byWire = false)
	{
		if (g == null || amount <= 0 || g.StoryOnly)
		{
			return 0;
		}
		if (!byWire)
		{
			try
			{
				if (PlayerMasked(Game.Player.Character))
				{
					GrudgeLog("NOTORIETY " + g.Id + ": " + why + " - masked, they don't know it was you");
					return 0;
				}
			}
			catch
			{
			}
		}
		int num = NotorietyOf(g);
		if (num <= 0)
		{
			return 0;
		}
		if (kind != null && cap > 0)
		{
			string key = $"{g.Id}|{kind}|{GameDayKey()}";
			_goodwillToday.TryGetValue(key, out var value);
			amount = Math.Min(amount, cap - value);
			if (amount <= 0)
			{
				GrudgeLog("NOTORIETY " + g.Id + ": " + why + " - no more " + kind + " counts today");
				return 0;
			}
			_goodwillToday[key] = value + amount;
		}
		int num2 = Math.Max(0, num - amount);
		Config.Notoriety[g.Id] = num2;
		Config.Save();
		GrudgeLog($"NOTORIETY {g.Id} {num} -> {num2} ({why})");
		Feed($"NOTORIETY {g.Name} {num} -> {num2} ({why})", Theme.Good);
		return num - num2;
	}

	internal void NoteMercy(Gang g, string who, string id = null)
	{
		if (g != null && !g.StoryOnly)
		{
			string key = id ?? who ?? "?";
			int num = GameDayKey();
			if (!_mercyDay.TryGetValue(key, out var value) || value != num)
			{
				_mercyDay[key] = num;
				int notorietyTaken = LowerNotoriety(g, 2, "you let " + (who ?? "one of their men") + " go", "mercy", 6);
				NoteMercyDeed(key, g, notorietyTaken, GoodDeed(Game.Player.Character.Position, "you showed mercy to one of the " + ShortOutfit(g), 0.6f, "mercy"));
			}
		}
	}
}
