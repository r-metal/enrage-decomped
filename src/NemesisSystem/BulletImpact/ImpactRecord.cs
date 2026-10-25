using System.Collections.Generic;
using RDR2.Math;

namespace NemesisSystem.BulletImpact;

public sealed class ImpactRecord
{
	public int PedHandle;

	public uint PedModel;

	public bool VictimDeadAfterHit;

	public bool Animal;

	public int BoneId = -1;

	public int BoneIndex = -1;

	public string BoneName;

	public bool HasImpact;

	public Vector3 Impact;

	public Vector3 Muzzle;

	public string MuzzleSource;

	public uint WeaponHash;

	public string WeaponName;

	public AmmoKind Ammo;

	public bool IsShotgun;

	public float Damage;

	public int PelletsOnVictim;

	public int ShotId;

	public int EventIndex;

	public Vector3 EventCoord;

	public float Distance;

	public int GameTime;

	public WeaponClass Class;

	public List<BonePoint> BonesLocal = new List<BonePoint>();

	public V3 EntryLocal;

	public V3 DirLocal;

	public V3 MuzzleLocal;

	public bool ImpactOnVictim;

	public int BlowBone = -1;

	public bool Butt;

	public bool Kick;

	public bool Choke;

	public bool Stomp;

	public bool Down;

	public int HeldMs;

	public bool FightChoke;
}
