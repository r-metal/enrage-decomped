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
	private bool _missionOn;

	private int _missionSince;

	private Vector3 _missionStartPos;

	private readonly Dictionary<string, (int seen, bool gang)> _missionCast = new Dictionary<string, (int, bool)>();

	private readonly List<Vector3> _missionTrail = new List<Vector3>();

	private int _missionTrailAt;

	private static readonly string[] VdlRumours = new string[8] { "Folks in {t} say the Van der Linde gang was seen on the road", "A stagecoach out of {t} was robbed - the driver swears it was Dutch's boys", "The Pinkertons were asking questions in {t} about Dutch van der Linde", "A bounty went up in {t} on a man who rides with Dutch", "Word in {t}: the Van der Linde gang is camped somewhere close", "Somebody shot up a saloon in {t} - folks blame Dutch's people", "The law in {t} doubled its deputies, on account of the Van der Linde gang", "A rancher near {t} says Dutch's men bought horses off him, paid in gold" };

	private static readonly string[] VdlRumours1907 = new string[11]
	{
		"Folks in {t} still tell stories about the Van der Linde gang - most of them wrong", "A drifter in {t} swears he saw Dutch van der Linde, riding alone and talking to himself", "Word in {t}: Micah Bell runs a gang of his own now, and nobody who crosses it lives", "A bounty hunter called Sadie Adler brought a wanted man in to {t} - dead", "An old wanted poster in {t} still names Arthur Morgan. Folks say he's been dead for years", "Somebody in {t} was asking after a rancher who calls himself Jim Milton", "Simon Pearson keeps a store in Rhodes now, and won't talk about the old days", "A man in {t} says Bill Williamson is gathering men out west", "Word in {t}: Javier Escuella went south to Mexico and never came back", "A book by a Mary-Beth Gaskill is selling in {t} - folks say she knew outlaws once",
		"They say in {t} the Pinkertons closed the Van der Linde file - and that somebody in Washington opened it again"
	};

	private bool _oldMissionDeedsCleared;

	private WorldEvent SimVanDerLinde(long now)
	{
		string[] array = new string[7] { "Valentine", "Rhodes", "Saint Denis", "Strawberry", "Annesburg", "Van Horn", "Blackwater" };
		string text = array[_rng.Next(array.Length)];
		bool num = Gangs.Era >= 1907;
		string text2 = (num ? VdlRumours1907[_rng.Next(VdlRumours1907.Length)] : VdlRumours[_rng.Next(VdlRumours.Length)]);
		if (!text2.Contains("{t}") && text2.Contains("Rhodes"))
		{
			text = "Rhodes";
		}
		WorldEvent worldEvent = NewEvent("VDL", now);
		worldEvent.Gangs.Add("vdl");
		worldEvent.Title = text2.Replace("{t}", text);
		worldEvent.Place = "in " + text;
		if (TownCore(text, out var centre, out var _))
		{
			worldEvent.HasPos = true;
			worldEvent.X = centre.X;
			worldEvent.Y = centre.Y;
			worldEvent.Z = centre.Z;
		}
		NemesisRecord nemesisRecord = (num ? null : (from _ in Roster
			where _.Gang == Gangs.ODriscolls.Id && _.IsAlive && OffStage(_)
			orderby _rng.Next()
			select _).FirstOrDefault());
		if (nemesisRecord != null && _rng.Next(100) < 50)
		{
			worldEvent.Gangs.Add(nemesisRecord.Gang);
			WorldPart worldPart = PartFor(nemesisRecord, "SWORE ON IT", "Dutch's boys will pay");
			worldEvent.People.Add(worldPart);
			AddThought(nemesisRecord, "Bitter", "Dutch van der Linde's gang", -2, 48, now, worldPart);
			int hate = nemesisRecord.Hate;
			nemesisRecord.Hate = Math.Min(100, nemesisRecord.Hate + 2);
			CapScuffle(nemesisRecord, hate);
			worldPart.Effects.Add($"hate {hate} -> {nemesisRecord.Hate}");
		}
		return worldEvent;
	}

	private void UpdateMissionDeeds(Ped player)
	{
		if (!_oldMissionDeedsCleared)
		{
			_oldMissionDeedsCleared = true;
			int num = Chronicle.Events.RemoveAll((WorldEvent e) => e.Kind == "YOU" && e.Title == "You rode with the gang");
			if (num > 0)
			{
				SaveWorld();
				GrudgeLog($"VDL {num} old 'You rode with the gang' entries taken out of the World tab");
			}
		}
		bool mISSION_FLAG = MISC.GET_MISSION_FLAG();
		if (mISSION_FLAG && !_missionOn)
		{
			_missionSince = Game.GameTime;
			_missionStartPos = player.Position;
			_missionTrail.Clear();
			_missionTrailAt = 0;
			_missionCast.Clear();
		}
		if (mISSION_FLAG && Game.GameTime - _missionTrailAt > 5000 && _missionTrail.Count < 400)
		{
			_missionTrail.Add(player.Position);
			_missionTrailAt = Game.GameTime;
			Vector3 position = player.Position;
			foreach (KeyValuePair<int, Named> item in _named.Where((KeyValuePair<int, Named> x) => x.Value.Story).ToList())
			{
				if (ENTITY.DOES_ENTITY_EXIST(item.Key) && !ENTITY.IS_ENTITY_DEAD(item.Key) && !(ENTITY.GET_ENTITY_COORDS(item.Key, alive: true, realCoords: true).DistanceTo(position) > 30f) && StoryCast.TryGet(ModelName(ENTITY.GET_ENTITY_MODEL(item.Key)), out var p) && !string.IsNullOrEmpty(p.Name))
				{
					_missionCast.TryGetValue(p.Name, out (int, bool) value);
					_missionCast[p.Name] = (value.Item1 + 1, p.Gang == "vdl");
				}
			}
		}
		if (!mISSION_FLAG && _missionOn)
		{
			ClearCampsTheMissionCleared(player);
		}
		if (!mISSION_FLAG && _missionOn && Game.GameTime - _missionSince > 90000 && Config.WorldEvents)
		{
			int num2 = (Game.GameTime - _missionSince) / 1000;
			List<KeyValuePair<string, (int seen, bool gang)>> list = (from x in _missionCast
				where x.Value.seen >= 2
				orderby x.Value.seen descending
				select x).ToList();
			List<string> list2 = (from x in list
				where x.Value.gang
				select x.Key).ToList();
			List<string> list3 = (from x in list
				where !x.Value.gang
				select x.Key).ToList();
			if (list.Count == 0)
			{
				GrudgeLog($"VDL mission over after {num2}s - no story character with you: not chronicled");
			}
			else
			{
				WorldEvent worldEvent = NewEvent("YOU", GameMinutesNow());
				worldEvent.Heard = true;
				if (list2.Count > 0)
				{
					worldEvent.Tag = "mission";
					worldEvent.Title = "Rode out with " + Two(list2);
					worldEvent.Gangs.Add("vdl");
				}
				else
				{
					worldEvent.Tag = "stranger";
					worldEvent.Title = "A stranger mission with " + Two(list3);
				}
				worldEvent.Place = "near " + GetZoneName(_missionStartPos);
				worldEvent.HasPos = true;
				worldEvent.X = _missionStartPos.X;
				worldEvent.Y = _missionStartPos.Y;
				worldEvent.Z = _missionStartPos.Z;
				List<string> list4 = list2.Concat(list3).ToList();
				if (list4.Count > 2)
				{
					worldEvent.Lines.Add("With " + string.Join(", ", list4.Take(list4.Count - 1).Take(5)) + " and " + list4.Last() + ".");
				}
				string zoneName = GetZoneName(_missionStartPos);
				string zoneName2 = GetZoneName(player.Position);
				worldEvent.Lines.Add(string.Format("{0} minutes{1}.", Math.Max(1, num2 / 60), (zoneName == zoneName2) ? (", around " + zoneName) : (", from " + zoneName + " to " + zoneName2)));
				Chronicle.Events.Add(worldEvent);
				SaveWorld();
				GrudgeLog(string.Format("VDL mission over after {0}s - chronicled: {1} ({2})", num2, worldEvent.Title, string.Join(", ", list.Select<KeyValuePair<string, (int, bool)>, string>((KeyValuePair<string, (int seen, bool gang)> x) => $"{x.Key} x{x.Value.seen}"))));
			}
		}
		_missionOn = mISSION_FLAG;
		static string Two(List<string> l)
		{
			if (l.Count != 1)
			{
				return l[0] + " and " + l[1];
			}
			return l[0];
		}
	}
}
