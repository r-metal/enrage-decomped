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
	private sealed class Strongbox
	{
		public int Obj;

		public int Amount;

		public bool Opened;

		public Vector3 At;
	}

	private sealed class LiveTreasury
	{
		public string SiteId;

		public Gang Gang;

		public readonly List<Strongbox> Boxes = new List<Strongbox>();

		public int Taken;
	}

	internal const int PurseRich = 1200;

	internal const int PurseCap = 5000;

	internal const int ArmyCap = 12000;

	internal const int ArmyPay = 250;

	private readonly Dictionary<int, int> _purseRobbedAt = new Dictionary<int, int>();

	private const int TreasuryExtraGuards = 3;

	private const int TreasuryMaxPeds = 12;

	private const int BoxOpenMs = 2500;

	private const int BoxGroupId = 8280593;

	private static readonly string[] StrongboxModels = new string[7] { "p_strongbox01x", "p_chest01x", "p_chest02x", "p_trunk04x", "p_trunk01x", "p_crate01x", "p_cratestack01x" };

	private string _boxModel;

	private bool _boxModelLogged;

	private readonly Dictionary<string, LiveTreasury> _liveTreasuries = new Dictionary<string, LiveTreasury>();

	private int _pBox;

	private bool _boxArmed;

	private Strongbox _boxOpening;

	private LiveTreasury _boxOpeningIn;

	private int _boxOpenUntil;

	private int _nextStashSpawn;

	private int _nextStashBlips;

	private readonly HashSet<string> _stashNoKeeperSaid = new HashSet<string>();

	private readonly Dictionary<string, (int blip, string label)> _stashBlips = new Dictionary<string, (int, string)>();

	private Dictionary<string, int> Treasury => Chronicle.Treasury ?? (Chronicle.Treasury = new Dictionary<string, int>());

	internal bool HasPurse(Gang g)
	{
		if (g != null)
		{
			return !g.StoryOnly;
		}
		return false;
	}

	internal int PurseOf(Gang g)
	{
		if (!HasPurse(g))
		{
			return int.MaxValue;
		}
		if (!Treasury.TryGetValue(g.Id, out var value))
		{
			value = (StartPurse.TryGetValue(g.Id, out var value2) ? value2 : 300);
			Treasury[g.Id] = value;
		}
		return value;
	}

	internal void AddPurse(Gang g, int amount, string why)
	{
		if (!HasPurse(g) || amount == 0 || IsDestroyed(g))
		{
			return;
		}
		if (amount > 0)
		{
			amount = Math.Max(1, (int)Math.Round((double)amount * TaperOf(g)));
		}
		int num = PurseOf(g);
		int num2 = Math.Max(0, Math.Min(CapOf(g), num + amount));
		if (num2 == num)
		{
			return;
		}
		Treasury[g.Id] = num2;
		GrudgeLog($"PURSE {g.Name}: ${num} -> ${num2} ({why})");
		SaveWorld();
		List<string> list = Chronicle.BrokeSaid ?? (Chronicle.BrokeSaid = new List<string>());
		if (num2 < 60 && num >= 60 && !list.Contains(g.Id))
		{
			list.Add(g.Id);
			WorldEvent worldEvent = NewEvent("TURMOIL", GameMinutesNow());
			worldEvent.Gangs.Add(g.Id);
			worldEvent.Title = "The " + g.Name + " are out of money";
			worldEvent.Lines.Add("No pay, no new men - their empty seats will stay empty, and fewer ride with each crew.");
			worldEvent.Heard = true;
			Chronicle.Events.Add(worldEvent);
			if (Config.WorldNews >= 2)
			{
				WorldNotice("The " + g.Name + " are out of money.");
			}
		}
		else if (num2 >= 180 && list.Contains(g.Id))
		{
			list.Remove(g.Id);
		}
	}

	internal static string PurseWord(int v)
	{
		if (v != int.MaxValue)
		{
			if (v >= 60)
			{
				if (v >= 200)
				{
					if (v >= 700)
					{
						if (v >= 1200)
						{
							return "Rich";
						}
						return "Flush";
					}
					return "Steady";
				}
				return "Thin";
			}
			return "Broke";
		}
		return "";
	}

	internal static (int r, int g, int b) PurseColor(int v)
	{
		if (v >= 60)
		{
			if (v >= 200)
			{
				if (v >= 1200)
				{
					return (r: 215, g: 178, b: 95);
				}
				return Theme.Text;
			}
			return Theme.Warn;
		}
		return Theme.Blood;
	}

	internal int PurseCrewShift(Gang g)
	{
		if (!HasPurse(g))
		{
			return 0;
		}
		int num = PurseOf(g);
		int num2 = ((num < 60) ? (-1) : ((num >= 1200) ? 1 : 0));
		if (MenShare(g) < 0.4)
		{
			num2--;
		}
		if (LowSupplies(g))
		{
			num2--;
		}
		return Math.Max(-2, num2);
	}

	internal static int RecruitCost(int tier)
	{
		if (tier != 0)
		{
			return 60;
		}
		return 30;
	}

	internal int HireBudget(Gang g)
	{
		if (!HasPurse(g))
		{
			return int.MaxValue;
		}
		int num = PurseOf(g);
		if (g.OrdersNotFeelings)
		{
			return num;
		}
		double num2 = ((num < 60) ? 0.25 : ((num < 250) ? 0.35 : ((num < 600) ? 0.4 : 0.5)));
		return (int)((double)num * num2);
	}

	private void DailyPurses(int days)
	{
		foreach (Gang g in Gangs.All.Where(HasPurse))
		{
			if (!IsDestroyed(g))
			{
				List<NemesisRecord> list = Roster.Where((NemesisRecord n) => n.Gang == g.Id && n.IsAlive && !n.PresumedDead).ToList();
				int count = list.Count;
				double num = list.Sum((NemesisRecord n) => (n.Rank < 4) ? ((n.Rank < 2) ? 2.5 : 5.0) : 10.0);
				int num2 = WarsOf(g);
				num += (double)(15 * num2);
				int num3 = MenOf(g);
				num += 0.04 * (double)num3 * Math.Min(1.0, 450.0 / (double)Math.Max(1, MenStart(g)));
				double num4 = 15.0 + 60.0 * Math.Min(1.3, TerritoryShare(g));
				if (!g.OrdersNotFeelings)
				{
					num4 *= Math.Max(1.0, Math.Sqrt((double)MenStart(g) / 450.0));
				}
				if (g.OrdersNotFeelings)
				{
					num4 += 250.0;
				}
				if (AgendaOf(g)?.Kind == "rich")
				{
					num4 *= 1.25;
				}
				int num5 = PurseOf(g);
				string text = null;
				if (!g.OrdersNotFeelings && num5 < 60)
				{
					num *= 0.5;
					text = "half pay - they're broke";
				}
				else if (!g.OrdersNotFeelings && num5 < 150)
				{
					num *= 0.75;
					text = "short pay - money's tight";
				}
				int num6 = Math.Min(days, 30);
				double num7 = TaperOf(g);
				int num8 = (int)Math.Round((num4 * num7 - num) * (double)num6);
				try
				{
					BrokeDay(g, num6, num5 + num8);
				}
				catch (Exception ex)
				{
					GrudgeLog("PURSE broke check failed: " + ex.Message);
				}
				if (num8 != 0)
				{
					AddPurseRaw(g, num8, string.Format("{0} day(s): takings from its ground ({1:0}/day), {2} named + {3} men to keep{4} ({5:0}/day{6})", num6, num4 * num7, count, num3, (num2 > 0) ? string.Format(" + {0} war{1}", num2, (num2 == 1) ? "" : "s") : "", num, (text != null) ? (", " + text) : ""));
				}
			}
		}
		try
		{
			DailyHiring(days);
		}
		catch (Exception ex2)
		{
			GrudgeLog("MEN hiring failed: " + ex2.Message);
		}
		try
		{
			DailySupplies(days);
		}
		catch (Exception ex3)
		{
			GrudgeLog("SUPPLIES daily failed: " + ex3.Message);
		}
	}

	private void AddPurseRaw(Gang g, int amount, string why)
	{
		if (amount <= 0)
		{
			AddPurse(g, amount, why);
			return;
		}
		int num = PurseOf(g);
		Treasury[g.Id] = Math.Min(CapOf(g), num + amount);
		GrudgeLog($"PURSE {g.Name}: ${num} -> ${Treasury[g.Id]} ({why})");
		SaveWorld();
	}

	internal void PurseForJob(Gang g, bool ok, bool youThere, bool desperate = false)
	{
		if (!HasPurse(g))
		{
			return;
		}
		if (youThere)
		{
			AddPurse(g, -30, "you broke up its job");
			return;
		}
		if (!ok)
		{
			AddPurse(g, -25, desperate ? "a desperate job that went wrong" : "a job that went wrong");
			return;
		}
		int num = 60 + _rng.Next(81);
		if (AgendaOf(g)?.Kind == "rich")
		{
			num = (int)((double)num * 1.4);
		}
		if (desperate)
		{
			num *= 2;
		}
		AddPurse(g, num, desperate ? "a desperate job that paid" : "a job that paid");
	}

	private Dictionary<string, int> BrokeDays => Chronicle.BrokeDays ?? (Chronicle.BrokeDays = new Dictionary<string, int>());

	private void BrokeDay(Gang g, int days, int purseAfter)
	{
		if (g == null || g.OrdersNotFeelings || IsDestroyed(g))
		{
			return;
		}
		BrokeDays.TryGetValue(g.Id, out var value);
		if (purseAfter >= 60)
		{
			if (value > 0)
			{
				BrokeDays[g.Id] = 0;
				GrudgeLog($"PURSE {g.Name}: out of the red after {value} broke day(s)");
			}
			return;
		}
		int num = value + Math.Max(1, days);
		BrokeDays[g.Id] = num;
		if (num >= 3)
		{
			int num2 = (int)Math.Ceiling((double)MenOf(g) * (0.01 + _rng.NextDouble() * 0.01) * (double)Math.Min(days, 7));
			if (num2 > 0)
			{
				LoseMen(g, num2, "unpaid men walked away");
			}
			if (value < 3)
			{
				BrokeEvent(g, "Men are leaving the " + g.Name, $"No pay for {num} days - the {g.Name}'s men are drifting off. More will go until the money comes back.");
			}
		}
		if (value < 5 && num >= 5)
		{
			Dictionary<string, int> nextShakeUp = Chronicle.NextShakeUp;
			if (nextShakeUp != null && nextShakeUp.ContainsKey(g.Id))
			{
				try
				{
					nextShakeUp[g.Id] = GameDate.DayKey(DayOf(GameDayKey()).AddDays(1.0));
				}
				catch
				{
				}
				GrudgeLog($"SUCCESSION {g.Id}: broke {num} days - the men want change, a shake-up tomorrow");
			}
		}
		if (num >= 7 && num % 3 == 1 && _rng.Next(100) < 25)
		{
			BossUnderPressure(g, num);
		}
		SaveWorld();
	}

	private void BossUnderPressure(Gang g, int broke)
	{
		string text = g.LeaderName ?? "the boss";
		NemesisRecord nemesisRecord = (from n in Roster
			where n.Gang == g.Id && n.IsAlive && !n.PresumedDead && OffStage(n) && n.Rank >= 2 && n.Id != SuccessorId(g) && !LoreProtected(n)
			orderby Mind.F(n, "Ambition") descending, n.Rank descending
			select n).FirstOrDefault();
		int num = _rng.Next(100);
		if (num < 50 || nemesisRecord == null)
		{
			Agenda agenda = AgendaOf(g);
			if (agenda?.Kind != "rich")
			{
				Chronicle.Agendas[g.Id] = new Agenda
				{
					Kind = "rich",
					By = text,
					ById = agenda?.ById,
					SetAt = GameMinutesNow(),
					ReviewAt = GameMinutesNow() + 14400
				};
				BrokeEvent(g, text + "'s men lean on him over the empty purse", $"{broke} days without pay. Money comes first now: the {g.Name} mean to get rich.");
				return;
			}
			num = 50;
		}
		if (num < 80 && nemesisRecord != null)
		{
			WorldEvent worldEvent = NewEvent("TURMOIL", GameMinutesNow());
			worldEvent.Gangs.Add(g.Id);
			worldEvent.Title = nemesisRecord.Name + " talked against " + text + " - and paid for it";
			WorldPart worldPart = PartFor(nemesisRecord, "PUT DOWN", "spoke against the boss");
			worldEvent.People.Add(worldPart);
			OffscreenDeath(nemesisRecord, worldEvent, worldPart, text + "'s men", gun: true);
			worldEvent.Lines.Add($"{broke} days without pay, and {nemesisRecord.Name} said so once too often.");
			worldEvent.Heard = true;
			Chronicle.Events.Add(worldEvent);
			GrudgeLog($"PURSE {g.Name}: broke {broke} days - {nemesisRecord.Name} moved against {text} and was put down");
		}
		else
		{
			int num2 = 100 + _rng.Next(101);
			AddPurse(g, num2, text + " paid his men out of his own pocket");
			BrokeDays[g.Id] = 0;
			BrokeEvent(g, text + " pays the " + g.Name + " out of his own pocket", $"{broke} days without pay - he dug into his own stash (${num2}) to keep them.");
		}
	}

	private void BrokeEvent(Gang g, string title, string line)
	{
		WorldEvent worldEvent = NewEvent("TURMOIL", GameMinutesNow());
		worldEvent.Gangs.Add(g.Id);
		worldEvent.Title = title;
		worldEvent.Lines.Add(line);
		worldEvent.Heard = true;
		Chronicle.Events.Add(worldEvent);
		GrudgeLog("PURSE " + g.Name + ": " + title + " - " + line);
		Feed("WORLD " + title, Theme.Warn);
	}

	internal void PurseForClash(Gang winner, Gang loser)
	{
		AddPurse(winner, 20 + _rng.Next(31), "loot from a clash with the " + loser?.Name);
		AgendaClashWon(winner, loser);
		AddPurse(loser, -(20 + _rng.Next(21)), "lost a clash to the " + winner?.Name);
	}

	internal void PurseRobbed(int victim)
	{
		if (!_purseRobbedAt.TryGetValue(victim, out var value) || Game.GameTime - value >= 10000)
		{
			Gang g = Gangs.ById(RecordAt(victim)?.Gang) ?? ((Entity.FromHandle(victim) is Ped ped && ped.Exists()) ? Gangs.ForModel((uint)ped.Model.Hash) : null);
			if (HasPurse(g))
			{
				_purseRobbedAt[victim] = Game.GameTime;
				AddPurse(g, -(10 + _rng.Next(21)), "you robbed one of its men");
			}
		}
	}

	internal void PurseCampCleared(Gang g, string label)
	{
		if (HasPurse(g))
		{
			int num = PurseOf(g);
			AddPurse(g, -Math.Max(40, num / 5), "you cleared " + label + " - what they kept there is gone");
		}
	}

	internal string PurseLine()
	{
		return "purses: " + string.Join(", ", from g in Gangs.All
			where HasPurse(g) && !IsDestroyed(g)
			select $"{g.Id} ${PurseOf(g)} ({PurseWord(PurseOf(g))})");
	}

	private NemesisRecord HolderOf(CampSite s)
	{
		if (s != null)
		{
			return (from n in Roster
				where n.CampId == s.Id && Mans(n)
				orderby n.Rank descending
				select n).FirstOrDefault();
		}
		return null;
	}

	private void UpdateTreasuries(Ped player)
	{
		if (Config.CampsEnabled)
		{
			UpdateStashes(player);
			UpdateStrongboxes(player);
		}
	}

	internal bool RevealTreasury(Gang g, string from)
	{
		CampSite campSite = TreasuryOf(g);
		if (campSite == null || TreasuryKnown(g))
		{
			return false;
		}
		Hold.KnowTreasury(g);
		SaveWorld();
		NemesisRecord nemesisRecord = HolderOf(campSite);
		if (nemesisRecord != null && !nemesisRecord.CampKnown)
		{
			RevealCamp(nemesisRecord, found: false);
		}
		RedoCampBlip(campSite.Id);
		RefreshStashBlips();
		try
		{
			FlashCampBlip(campSite.Id);
		}
		catch
		{
		}
		GrudgeLog("TREASURY " + g.Name + ": you know it now - " + campSite.Label + " (from " + from + ")");
		ReplyNotice("INTEL: the " + g.Name + " keep their money at " + campSite.Label + ". Marked on your map.");
		Feed("TREASURY " + g.Name + ": " + campSite.Label, Theme.Gold);
		return true;
	}

	private (Acquaintance a, Gang g, CampSite site) TreasuryTraitor()
	{
		foreach (Acquaintance item in from _ in People
			where !_.Dead && _.Id != null && _.Like >= 40
			orderby _rng.Next()
			select _)
		{
			Gang gang = Gangs.ById(item.Affil) ?? Gangs.ForModel(item.Model);
			if (gang != null && HoldsCamps(gang) && !TreasuryKnown(gang) && !TreasuryRobbedNow(gang))
			{
				CampSite campSite = TreasuryOf(gang);
				if (campSite != null)
				{
					return (a: item, g: gang, site: campSite);
				}
			}
		}
		return (a: null, g: null, site: null);
	}

	private void SpawnStrongboxes(CampSite site)
	{
		Gang gang = TreasuryGangOf(site);
		if (gang == null)
		{
			return;
		}
		LiveTreasury liveTreasury = new LiveTreasury
		{
			SiteId = site.Id,
			Gang = gang
		};
		_liveTreasuries[site.Id] = liveTreasury;
		if (TreasuryRobbedNow(gang))
		{
			GrudgeLog("TREASURY " + gang.Name + ": " + site.Label + " - you emptied it; nothing in it until they move it");
			return;
		}
		int num = (int)((double)PurseOf(gang) * 0.4);
		if (num < 20)
		{
			GrudgeLog($"TREASURY {gang.Name}: {site.Label} - the purse is too thin to fill a box (${PurseOf(gang)})");
			return;
		}
		int num2 = ((num >= 150) ? 3 : 2);
		int num3 = 0;
		Vector3 vector = Kit.Centre(site);
		double num4 = _rng.NextDouble() * Math.PI * 2.0;
		for (int i = 0; i < num2; i++)
		{
			double num5 = num4 + (double)i * (Math.PI * 2.0 / (double)num2) + (_rng.NextDouble() - 0.5) * 0.6;
			float num6 = 4.5f + (float)_rng.NextDouble() * 2f;
			Vector3 vector2 = Vector3.Zero;
			for (int j = 0; j < 8; j++)
			{
				if (!(vector2 == Vector3.Zero))
				{
					break;
				}
				Vector3 vector3 = GroundOnSpot(vector + new Vector3((float)Math.Cos(num5 + (double)j * 0.5) * (num6 + (float)j * 0.3f), (float)Math.Sin(num5 + (double)j * 0.5) * (num6 + (float)j * 0.3f), 0f));
				if (vector3 != Vector3.Zero && Kit.ClearOf(site.Id, vector3, 0.8f))
				{
					vector2 = vector3;
				}
			}
			if (!(vector2 == Vector3.Zero))
			{
				int num7 = MakeStrongbox(vector2);
				if (num7 == 0)
				{
					break;
				}
				int num8 = ((i == num2 - 1) ? (num - num3) : ((int)((float)num / (float)num2 * (0.8f + (float)_rng.NextDouble() * 0.4f))));
				num3 += num8;
				liveTreasury.Boxes.Add(new Strongbox
				{
					Obj = num7,
					Amount = Math.Max(1, num8),
					At = vector2
				});
			}
		}
		GrudgeLog(string.Format("TREASURY {0}: {1} - {2} strongboxes ({3}), ${4} of their ${5}", gang.Name, site.Label, liveTreasury.Boxes.Count, _boxModel ?? "none", liveTreasury.Boxes.Sum((Strongbox b) => b.Amount), PurseOf(gang)));
	}

	private int MakeStrongbox(Vector3 at)
	{
		string[] array = ((_boxModel == null) ? StrongboxModels : new string[1] { _boxModel });
		foreach (string text in array)
		{
			Model m = new Model(text);
			if (m.IsValid && m.IsInCdImage && LoadModelSafe(m, 1500))
			{
				Crumbs.Native("treasury: create box");
				int num = OBJECT.CREATE_OBJECT((uint)m.Hash, at.X, at.Y, at.Z, isNetwork: false, bScriptHostObj: false, dynamic: false, p7: false, p8: true);
				STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED((uint)m.Hash);
				if (num != 0)
				{
					OBJECT.PLACE_OBJECT_ON_GROUND_PROPERLY(num, p1: false);
					ENTITY.SET_ENTITY_HEADING(num, _rng.Next(360));
					ENTITY.FREEZE_ENTITY_POSITION(num, toggle: true);
					ENTITY.SET_ENTITY_AS_MISSION_ENTITY(num, p1: true, p2: true);
					_boxModel = text;
					return num;
				}
			}
		}
		if (!_boxModelLogged)
		{
			_boxModelLogged = true;
			GrudgeLog("TREASURY none of the strongbox models could be made: " + string.Join(", ", StrongboxModels));
		}
		return 0;
	}

	private unsafe void DespawnStrongboxes(string siteId)
	{
		ClearObjectives(siteId);
		if (siteId == null || !_liveTreasuries.TryGetValue(siteId, out var value))
		{
			return;
		}
		_liveTreasuries.Remove(siteId);
		if (_boxOpening != null && value.Boxes.Contains(_boxOpening))
		{
			_boxOpening = null;
		}
		foreach (Strongbox box in value.Boxes)
		{
			int obj = box.Obj;
			box.Obj = 0;
			if (obj != 0 && ENTITY.DOES_ENTITY_EXIST(obj))
			{
				Crumbs.Native("treasury: delete box");
				OBJECT.DELETE_OBJECT(&obj);
			}
		}
	}

	private void UpdateStrongboxes(Ped player)
	{
		if (_liveTreasuries.Count == 0)
		{
			HideBoxPrompt();
			return;
		}
		if (_boxOpening != null)
		{
			if (player.IsDead || player.Position.DistanceTo(_boxOpening.At) > 2.6f || !_liveTreasuries.ContainsValue(_boxOpeningIn))
			{
				GrudgeLog("TREASURY you left the strongbox before it was open");
				_boxOpening = null;
			}
			else if (Game.GameTime >= _boxOpenUntil)
			{
				Strongbox boxOpening = _boxOpening;
				LiveTreasury boxOpeningIn = _boxOpeningIn;
				_boxOpening = null;
				try
				{
					TASK.CLEAR_PED_TASKS(player.Handle, p1: false, p2: false);
				}
				catch
				{
				}
				OpenStrongbox(boxOpeningIn, boxOpening, player);
			}
			return;
		}
		if (player.IsDead || player.IsInVehicle || player.IsOnMount || _menu.IsOpen || _convo != null || _tg != null || _interro != null)
		{
			HideBoxPrompt();
			return;
		}
		Strongbox near = null;
		LiveTreasury liveTreasury = null;
		float num = 1.9f;
		foreach (LiveTreasury value2 in _liveTreasuries.Values)
		{
			foreach (Strongbox box in value2.Boxes)
			{
				if (!box.Opened && box.Obj != 0)
				{
					float num2 = player.Position.DistanceTo(box.At);
					if (num2 < num)
					{
						num = num2;
						near = box;
						liveTreasury = value2;
					}
				}
			}
		}
		if (near == null)
		{
			HideBoxPrompt();
			return;
		}
		if (_pBox == 0 || !PromptValid(_pBox))
		{
			_pBox = MakePrompt(eInputType.InteractOption1, "Break It Open", hold: true, 8280593);
			_boxArmed = false;
		}
		HUD._UI_PROMPT_SET_VISIBLE(_pBox, toggle: true);
		HUD._UI_PROMPT_SET_ENABLED(_pBox, toggle: true);
		Function.Call(14292813145410643495uL, 8280593u, new InputArgument(UiDraw.VarString("Strongbox")), 0, 0, 0, 0);
		if (!HUD._UI_PROMPT_HAS_HOLD_MODE_COMPLETED(_pBox))
		{
			_boxArmed = true;
		}
		else if (_boxArmed)
		{
			_boxArmed = false;
			HideBoxPrompt();
			ENTITY.SET_ENTITY_HEADING(player.Handle, HeadingTo(player.Position, near.At));
			Crumbs.Native("treasury: crouch");
			TASK.TASK_START_SCENARIO_IN_PLACE_HASH(player.Handle, MISC.GET_HASH_KEY("WORLD_HUMAN_CROUCH_INSPECT"), 0, playEnterAnim: true, 0u, -1f, p6: false);
			_boxOpening = near;
			_boxOpeningIn = liveTreasury;
			_boxOpenUntil = Game.GameTime + 2500;
			if (_liveCamps.TryGetValue(liveTreasury.SiteId, out var value) && !value.Engaged && value.Peds.Any((Ped p) => p.Exists() && !p.IsDead && !LooksKnockedOut(p) && p.Position.DistanceTo(near.At) < 35f))
			{
				GrudgeLog("TREASURY " + liveTreasury.Gang.Name + ": they heard you at the strongbox - the camp turns on you");
				EngageCamp(value, player);
			}
		}
	}

	private void OpenStrongbox(LiveTreasury lt, Strongbox box, Ped player)
	{
		if (box.Opened)
		{
			return;
		}
		box.Opened = true;
		Gang gang = lt.Gang;
		CampSite campSite = Camps.ById(lt.SiteId);
		int num = Math.Max(0, Math.Min(box.Amount, PurseOf(gang)));
		bool flag = !TreasuryRobbedNow(gang);
		if (num > 0)
		{
			AddPurse(gang, -num, "you robbed their treasury at " + campSite?.Label);
			PayPlayer(num * 100);
			lt.Taken += num;
		}
		int num2 = lt.Boxes.Count((Strongbox b) => !b.Opened && b.Obj != 0);
		GrudgeLog($"TREASURY {gang.Name}: you broke a strongbox open - ${num} (${lt.Taken} so far, {num2} left)");
		GrudgeNotice((num > 0) ? $"${num} from the {MemberWord(gang)} strongbox." : "The strongbox is empty.");
		Feed(string.Format("TREASURY ${0} from the {1} strongbox{2}", num, OutfitPoss(gang), (num2 > 0) ? $" - {num2} more" : ""), Theme.Gold);
		if (flag)
		{
			Hold.RecordRobbery(gang, lt.SiteId);
			try
			{
				RefreshStashBlips();
			}
			catch
			{
			}
			SaveWorld();
			AddNotoriety(gang, 6, "you robbed their treasury");
			LiveCamp value;
			NemesisRecord nemesisRecord = ((campSite != null) ? (HolderOf(campSite) ?? (_liveCamps.TryGetValue(campSite.Id, out value) ? value.Boss : null)) : null);
			if (nemesisRecord != null)
			{
				nemesisRecord.AddHate(15);
				History(nemesisRecord, "ROBBED: you broke open the strongboxes he kept at " + campSite.Label, Theme.Blood);
				SaveRoster();
			}
			try
			{
				ChronicleDeed("treasury", "You robbed the " + OutfitPoss(gang) + " treasury", (campSite != null) ? ("at " + campSite.Label) : "", (campSite != null) ? new Vector3?(SitePos(campSite)) : ((Vector3?)null), gang.Id);
			}
			catch (Exception ex)
			{
				GrudgeLog("TREASURY deed failed: " + ex.Message);
			}
			if (campSite != null)
			{
				RedoCampBlip(campSite.Id);
			}
		}
	}

	private void HideBoxPrompt()
	{
		if (_pBox != 0 && PromptValid(_pBox))
		{
			HUD._UI_PROMPT_SET_VISIBLE(_pBox, toggle: false);
			HUD._UI_PROMPT_SET_ENABLED(_pBox, toggle: false);
		}
	}

	private void DeleteBoxPrompt()
	{
		if (PromptValid(_pBox))
		{
			HUD._UI_PROMPT_DELETE(_pBox);
		}
		_pBox = 0;
	}

	private void DebugTreasuryHere(Ped p)
	{
		CampSite campSite = (from s in Camps.AllSites
			where HoldsCamps(Gangs.ById(SiteOwner(s))) && HolderOf(s) != null
			orderby SitePos(s).DistanceTo(p.Position)
			select s).FirstOrDefault();
		if (campSite == null)
		{
			SpawnerSays("no manned outfit camp anywhere.");
			return;
		}
		Gang g = Gangs.ById(SiteOwner(campSite));
		Hold.SetTreasury(g, campSite);
		if (Cleared.Remove(campSite.Id))
		{
			Config.ClearedCamps = string.Join(",", Cleared.Select((KeyValuePair<string, int> kv) => $"{kv.Key}:{kv.Value}"));
			Config.Save();
		}
		if (PurseOf(g) < 200)
		{
			AddPurse(g, 300 - PurseOf(g), "debug: a treasury worth robbing");
		}
		RevealTreasury(g, "the debug spawner");
		if (_liveCamps.TryGetValue(campSite.Id, out var value))
		{
			DespawnStrongboxes(campSite.Id);
			DespawnCamp(value, delete: true);
		}
		_campHeldOff.Remove(campSite.Id);
		SaveWorld();
		GrudgeLog($"TREASURY debug: {campSite.Label} is the {OutfitPoss(g)} treasury ({SitePos(campSite).DistanceTo(p.Position):0} m off)");
		SpawnerSays($"the {OutfitPoss(g)} treasury: {campSite.Label}, {SitePos(campSite).DistanceTo(p.Position):0} m - marked on your map.");
	}

	private void DebugRevealTreasuries(Ped p)
	{
		HoldingsPass();
		int num = 0;
		foreach (Gang item in Gangs.All.Where(HoldsCamps))
		{
			if (RevealTreasury(item, "the debug spawner"))
			{
				num++;
			}
		}
		SpawnerSays((num > 0) ? $"{num} treasuries marked on the map." : "every treasury is already known (or no outfit has one).");
	}

	private void DebugRefillTreasury(Ped p)
	{
		(Gang, CampSite) tuple = (from g in Gangs.All.Where(HoldsCamps)
			select (g: g, s: TreasuryOf(g)) into t
			where t.s != null
			orderby SitePos(t.s).DistanceTo(p.Position)
			select t).FirstOrDefault();
		if (tuple.Item2 == null)
		{
			SpawnerSays("no outfit has a treasury yet.");
			return;
		}
		Hold.ForgetRobbery(tuple.Item1);
		AddPurse(tuple.Item1, 300, "debug: treasury refilled");
		if (_liveCamps.TryGetValue(tuple.Item2.Id, out var value))
		{
			DespawnStrongboxes(tuple.Item2.Id);
			DespawnCamp(value, delete: true);
		}
		SaveWorld();
		RedoCampBlip(tuple.Item2.Id);
		SpawnerSays("the " + OutfitPoss(tuple.Item1) + " treasury is full again (+$300) - " + tuple.Item2.Label + ".");
	}

	internal string TreasuryDebugLine()
	{
		return "treasuries: " + string.Join(", ", Treasuries.Select((KeyValuePair<string, string> kv) => kv.Key + " " + (Camps.ById(kv.Value)?.Label ?? kv.Value) + (TreasuryKnownList.Contains(kv.Key) ? " (known)" : "") + (TreasuryRobbed.ContainsKey(kv.Key) ? " (robbed)" : "")));
	}

	private string CampWord(LiveCamp lc)
	{
		if (!IsStashSite(lc.Site))
		{
			if (!lc.Site.Hideout)
			{
				return "camp";
			}
			return "hideout";
		}
		return "stash";
	}

	private void FoundStash(CampSite site)
	{
		Gang gang = TreasuryGangOf(site);
		if (gang != null && !TreasuryKnown(gang))
		{
			Hold.KnowTreasury(gang);
			SaveWorld();
			RefreshStashBlips();
			GrudgeLog("TREASURY " + gang.Name + ": you found their stash - " + site.Label);
			ReplyNotice("You've found the " + OutfitPoss(gang) + " stash. Marked on your map.");
			Feed("TREASURY " + gang.Name + ": stash found - " + site.Label, Theme.Gold);
		}
	}

	private void UpdateStashes(Ped player)
	{
		if (Game.GameTime < _nextStashSpawn)
		{
			return;
		}
		_nextStashSpawn = Game.GameTime + 1000;
		if (Game.GameTime >= _nextStashBlips)
		{
			_nextStashBlips = Game.GameTime + 15000;
			RefreshStashBlips();
		}
		if (StashList.Count == 0 || player.IsDead || NoSpawnReason(player, place: true) != null)
		{
			return;
		}
		Vector3 position = player.Position;
		foreach (string item in StashList.ToList())
		{
			Gang gang = Gangs.ById(item);
			CampSite campSite = TreasuryOf(gang);
			if (campSite == null || _liveCamps.ContainsKey(campSite.Id) || IsCleared(campSite) || HolderOf(campSite) != null)
			{
				continue;
			}
			float num = position.DistanceTo(SitePos(campSite));
			if (!(num > Config.CampSpawnRange) && !(num < 60f) && HiddenFromView(player, SitePos(campSite) + new Vector3(0f, 0f, 1.2f)) && TownSiteReady(campSite))
			{
				NemesisRecord nemesisRecord = StashKeeper(gang);
				if (nemesisRecord != null)
				{
					GrudgeLog($"TREASURY {gang.Name}: their stash at {campSite.Label} ({num:0} m) - kept by {nemesisRecord.Name}");
					SpawnCamp(campSite, nemesisRecord, player);
					break;
				}
				if (_stashNoKeeperSaid.Add(campSite.Id))
				{
					GrudgeLog("TREASURY " + gang.Name + ": the stash at " + campSite.Label + " - none of theirs free to keep it, nobody there");
				}
			}
		}
	}

	private unsafe void RefreshStashBlips()
	{
		if (!MapSettled())
		{
			_mapPending.Add(RefreshStashBlips);
			return;
		}
		if (_mapHidden)
		{
			CleanupStashBlips();
			return;
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		foreach (string stash in StashList)
		{
			Gang g = Gangs.ById(stash);
			CampSite campSite = TreasuryOf(g);
			if (campSite != null && TreasuryKnown(g))
			{
				dictionary[campSite.Id] = MemberWord(g) + " Treasury - stash" + (TreasuryRobbedNow(g) ? " (robbed)" : "");
			}
		}
		foreach (string item2 in _stashBlips.Keys.ToList())
		{
			if (!dictionary.TryGetValue(item2, out var value) || !(value == _stashBlips[item2].label))
			{
				int item = _stashBlips[item2].blip;
				MAP.REMOVE_BLIP(&item);
				_stashBlips.Remove(item2);
			}
		}
		foreach (KeyValuePair<string, string> item3 in dictionary)
		{
			if (!_stashBlips.ContainsKey(item3.Key))
			{
				CampSite campSite2 = Camps.ById(item3.Key);
				int num = MAP.BLIP_ADD_FOR_COORDS(2339406903u, campSite2.X, campSite2.Y, campSite2.Z);
				if (num != 0)
				{
					MAP.SET_BLIP_SPRITE(num, MISC.GET_HASH_KEY("blip_robbery_home"), p2: true);
					Function.Call(11290983183885202434uL, num, new InputArgument(UiDraw.ForeverVarString(item3.Value)));
					_stashBlips[item3.Key] = (num, item3.Value);
				}
			}
		}
	}

	private unsafe void CleanupStashBlips()
	{
		foreach (string item2 in _stashBlips.Keys.ToList())
		{
			int item = _stashBlips[item2].blip;
			MAP.REMOVE_BLIP(&item);
		}
		_stashBlips.Clear();
	}
}
