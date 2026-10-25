using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NemesisSystem.BulletImpact;

public sealed class XRayEntry
{
	public int Id;

	public int ShootoutId;

	public DateTime Time;

	public long GameMin;

	public string Location;

	public string Victim;

	public uint VictimModel;

	public int VictimId;

	public string NemesisId;

	public string NemesisName;

	public string FollowerOf;

	public uint WeaponHash;

	public string WeaponName;

	[JsonConverter(typeof(StringEnumConverter))]
	public WeaponClass WeaponClass;

	public int Pellets;

	[JsonConverter(typeof(StringEnumConverter))]
	public AmmoKind Ammo;

	public float Distance;

	public int RecordedBoneId;

	public bool ImpactOnVictim;

	public V3 Entry;

	public V3 Dir;

	public V3 Muzzle;

	public List<BonePoint> Bones = new List<BonePoint>();

	public string Effect;

	public bool Killed;

	public bool FatalShot;

	public bool Animal;

	public List<Blow> Blows;

	public bool KnockedOut;

	public bool LeftForDead;

	public bool StillUp;

	public float HitShare;

	[JsonIgnore]
	public int PedHandle;

	[JsonIgnore]
	public XRayAnalysis Analysis;

	public XRayEntry Copy()
	{
		XRayEntry obj = (XRayEntry)MemberwiseClone();
		obj.Analysis = null;
		return obj;
	}
}
