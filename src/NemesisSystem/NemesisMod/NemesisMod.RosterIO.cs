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
	private bool _rosterDirty;

	private int _nextRosterSave;

	private volatile string _rosterWriteError;

	private bool _rosterLoadFailed;

	private bool _rosterSaveFailedShown;

	private void MoveForeignXRays()
	{
		HashSet<string> hashSet = new HashSet<string>(from r in Roster
			where r.Id != null && (r.Origin != GrudgeOrigin.GangMember || (r.Scars?.Count ?? 0) > 0 || !r.IsAlive)
			select r.Id);
		try
		{
			List<NemesisRecord> list = SafeFile.Read<List<NemesisRecord>>(Path.Combine(_saveDir, "fallen.json"));
			if (list != null)
			{
				foreach (NemesisRecord item in list)
				{
					if (item != null && item.Id != null)
					{
						hashSet.Add(item.Id);
					}
				}
			}
		}
		catch
		{
		}
		if (hashSet.Count != 0)
		{
			string text = Path.Combine(_saveDir, $"xray_log.other-world-{DateTime.Now:yyyyMMdd-HHmmss}.json");
			int men;
			int num = _bulletImpact.MoveForeignAside(hashSet.Contains, text, out men);
			if (num > 0)
			{
				GrudgeLog($"XRAY {num} shots on {men} men from another playthrough moved out of this world's X-ray log ({Path.GetFileName(text)})");
			}
		}
	}

	private void LoadRoster()
	{
		bool found;
		string from;
		List<NemesisRecord> list = SafeFile.Read<List<NemesisRecord>>(_savePath, GrudgeLog, out found, out from);
		if (list == null && found)
		{
			_rosterLoadFailed = true;
			GrudgeLog("LOAD every copy of nemeses.json is unreadable - the roster won't be saved this session (files kept as .unreadable-*)");
		}
		_rosterGen++;
		ReapplyLeaders();
		_leaderDupesCleaned = false;
		if (list != null)
		{
			Roster = list.Where((NemesisRecord r) => r != null).ToList();
		}
		if (from != null && from != _savePath)
		{
			ShowNotification("enRAGE: nemeses.json was unreadable - loaded the backup (" + Path.GetFileName(from) + ").");
			SaveRoster();
		}
		if (from == null && found)
		{
			ShowNotification("enRAGE: your nemeses couldn't be read - nothing will be saved over them this session. The files are kept in the enRAGE folder (.unreadable-*).");
		}
		if (from == _savePath && SafeFile.Daily(_savePath, Path.Combine(_saveDir, "backups")))
		{
			GrudgeLog("LOAD daily roster backup written");
		}
		int num = 0;
		foreach (NemesisRecord item in Roster.ToList())
		{
			try
			{
				MigrateRecord(item);
			}
			catch (Exception ex)
			{
				num++;
				GrudgeLog("LOAD " + item.Name + " (" + item.Id + ") migration failed - kept as is: " + ex.Message);
			}
		}
		if (num > 0)
		{
			ShowNotification(string.Format("{0}: {1} records couldn't be updated (see grudges.log).", "enRAGE", num));
		}
		ArchiveFallen();
	}

	private void MigrateRecord(NemesisRecord rec)
	{
		rec.Scars = rec.Scars ?? new List<Scar>();
		rec.EnsureHate();
		rec.EnsureId();
		if (rec.Gang == null)
		{
			rec.Gang = Gangs.ForModel(rec.ModelHash)?.Id;
		}
		if (!rec.TraitsRolled)
		{
			Traits.Roll(rec, _rng, null);
			_personalitiesAssigned = true;
		}
		rec.Past = rec.Past ?? new List<PastEntry>();
		if (!rec.Personality.HasValue)
		{
			rec.Personality = Personalities.Pick(_rng, rec.Gang != null);
			if (rec.Gang == null && rec.TimesKilledPlayer == 0 && rec.Origin != GrudgeOrigin.Killed)
			{
				rec.Hate = Math.Max(10, Math.Min(100, rec.Hate + Personalities.StartingHate(rec.Personality.Value, rec.Origin) - Hate.Initial(rec.Origin)));
			}
			_personalitiesAssigned = true;
		}
		if (!rec.PastRolled)
		{
			EnsurePast(rec);
			_personalitiesAssigned = true;
		}
		if (!rec.MindRolled)
		{
			EnsureMind(rec);
			_personalitiesAssigned = true;
		}
		if (rec.IsAlive && rec.KinId != null && rec.PastRolled)
		{
			NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == rec.KinId);
			if (nemesisRecord != null && !Backstory.KinAgeFits(rec, nemesisRecord))
			{
				int bornYear = rec.BornYear;
				Backstory.FitKinAge(rec, nemesisRecord, Seeds.For(rec, "kin"), GameYear());
				Backstory.Roll(rec, Seeds.For(rec, "life"), GameYear(), GameMonth(), derive: false);
				rec.Woven = false;
				GrudgeLog($"HISTORY {rec.Name}: {rec.KinRelation} of {nemesisRecord.Name} was born {bornYear} (his {nemesisRecord.BornYear}) - now {rec.BornYear}, past rewritten");
				_personalitiesAssigned = true;
			}
		}
	}

	private void ArchiveFallen()
	{
		try
		{
			List<NemesisRecord> list = Roster.Where((NemesisRecord n) => !n.IsAlive && !n.PresumedDead).ToList();
			if (list.Count <= 200)
			{
				return;
			}
			List<NemesisRecord> living = Roster.Where((NemesisRecord n) => n.IsAlive).ToList();
			List<NemesisRecord> list2 = (from d in list
				where !Tied(d)
				orderby d.LastDealt
				select d).Take(list.Count - 200).ToList();
			if (list2.Count == 0)
			{
				return;
			}
			string path = Path.Combine(_saveDir, "fallen.json");
			List<NemesisRecord> list3 = SafeFile.Read<List<NemesisRecord>>(path, GrudgeLog, out var found, out var _);
			if (list3 == null && found)
			{
				GrudgeLog("LOAD fallen.json unreadable - kept aside, nothing archived this time");
				return;
			}
			list3 = list3 ?? new List<NemesisRecord>();
			HashSet<string> ids = new HashSet<string>(from d in list2
				select d.Id into i
				where i != null
				select i);
			int num = list3.RemoveAll((NemesisRecord a) => a != null && a.Id != null && ids.Contains(a.Id));
			list3.AddRange(list2);
			list3 = (from a in list3
				where a != null
				group a by a.Id ?? Guid.NewGuid().ToString() into g
				select g.Last()).ToList();
			if (num > 0)
			{
				GrudgeLog($"LOAD fallen.json: {num} men already there replaced (no duplicates)");
			}
			SafeFile.Write(path, JsonConvert.SerializeObject(list3), keepBak: true);
			foreach (NemesisRecord item in list2)
			{
				Roster.Remove(item);
			}
			GrudgeLog($"LOAD archived {list2.Count} long-dead men to fallen.json ({list3.Count} there now)");
			SaveRoster();
			bool Tied(NemesisRecord d)
			{
				if (d.TimesKilledPlayer <= 0 && (d.Id == null || (!d.Id.StartsWith("fallen_") && !d.Id.StartsWith("lore_"))))
				{
					return living.Any(delegate(NemesisRecord l)
					{
						if (!(l.KinId == d.Id) && !(d.KinId == l.Id))
						{
							List<string> friendIds = l.FriendIds;
							if (friendIds == null || !friendIds.Contains(d.Id))
							{
								return l.BossId == d.Id;
							}
						}
						return true;
					});
				}
				return true;
			}
		}
		catch (Exception ex)
		{
			GrudgeLog("LOAD fallen archive failed: " + ex.Message);
		}
	}

	private void SaveRoster()
	{
		_rosterDirty = true;
	}

	internal void FlushRosterSave(bool force = false, bool sliced = false)
	{
		if (!_rosterDirty || (!force && Game.GameTime < _nextRosterSave) || (sliced && !force && Slicing("roster")))
		{
			return;
		}
		_nextRosterSave = Game.GameTime + 60000;
		_lastRosterFlushAt = Game.GameTime;
		if (force)
		{
			_rosterGen++;
			if (SafeFile.WaitWrites())
			{
				if (SaveRosterNow())
				{
					_rosterDirty = false;
				}
				return;
			}
			if (_rosterLoadFailed)
			{
				_rosterDirty = false;
				return;
			}
			RefreshArmyNames();
			string text;
			try
			{
				text = JsonConvert.SerializeObject(Roster);
			}
			catch (Exception ex)
			{
				GrudgeLog("SAVE roster failed to serialize: " + ex.Message);
				return;
			}
			_rosterDirty = false;
			try
			{
				Directory.CreateDirectory(_saveDir);
			}
			catch
			{
			}
			SafeFile.WriteLater(_savePath, text, keepBak: true, delegate(Exception err)
			{
				if (err != null)
				{
					_rosterDirty = true;
					_rosterWriteError = err.Message;
				}
			});
			GrudgeLog("SAVE roster: the background writer is busy - queued behind it");
			return;
		}
		if (_rosterLoadFailed)
		{
			_rosterDirty = false;
			return;
		}
		RefreshArmyNames();
		if (sliced)
		{
			_rosterDirty = false;
			try
			{
				Directory.CreateDirectory(_saveDir);
			}
			catch
			{
			}
			StartSlice("roster", _savePath, Roster, _rosterGen, delegate(Exception err)
			{
				if (err != null)
				{
					_rosterDirty = true;
					_rosterWriteError = err.Message;
				}
			});
			return;
		}
		_rosterGen++;
		string text2;
		try
		{
			text2 = JsonConvert.SerializeObject(Roster);
		}
		catch (Exception ex2)
		{
			GrudgeLog("SAVE roster failed to serialize: " + ex2.Message);
			return;
		}
		_rosterDirty = false;
		try
		{
			Directory.CreateDirectory(_saveDir);
		}
		catch
		{
		}
		SafeFile.WriteLater(_savePath, text2, keepBak: true, delegate(Exception err)
		{
			if (err != null)
			{
				_rosterDirty = true;
				_rosterWriteError = err.Message;
			}
		});
	}

	private bool SaveRosterNow()
	{
		if (_rosterLoadFailed)
		{
			GrudgeLog("SAVE roster skipped - it failed to load this session (nemeses.json left as it is)");
			return true;
		}
		RefreshArmyNames();
		try
		{
			Directory.CreateDirectory(_saveDir);
			SafeFile.Write(_savePath, JsonConvert.SerializeObject(Roster), keepBak: true);
			if (_rosterSaveFailedShown)
			{
				GrudgeLog("SAVE roster written again");
			}
			_rosterSaveFailedShown = false;
			return true;
		}
		catch (Exception ex)
		{
			GrudgeLog("SAVE roster failed (tried again on the next pass): " + ex.Message);
			if (!_rosterSaveFailedShown)
			{
				ShowNotification("enRAGE save failed: " + ex.Message + " - trying again.");
			}
			_rosterSaveFailedShown = true;
			return false;
		}
	}
}
