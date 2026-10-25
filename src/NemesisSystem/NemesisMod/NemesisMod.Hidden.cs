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
	private const ulong DecorIsRegisteredAsTypeFn = 8229574572667171442uL;

	private const ulong DecorSetIntFn = 16757697947711703311uL;

	private const string HonorDecor = "honor_override";

	private TownMan HiddenTownMan(int handle)
	{
		return _townMen.FirstOrDefault((TownMan t) => t.Disguised && !t.Rec.CoverBlown && t.Ped != null && t.Ped.Handle == handle);
	}

	private void TryHiddenInTown(string town, Ped player)
	{
		foreach (Gang g in Gangs.All.Where((Gang x) => x.NoTowns && x.TownModels != null))
		{
			if ((g.OnlyDistricts != null && !g.OnlyDistricts.Contains(_regionDistrict)) || _townMen.Any((TownMan t) => t.Rec.Gang == g.Id))
			{
				continue;
			}
			if (_townMen.Count + _townArrivals.Count >= Balance.TownMaxMen)
			{
				GrudgeLog($"KLAN town {town} already has its {Balance.TownMaxMen} men");
				break;
			}
			if ((float)_rng.Next(100) >= (float)g.TownChancePercent * NightTownMult())
			{
				GrudgeLog("KLAN none of the " + g.Name + " in " + town + " this time");
				continue;
			}
			List<NemesisRecord> list = Roster.Where((NemesisRecord n) => n.Gang == g.Id && n.IsAlive && !n.PresumedDead && n.CanHunt && !Resting(n) && !_worldNemeses.ContainsValue(n) && n != ActiveRecord).ToList();
			if (list.Count != 0)
			{
				NemesisRecord nemesisRecord = list[_rng.Next(list.Count)];
				GrudgeLog("KLAN " + nemesisRecord.FullName + " is in " + town + " out of his robe" + (nemesisRecord.CoverBlown ? " (you know him)" : ""));
				SpawnTownPresence(nemesisRecord, town, player);
			}
		}
	}

	private bool GreetHidden(int target)
	{
		TownMan townMan = HiddenTownMan(target);
		if (townMan == null)
		{
			return false;
		}
		NemesisRecord rec = townMan.Rec;
		rec.PlainGreets++;
		rec.Touch();
		if (rec.PlainGreets >= 3)
		{
			Unmask(townMan, "greet");
			return true;
		}
		string[] array = ((rec.PlainGreets != 1) ? new string[4] { "You again. Folks round here keep to their own, you know.", "Twice in a day. You keep good company?", "We like things the way they are in this county.", "Mind how you go. It gets dark out there." } : new string[4] { "Afternoon, friend.", "Fine day for it.", "Evenin'. Haven't seen you round here.", "Mister." });
		ShowSubtitle(rec.Name + ": \"" + array[_rng.Next(array.Length)] + "\"");
		Feed($"GREETED {rec.FullName} - a townsman ({rec.PlainGreets}/3)", Theme.Dim);
		SaveRoster();
		return true;
	}

	private void HiddenSlip(NemesisRecord rec, string how)
	{
		if (rec == null || rec.CoverBlown)
		{
			return;
		}
		TownMan townMan = _townMen.FirstOrDefault((TownMan t) => t.Rec == rec && t.Disguised);
		if (townMan == null)
		{
			return;
		}
		if (how == "antagonize")
		{
			int num = ((rec.Personality == Personality.Hothead) ? 80 : ((rec.Personality == Personality.Proud) ? 65 : 35));
			if (_rng.Next(100) >= num)
			{
				GrudgeLog($"KLAN {rec.Name} held his tongue ({num}%)");
				return;
			}
		}
		Unmask(townMan, how);
	}

	private void Unmask(TownMan tm, string how)
	{
		NemesisRecord rec = tm.Rec;
		Gang gang = Gangs.ById(rec.Gang);
		rec.CoverBlown = true;
		tm.Disguised = false;
		string text = how switch
		{
			"greet" => (rec.Personality == Personality.Hothead) ? "Keep sniffin' round me, the night riders'll come callin' for you." : "Folks like you don't last long in this county. Ask around - after dark.", 
			"antagonize" => "You'll learn your place when the fires come, boy.", 
			"rob" => null, 
			"questioned" => "Alright! Alright... I ride with 'em. Everybody here does, near enough.", 
			_ => "Get your hands off me! You don't know who you're dealin' with!", 
		};
		if (text != null)
		{
			ShowSubtitle(rec.Name + ": \"" + text + "\"");
		}
		string text2 = how switch
		{
			"questioned" => "what he told you", 
			"greet" => "what he let slip", 
			"rob" => "a white hood folded in his coat", 
			_ => "a white hood falling out of his coat", 
		};
		if (tm.Blip == null || !tm.Blip.Exists())
		{
			Crumbs.Native("KlanCover blip add (V2)");
			tm.Blip = tm.Ped.AddBlip(BlipType.BLIP_STYLE_NEUTRAL);
			if (tm.Blip != null)
			{
				Function.Call(11290983183885202434uL, tm.Blip.Handle, new InputArgument(UiDraw.ForeverVarString(rec.FullName + " (" + (gang?.Name ?? "Klan") + ", out of his robe)")));
			}
		}
		Remember(rec, (rec.Personality == Personality.Coward) ? "Fear" : "Anger", 45, "you found him out in town");
		History(rec, "FOUND OUT in " + tm.Town + ": one of " + (gang?.Name ?? "them") + ", living as a townsman (" + text2 + ")", Theme.Warn);
		Feed("UNMASKED " + rec.FullName + " rides with " + (gang?.Name ?? "the Klan") + " - " + text2, Theme.Warn, rec);
		GrudgeNotice(rec.FullName + " is one of " + (gang?.Name ?? "the Klan") + " - " + text2 + ".");
		GrudgeLog("KLAN " + rec.FullName + " unmasked in " + tm.Town + " by " + how);
		SaveRoster();
	}
}
