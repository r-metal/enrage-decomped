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
	private readonly Dictionary<string, Dictionary<string, int>> _objBlips = new Dictionary<string, Dictionary<string, int>>();

	private readonly Dictionary<string, Blip> _objHorseBlips = new Dictionary<string, Blip>();

	private readonly HashSet<string> _objShown = new HashSet<string>();

	private readonly HashSet<string> _objDone = new HashSet<string>();

	private int _nextObjectiveCheck;

	private void UpdateCampObjectives(Ped player)
	{
		if (Game.GameTime < _nextObjectiveCheck)
		{
			return;
		}
		_nextObjectiveCheck = Game.GameTime + 1000;
		foreach (string siteId in (from s in _liveSupplies.Values
			where !s.Town
			select s.SiteId).Concat(_liveTreasuries.Keys).Distinct().ToList())
		{
			if (!_liveCamps.TryGetValue(siteId, out var value))
			{
				continue;
			}
			int count = value.Peds.Count;
			int num = value.Peds.Count((Ped p) => p.Exists() && !p.IsDead && !LooksKnockedOut(p));
			if (count <= 0 || (num != 0 && (!value.Engaged || num > Math.Max(1, count / 4))))
			{
				continue;
			}
			_liveSupplies.TryGetValue(siteId, out var value2);
			_liveTreasuries.TryGetValue(siteId, out var value3);
			Gang gang = value2?.Gang ?? value3?.Gang;
			if (gang == null)
			{
				continue;
			}
			List<(string, Vector3, string)> list = new List<(string, Vector3, string)>();
			List<(string, Ped)> list2 = new List<(string, Ped)>();
			if (value2 != null)
			{
				foreach (SupplyCrate item2 in value2.Crates.Where((SupplyCrate c) => !c.Gone))
				{
					list.Add(($"crate{item2.At.X:0}{item2.At.Y:0}", item2.At, item2.Big ? "Their stores - dynamite" : "Their crates"));
				}
				if (value2.Wagon != null && value2.Wagon.Exists() && !value2.WagonCounted)
				{
					list.Add(("wagon", value2.Wagon.Position, "Their supply wagon"));
				}
				foreach (var horse in value2.Horses)
				{
					Ped item = horse.horse;
					if (item != null && item.Exists() && !item.IsDead && !value2.HorsesCounted.Contains(item.Handle))
					{
						list2.Add(($"horse{item.Handle}", item));
					}
				}
			}
			if (value3 != null)
			{
				foreach (Strongbox item3 in value3.Boxes.Where((Strongbox b) => !b.Opened && b.Obj != 0))
				{
					list.Add(($"box{item3.At.X:0}{item3.At.Y:0}", item3.At, "Strongbox"));
				}
			}
			int num2 = list.Count + list2.Count;
			if (!_objShown.Contains(siteId))
			{
				_objShown.Add(siteId);
				if (num2 == 0)
				{
					continue;
				}
				string text = ShortOutfit(gang);
				text += (text.EndsWith("s") ? "'" : "'s");
				string text2 = ((value3 != null) ? ("The " + text + " treasury is yours for the taking - rob the strongboxes.") : ("The " + text + " supply camp is broken - blow up their stores, shoot up their crates, take or wreck the wagon, run off their horses."));
				ShowNotification(text2);
				GrudgeNotice(text2);
				GrudgeLog($"OBJECTIVE {siteId}: {text2} ({num2} targets)");
			}
			if (!_objBlips.TryGetValue(siteId, out var value4))
			{
				value4 = (_objBlips[siteId] = new Dictionary<string, int>());
			}
			HashSet<string> want = new HashSet<string>(list.Select<(string, Vector3, string), string>(((string key, Vector3 at, string name) c) => c.key));
			foreach (string item4 in value4.Keys.Where((string k) => !want.Contains(k)).ToList())
			{
				RemoveObjBlip(value4, item4);
			}
			foreach (var (text3, vector, text4) in list)
			{
				if (value4.ContainsKey(text3))
				{
					if (text3 == "wagon")
					{
						MAP.SET_BLIP_COORDS(value4[text3], vector.X, vector.Y, vector.Z);
					}
					continue;
				}
				Crumbs.Native("objective blip");
				int num3 = MAP.BLIP_ADD_FOR_COORDS(Joaat.Of("BLIP_STYLE_OBJECTIVE"), vector.X, vector.Y, vector.Z);
				Function.Call(11290983183885202434uL, num3, new InputArgument(UiDraw.ForeverVarString(text4)));
				value4[text3] = num3;
			}
			HashSet<string> horseKeys = new HashSet<string>(list2.Select<(string, Ped), string>(((string key, Ped horse) h) => siteId + h.key));
			foreach (string item5 in _objHorseBlips.Keys.Where((string k) => k.StartsWith(siteId + "horse") && !horseKeys.Contains(k)).ToList())
			{
				Blip blip = _objHorseBlips[item5];
				if (blip != null && blip.Exists())
				{
					blip.Delete();
				}
				_objHorseBlips.Remove(item5);
			}
			foreach (var (text5, ped) in list2)
			{
				if (!_objHorseBlips.ContainsKey(siteId + text5))
				{
					Crumbs.Native("objective horse blip");
					Blip blip2 = ped.AddBlip((BlipType)Joaat.Of("BLIP_STYLE_OBJECTIVE"));
					if (blip2 != null)
					{
						Function.Call(11290983183885202434uL, blip2.Handle, new InputArgument(UiDraw.ForeverVarString("Their horses")));
						_objHorseBlips[siteId + text5] = blip2;
					}
				}
			}
			if (num2 == 0 && !_objDone.Contains(siteId))
			{
				_objDone.Add(siteId);
				string text6 = ((value3 != null) ? "The strongboxes are empty." : "Nothing left of their supplies here.");
				ShowNotification(text6);
				GrudgeLog("OBJECTIVE " + siteId + ": done");
			}
		}
	}

	private unsafe void RemoveObjBlip(Dictionary<string, int> have, string key)
	{
		int blip = have[key];
		Crumbs.Native("objective blip remove");
		try
		{
			if (MAP.DOES_BLIP_EXIST(blip))
			{
				MAP.REMOVE_BLIP(&blip);
			}
		}
		catch
		{
		}
		have.Remove(key);
	}

	private void ClearObjectives(string siteId)
	{
		if (siteId == null)
		{
			return;
		}
		if (_objBlips.TryGetValue(siteId, out var value))
		{
			foreach (string item in value.Keys.ToList())
			{
				RemoveObjBlip(value, item);
			}
			_objBlips.Remove(siteId);
		}
		foreach (string item2 in _objHorseBlips.Keys.Where((string k) => k.StartsWith(siteId + "horse")).ToList())
		{
			Blip blip = _objHorseBlips[item2];
			if (blip != null && blip.Exists())
			{
				blip.Delete();
			}
			_objHorseBlips.Remove(item2);
		}
		_objShown.Remove(siteId);
		_objDone.Remove(siteId);
	}
}
