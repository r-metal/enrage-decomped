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
	private readonly HashSet<string> _loggedUnique = new HashSet<string>();

	private readonly Dictionary<int, Named> _named = new Dictionary<int, Named>();

	private int _nextNameScan;

	private int _namesDropped;

	private int _namesGiven;

	private int _lastAimNamed;

	private int _nextNemesisNames;

	internal string NamingScanNote = "";

	private static List<Ped> _aroundCache = new List<Ped>();

	private static int _aroundAt = -100000;

	private static List<(Ped p, Vector3 pos)> _snap = new List<(Ped, Vector3)>();

	private static int _snapAt = -100000;

	private static List<Ped> _nearbyCache = new List<Ped>();

	private static int _nearbyAt = -100000;

	private void UpdateNaming(Ped player)
	{
		if (!Config.NamingEnabled)
		{
			return;
		}
		UpdateUnease(player);
		Vector3 pos;
		if (Game.GameTime >= _nextNameScan)
		{
			_nextNameScan = Game.GameTime + 1000;
			pos = player.Position;
			List<Ped> list = PedsAround(player);
			int num = 0;
			int num2 = 0;
			foreach (Ped item in list)
			{
				if (item == null || !item.Exists() || item.IsPlayer || !item.IsHuman || item.IsDead || item.Position.DistanceTo(pos) > Config.NamingRadius)
				{
					continue;
				}
				num++;
				if (_named.TryGetValue(item.Handle, out var value))
				{
					value.SeenAt = Game.GameTime;
				}
				else
				{
					if (_worldNemeses.ContainsKey(item.Handle) || (_activeNemesisPed != null && item.Handle == _activeNemesisPed.Handle))
					{
						continue;
					}
					string text = Models.Raw((uint)item.Model.Hash).ToLowerInvariant();
					if (Kids.IsChild(item) && (!text.StartsWith("cs_") || !StoryCast.TryGet(text, out var _)))
					{
						if (!IsStoryPed(item))
						{
							KidFor(item);
						}
						continue;
					}
					if (text.StartsWith("u_") && !GameNamed(text) && _loggedUnique.Add(text))
					{
						GrudgeLog("NAMING unique " + text + ": no name known - named like anyone else");
					}
					if (GameNamed(text))
					{
						string text2 = null;
						string storyTemper = null;
						string storyNote = null;
						if (StoryCast.TryGet(text, out var p2))
						{
							text2 = p2.Name;
							storyTemper = p2.Temper;
							storyNote = p2.Note;
						}
						else if (LoreModels.Contains(text))
						{
							text2 = LoreNameAt(text, item.Position);
							storyNote = "wanted";
						}
						else
						{
							text2 = TownCast.NameFor(text);
							if (text.Contains("_bht_") || text.Contains("bounty"))
							{
								storyNote = "wanted - a bounty";
							}
						}
						if (_loggedUnique.Add(text))
						{
							GrudgeLog("NAMING unique " + text + ": " + (text2 ?? "no name known"));
						}
						if (text2 != null)
						{
							_named[item.Handle] = new Named
							{
								Name = text2,
								Title = "",
								SeenAt = Game.GameTime,
								NamedAt = Game.GameTime,
								Met = true,
								Importance = 3,
								Story = true,
								StoryTemper = storyTemper,
								StoryNote = storyNote
							};
							_namesGiven++;
						}
					}
					else if (num2 < 12 && (!Config.LinkLooks || LookReadable(item)))
					{
						num2++;
						_named[item.Handle] = NameByLook(item);
						_namesGiven++;
					}
				}
			}
			LookSync(player);
			UpdateRoadside(player);
			UpdateWitnessRecognition(player);
			UpdateWitnessReports(player);
			NamingScanNote = $"scan: {list.Count} peds around, {_nearbyCache.Count} from the near list, {num} within {Config.NamingRadius:0} m";
			ForgetKids();
			foreach (KeyValuePair<int, Named> item2 in _named.ToList())
			{
				Named value2 = item2.Value;
				bool flag = !ENTITY.DOES_ENTITY_EXIST(item2.Key) || ENTITY.IS_ENTITY_DEAD(item2.Key);
				if (!flag && ENTITY.DOES_ENTITY_EXIST(item2.Key) && new Ped(item2.Key).Position.DistanceTo(pos) <= 45f)
				{
					value2.SeenAt = Game.GameTime;
				}
				int num3 = (value2.Met ? 1800000 : ((value2.Importance >= 3) ? 600000 : ((value2.Importance >= 1) ? 180000 : 15000)));
				if (!flag && value2.Met && ENTITY.IS_ENTITY_A_PED(item2.Key) && new Ped(item2.Key).Position.DistanceTo(pos) < 60f && !value2.Story)
				{
					SetPromptName(item2.Key, value2.Name);
				}
				if (!ENTITY.DOES_ENTITY_EXIST(item2.Key) || (flag && value2.Importance == 0) || Game.GameTime - value2.SeenAt > num3)
				{
					_named.Remove(item2.Key);
					_namesDropped++;
				}
			}
			if (_named.Count > Config.NamingCap)
			{
				foreach (int item3 in (from t in (from kv in _named
						select (Key: kv.Key, Importance: kv.Value.Importance, Met: kv.Value.Met, d: Dist(kv.Key)) into t
						where !t.Met && t.d > 15f
						orderby t.Importance, t.d descending
						select t).Take(_named.Count - Config.NamingCap)
					select t.Key).ToList())
				{
					_named.Remove(item3);
					_namesDropped++;
				}
			}
		}
		if (Game.GameTime >= _nextNemesisNames)
		{
			_nextNemesisNames = Game.GameTime + 1000;
			Vector3 position = player.Position;
			foreach (KeyValuePair<int, NemesisRecord> worldNemese in _worldNemeses)
			{
				if (ENTITY.DOES_ENTITY_EXIST(worldNemese.Key) && ENTITY.IS_ENTITY_A_PED(worldNemese.Key) && !ENTITY.IS_ENTITY_DEAD(worldNemese.Key) && new Ped(worldNemese.Key).Position.DistanceTo(position) < 60f)
				{
					SetPromptName(worldNemese.Key, worldNemese.Value.Name);
				}
			}
			if (_activeNemesisPed != null && _activeNemesisPed.Exists() && ActiveRecord != null)
			{
				SetPromptName(_activeNemesisPed.Handle, ActiveRecord.Name);
			}
		}
		DrawNameplate(player);
		float Dist(int h)
		{
			if (!ENTITY.DOES_ENTITY_EXIST(h))
			{
				return 9999f;
			}
			return new Ped(h).Position.DistanceTo(pos);
		}
	}

	internal static List<Ped> PedsAround(Ped player)
	{
		if (Game.GameTime - _aroundAt < 250)
		{
			return _aroundCache;
		}
		_aroundAt = Game.GameTime;
		HashSet<int> hashSet = new HashSet<int>();
		List<Ped> list = new List<Ped>();
		foreach (Ped item in World.GetAllPeds().Concat(NearbyPeds(player)))
		{
			if (item != null && hashSet.Add(item.Handle))
			{
				list.Add(item);
			}
		}
		_aroundCache = list;
		_snapAt = -100000;
		return list;
	}

	internal static List<(Ped p, Vector3 pos)> PedSnapshot(Ped player)
	{
		List<Ped> list = PedsAround(player);
		if (_snapAt >= 0 && Game.GameTime - _snapAt < 250)
		{
			return _snap;
		}
		_snapAt = Game.GameTime;
		List<(Ped, Vector3)> list2 = new List<(Ped, Vector3)>(list.Count);
		foreach (Ped item in list)
		{
			if (item != null && ENTITY.DOES_ENTITY_EXIST(item.Handle))
			{
				list2.Add((item, ENTITY.GET_ENTITY_COORDS(item.Handle, alive: true, realCoords: true)));
			}
		}
		_snap = list2;
		return list2;
	}

	private unsafe static List<Ped> NearbyPeds(Ped player)
	{
		if (Game.GameTime - _nearbyAt < 500)
		{
			return _nearbyCache;
		}
		_nearbyAt = Game.GameTime;
		List<Ped> list = new List<Ped>();
		ulong* ptr = stackalloc ulong[129];
		for (int i = 0; i < 129; i++)
		{
			ptr[i] = 0uL;
		}
		*ptr = 48uL;
		Crumbs.Native("Naming nearby peds");
		int num = PED.GET_PED_NEARBY_PEDS(player.Handle, ptr, -1, 0);
		for (int j = 0; j < num && j < 48; j++)
		{
			int num2 = (int)(ptr[j + 1] & 0xFFFFFFFFu);
			if (num2 != 0 && num2 != player.Handle && ENTITY.DOES_ENTITY_EXIST(num2) && ENTITY.IS_ENTITY_A_PED(num2))
			{
				list.Add(new Ped(num2));
			}
		}
		_nearbyCache = list;
		return list;
	}

	private static void SetPromptName(int ped, string name)
	{
		if (!UniqueModel(ped))
		{
			Function.Call(5352729231319872684uL, ped, new InputArgument(UiDraw.ForeverString(name)));
		}
	}

	private bool Introduce(int handle, bool hostile = false)
	{
		if (!Config.NamingEnabled)
		{
			return false;
		}
		if (!_named.TryGetValue(handle, out var value))
		{
			Ped ped = Entity.FromHandle(handle) as Ped;
			if (ped == null || !ped.Exists() || !ped.IsHuman || ped.IsPlayer || Kids.IsChild(ped) || RecordAt(handle) != null)
			{
				return false;
			}
			if (IsStoryPed(ped))
			{
				return false;
			}
			value = (_named[handle] = NameByLook(ped));
			_namesGiven++;
			string name = value.Name;
			GrudgeLog($"NAMING {name} named on the spot (ped {handle} had no name yet; {_named.Count} names held)");
		}
		value.Importance = Math.Max(value.Importance, 3);
		RememberPerson(handle, met: true);
		_bulletImpact.NameVictim(handle, value.Name);
		value.SeenAt = Game.GameTime;
		if (value.Story)
		{
			return true;
		}
		SetPromptName(handle, value.Name);
		if (!value.Met)
		{
			value.Met = true;
			string text = value.Name.Split(' ')[0];
			string[] array = ((!hostile) ? new string[5]
			{
				"Name's " + text + ".",
				value.Name + ". Pleasure.",
				"Folks call me " + text + ".",
				(value.Name.Length > text.Length + 1) ? (text + ". " + value.Name.Substring(text.Length + 1) + ".") : (text + ". Just " + text + "."),
				"..." + text + ". What's it to you?"
			} : new string[4]
			{
				value.Name + ". Remember it - I'll remember you.",
				"You don't know who you're talking to. " + value.Name + ".",
				"Name's " + text + ", and you just made a mistake.",
				text + ". Say that again, I dare you."
			});
			if (hostile)
			{
				ShowSubtitle(value.Name + ": \"" + array[_rng.Next(array.Length)] + "\"");
			}
			GrudgeLog($"NAMING met {value.Name} (ped {handle})");
		}
		return true;
	}

	private void MarkNamed(int handle, int importance)
	{
		if (_named.TryGetValue(handle, out var value))
		{
			value.Importance = Math.Max(value.Importance, importance);
			value.SeenAt = Game.GameTime;
		}
	}

	private unsafe void DrawNameplate(Ped player)
	{
		int num = 0;
		int handle = Game.Player.Handle;
		if (((PLAYER.GET_PLAYER_INTERACTION_TARGET_ENTITY(handle, &num, p2: false, p3: false) && num != 0) || (PLAYER.GET_ENTITY_PLAYER_IS_FREE_AIMING_AT(handle, &num) && num != 0)) && num != _lastAimNamed && _named.TryGetValue(num, out var value))
		{
			_lastAimNamed = num;
			value.Importance = Math.Max(value.Importance, 1);
		}
	}
}
