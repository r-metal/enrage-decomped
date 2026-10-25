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
	private int _nextNewsCheck;

	private bool _debugOn;

	internal string LastEncounterNote = "";

	private void QueueDeathNews(NemesisRecord dead, bool byYou, string by, WorldEvent ev, Vector3? pos)
	{
		List<NemesisRecord> list = Roster.Where((NemesisRecord n) => n.IsAlive && n != dead && (n.KinId == dead.Id || dead.KinId == n.Id)).ToList();
		foreach (NemesisRecord item4 in FriendsOf(dead))
		{
			if (item4.IsAlive && !list.Contains(item4))
			{
				list.Add(item4);
			}
		}
		if (byYou && pos.HasValue)
		{
			try
			{
				QueuePeopleNews(dead.Name, dead.KillLocation, dead.CircleAcq, pos.Value, dead);
			}
			catch (Exception ex)
			{
				GrudgeLog("NEWS failed: " + ex.Message);
			}
		}
		if (list.Count == 0)
		{
			return;
		}
		long num = GameMinutesNow();
		List<DeathNews> list2 = Chronicle.DeathNews ?? (Chronicle.DeathNews = new List<DeathNews>());
		foreach (NemesisRecord item5 in list)
		{
			(long minutes, string why) tuple = NewsDelay(item5, dead, ev, pos);
			long item = tuple.minutes;
			string item2 = tuple.why;
			DeathNews item3 = new DeathNews
			{
				DeadId = dead.Id,
				DeadName = dead.Name,
				KinId = item5.Id,
				ByYou = byYou,
				By = by,
				EventId = ev?.Id,
				At = num,
				DueAt = num + item
			};
			list2.Add(item3);
			if (item <= 0)
			{
				_nextNewsCheck = Math.Min(_nextNewsCheck, Game.GameTime + 1500);
				GrudgeLog(Pron.Her("NEWS " + item5.FullName + " saw " + dead.FullName + " go down (" + item2 + ")", Models.Female(item5.ModelHash)));
			}
			else
			{
				GrudgeLog(Pron.Her($"NEWS {item5.FullName} will hear of {dead.FullName}'s death in {(float)item / 60f:0.#} h ({item2})", Models.Female(item5.ModelHash)));
			}
		}
		SaveWorld();
	}

	private (long minutes, string why) NewsDelay(NemesisRecord k, NemesisRecord dead, WorldEvent ev, Vector3? pos)
	{
		if (ev != null && ev.People.Any((WorldPart p) => p.Id == k.Id))
		{
			return (minutes: 0L, why: "he was there");
		}
		if (pos.HasValue)
		{
			foreach (KeyValuePair<int, NemesisRecord> worldNemese in _worldNemeses)
			{
				if (worldNemese.Value == k)
				{
					Ped ped = Entity.FromHandle(worldNemese.Key) as Ped;
					if (ped != null && ped.Exists() && !ped.IsDead && ped.Position.DistanceTo(pos.Value) < 60f)
					{
						return (minutes: 0L, why: "he saw it");
					}
				}
			}
			if (ActiveRecord == k && _activeNemesisPed != null && _activeNemesisPed.Exists() && _activeNemesisPed.Position.DistanceTo(pos.Value) < 60f)
			{
				return (minutes: 0L, why: "he saw it");
			}
		}
		if (k.Gang != null && k.Gang == dead.Gang)
		{
			(int, string) tuple = (((k.CampId != null && k.CampId == dead.CampId) || k.BossId == dead.Id || dead.BossId == k.Id || (k.BossId != null && k.BossId == dead.BossId)) ? (H(2, 8), "his crew carried it") : (H(6, 24), "word through the outfit"));
			return (minutes: tuple.Item1, why: tuple.Item2);
		}
		if (k.HasOrigin && pos.HasValue && new Vector3(k.OriginX - pos.Value.X, k.OriginY - pos.Value.Y, 0f).Length() < 800f)
		{
			return (minutes: H(4, 16), why: "it happened near his home");
		}
		return (minutes: H(24, 72), why: "word travels slow");
		int H(int lo, int hi)
		{
			return (lo + _rng.Next(hi - lo + 1)) * 60 + _rng.Next(60);
		}
	}

	private void UpdateDeathNews()
	{
		if (Game.GameTime < _nextNewsCheck)
		{
			return;
		}
		_nextNewsCheck = Game.GameTime + 5000;
		UpdatePeopleNews();
		List<DeathNews> deathNews = Chronicle.DeathNews;
		if (deathNews == null || deathNews.Count == 0)
		{
			return;
		}
		long now = GameMinutesNow();
		List<DeathNews> list = (from n in deathNews
			where n.DueAt <= now
			orderby n.DueAt
			select n).ToList();
		if (list.Count == 0)
		{
			return;
		}
		foreach (DeathNews item in list)
		{
			deathNews.Remove(item);
			DeliverNews(item);
		}
		SaveWorld();
	}

	private void DeliverNews(DeathNews n)
	{
		NemesisRecord nemesisRecord = FindRecord(n.KinId);
		NemesisRecord nemesisRecord2 = FindRecord(n.DeadId);
		if (nemesisRecord == null || !nemesisRecord.IsAlive || nemesisRecord2 == null)
		{
			GrudgeLog("NEWS of " + n.DeadName + " dropped (he's gone)");
			return;
		}
		if (nemesisRecord2.IsAlive && !nemesisRecord2.PresumedDead)
		{
			GrudgeLog("NEWS of " + n.DeadName + " dropped (he's alive)");
			return;
		}
		if (n.DueAt > n.At)
		{
			GrudgeLog($"NEWS {nemesisRecord.FullName} hears of {nemesisRecord2.FullName}'s death ({(float)(GameMinutesNow() - n.At) / 60f:0.#} h after)");
		}
		if (n.ByYou)
		{
			KinReact(nemesisRecord2, nemesisRecord.Id);
			return;
		}
		OffscreenGrief(nemesisRecord2, n.By, (n.EventId != null) ? Chronicle.Events.FirstOrDefault((WorldEvent e) => e.Id == n.EventId) : null, nemesisRecord.Id, n.DueAt > n.At);
	}

	internal string DeathNewsLine()
	{
		List<DeathNews> deathNews = Chronicle.DeathNews;
		if (deathNews == null || deathNews.Count == 0)
		{
			return "death news: nothing on the road";
		}
		long num = GameMinutesNow();
		DeathNews deathNews2 = deathNews.OrderBy((DeathNews n) => n.DueAt).First();
		return string.Format("death news: {0} on the road - next: {1} hears of {2} in {3:0.#} h", deathNews.Count, FindRecord(deathNews2.KinId)?.Name ?? "?", deathNews2.DeadName, (float)Math.Max(0L, deathNews2.DueAt - num) / 60f);
	}
}
