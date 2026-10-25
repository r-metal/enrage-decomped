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
	private int _visitLastFightAt = -1000000;

	private bool _visitSecondRolled;

	private int _visitLeftAt = -1;

	private int _visitStartedAt = -1000000;

	private RoadAmbush _roadAmbush;

	private readonly List<TownMan> _townMen = new List<TownMan>();

	private void TrackTownVisit()
	{
		if (_currentTown == _visitTown)
		{
			_visitLeftAt = -1;
			return;
		}
		if (_currentTown == null && _visitTown != null)
		{
			if (_visitLeftAt < 0)
			{
				_visitLeftAt = Game.GameTime;
			}
			if (Game.GameTime - _visitLeftAt < 60000)
			{
				return;
			}
		}
		_visitLeftAt = -1;
		if (_currentTown != null)
		{
			GrudgeLog("TOWNCALM new visit: " + _currentTown);
		}
		_visitTown = _currentTown;
		_visitStartedAt = Game.GameTime;
		_arrivalRetried.Clear();
		_visitAggressors = 0;
		_visitLastFightAt = -1000000;
		_visitSecondRolled = false;
	}

	private bool TakeAggressorSlot(out string why)
	{
		if (_visitAggressors == 0)
		{
			_visitAggressors = 1;
			why = "first this visit";
			return true;
		}
		if (_visitAggressors >= 2)
		{
			why = "two already this visit";
			return false;
		}
		if (_townMen.Any((TownMan t) => t.Engaged) || (_enc != null && _enc.Kind == EncounterKind.Callout))
		{
			why = "one is still on";
			return false;
		}
		int num = Balance.TownQuietAfterFight * 1000 - (Game.GameTime - _visitLastFightAt);
		if (num > 0)
		{
			why = $"quiet for {num / 1000}s more";
			return false;
		}
		if (_visitSecondRolled)
		{
			why = "the second chance was rolled already";
			return false;
		}
		_visitSecondRolled = true;
		int num2 = _rng.Next(100);
		bool flag = num2 < Balance.TownSecondAggressorPct;
		GrudgeLog(string.Format("TOWNCALM second aggressor in {0}: {1}%, rolled {2} -> {3}", _visitTown, Balance.TownSecondAggressorPct, num2, flag ? "yes" : "no"));
		if (flag)
		{
			_visitAggressors = 2;
		}
		why = (flag ? "a rare second" : "no second this visit");
		return flag;
	}

	private void NoteTownFightOver()
	{
		_visitLastFightAt = Game.GameTime;
	}

	internal string TownCalmNote
	{
		get
		{
			if (_visitTown != null)
			{
				return string.Format("{0}: {1} aggressor{2} this visit{3}", _visitTown, _visitAggressors, (_visitAggressors == 1) ? "" : "s", (_roadAmbush != null) ? (", " + _roadAmbush.Rec.Name + " waits on the road out") : "");
			}
			return "";
		}
	}

	internal (int chance, string why) WaitOutsideChance(NemesisRecord r)
	{
		List<string> parts = new List<string>();
		int c = Balance.TownWaitOutsideBase;
		switch (r.Personality)
		{
		case Personality.Cold:
			Add(25, "cold");
			break;
		case Personality.Coward:
			Add(25, "coward");
			break;
		case Personality.Loyal:
			Add(10, "loyal");
			break;
		case Personality.Drifter:
			Add(10, "drifter");
			break;
		case Personality.Greedy:
			Add(5, "greedy");
			break;
		case Personality.Proud:
			Add(-20, "proud");
			break;
		case Personality.Hothead:
			Add(-30, "hothead");
			break;
		}
		if (r.MindRolled)
		{
			Add(-(Mind.F(r, "Anger") - 50) / 2, "anger");
			Add((50 - Mind.F(r, "Bravery")) / 3, "bravery");
			Add(-(Mind.F(r, "Pride") - 50) / 4, "pride");
			Add(Mind.V(r, "Law") * 4, "the law");
			if (r.Stress >= 70)
			{
				Add(-10, "on edge");
			}
			if (Mind.HasAgainstYou(r, "Prove"))
			{
				Add(-10, "wants to prove it");
			}
			if (Mind.HasAgainstYou(r, "Avenge"))
			{
				Add(-5, "wants it now");
			}
		}
		if (r.TimesKilledPlayer > 0)
		{
			Add(-5, "killed you before");
		}
		c = Math.Max(5, Math.Min(90, c));
		return (chance: c, why: string.Join(", ", parts));
		void Add(int v, string w)
		{
			if (v != 0)
			{
				c += v;
				parts.Add($"{w} {v:+0;-0}");
			}
		}
	}

	private bool RollWaitOutside(NemesisRecord rec, string town, Ped ped, Ped player, bool callout = false)
	{
		if (rec == null || _roadAmbush != null || rec.HateTier != HateTier.Murderous)
		{
			return false;
		}
		if (ped != null && ped.Exists() && (ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(ped.Handle, player.Handle, p2: true, p3: true) || PED.IS_PED_IN_COMBAT(ped.Handle, player.Handle) || (_hitByYouAt.TryGetValue(ped.Handle, out var value) && Game.GameTime - value < 60000)))
		{
			GrudgeLog("TOWNCALM " + rec.Name + ": no taking it outside - you've already hurt him / he's fighting you");
			return false;
		}
		(int chance, string why) tuple = WaitOutsideChance(rec);
		int item = tuple.chance;
		string item2 = tuple.why;
		int num = _rng.Next(100);
		bool flag = num < item;
		GrudgeLog(string.Format("TOWNCALM {0} saw you in {1}: wait outside {2}% ({3}), rolled {4} -> {5}", rec.Name, town, item, item2, num, flag ? "he'll wait on the road" : (callout ? "he'll do it here" : "he stays put, holding it")));
		Feed(string.Format("TOWN {0}: take it outside? {1}%, rolled {2} -> {3}", rec.Name, item, num, flag ? "he walks off - he'll be waiting" : (callout ? "here and now" : "he stays put")), flag ? Theme.Warn : Theme.Blood, rec);
		if (!flag)
		{
			return false;
		}
		string text = ((rec.Personality == Personality.Coward) ? "...Not here. Not with the law about." : ((rec.Personality == Personality.Cold) ? "Not here. Too many eyes." : ((rec.Personality == Personality.Loyal) ? "Not in town. You'll keep." : "Not here. You ride out of this town - I'll be waiting.")));
		ShowSubtitle(rec.Name + ": \"" + text + "\"");
		if (ped != null && ped.Exists())
		{
			_toldToFlee[ped.Handle] = Game.GameTime;
		}
		if (ped != null && ped.Exists())
		{
			TASK.CLEAR_PED_TASKS(ped.Handle, p1: true, p2: true);
			TASK.TASK_TURN_PED_TO_FACE_ENTITY(ped.Handle, player.Handle, 2500, 0f, 0f, 0f);
			TASK.TASK_WANDER_STANDARD(ped.Handle, 10f, 10);
		}
		_roadAmbush = new RoadAmbush
		{
			Rec = rec,
			Town = town,
			Until = Game.GameTime + Balance.RoadAmbushMinutes * 60000
		};
		History(rec, "SAW YOU IN " + town.ToUpperInvariant() + " - and went to wait for you on the road out", Theme.Warn);
		Remember(rec, "Anger", 40, "let you walk through " + town);
		rec.Touch();
		SaveRoster();
		return true;
	}
}
