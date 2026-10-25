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
	private sealed class RivalScene
	{
		public Ped Ped;

		public NemesisRecord Rec;

		public NemesisRecord Target;

		public readonly List<Ped> Men = new List<Ped>();

		public int EndAt = -1;

		public Blip Blip;
	}

	private bool _rivalRolled;

	private static readonly string[] RivalWords = new string[6] { "{a} and {b} had words", "{w} called {l} a coward in front of everyone", "{a} and {b} nearly came to blows over cards", "{w} laughed {l} out of the room", "{a} and {b} squared up, then backed off", "{w} told the whole camp what {l} really is" };

	private static readonly string[] RivalFists = new string[6] { "{w} beat {l} in a fist fight", "{w} knocked {l} into the fire", "{w} and {l} fought until {l} couldn't stand", "{w} broke {l}'s nose over an old insult", "{w} put {l} in the dirt", "{w} gave {l} a beating he won't forget" };

	private static readonly string[] RivalDuels = new string[6] { "{a} and {b} settled it with a duel", "{a} and {b} drew on each other at sundown", "{w} shot it out with {l}", "{a} and {b} walked ten paces and turned", "{w} answered {l}'s challenge with a pistol", "{a} and {b} ended it with guns" };

	private static readonly (string token, string note)[] ReNotes = new(string, string)[51]
	{
		("strandedrider", "his horse threw a shoe on the road"),
		("injuredrider", "thrown from his horse, hurt"),
		("snakebite", "snakebitten by the road"),
		("lostman", "lost in the wilds"),
		("lostfriend", "looking for a lost friend"),
		("lostdrunk", "drunk and lost"),
		("kidnappedvictim", "kidnapped - you came across him"),
		("hostagerescue", "held hostage"),
		("trappedwoman", "trapped under a wagon"),
		("crashedwagon", "his wagon crashed"),
		("beartrap", "caught in a bear trap"),
		("animalattack", "attacked by an animal"),
		("animalmauling", "mauled by an animal"),
		("wagonthreat", "threatened on his wagon"),
		("footrobbery", "robbed on the road"),
		("townrobbery", "a robbery in town"),
		("coachrobbery", "a coach hold-up"),
		("prisonwagon", "the prison wagon"),
		("loneprisoner", "a prisoner left tied up"),
		("ontherun", "on the run"),
		("fleeingfamily", "a family on the run"),
		("escort", "needed an escort"),
		("disabledbeggar", "begging by the road"),
		("duelboaster", "wanted a duel"),
		("drunkdueler", "a drunk who wanted a duel"),
		("duelwinner", "won a duel"),
		("showoff", "a show-off with a gun"),
		("sharpshooter", "a sharpshooter"),
		("treasurehunter", "hunting treasure"),
		("goldpanner", "panning for gold"),
		("friendlyoutdoorsman", "a friendly outdoorsman"),
		("fundraiser", "collecting money"),
		("townwidow", "a widow in town"),
		("townburial", "a burial in town"),
		("domesticdispute", "a fight at home"),
		("pickpocket", "a pickpocket"),
		("darkalley", "trouble in a dark alley"),
		("rowdydrunks", "a rowdy drunk"),
		("drunkcamp", "drinking at a camp"),
		("horserace", "a horse race"),
		("wealthycouple", "a wealthy couple"),
		("skippingstones", "skipping stones"),
		("naked", "swimming"),
		("washedashore", "washed ashore"),
		("poisoned", "poisoned"),
		("frozentodeath", "nearly froze"),
		("policechase", "a police chase"),
		("checkpoint", "a checkpoint on the road"),
		("rally", "a rally"),
		("taunting", "taunted you"),
		("street_fight", "a street fight")
	};

	private readonly Dictionary<int, int> _reNearSince = new Dictionary<int, int>();

	private static bool IsRivalTie(Tie t)
	{
		if (t != null)
		{
			if (!(t.Type == "rival"))
			{
				return t.Type == "enemy";
			}
			return true;
		}
		return false;
	}

	private static int RivalryCount(Tie t)
	{
		if (t == null || t.Note == null)
		{
			return 0;
		}
		Match match = Regex.Match(t.Note, "rivalry (\\d+)");
		if (!match.Success)
		{
			return 0;
		}
		return int.Parse(match.Groups[1].Value);
	}

	private static void BumpRivalry(NemesisRecord a, NemesisRecord b)
	{
		(NemesisRecord, NemesisRecord)[] array = new(NemesisRecord, NemesisRecord)[2]
		{
			(a, b),
			(b, a)
		};
		for (int i = 0; i < array.Length; i++)
		{
			(NemesisRecord, NemesisRecord) tuple = array[i];
			NemesisRecord item = tuple.Item1;
			NemesisRecord y = tuple.Item2;
			Tie tie = item.Ties?.FirstOrDefault((Tie z) => z.Id == y.Id && IsRivalTie(z));
			if (tie == null)
			{
				item.Ties = item.Ties ?? new List<Tie>();
				item.Ties.Add(tie = new Tie
				{
					Id = y.Id,
					Type = "rival",
					Strength = -40,
					Note = ""
				});
			}
			int num = RivalryCount(tie) + 1;
			tie.Note = Regex.Replace(tie.Note ?? "", "\\s*rivalry \\d+", "").Trim() + $" rivalry {num}";
			tie.Strength = Math.Max(-100, tie.Strength - 10);
		}
	}

	private List<(NemesisRecord a, NemesisRecord b)> RivalPairs(Func<NemesisRecord, bool> ok)
	{
		List<(NemesisRecord, NemesisRecord)> list = new List<(NemesisRecord, NemesisRecord)>();
		foreach (NemesisRecord item in Roster.Where(ok))
		{
			foreach (Tie t in (item.Ties ?? new List<Tie>()).Where(IsRivalTie))
			{
				NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == t.Id);
				if (nemesisRecord != null && ok(nemesisRecord) && string.CompareOrdinal(item.Id, nemesisRecord.Id) < 0)
				{
					list.Add((item, nemesisRecord));
				}
			}
		}
		return list;
	}

	private WorldEvent SimRivalry(long now)
	{
		List<(NemesisRecord, NemesisRecord)> list = RivalPairs((NemesisRecord n) => OffStage(n) && !n.Paralyzed);
		if (list.Count == 0)
		{
			return null;
		}
		(NemesisRecord, NemesisRecord) tuple = list[_rng.Next(list.Count)];
		NemesisRecord item = tuple.Item1;
		NemesisRecord b = tuple.Item2;
		WorldEvent worldEvent = NewEvent("RIVALRY", now);
		worldEvent.Gangs.Add(item.Gang ?? "");
		if (b.Gang != item.Gang)
		{
			worldEvent.Gangs.Add(b.Gang ?? "");
		}
		worldEvent.Place = ((item.Gang != null && item.Gang == b.Gang) ? "in camp" : "in a saloon");
		float num = Mind.F(item, "Bravery") + item.Rank * 8 + item.Aggression / 2 + _rng.Next(50);
		float num2 = Mind.F(b, "Bravery") + b.Rank * 8 + b.Aggression / 2 + _rng.Next(50);
		NemesisRecord nemesisRecord3;
		NemesisRecord nemesisRecord4;
		if (!(num >= num2))
		{
			NemesisRecord nemesisRecord = b;
			NemesisRecord nemesisRecord2 = item;
			nemesisRecord3 = nemesisRecord2;
			nemesisRecord4 = nemesisRecord;
		}
		else
		{
			NemesisRecord nemesisRecord2 = b;
			nemesisRecord3 = nemesisRecord2;
			nemesisRecord4 = item;
		}
		int num3 = _rng.Next(100);
		WorldPart worldPart = PartFor(nemesisRecord4, "WON", "");
		WorldPart worldPart2 = PartFor(nemesisRecord3, "LOST", "");
		worldEvent.People.Add(worldPart);
		worldEvent.People.Add(worldPart2);
		if (num3 < 35)
		{
			worldEvent.Title = PickTitle(RivalWords, "rival words").Replace("{a}", item.Name).Replace("{b}", b.Name).Replace("{w}", nemesisRecord4.Name)
				.Replace("{l}", nemesisRecord3.Name);
			worldPart.Note = "had the last word";
			worldPart2.Note = "lost face";
			AddThought(nemesisRecord4, "Proud", "putting " + nemesisRecord3.Name + " in his place", 2, 48, now, worldPart);
			AddThought(nemesisRecord3, "Humiliated", nemesisRecord4.Name + "'s mouth", -2, 72, now, worldPart2);
		}
		else if (num3 < 75)
		{
			worldEvent.Title = PickTitle(RivalFists, "rival fists").Replace("{w}", nemesisRecord4.Name).Replace("{l}", nemesisRecord3.Name);
			ScarOption scarOption = BeatingScars[_rng.Next(BeatingScars.Length)];
			nemesisRecord3.Scars.Add(new Scar
			{
				DamagePack = scarOption.Pack,
				Description = scarOption.Description,
				Location = worldEvent.Place,
				ReceivedAt = DateTime.Now
			});
			worldPart.Note = "won the fight";
			worldPart2.Note = scarOption.Description;
			worldPart2.Effects.Add("scar");
			AddThought(nemesisRecord3, "Bitter", "the beating " + nemesisRecord4.Name + " gave him", -3, 96, now, worldPart2);
			Remember(nemesisRecord3, "Shame", 60, "beaten by " + nemesisRecord4.Name, nemesisRecord4.Id);
		}
		else
		{
			worldEvent.Title = PickTitle(RivalDuels, "rival duel").Replace("{a}", item.Name).Replace("{b}", b.Name).Replace("{w}", nemesisRecord4.Name)
				.Replace("{l}", nemesisRecord3.Name);
			worldPart.Note = "won the duel";
			worldPart2.Note = "lost the duel";
			if (_rng.Next(100) < 40 * Config.WorldLethality / 100)
			{
				OffscreenDeath(nemesisRecord3, worldEvent, worldPart2, nemesisRecord4.Name, gun: true);
			}
			else
			{
				ScarOption scarOption2 = GunshotScars[_rng.Next(GunshotScars.Length)];
				nemesisRecord3.Scars.Add(new Scar
				{
					DamagePack = scarOption2.Pack,
					Description = scarOption2.Description,
					Location = worldEvent.Place,
					ReceivedAt = DateTime.Now
				});
				worldPart2.Effects.Add("scar");
				worldPart2.Note = "shot, lived";
			}
			Remember(nemesisRecord4, "Pride", 65, "beat " + nemesisRecord3.Name + " in a duel", nemesisRecord3.Id);
		}
		BumpRivalry(item, b);
		GrudgeLog($"RIVAL off screen: {worldEvent.Title} (rivalry {RivalryCount(item.Ties.FirstOrDefault((Tie t) => t.Id == b.Id))})");
		return worldEvent;
	}

	private void MaybeRivalArrives(Ped player)
	{
		if (_rival != null || _rivalRolled || _enc == null || ActiveRecord == null || _activeNemesisPed == null || _enc.Kind != EncounterKind.Idle || TownAt(player.Position) != null || NoSpawnReason(player) != null || (!_enc.Engaged && _enc.StandoffUntil <= 0))
		{
			return;
		}
		_rivalRolled = true;
		NemesisRecord activeRecord = ActiveRecord;
		(Tie, NemesisRecord) tuple = (from t in (activeRecord.Ties ?? new List<Tie>()).Where(IsRivalTie).ToList()
			select (t: t, r: Roster.FirstOrDefault((NemesisRecord n) => n.Id == t.Id)) into x
			where x.r != null && x.r.CanHunt && !x.r.PresumedDead && OffStage(x.r) && !Resting(x.r) && !Gangs.Squads(x.r)
			orderby RivalryCount(x.t) descending
			select x).FirstOrDefault();
		if (tuple.Item2 != null)
		{
			int num = ((RivalryCount(tuple.Item1) >= 2) ? 25 : 10);
			int num2 = _rng.Next(100);
			GrudgeLog($"RIVAL {tuple.Item2.Name} vs {activeRecord.Name}: {num}%, rolled {num2}");
			if (num2 < num)
			{
				SpawnRival(tuple.Item2, activeRecord, player);
			}
		}
	}

	private void SpawnRival(NemesisRecord rival, NemesisRecord target, Ped player)
	{
		Vector3 position = _activeNemesisPed.Position;
		Vector3 vector = Vector3.Zero;
		for (int i = 0; i < 8; i++)
		{
			if (!(vector == Vector3.Zero))
			{
				break;
			}
			double num = _rng.NextDouble() * Math.PI * 2.0;
			Vector3 vector2 = GroundOnSpot(position + new Vector3((float)Math.Cos(num) * (60f + (float)_rng.Next(30)), (float)Math.Sin(num) * (60f + (float)_rng.Next(30)), 0f));
			if (HiddenFromView(player, vector2) && Dry(vector2))
			{
				vector = vector2;
			}
		}
		if (vector == Vector3.Zero)
		{
			GrudgeLog("RIVAL " + rival.Name + ": no hidden dry spot - not sent");
			return;
		}
		if (_rivalGroup == 0)
		{
			_rivalGroup = World.AddRelationshipGroup("NEMESIS_RIVAL");
		}
		Model model;
		Ped ped = SpawnNemesisPed(rival, vector, HeadingTo(vector, position), _rivalGroup, out model);
		if (ped == null)
		{
			return;
		}
		ArmForHate(ped, rival, drawNow: true);
		World.SetRelationshipBetweenGroups(eRelationshipType.Hate, _rivalGroup, NemesisGroup());
		World.SetRelationshipBetweenGroups(eRelationshipType.Hate, NemesisGroup(), _rivalGroup);
		World.SetRelationshipBetweenGroups(eRelationshipType.Ignore, _rivalGroup, player.RelationshipGroup);
		RivalScene rivalScene = new RivalScene
		{
			Ped = ped,
			Rec = rival,
			Target = target
		};
		_worldNemeses[ped.Handle] = rival;
		int num2 = _rng.Next(3);
		Gang gang = Gangs.Of(rival);
		for (int j = 0; j < num2; j++)
		{
			if (gang == null)
			{
				break;
			}
			if (gang.TroopModels.Length == 0)
			{
				break;
			}
			Vector3 vector3 = GroundOnSpot(vector + new Vector3(2f + (float)j * 2f, 1.5f, 0f));
			Ped ped2 = SpawnPed(gang.TroopModels[_rng.Next(gang.TroopModels.Length)], vector3, HeadingTo(vector3, position));
			if (!(ped2 == null))
			{
				MakeOurs(ped2.Handle);
				ArmTroop(ped2, gang);
				ped2.RelationshipGroup = _rivalGroup;
				ped2.BlockPermanentEvents = true;
				_followerOf[ped2.Handle] = rival;
				rivalScene.Men.Add(ped2);
			}
		}
		foreach (Ped item in new Ped[1] { ped }.Concat(rivalScene.Men))
		{
			item.BlockPermanentEvents = true;
			TASK.TASK_COMBAT_PED(item.Handle, _activeNemesisPed.Handle, 0, 16);
		}
		Crumbs.Native("Rivals blip add (V2)");
		rivalScene.Blip = ped.AddBlip(BlipType.BLIP_STYLE_NEUTRAL);
		_rival = rivalScene;
		rival.Touch();
		History(rival, "CAME FOR " + target.Name.ToUpperInvariant() + " while he was busy with you", Theme.Warn);
		History(target, rival.Name.ToUpperInvariant() + " CAME FOR HIM in the middle of it", Theme.Warn);
		ShowSubtitle(rival.Name + ": \"" + target.Name + "! You and me - now!\"");
		Feed($"RIVAL {rival.Name} rode in for {target.Name} ({num2} men)", Theme.Warn);
		GrudgeNotice(rival.Name + " rode in - he's here for " + target.Name + ", not you.");
		GrudgeLog($"RIVAL {rival.Name} spawned for {target.Name} at {vector.X:0},{vector.Y:0} with {num2} men");
	}

	private void UpdateRival(Ped player)
	{
		RivalScene rival = _rival;
		if (rival == null)
		{
			if (_enc == null)
			{
				_rivalRolled = false;
			}
			else
			{
				MaybeRivalArrives(player);
			}
			return;
		}
		bool flag = rival.Ped != null && rival.Ped.Exists() && !rival.Ped.IsDead;
		if (flag && ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(rival.Ped.Handle, player.Handle, p2: true, p3: true))
		{
			World.SetRelationshipBetweenGroups(eRelationshipType.Hate, _rivalGroup, player.RelationshipGroup);
		}
		if (_enc == null && rival.EndAt < 0)
		{
			rival.EndAt = Game.GameTime + 30000;
		}
		if (flag && (_activeNemesisPed == null || !_activeNemesisPed.Exists() || _activeNemesisPed.IsDead) && rival.EndAt < 0)
		{
			Remember(rival.Rec, "Pride", 60, "saw " + rival.Target.Name + " go down", rival.Target.Id);
			ShowSubtitle(rival.Rec.Name + ": \"That settles that.\"");
			rival.EndAt = Game.GameTime + 15000;
			TASK.TASK_WANDER_STANDARD(rival.Ped.Handle, 10f, 10);
		}
		bool flag2 = flag && rival.Ped.Position.DistanceTo(player.Position) > 300f;
		if (!flag || flag2 || (rival.EndAt > 0 && Game.GameTime > rival.EndAt))
		{
			EndRival(flag ? "left" : "dead");
		}
	}

	private unsafe void EndRival(string why)
	{
		RivalScene rival = _rival;
		_rival = null;
		if (rival == null)
		{
			return;
		}
		if (rival.Blip != null && rival.Blip.Exists())
		{
			rival.Blip.Delete();
		}
		if (why == "left")
		{
			RegisterBand(new Ped[1] { rival.Ped }.Concat(rival.Men), Gangs.ById(rival.Rec?.Gang), (rival.Rec?.Name ?? "the rival") + " and his men");
		}
		foreach (Ped item in new Ped[1] { rival.Ped }.Concat(rival.Men))
		{
			if (!(item == null) && item.Exists())
			{
				_followerOf.Remove(item.Handle);
				if (item == rival.Ped && !item.IsDead)
				{
					_worldNemeses.Remove(item.Handle);
				}
				int handle = item.Handle;
				item.BlockPermanentEvents = false;
				ReleaseIfOurs(&handle);
			}
		}
		BumpRivalry(rival.Rec, rival.Target);
		GrudgeLog("RIVAL scene over (" + why + ")");
	}
}
