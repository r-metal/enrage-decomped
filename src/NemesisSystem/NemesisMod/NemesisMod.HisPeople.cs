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
	private sealed class HisPeopleHold
	{
		public int Him;

		public string What;

		public Raid Patrol;

		public bool Crew;

		public bool GunOn;

		public readonly List<Ped> Men = new List<Ped>();

		public readonly HashSet<int> Turned = new HashSet<int>();

		public readonly HashSet<int> Grudged = new HashSet<int>();

		public readonly HashSet<int> Blocked = new HashSet<int>();

		public readonly Dictionary<int, string> Stance = new Dictionary<int, string>();

		public readonly Dictionary<int, int> AimAt = new Dictionary<int, int>();

		public readonly Dictionary<int, int> BackedAt = new Dictionary<int, int>();

		public int NextAt;

		public int LookAt;
	}

	private const uint WeaponUnarmed = 2725352035u;

	private int _nextHistoryCheck;

	internal static List<LoreRow> LoreRows = new List<LoreRow>
	{
		new LoreRow
		{
			Year = 1889,
			Month = 1,
			Title = "The O'Driscoll - Van der Linde feud",
			Place = "",
			Gang = "odriscoll",
			When = "About 1889",
			Lines = { "Dutch van der Linde killed Colm O'Driscoll's brother; Colm killed Dutch's woman, Annabelle.", "Neither man has forgotten - and neither gang lets the other ride past in peace." }
		},
		new LoreRow
		{
			Year = 1893,
			Month = 5,
			Title = "The Panic of '93",
			Place = "across the country",
			Lines = { "Banks fail from New York to Saint Denis; farms and stores go under by the hundred." }
		},
		new LoreRow
		{
			YearsAgo = 5,
			Month = 3,
			Title = "Sisika Penitentiary fills up",
			Place = "Sisika Penitentiary",
			Lines = { "The island prison takes every road agent and rustler the law can catch." }
		},
		new LoreRow
		{
			YearsAgo = 4,
			Month = 6,
			Title = "The Lemoyne Raiders ride",
			Place = "Lemoyne",
			Gang = "lemoyneraiders",
			Lines = { "Old Confederates who never laid down their arms ride the bayous and the fields, burning out northern money." }
		},
		new LoreRow
		{
			YearsAgo = 4,
			Month = 2,
			Title = "Annesburg's coal",
			Place = "Annesburg",
			Lines = { "Men go down the mine poor and come up poorer." }
		},
		new LoreRow
		{
			YearsAgo = 3,
			Month = 9,
			Title = "The Braithwaites and the Grays",
			Place = "Rhodes",
			Lines = { "Two old Scarlett Meadows families and an older feud - over land, over the war, over moonshine." }
		},
		new LoreRow
		{
			YearsAgo = 3,
			Month = 4,
			Title = "Angelo Bronte's Saint Denis",
			Place = "Saint Denis",
			Lines = { "Bronte's men run the docks, the saloons and half the police." }
		},
		new LoreRow
		{
			YearsAgo = 2,
			Month = 7,
			Title = "Cornwall's railroad",
			Place = "the territories",
			Lines = { "Leviticus Cornwall's money lays track, drills oil and boils kerosene - and pays the men who do it in pennies." }
		},
		new LoreRow
		{
			YearsAgo = 2,
			Month = 10,
			Title = "Night riders in Scarlett Meadows",
			Place = "Scarlett Meadows",
			Gang = "klan",
			Lines = { "Hooded fools burn crosses in the dark - and scatter the moment anyone shoots back." }
		},
		new LoreRow
		{
			YearsAgo = 1,
			Month = 5,
			Title = "Fort Wallace",
			Place = "Fort Wallace",
			Gang = "army",
			Lines = { "Colonel Henry Favours holds Ambarino for the Army; his patrols ride the Cumberland Forest roads." }
		},
		new LoreRow
		{
			YearsAgo = 1,
			Month = 8,
			Title = "The Wapiti reservation",
			Place = "Ambarino",
			Gang = "army",
			Lines = { "The Army hems the Wapiti onto their reservation - and oil men are already eyeing the land." }
		},
		new LoreRow
		{
			YearsAgo = 1,
			Month = 9,
			Title = "Cattle money in Valentine",
			Place = "Valentine",
			Lines = { "The stockyards are full and Smithfield's Saloon never closes." }
		},
		new LoreRow
		{
			Year = 1899,
			Month = 2,
			Title = "The Blackwater ferry job",
			Place = "Blackwater",
			Lines = { "The Van der Linde gang's robbery on the Blackwater ferry goes wrong - a woman is killed, and the gang runs for the mountains.", "Pinkerton agents take the case; there's a price on every Van der Linde head." }
		}
	};

	private HisPeopleHold FindHisPeople(Ped him)
	{
		int h = him.Handle;
		HisPeopleHold hisPeopleHold = new HisPeopleHold
		{
			Him = h
		};
		if (_enc != null && _activeNemesisPed != null && (h == _activeNemesisPed.Handle || _followers.Any((Ped f) => f != null && f.Exists() && f.Handle == h)))
		{
			if (_enc.Engaged)
			{
				return null;
			}
			hisPeopleHold.Crew = true;
			hisPeopleHold.Men.AddRange(from p in new Ped[1] { _activeNemesisPed }.Concat(_followers)
				where p != null && p.Exists() && !p.IsDead && p.Handle != h
				select p);
			hisPeopleHold.What = (IsMourner(him) ? "the mourners with " : "the crew of ") + (ActiveRecord?.Name ?? "his leader");
		}
		else
		{
			Raid raid = _raids.FirstOrDefault((Raid x) => x.Men.Any((Ped m) => m.Exists() && m.Handle == h));
			if (raid != null)
			{
				if (raid.Hostile)
				{
					return null;
				}
				hisPeopleHold.Patrol = raid;
				hisPeopleHold.Men.AddRange(raid.Men.Where((Ped m) => m.Exists() && !m.IsDead && m.Handle != h));
				hisPeopleHold.What = raid.Gang.Name + " patrol";
			}
			else
			{
				Band band = BandOf(h);
				if (band != null)
				{
					if (band.Turned)
					{
						return null;
					}
					hisPeopleHold.Men.AddRange(band.Men.Where((Ped m) => m.Exists() && !m.IsDead && m.Handle != h && m.Position.DistanceTo(him.Position) < 40f));
					hisPeopleHold.What = band.What;
				}
				else if (!IsOurs(h))
				{
					hisPeopleHold.Men.AddRange(GroupOf(him));
					hisPeopleHold.What = "the people with him";
				}
			}
		}
		if (hisPeopleHold.Men.Count <= 0)
		{
			return null;
		}
		return hisPeopleHold;
	}

	private void HoldHisPeople(Ped him)
	{
		ReleaseHisPeople(wentForYou: false, quietly: true);
		_hisPeople = ((him != null && him.Exists()) ? FindHisPeople(him) : null);
		if (_hisPeople != null)
		{
			GrudgeLog(string.Format("INTERRO his people: {0}, {1} {2} - held while you question him", _hisPeople.What, _hisPeople.Men.Count, (_hisPeople.Men.Count == 1) ? "man" : "men"));
		}
	}

	private string HisPeopleStance(HisPeopleHold hp, Ped m, Ped player)
	{
		if (StoryBansWeapons(player.Position) != null)
		{
			return "stare";
		}
		NemesisRecord nemesisRecord = RecordAt(m.Handle);
		if (!hp.Crew && hp.Patrol == null && (nemesisRecord == null || nemesisRecord.Gang == null) && Gangs.ForModel((uint)m.Model.Hash) == null)
		{
			return "game";
		}
		Personality? personality = nemesisRecord?.Personality;
		if (!personality.HasValue && Enum.TryParse<Personality>(TemperOf(m), out var result))
		{
			personality = result;
		}
		if (personality != Personality.Coward)
		{
			return "aim";
		}
		return "back";
	}

	private void HoldHisPeopleTick(Ped player)
	{
		HisPeopleHold hp = _hisPeople;
		if (hp == null || Game.GameTime < hp.NextAt)
		{
			return;
		}
		hp.NextAt = Game.GameTime + 500;
		Ped ped = Entity.FromHandle(hp.Him) as Ped;
		bool flag = StoryBansWeapons(player.Position) != null;
		if (!hp.GunOn && ped != null && ped.Exists() && PLAYER.IS_PLAYER_FREE_AIMING_AT_ENTITY(Game.Player.Handle, ped.Handle) && WEAPON.IS_PED_ARMED(player.Handle, 6))
		{
			hp.GunOn = true;
			foreach (Ped item in hp.Men.Where((Ped m) => m != null && m.Exists() && !m.IsDead))
			{
				hp.Stance[item.Handle] = HisPeopleStance(hp, item, player);
			}
			GrudgeLog(string.Format("INTERRO his people ({0}): a gun on their man - {1} draw on you, {2} back off, {3} stare, {4} left to the game", hp.What, Count("aim"), Count("back"), Count("stare"), Count("game")));
		}
		if (hp.GunOn && !flag)
		{
			bool flag2 = PED.IS_PED_SHOOTING(player.Handle);
			if (flag2 || hp.Men.Any((Ped m) => m != null && m.Exists() && !m.IsDead && ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(m.Handle, player.Handle, p2: true, p3: true)))
			{
				GrudgeLog("INTERRO his people (" + hp.What + "): " + (flag2 ? "you fired" : "you hurt one of them") + " with them covering you - it's a fight");
				ReleaseHisPeople(wentForYou: true);
				return;
			}
		}
		bool flag3 = Game.GameTime >= hp.LookAt;
		if (flag3)
		{
			hp.LookAt = Game.GameTime + 4000;
		}
		foreach (Ped man in hp.Men)
		{
			if (man == null || !man.Exists() || man.IsDead || PED.IS_PED_IN_COMBAT(man.Handle, 0))
			{
				continue;
			}
			int handle = man.Handle;
			if (!hp.Stance.TryGetValue(handle, out var value))
			{
				object obj;
				if (!hp.GunOn)
				{
					obj = "stare";
				}
				else
				{
					string text = (hp.Stance[handle] = HisPeopleStance(hp, man, player));
					obj = text;
				}
				value = (string)obj;
			}
			if (hp.GunOn && hp.Grudged.Add(handle))
			{
				GunOnHisFriend(man);
			}
			if (value == "game")
			{
				continue;
			}
			man.BlockPermanentEvents = true;
			hp.Blocked.Add(handle);
			if (flag3)
			{
				TASK.TASK_LOOK_AT_ENTITY(handle, player.Handle, 5000, 0, 51, 0);
			}
			if (hp.GunOn && value == "aim")
			{
				CoverYou(hp, man, player);
				continue;
			}
			bool flag4 = false;
			if (hp.GunOn && value == "back")
			{
				if (!hp.BackedAt.TryGetValue(handle, out var value2))
				{
					hp.BackedAt[handle] = Game.GameTime;
					Flee(handle, player.Handle, 7f, 2500, 1.2f);
					continue;
				}
				if (Game.GameTime - value2 < 2600)
				{
					continue;
				}
				flag4 = true;
			}
			if ((!hp.Crew || flag4) && !hp.Turned.Contains(handle) && !PED.IS_PED_USING_ANY_SCENARIO(handle))
			{
				hp.Turned.Add(handle);
				TASK.TASK_TURN_PED_TO_FACE_ENTITY(handle, player.Handle, -1, 0f, 0f, 0f);
			}
		}
		int Count(string s)
		{
			return hp.Stance.Values.Count((string v) => v == s);
		}
	}

	private void CoverYou(HisPeopleHold hp, Ped m, Ped player)
	{
		int handle = m.Handle;
		if (PED.IS_PED_USING_ANY_SCENARIO(handle))
		{
			TASK.CLEAR_PED_TASKS(handle, p1: false, p2: false);
		}
		else
		{
			if (hp.AimAt.TryGetValue(handle, out var value) && Game.GameTime < value)
			{
				return;
			}
			if (!hp.AimAt.ContainsKey(handle))
			{
				uint num = 0u;
				try
				{
					num = Function.Call<uint>(9548732433391192802uL, new InputArgument[3] { handle, false, false });
				}
				catch
				{
				}
				if (num == 0 || !GunMelee.IsGun(num))
				{
					hp.AimAt[handle] = int.MaxValue;
					hp.Turned.Add(handle);
					TASK.TASK_TURN_PED_TO_FACE_ENTITY(handle, player.Handle, -1, 0f, 0f, 0f);
					return;
				}
				try
				{
					WEAPON.SET_CURRENT_PED_WEAPON(handle, num, equipNow: true, 0, p4: false, p5: false);
				}
				catch
				{
				}
			}
			hp.AimAt[handle] = Game.GameTime + 2500;
			TASK.TASK_AIM_GUN_AT_ENTITY(handle, player.Handle, 3000, p3: false, 0);
		}
	}

	private void GunOnHisFriend(Ped m)
	{
		NemesisRecord nemesisRecord = RecordAt(m.Handle);
		if (nemesisRecord != null)
		{
			ApplyToRecord(nemesisRecord, GrudgeAction.FriendThreatened);
			return;
		}
		Provocation provocation = RememberStranger(m.Handle);
		if (provocation != null)
		{
			Grudge(m.Handle, provocation, GrudgeAction.FriendThreatened);
		}
	}

	private void ReleaseHisPeople(bool wentForYou, bool quietly = false)
	{
		HisPeopleHold hisPeople = _hisPeople;
		_hisPeople = null;
		if (hisPeople == null)
		{
			return;
		}
		Ped character = Game.Player.Character;
		int num = 0;
		foreach (Ped man in hisPeople.Men)
		{
			if (man == null || !man.Exists() || man.IsDead)
			{
				continue;
			}
			num++;
			int handle = man.Handle;
			TASK.TASK_CLEAR_LOOK_AT(handle);
			if (PED.IS_PED_IN_COMBAT(handle, 0))
			{
				continue;
			}
			hisPeople.Stance.TryGetValue(handle, out var value);
			int value2;
			bool flag = hisPeople.AimAt.TryGetValue(handle, out value2) && value2 != int.MaxValue;
			if (wentForYou && hisPeople.GunOn && (value == "aim" || value == "back"))
			{
				man.BlockPermanentEvents = true;
				TASK.TASK_COMBAT_PED(handle, character.Handle, 0, 16);
				continue;
			}
			if (!wentForYou && flag)
			{
				TASK.TASK_AIM_GUN_AT_ENTITY(handle, character.Handle, 2000, p3: false, 0);
				_lowerGuns.Add((handle, Game.GameTime + 2000, hisPeople.Patrol));
				continue;
			}
			if (hisPeople.Turned.Contains(handle) || hisPeople.BackedAt.ContainsKey(handle))
			{
				TASK.CLEAR_PED_TASKS(handle, p1: false, p2: false);
			}
			if ((wentForYou || !IsMourner(man) || (!hisPeople.Turned.Contains(handle) && !hisPeople.BackedAt.ContainsKey(handle)) || !BackToGrave(man)) && (value != "game" || hisPeople.Blocked.Contains(handle)))
			{
				man.BlockPermanentEvents = !_patrolOpen.Contains(handle) && StaysBlocked(man);
			}
		}
		if (hisPeople.Patrol != null && _raids.Contains(hisPeople.Patrol))
		{
			if (wentForYou && !hisPeople.Patrol.Hostile)
			{
				hisPeople.Patrol.Hostile = true;
				hisPeople.Patrol.WatchYouUntil = 0;
				hisPeople.Patrol.Watch = false;
				SetPatrolRelations(character);
				foreach (Ped item in hisPeople.Patrol.Men.Where((Ped m) => m.Exists() && !m.IsDead))
				{
					if (_patrolOpen.Remove(item.Handle))
					{
						item.BlockPermanentEvents = true;
					}
					TASK.TASK_COMBAT_PED(item.Handle, character.Handle, 0, 16);
				}
			}
			else if (!wentForYou)
			{
				hisPeople.Patrol.NextOrder = Game.GameTime + (hisPeople.GunOn ? 2200 : 0);
			}
		}
		if (!quietly)
		{
			GrudgeLog(string.Format("INTERRO his people ({0}, {1}): {2}", hisPeople.What, num, wentForYou ? "they back him" : (hisPeople.GunOn ? "they keep you covered a moment, then go back to what they were doing" : "back to what they were doing")));
		}
	}

	private void HisPeopleAfterTick(Ped player)
	{
		if (_lowerGuns.Count == 0)
		{
			return;
		}
		for (int num = _lowerGuns.Count - 1; num >= 0; num--)
		{
			var (num2, num3, raid) = _lowerGuns[num];
			if (Game.GameTime >= num3)
			{
				_lowerGuns.RemoveAt(num);
				Ped ped = Entity.FromHandle(num2) as Ped;
				if (!(ped == null) && ped.Exists() && !ped.IsDead && !PED.IS_PED_IN_COMBAT(num2, 0))
				{
					try
					{
						WEAPON.SET_CURRENT_PED_WEAPON(num2, 2725352035u, equipNow: true, 0, p4: false, p5: false);
					}
					catch
					{
					}
					TASK.CLEAR_PED_TASKS(num2, p1: false, p2: false);
					if (!IsMourner(ped) || !BackToGrave(ped))
					{
						ped.BlockPermanentEvents = !_patrolOpen.Contains(num2) && StaysBlocked(ped);
						if (raid != null && _raids.Contains(raid) && !raid.Hostile)
						{
							raid.NextOrder = 0;
						}
					}
				}
			}
		}
	}

	private bool BackToHisPatrol(int h)
	{
		Raid raid = _raids.FirstOrDefault((Raid x) => x.Men.Any((Ped m) => m.Exists() && m.Handle == h));
		if (raid == null || raid.Hostile)
		{
			return false;
		}
		TASK.CLEAR_PED_TASKS(h, p1: false, p2: false);
		raid.NextOrder = 0;
		return true;
	}
}
