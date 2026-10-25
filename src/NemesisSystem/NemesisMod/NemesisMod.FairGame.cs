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
	private const ulong PostponeDisturbanceFn = 3900265406721784602uL;

	private const ulong WitnessesPendingFn = 844106609231005222uL;

	private const ulong WitnessesActiveFn = 7629521592523355275uL;

	private const float FairNearM = 40f;

	private const int FairLingerMaxMs = 180000;

	private int _fairFightAt = -1073741824;

	private bool _fairPostponed;

	private string _fairHeldSaid;

	private readonly Dictionary<int, Gang> _fairMen = new Dictionary<int, Gang>();

	private readonly Dictionary<int, int> _drewOnYouAt = new Dictionary<int, int>();

	private int _nextFairPulse;

	private readonly List<(int at, string text, (int r, int g, int b) c)> _feed = new List<(int, string, (int, int, int))>();

	private long? _historyAt;

	private void FairFightPulse(Ped player)
	{
		if (Game.GameTime < _nextFairPulse)
		{
			return;
		}
		_nextFairPulse = Game.GameTime + 1000;
		Gang gang = null;
		string text = null;
		foreach (LiveCamp value2 in _liveCamps.Values)
		{
			if (!value2.Engaged || !FairOutfit(Gangs.ById(value2.Boss?.Gang)) || !(SitePos(value2.Site).DistanceTo(player.Position) < 160f) || !value2.Peds.Any((Ped x) => x != null && x.Exists() && !x.IsDead && PED.IS_PED_IN_COMBAT(x.Handle, player.Handle)))
			{
				continue;
			}
			gang = Gangs.ById(value2.Boss.Gang);
			text = "camp fight";
			foreach (Ped ped2 in value2.Peds)
			{
				if (ped2 != null && ped2.Exists())
				{
					_fairMen[ped2.Handle] = gang;
				}
			}
			break;
		}
		string text2 = null;
		foreach (KeyValuePair<int, int> combatant in _combatants)
		{
			if (Game.GameTime - combatant.Value > 15000 || !ENTITY.DOES_ENTITY_EXIST(combatant.Key) || RoleOf(combatant.Key) == OurRole.Companion || IsLawman(new Ped(combatant.Key)) || XRayHandsOff(combatant.Key))
			{
				continue;
			}
			Gang gang2 = Gangs.ById(RecordAt(combatant.Key)?.Gang) ?? Gangs.ForModel(ENTITY.GET_ENTITY_MODEL(combatant.Key));
			if (FairOutfit(gang2))
			{
				_fairMen[combatant.Key] = gang2;
				if (gang == null)
				{
					gang = gang2;
					text = (FieldRole(RoleOf(combatant.Key)) ? "a fight" : (AttackerLabel(combatant.Key) + " came at you"));
				}
			}
			else
			{
				if ((RoleOf(combatant.Key) == OurRole.None && RecordAt(combatant.Key) == null && !_provoked.ContainsKey(combatant.Key)) || !CameAtYou(combatant.Key))
				{
					continue;
				}
				if (!ENTITY.IS_ENTITY_DEAD(combatant.Key) && DrewOnYou(combatant.Key))
				{
					_drewOnYouAt[combatant.Key] = Game.GameTime;
				}
				if (_drewOnYouAt.TryGetValue(combatant.Key, out var value) && Game.GameTime - value <= 15000)
				{
					_fairMen[combatant.Key] = null;
					if (text2 == null)
					{
						text2 = AttackerLabel(combatant.Key);
					}
				}
			}
		}
		if (_drewOnYouAt.Count > 64)
		{
			foreach (int item in (from x in _drewOnYouAt
				where Game.GameTime - x.Value > 60000
				select x.Key).ToList())
			{
				_drewOnYouAt.Remove(item);
			}
		}
		if (gang == null && _convo != null && _convo.Kind == ConvoKind.Interrogate && _convo.Ped != null && _convo.Ped.Exists() && FieldRole(RoleOf(_convo.Ped.Handle)))
		{
			Gang gang3 = _convo.It?.Gang ?? Gangs.ById(_convo.Rec?.Gang);
			if (FairOutfit(gang3))
			{
				gang = gang3;
				text = "questioning";
				_fairMen[_convo.Ped.Handle] = gang3;
			}
		}
		if (_fairMen.Count > 64)
		{
			_fairMen.Clear();
		}
		if (gang != null)
		{
			bool num = Game.GameTime > _fairUntil;
			OpenFair(gang, kill: false);
			if (num)
			{
				GrudgeLog("FAIRGAME " + text + " with the " + ShortOutfit(gang) + " - outlaws: its disturbance crimes wait until it's over");
			}
			return;
		}
		if (text2 != null)
		{
			bool num2 = Game.GameTime > _fairUntil;
			OpenFair(null, kill: false, fight: true, text2);
			if (num2)
			{
				GrudgeLog("FAIRGAME " + text2 + " came at you and drew a weapon - defending yourself: the fight's disturbance crimes wait until it's over");
			}
			return;
		}
		foreach (int item2 in _fairMen.Keys.ToList())
		{
			Ped ped = Entity.FromHandle(item2) as Ped;
			if (ped == null || !ped.Exists() || Downed.IsFinished(item2) || (ped.IsDead && !LooksKnockedOut(ped)))
			{
				_fairMen.Remove(item2);
				continue;
			}
			float num3 = ped.Position.DistanceTo(player.Position);
			if (num3 > 40f)
			{
				if (num3 > 150f)
				{
					_fairMen.Remove(item2);
				}
				continue;
			}
			if (Game.GameTime - _fairFightAt <= 180000)
			{
				OpenFair(_fairMen[item2], kill: false, fight: false, (_fairMen[item2] == null) ? AttackerLabel(item2) : null);
				if (_fairHeldSaid != "hurt")
				{
					_fairHeldSaid = "hurt";
					GrudgeLog(string.Format("FAIRGAME kept open - {0} from the fight lies {1} {2:0} m from you", AttackerLabel(item2), ped.IsDead ? "out cold" : "hurt", num3));
				}
			}
			break;
		}
	}

	internal void FairNow(Gang g, string who)
	{
		if (!LawTestOn && !MISC.GET_MISSION_FLAG())
		{
			bool flag = Game.GameTime > _fairUntil;
			OpenFair(g, kill: false, fight: true, (g == null) ? who : null);
			if (!_fairPostponed)
			{
				Crumbs.Native("FairGame postpone disturbance");
				Function.Call(3900265406721784602uL, Game.Player.Handle, true);
				_fairPostponed = true;
			}
			if (flag)
			{
				GrudgeLog("FAIRGAME " + who + " squared up to you" + ((g != null) ? (" (" + ShortOutfit(g) + ")") : "") + " - the fight's disturbance crimes wait until it's over");
			}
		}
	}

	private void OpenFair(Gang g, bool kill, bool fight = true, string who = null)
	{
		if (!LawTestOn)
		{
			if (Game.GameTime > _fairUntil)
			{
				_fairHeldSaid = null;
			}
			_fairUntil = Game.GameTime + 60000;
			if (fight)
			{
				_fairFightAt = Game.GameTime;
			}
			_fairWho = who ?? ((g != null) ? ("the " + ShortOutfit(g)) : _fairWho);
		}
	}

	private void UpdateFairGame(Ped player)
	{
		try
		{
			FairFightPulse(player);
		}
		catch (Exception ex)
		{
			GrudgeLog("FAIRGAME pulse failed: " + ex.Message);
		}
		bool flag = !MISC.GET_MISSION_FLAG() && Game.GameTime <= _fairUntil;
		if (flag != _fairPostponed)
		{
			Crumbs.Native("FairGame postpone disturbance");
			Function.Call(3900265406721784602uL, Game.Player.Handle, flag);
			_fairPostponed = flag;
			if (!flag)
			{
				GrudgeLog("FAIRGAME closed - disturbance crimes back to normal");
			}
		}
	}

	internal void FairGameOff()
	{
		if (_fairPostponed)
		{
			Function.Call(3900265406721784602uL, Game.Player.Handle, false);
			_fairPostponed = false;
		}
	}
}
