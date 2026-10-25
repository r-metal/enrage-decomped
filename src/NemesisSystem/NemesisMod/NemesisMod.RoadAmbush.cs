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
	private sealed class RoadAmbush
	{
		public NemesisRecord Rec;

		public string Town;

		public int Until;

		public int SpawnAt = -1;
	}

	private readonly Dictionary<string, int> _townCooldown = new Dictionary<string, int>();

	private void UpdateRoadAmbush(Ped player)
	{
		RoadAmbush ra = _roadAmbush;
		if (ra == null)
		{
			return;
		}
		Vector3 centre;
		float radius;
		if (Game.GameTime > ra.Until || !ra.Rec.IsAlive || ra.Rec.PresumedDead || !Roster.Contains(ra.Rec) || !HasReason(ra.Rec))
		{
			GrudgeLog("TOWNCALM " + ra.Rec.Name + " gave up waiting outside " + ra.Town);
			_roadAmbush = null;
		}
		else if (_currentTown != null || !TownCore(ra.Town, out centre, out radius) || !(player.Position.DistanceTo2D(centre) > radius + 120f))
		{
			ra.SpawnAt = -1;
		}
		else
		{
			if (_enc != null || _menu.IsOpen || NoSpawnReason(player) != null)
			{
				return;
			}
			if (TownCore(ra.Town, out var centre2, out var radius2) && player.Position.DistanceTo2D(centre2) > radius2 + 1500f)
			{
				GrudgeLog("TOWNCALM " + ra.Rec.Name + " waited outside " + ra.Town + ", but you never rode out that way");
				_roadAmbush = null;
			}
			else if (ra.SpawnAt < 0)
			{
				ra.SpawnAt = Game.GameTime + 15000 + _rng.Next(35000);
			}
			else
			{
				if (Game.GameTime < ra.SpawnAt)
				{
					return;
				}
				foreach (int item in (from kv in _worldNemeses
					where kv.Value == ra.Rec
					select kv.Key).ToList())
				{
					Ped ped = Entity.FromHandle(item) as Ped;
					if (ped != null && ped.Exists() && ped.Position.DistanceTo(player.Position) < 120f)
					{
						return;
					}
					if (ped != null && ped.Exists() && SafeToDelete(ped))
					{
						ped.Delete();
					}
					_worldNemeses.Remove(item);
				}
				bool flag = PED.IS_PED_ON_MOUNT(player.Handle);
				string stage = (flag ? "ahead" : "idle");
				EncounterCard encounterCard = Cards.FirstOrDefault((EncounterCard c) => c.Enabled && c.Id == stage) ?? BuiltInCards().First((EncounterCard c) => c.Id == stage);
				_roadAmbush = null;
				GrudgeLog("TOWNCALM " + ra.Rec.Name + " was waiting outside " + ra.Town + ": " + stage);
				History(ra.Rec, "WAITED FOR YOU outside " + ra.Town, Theme.Blood);
				Feed("ENCOUNTER " + ra.Rec.Name + " was waiting for you outside " + ra.Town, Theme.Blood, ra.Rec);
				SpawnEncounter(ra.Rec, StageKind(encounterCard.Stage), player, encounterCard);
				if (ActiveRecord != ra.Rec)
				{
					GrudgeLog("TOWNCALM " + ra.Rec.Name + "'s road ambush failed to spawn (" + encounterCard.Id + ") - dropped");
				}
			}
		}
	}
}
