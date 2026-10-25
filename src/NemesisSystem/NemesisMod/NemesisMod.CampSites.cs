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
	private static readonly HashSet<int> CampSocialTypes = new HashSet<int>
	{
		3, 4, 7, 8, 16, 17, 19, 20, 21, 37,
		38
	};

	private const float CampDespawnRange = 260f;

	private const float CampDiscoverRange = 90f;

	private const float CampEngageRange = 35f;

	private const int MaxCampPeds = 9;

	private const uint BlipStyleCampEnemy = 2339406903u;

	private const ulong SetBlipNameHash = 11290983183885202434uL;

	private static readonly string[] CampScenarios = new string[7] { "WORLD_HUMAN_FIRE_SIT", "WORLD_HUMAN_FIRE_SIT", "WORLD_HUMAN_FIRE_STAND", "WORLD_HUMAN_GUARD_SCOUT", "WORLD_HUMAN_GUARD_LAZY", "WORLD_HUMAN_DRINKING", "WORLD_HUMAN_CROUCH_INSPECT" };

	private readonly Dictionary<string, LiveCamp> _liveCamps = new Dictionary<string, LiveCamp>();

	private readonly Dictionary<string, int> _campBlips = new Dictionary<string, int>();

	private readonly Dictionary<string, string> _campBlipSigs = new Dictionary<string, string>();

	private uint _campGroup;

	private readonly Dictionary<string, uint> _factionCampGroups = new Dictionary<string, uint>();

	private int _nextCampCheck;

	private int _nextCampAssign;

	private string _gpsCamp;

	private readonly Dictionary<string, bool> _wetSite = new Dictionary<string, bool>();

	private bool _wetAudited;

	private readonly Dictionary<string, bool> _inAnyTown = new Dictionary<string, bool>();

	private readonly Dictionary<string, Vector3> _streetCentres = new Dictionary<string, Vector3>();

	private readonly Dictionary<string, int> _noStreetUntil = new Dictionary<string, int>();

	private readonly Dictionary<string, int> _streetWaitSince = new Dictionary<string, int>();

	private const int StreetWaitMs = 60000;

	private readonly Dictionary<string, bool> _townSite = new Dictionary<string, bool>();

	private string CampKeyOf(int handle)
	{
		if (handle == 0 || !ENTITY.DOES_ENTITY_EXIST(handle) || !ENTITY.IS_ENTITY_A_PED(handle))
		{
			return null;
		}
		uint num = ENTITY.GET_ENTITY_MODEL(handle);
		foreach (KeyValuePair<string, (string, int)> campPerson in CampPeople)
		{
			if (MISC.GET_HASH_KEY(campPerson.Value.Item1) == num)
			{
				return campPerson.Key;
			}
		}
		return null;
	}

	private void CampPrompt(int target, int type)
	{
		string text = CampKeyOf(target);
		if (text == null)
		{
			return;
		}
		switch (type)
		{
		case 3:
		case 38:
			if (RegardOnce(text, "greet", 300000))
			{
				AddRegard(text, 1, "you said hello");
			}
			return;
		case 4:
		case 17:
		case 37:
			AddRegard(text, -4, "you antagonized them");
			return;
		}
		if (!CampSocialTypes.Contains(type))
		{
			GrudgeLog($"CAMP prompt type {type} on {text} - counted as a favour");
			if (RegardOnce(text, "favour", 600000))
			{
				AddRegard(text, 6, "you did what they asked (camp request)");
			}
		}
	}

	private uint CampGroupOf(LiveCamp lc)
	{
		string text = lc?.Boss?.Gang ?? lc?.Site?.Gang;
		if (text == null || text == Gangs.ODriscolls.Id)
		{
			if (_campGroup == 0)
			{
				_campGroup = World.AddRelationshipGroup("NEMESIS_CAMP");
			}
			return _campGroup;
		}
		if (!_factionCampGroups.TryGetValue(text, out var value))
		{
			value = (_factionCampGroups[text] = World.AddRelationshipGroup("NEMESIS_CAMP_" + text.ToUpperInvariant()));
		}
		return value;
	}

	private static Vector3 SitePos(CampSite s)
	{
		return new Vector3(s.X, s.Y, s.Z);
	}

	private unsafe bool WetSite(CampSite s)
	{
		if (s == null)
		{
			return false;
		}
		if (_wetSite.TryGetValue(s.Id, out var value))
		{
			return value;
		}
		float num = 0f;
		value = WATER.GET_WATER_HEIGHT(s.X, s.Y, s.Z + 2f, &num) && num > s.Z + 0.5f;
		_wetSite[s.Id] = value;
		if (value)
		{
			GrudgeLog($"CAMP site {s.Id} ({s.Label}) is in the water ({num - s.Z:0.0} m deep at {s.X:0},{s.Y:0}) - no lie will point there");
		}
		return value;
	}

	internal bool SiteInAnyTown(CampSite s)
	{
		if (s == null || s.Hideout || s.Imap != 0)
		{
			return false;
		}
		if (_inAnyTown.TryGetValue(s.Id, out var value))
		{
			return value;
		}
		Vector3 vector = SitePos(s);
		return _inAnyTown[s.Id] = TownAt(vector) != null || InsideAnyTown(vector);
	}

	private static bool Roofed(Vector3 g)
	{
		return Ray.Blocked(g + new Vector3(0f, 0f, 0.4f), g + new Vector3(0f, 0f, 12f), -1, 0);
	}

	internal bool TownSiteReady(CampSite site)
	{
		if (SiteInAnyTown(site))
		{
			return TownCampCentre(site).HasValue;
		}
		return true;
	}

	internal unsafe Vector3? TownCampCentre(CampSite site)
	{
		if (_streetCentres.TryGetValue(site.Id, out var value))
		{
			return value;
		}
		if (_noStreetUntil.TryGetValue(site.Id, out var value2) && Game.GameTime < value2)
		{
			return null;
		}
		Vector3 vector = SitePos(site);
		float heading;
		Vector3? vector2 = RoadNodeNear(vector, 120f, out heading);
		if (!vector2.HasValue)
		{
			NoStreet(site, "no road within 120 m");
			return null;
		}
		float street = vector2.Value.Z;
		float num = default(float);
		if (!MISC.GET_GROUND_Z_FOR_3D_COORD(vector.X, vector.Y, Math.Max(vector.Z, street) + 50f, &num, p4: false))
		{
			STREAMING.REQUEST_COLLISION_AT_COORD(vector.X, vector.Y, street);
			if (!_streetWaitSince.TryGetValue(site.Id, out var value3))
			{
				_streetWaitSince[site.Id] = Game.GameTime;
			}
			else if (Game.GameTime - value3 > 60000)
			{
				_streetWaitSince.Remove(site.Id);
				NoStreet(site, "its ground never loaded");
			}
			return null;
		}
		_streetWaitSince.Remove(site.Id);
		Vector3? result = Try(vector);
		int num2 = 0;
		while (!result.HasValue && num2 < 5)
		{
			float num3 = (new float[5] { 8f, 14f, 20f, 28f, 36f })[num2];
			for (int i = 0; i < 12; i++)
			{
				if (result.HasValue)
				{
					break;
				}
				double num4 = (double)i * Math.PI / 6.0 + (double)num2 * 0.26;
				result = Try(vector + new Vector3((float)Math.Cos(num4) * num3, (float)Math.Sin(num4) * num3, 0f));
			}
			num2++;
		}
		if (!result.HasValue)
		{
			Vector3 safeCoordForPed = World.GetSafeCoordForPed(vector2.Value, onGround: true, 0);
			if ((safeCoordForPed.X != 0f || safeCoordForPed.Y != 0f) && safeCoordForPed.DistanceTo2D(vector2.Value) < 15f && Math.Abs(safeCoordForPed.Z - street) <= 2.5f && Outdoors(safeCoordForPed) && !Roofed(safeCoordForPed))
			{
				result = safeCoordForPed;
			}
		}
		if (!result.HasValue)
		{
			NoStreet(site, $"no street-level ground within 36 m of it or by its road (street {street:0.0}, the site {vector.Z:0.0})");
			return null;
		}
		_streetCentres[site.Id] = result.Value;
		GrudgeLog($"CAMP {site.Id}: in town - its men stand at street level, {result.Value.DistanceTo2D(vector):0} m from the site (street {street:0.0}, the site {vector.Z:0.0})");
		return result;
		unsafe Vector3? Try(Vector3 c)
		{
			float num5 = default(float);
			if (!MISC.GET_GROUND_Z_FOR_3D_COORD(c.X, c.Y, street + 3f, &num5, p4: false) || Math.Abs(num5 - street) > 2.5f)
			{
				return null;
			}
			Vector3 vector3 = new Vector3(c.X, c.Y, num5);
			if (!Outdoors(vector3) || !Dry(vector3) || Roofed(vector3) || Raised(vector3))
			{
				return null;
			}
			return vector3;
		}
	}

	private void NoStreet(CampSite site, string why)
	{
		bool num = !_noStreetUntil.ContainsKey(site.Id);
		_noStreetUntil[site.Id] = Game.GameTime + 120000;
		if (num)
		{
			GrudgeLog("CAMP " + site.Id + " (" + site.Label + "): in town, " + why + " - it doesn't come (place it again)");
		}
	}

	private bool SiteInTown(CampSite s)
	{
		if (s == null)
		{
			return false;
		}
		if (_townSite.TryGetValue(s.Id, out var value))
		{
			return value;
		}
		string text = TownAt(SitePos(s));
		value = (_townSite[s.Id] = text == "Saint Denis");
		if (value)
		{
			GrudgeLog("CAMP site " + s.Id + " (" + s.Label + ") is in " + text + " - never a supply camp");
		}
		return value;
	}

	private void AuditWetSites()
	{
		if (_wetAudited || !MapSettled())
		{
			return;
		}
		_wetAudited = true;
		int num = 0;
		foreach (CampSite allSite in Camps.AllSites)
		{
			if (WetSite(allSite))
			{
				num++;
			}
		}
		GrudgeLog($"CAMP water check: {num} of {Camps.AllSites.Count()} camp sites are in the water");
	}

	internal static bool IsOfficer(NemesisRecord n)
	{
		return n.Rank >= 2;
	}

	internal bool CantWalk(int h)
	{
		if (_bulletImpact != null)
		{
			return _bulletImpact.CrippleLevelOf(h) >= CrippleLevel.Legless;
		}
		return false;
	}

	internal static bool Mans(NemesisRecord n)
	{
		if (n != null && IsOfficer(n) && n.CanHunt && n.IsAlive)
		{
			return !n.PresumedDead;
		}
		return false;
	}
}
