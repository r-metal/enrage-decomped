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
	private readonly HashSet<int> _reMet = new HashSet<int>();

	private readonly HashSet<int> _reRode = new HashSet<int>();

	private static string ReNote(string modelLower)
	{
		if (!modelLower.StartsWith("re_"))
		{
			return null;
		}
		(string, string)[] reNotes = ReNotes;
		for (int i = 0; i < reNotes.Length; i++)
		{
			var (value, result) = reNotes[i];
			if (modelLower.Contains(value))
			{
				return result;
			}
		}
		return "met in some trouble on the road";
	}

	internal static bool Woman(Ped p)
	{
		if (p != null && p.Exists())
		{
			return !PED.IS_PED_MALE(p.Handle);
		}
		return false;
	}

	private unsafe void UpdateRoadside(Ped player)
	{
		int num = 0;
		PLAYER.GET_PLAYER_INTERACTION_TARGET_ENTITY(Game.Player.Handle, &num, p2: false, p3: false);
		int num2 = (player.IsOnMount ? PED.GET_MOUNT(player.Handle) : 0);
		Vector3 position = player.Position;
		foreach (KeyValuePair<int, Named> item in _named.ToList())
		{
			int key = item.Key;
			if (!ENTITY.DOES_ENTITY_EXIST(key) || !ENTITY.IS_ENTITY_A_PED(key) || ENTITY.IS_ENTITY_DEAD(key) || Registered(key))
			{
				continue;
			}
			string text = ModelName(ENTITY.GET_ENTITY_MODEL(key));
			string text2 = ReNote(text);
			if (text2 == null)
			{
				continue;
			}
			Ped ped = new Ped(key);
			float num3 = ped.Position.DistanceTo(position);
			if (!_reMet.Contains(key))
			{
				bool flag = num3 < 5f;
				if (flag && !_reNearSince.ContainsKey(key))
				{
					_reNearSince[key] = Game.GameTime;
				}
				if (!flag)
				{
					_reNearSince.Remove(key);
				}
				if ((num == key && num3 < 8f) || (flag && Game.GameTime - _reNearSince[key] >= 2000))
				{
					_reMet.Add(key);
					Introduce(key);
					RememberPerson(key, met: true);
					if (_acqOf.TryGetValue(key, out var value) && string.IsNullOrEmpty(value.Note))
					{
						value.Note = text2;
						_peopleDirty = true;
					}
					GrudgeLog("ROADSIDE met " + item.Value.Name + " (" + text + "): " + text2);
					WatchRoadsideDeed(key, text, text2);
				}
			}
			if (num2 != 0 && !_reRode.Contains(key) && num3 < 6f && ped.IsOnMount && PED.GET_MOUNT(key) == num2)
			{
				_reRode.Add(key);
				Favour(key, "you gave " + (Woman(ped) ? "her" : "him") + " a ride");
				GoodDeed(ped.Position, "you gave a " + (Woman(ped) ? "woman" : "man") + " in trouble a ride");
				if (_reDeeds.TryGetValue(key, out var value2))
				{
					value2.Done = true;
				}
			}
		}
		TrackRoadsideDeeds(player, num);
	}

	private void Favour(int handle, string what)
	{
		RememberPerson(handle, met: true);
		if (_acqOf.TryGetValue(handle, out var value))
		{
			value.Favours++;
			LikeBy(handle, 15, "favour: " + what);
			(value.FavourNotes ?? (value.FavourNotes = new List<string>())).Add(what);
			if (value.FavourNotes.Count > 6)
			{
				value.FavourNotes.RemoveAt(0);
			}
			_peopleDirty = true;
			GrudgeLog($"FAVOUR {value.Name}: {what} (favours {value.Favours})");
			Ped p = Entity.FromHandle(handle) as Ped;
			Feed(string.Format("FAVOUR {0}: {1} - {2} remember it (favours {3})", value.Name, what, Woman(p) ? "she'll" : "he'll", value.Favours), Theme.Good);
		}
	}

	internal bool KeepsHabits(NemesisRecord n)
	{
		if (n == null || n.Story || n.Person || n.LeaderPage || !n.CanHunt || n.PresumedDead || n.Rank < 2 || n.Gang == null)
		{
			return false;
		}
		Gang gang = Gangs.ById(n.Gang);
		if (gang != null && !gang.StoryOnly)
		{
			return !IsDestroyed(gang);
		}
		return false;
	}

	private static int RollDays(Random r, int count)
	{
		int num = 0;
		int num2 = 0;
		while (num2 < count)
		{
			int num3 = r.Next(7);
			if ((num & (1 << num3)) == 0)
			{
				num |= 1 << num3;
				num2++;
			}
		}
		return num;
	}
}
