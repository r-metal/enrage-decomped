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
	private const uint StAmbarino = 4073907364u;

	private const uint StLemoyne = 10837344u;

	private const uint StNewHanover = 3005831075u;

	private const uint StWestEliz = 1246494439u;

	private const uint StUpperWestEliz = 3047819085u;

	private const uint StLowerWestEliz = 2321575796u;

	private const uint StNewAustin = 2045157995u;

	private static readonly HashSet<uint> KlanDistricts = new HashSet<uint> { 3430691604u, 687704893u, 1308232528u, 3824751532u };

	private static readonly Dictionary<uint, uint> DistrictState = new Dictionary<uint, uint>
	{
		{ 1835499550u, 3005831075u },
		{ 131399519u, 3005831075u },
		{ 178647645u, 3005831075u },
		{ 2311828277u, 4073907364u },
		{ 4174810561u, 4073907364u },
		{ 2721678695u, 4073907364u },
		{ 1645618177u, 4073907364u },
		{ 2025841068u, 10837344u },
		{ 3374368891u, 10837344u },
		{ 3824751532u, 10837344u },
		{ 1308232528u, 10837344u },
		{ 687704893u, 10837344u },
		{ 3430691604u, 10837344u },
		{ 822658194u, 1246494439u },
		{ 3485840081u, 1246494439u },
		{ 210917480u, 1246494439u },
		{ 476637847u, 1246494439u },
		{ 1684533001u, 1246494439u },
		{ 1706859953u, 1246494439u },
		{ 4241435510u, 2045157995u },
		{ 892930832u, 2045157995u },
		{ 4186119282u, 2045157995u },
		{ 736160934u, 2045157995u },
		{ 3946348553u, 2045157995u },
		{ 2228727054u, 2045157995u },
		{ 2148975167u, 2045157995u },
		{ 779743808u, 2045157995u }
	};

	private const uint DiCumberland = 1835499550u;

	private uint _regionState;

	private uint _regionDistrict;

	private uint _regionPrinted;

	private uint _rawState;

	private bool _nearFort;

	private int _nextRegionCheck;

	private uint _lastLoggedState;

	private uint _lastLoggedDistrict;

	private void UpdateRegion(Ped player)
	{
		if (Game.GameTime >= _nextRegionCheck)
		{
			_nextRegionCheck = Game.GameTime + 2000;
			Vector3 position = player.Position;
			_rawState = ZONE._GET_MAP_ZONE_AT_COORDS(position.X, position.Y, position.Z, 0);
			_regionDistrict = ZONE._GET_MAP_ZONE_AT_COORDS(position.X, position.Y, position.Z, 10);
			_regionPrinted = ZONE._GET_MAP_ZONE_AT_COORDS(position.X, position.Y, position.Z, 11);
			_regionState = (KnownState(_rawState) ? _rawState : (DistrictState.TryGetValue(_regionDistrict, out var value) ? value : _rawState));
			_nearFort = _regionPrinted == 4195085991u || _regionDistrict == 1835499550;
			if (_regionState != _lastLoggedState || _regionDistrict != _lastLoggedDistrict)
			{
				_lastLoggedState = _regionState;
				_lastLoggedDistrict = _regionDistrict;
				GrudgeLog(string.Format("REGION {0}{1} - state 0x{2:X8} district 0x{3:X8} printed 0x{4:X8}", RegionName, _nearFort ? " (Army ground)" : "", _rawState, _regionDistrict, _regionPrinted));
			}
		}
	}

	private static bool KnownState(uint h)
	{
		if (h != 4073907364u && h != 10837344 && h != 3005831075u && h != 1246494439 && h != 3047819085u && h != 2321575796u)
		{
			return h == 2045157995;
		}
		return true;
	}

	internal string RegionRaw => $"state 0x{_rawState:X8} district 0x{_regionDistrict:X8} printed 0x{_regionPrinted:X8}";

	internal string RegionName
	{
		get
		{
			if (_regionState != 4073907364u)
			{
				if (_regionState != 10837344)
				{
					if (_regionState != 3005831075u)
					{
						if (_regionState != 1246494439 && _regionState != 3047819085u && _regionState != 2321575796u)
						{
							if (_regionState != 2045157995)
							{
								return "?";
							}
							return "New Austin";
						}
						return "West Elizabeth";
					}
					return "New Hanover";
				}
				return "Lemoyne";
			}
			return "Ambarino";
		}
	}

	private bool North
	{
		get
		{
			if (_regionState != 4073907364u)
			{
				return _regionState == 3005831075u;
			}
			return true;
		}
	}

	private bool South => _regionState == 10837344;

	internal double RegionMult(Gang g)
	{
		if (g == null)
		{
			return 1.0;
		}
		District currentDistrict = CurrentDistrict;
		if (currentDistrict != null)
		{
			if (g.OnlyDistricts != null && !g.OnlyDistricts.Contains(_regionDistrict))
			{
				return 0.0;
			}
			if (_nearFort && g.WeightNearFort >= 0.0)
			{
				return g.WeightNearFort;
			}
			return HoldMult(currentDistrict, g);
		}
		if (g.OnlyDistricts != null)
		{
			if (g.OnlyDistricts.Contains(_regionDistrict))
			{
				if (g.DistrictWeights == null || !g.DistrictWeights.TryGetValue(_regionDistrict, out var value))
				{
					return g.DistrictWeight;
				}
				return value;
			}
			return 0.0;
		}
		if (_nearFort && g.WeightNearFort >= 0.0)
		{
			return g.WeightNearFort;
		}
		if (!North)
		{
			if (!South)
			{
				return g.WeightElsewhere;
			}
			return g.WeightSouth;
		}
		return g.WeightNorth;
	}

	internal int BountyDollars => LAW.GET_BOUNTY(Game.Player.Handle) / 100;

	internal bool ArmyAfterYou => ReasonMet(Gangs.Army);

	internal bool HasReason(NemesisRecord n)
	{
		Gang gang = ((n == null || n.Gang == null) ? null : Gangs.ById(n.Gang));
		if (gang != null && gang.NeedsReason && n.Origin == GrudgeOrigin.GangMember)
		{
			return ReasonMet(gang);
		}
		return true;
	}

	internal bool ReasonMet(Gang g)
	{
		if (g != null && g.NeedsReason && NotorietyOf(g) < Balance.ArmyNotorietyGate)
		{
			if (g.ReasonFromBounty)
			{
				return BountyDollars >= Balance.ArmyBountyGate;
			}
			return false;
		}
		return true;
	}

	internal int CrewBonus(Gang g)
	{
		if (g != null)
		{
			return Math.Max(0, Notoriety.Level(NotorietyOf(g)) - 2);
		}
		return 0;
	}
}
