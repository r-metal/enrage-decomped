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
	private static readonly string[] ChapterNames = new string[8] { "", "Colter", "Horseshoe Overlook", "Clemens Point", "Shady Belle", "Guarma", "Beaver Hollow", "the epilogue" };

	private int _nextStoryCheck;

	private bool _storyApplied;

	private readonly HashSet<string> _storySeenAlive = new HashSet<string>();

	private int _storyUnknownLogged;

	private int _nextSuccessionCheck;

	private long _lastClockStamp = -1L;

	internal int StoryChapter
	{
		get
		{
			if (Config.StoryChapterSet <= 0 || Chronicle.StoryChapter >= 7)
			{
				return Chronicle.StoryChapter;
			}
			return Config.StoryChapterSet;
		}
	}

	private void UpdateStoryProgress(Ped player)
	{
		if (Game.GameTime < _nextStoryCheck)
		{
			return;
		}
		_nextStoryCheck = Game.GameTime + 5000;
		Vector3 pos = player.Position;
		int num = 0;
		string text = null;
		if (Models.Name(player) == "player_three")
		{
			num = 7;
			text = "playing as John";
		}
		else if (pos.Y < -4000f)
		{
			num = 5;
			text = "Guarma";
		}
		else
		{
			if (_gangHashes == null)
			{
				_gangHashes = new HashSet<uint>(CampKeyOfModel.Keys);
			}
			List<Ped> list = (from ped in PedsAround(player)
				where ped != null && ped.Exists() && !ped.IsDead && !ped.IsPlayer && _gangHashes.Contains((uint)ped.Model.Hash) && !IsOurs(ped.Handle) && ped.Position.DistanceTo(pos) < 70f
				select ped).ToList();
			foreach (Ped p in list)
			{
				string key = CampPeople.FirstOrDefault((KeyValuePair<string, (string model, int willing)> kv) => MISC.GET_HASH_KEY(kv.Value.model) == (uint)p.Model.Hash).Key;
				if (key != null)
				{
					_storySeenAlive.Add(key);
				}
			}
			if (list.Count >= 3 && !InsideAnyTown(pos) && !MISC.GET_MISSION_FLAG())
			{
				((int, string, float, float), float) tuple = (from c in ChapterCamps
					select (c: c, d: new Vector3(pos.X - c.x, pos.Y - c.y, 0f).Length()) into t
					orderby t.d
					select t).First();
				if (tuple.Item2 < 400f)
				{
					num = tuple.Item1.Item1;
					text = tuple.Item1.Item2;
				}
				else if (_storyUnknownLogged++ < 3)
				{
					GrudgeLog($"STORY gang camp at {pos.X:0},{pos.Y:0} matches no chapter camp (nearest {tuple.Item1.Item2}, {tuple.Item2:0} m)");
				}
			}
		}
		if (num == 7 && Chronicle.Era < 1907 && !_holdTurn)
		{
			bool alreadyEpilogue = Chronicle.StoryChapter >= 7;
			try
			{
				EightYearsLater(text, debug: false, alreadyEpilogue);
				return;
			}
			catch (Exception ex)
			{
				GrudgeLog("EPILOGUE the turn failed: " + ex);
				return;
			}
		}
		bool flag = Config.StoryChapterSet > 0 && num != 7 && Chronicle.StoryChapter < 7;
		if (flag)
		{
			num = Config.StoryChapterSet;
		}
		if (num == 0 || num == Chronicle.StoryChapter)
		{
			return;
		}
		int storyChapter = Chronicle.StoryChapter;
		Dictionary<string, int> regBefore = ChapterRegard.Keys.ToDictionary((string k) => k, RegardOf);
		Chronicle.StoryChapter = num;
		Chronicle.StoryChapterAt = GameMinutesNow();
		SaveWorld();
		GrudgeLog(string.Format("STORY {0} -> chapter {1} (was {2})", flag ? "chapter set by you ([Calm] StoryChapter)" : $"camp seen at {pos.X:0},{pos.Y:0} ({text})", num, (storyChapter == 0) ? "unknown" : storyChapter.ToString()));
		ApplyStoryChapter(storyChapter != 0);
		if (num == 4 && Gangs.Era < 1907 && SeatOf(Gangs.SaintDenisMob)?.Fallen == null)
		{
			ShowNotification("enRAGE: Settings, Your story (O in the menu) is where you tell the mod what's become of the outfits' leaders in your story.");
			GrudgeLog("STORY chapter 4: told you where the setup card's Your story rows are (Angelo Bronte)");
		}
		List<(string k, int a, int b)> list2 = (from k in ChapterRegard.Keys
			select (k: k, a: regBefore[k], b: RegardOf(k)) into t
			where t.a != t.b
			select t).ToList();
		if (list2.Count > 0)
		{
			GrudgeLog($"REGARD the story moved them (chapter {RegardChapter}): " + string.Join(", ", list2.Select(((string k, int a, int b) t) => $"{Nm(t.k)} {t.a} -> {t.b}")));
			IEnumerable<string> values = from t in list2.OrderByDescending(((string k, int a, int b) t) => Math.Abs(t.b - t.a)).Take(3)
				select $"{Nm(t.k)} {t.a} to {t.b}";
			Feed("REGARD the camp's feelings moved with the story: " + string.Join(", ", values) + ((list2.Count > 3) ? $" (+{list2.Count - 3} more)" : ""), Theme.Dim);
		}
		string Nm(string k)
		{
			return StoryRoster.FirstOrDefault((NemesisRecord r) => r.Id == "vdl_" + k)?.Name ?? k;
		}
	}

	internal void ApplyStoryChapter(bool announce)
	{
		int storyChapter = Chronicle.StoryChapter;
		if (storyChapter <= 0)
		{
			return;
		}
		int year = GameYear();
		int month = GameMonth();
		List<string> list = new List<string>();
		(string, int, bool, string)[] storyFates = StoryFates;
		for (int i = 0; i < storyFates.Length; i++)
		{
			(string key, int ch, bool dead, string how) f = storyFates[i];
			NemesisRecord nemesisRecord = StoryRoster.FirstOrDefault((NemesisRecord x) => x.Id == "vdl_" + f.key);
			if (nemesisRecord == null)
			{
				continue;
			}
			bool flag = storyChapter >= f.ch && (!f.dead || !_storySeenAlive.Contains(f.key) || storyChapter >= 7);
			if (flag && nemesisRecord.IsAlive)
			{
				nemesisRecord.IsAlive = false;
				nemesisRecord.StoryGone = (f.dead ? null : (f.how.StartsWith("Left") ? "Left the gang" : "Gone"));
				nemesisRecord.Past = nemesisRecord.Past ?? new List<PastEntry>();
				if (!nemesisRecord.Past.Any((PastEntry e) => e.Kind == "fate"))
				{
					nemesisRecord.Past.Add(new PastEntry
					{
						Year = year,
						Month = month,
						Kind = "fate",
						Text = f.how,
						Public = true,
						Known = true
					});
				}
				list.Add(f.dead ? (nemesisRecord.Name + " is dead") : (nemesisRecord.Name + ": " + f.how.ToLowerInvariant()));
			}
			else if (!flag && !nemesisRecord.IsAlive && nemesisRecord.Past != null && nemesisRecord.Past.Any((PastEntry e) => e.Kind == "fate"))
			{
				nemesisRecord.IsAlive = true;
				nemesisRecord.StoryGone = null;
				nemesisRecord.Past.RemoveAll((PastEntry e) => e.Kind == "fate");
			}
		}
		if (storyChapter >= 7 && SeatOf(Gangs.ODriscolls)?.Fallen == null)
		{
			Seats[Gangs.ODriscolls.Id] = new LeaderSeat
			{
				Fallen = "hanged by the law",
				OldName = (Gangs.ODriscolls.LoreLeader ?? "Colm O'Driscoll"),
				At = GameMinutesNow()
			};
			ApplyLeaders();
			list.Add("Colm O'Driscoll was hanged");
		}
		if (storyChapter >= 5 && SeatOf(Gangs.SaintDenisMob)?.Fallen == null)
		{
			Seats[Gangs.SaintDenisMob.Id] = new LeaderSeat
			{
				Fallen = "fed to an alligator by Dutch van der Linde",
				OldName = (Gangs.SaintDenisMob.LoreLeader ?? "Angelo Bronte"),
				At = GameMinutesNow()
			};
			ApplyLeaders();
			list.Add("Angelo Bronte is dead - Dutch fed him to an alligator");
		}
		if (list.Count == 0)
		{
			return;
		}
		GrudgeLog(string.Format("STORY chapter {0} ({1}): {2}", storyChapter, ChapterNames[Math.Min(storyChapter, 7)], string.Join("; ", list)));
		if (announce)
		{
			if (Config.WorldEvents)
			{
				WorldEvent worldEvent = NewEvent("VDL", GameMinutesNow());
				worldEvent.Gangs.Add("vdl");
				worldEvent.Heard = true;
				worldEvent.Title = storyChapter switch
				{
					7 => "The Van der Linde gang is finished", 
					5 => "The gang washed up on Guarma", 
					_ => "The gang made camp at " + ChapterNames[storyChapter], 
				};
				worldEvent.Place = ((storyChapter == 7) ? "years later" : ("at " + ChapterNames[Math.Min(storyChapter, 7)]));
				worldEvent.Lines.AddRange(list);
				Chronicle.Events.Add(worldEvent);
			}
			Feed("STORY " + ChapterNames[Math.Min(storyChapter, 7)] + ": " + string.Join("; ", list), Theme.Gold);
			SaveWorld();
		}
	}

	internal string StoryCompanionBlock(string key)
	{
		int storyChapter = Chronicle.StoryChapter;
		if (storyChapter == 5)
		{
			return "Guarma";
		}
		if (storyChapter >= 7)
		{
			return "the epilogue";
		}
		NemesisRecord nemesisRecord = StoryRoster.FirstOrDefault((NemesisRecord x) => x.Id == "vdl_" + key);
		if (nemesisRecord == null || nemesisRecord.IsAlive)
		{
			return null;
		}
		return "the story has taken him";
	}

	internal string StoryDebugLine()
	{
		return "story: chapter " + ((Chronicle.StoryChapter == 0) ? "unknown (visit the gang's camp)" : $"{Chronicle.StoryChapter} - {ChapterNames[Math.Min(Chronicle.StoryChapter, 7)]}") + $"  -  {StoryRoster.Count((NemesisRecord r) => r.Story && r.IsAlive)} of the gang alive, {_storySeenAlive.Count} seen in camp";
	}

	private static int GameDayKey()
	{
		return GameDate.TodayKey();
	}
}
