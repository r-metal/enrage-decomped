using System;
using System.Collections.Generic;
using System.Linq;
using RDR2;
using RDR2.Math;
using RDR2.Native;

namespace NemesisSystem.BulletImpact;

internal static class AnimalBones
{
	private static readonly HashSet<uint> _probed = new HashSet<uint>();

	private static readonly Dictionary<uint, int> _hitsLogged = new Dictionary<uint, int>();

	private static string[] _names;

	private static string[] Names
	{
		get
		{
			if (_names != null)
			{
				return _names;
			}
			List<string> list = new List<string>
			{
				"SKEL_ROOT", "SKEL_Pelvis", "SKEL_Pelvis1", "SKEL_Spine_Root", "SKEL_Head", "SKEL_Jaw", "SKEL_Neck_Root", "SKEL_L_Ear", "SKEL_R_Ear", "SKEL_Nose",
				"SKEL_Tail_Root", "SKEL_Saddle", "SKEL_Spine", "SKEL_Neck", "SKEL_Tail"
			};
			for (int i = 0; i <= 10; i++)
			{
				list.Add("SKEL_Spine" + i);
			}
			for (int j = 0; j <= 8; j++)
			{
				list.Add("SKEL_Neck" + j);
			}
			for (int k = 0; k <= 14; k++)
			{
				list.Add("SKEL_Tail" + k);
				list.Add("SKEL_Tail_0" + k);
			}
			string[] array = new string[50]
			{
				"Clavicle", "Scapula", "UpperArm", "Forearm", "Hand", "Finger00", "Finger01", "Finger02", "Finger10", "Finger11",
				"Thigh", "Calf", "Foot", "Toe0", "Toe1", "Toe2", "Toe00", "Toe01", "Humerus", "Radius",
				"Ulna", "Carpal", "Metacarpal", "Femur", "Tibia", "Fibula", "Tarsal", "Metatarsal", "Hoof", "FrontLeg",
				"HindLeg", "ForeLeg", "Shin", "Pastern", "Fetlock", "Cannon", "Hock", "Knee", "Elbow", "Wrist",
				"Ankle", "Paw", "Heel", "Wing", "Wing0", "Wing1", "Wing2", "Eye", "Horn", "Antler"
			};
			string[] array2 = new string[2] { "L", "R" };
			foreach (string text in array2)
			{
				string[] array3 = array;
				foreach (string text2 in array3)
				{
					list.Add("SKEL_" + text + "_" + text2);
				}
			}
			array2 = new string[2] { "L", "R" };
			foreach (string arg in array2)
			{
				for (int n = 0; n <= 3; n++)
				{
					string[] array3 = new string[4] { "FrontLeg", "HindLeg", "Arm", "Leg" };
					foreach (string arg2 in array3)
					{
						list.Add($"SKEL_{arg}_{arg2}{n}");
					}
				}
			}
			return _names = list.Distinct().ToArray();
		}
	}

	private static string ModelName(Ped p)
	{
		uint hash = (uint)p.Model.Hash;
		if (!Enum.IsDefined(typeof(PedHash), hash))
		{
			return $"0x{hash:X8}";
		}
		PedHash pedHash = (PedHash)hash;
		return pedHash.ToString();
	}

	private static Vector3 Local(Ped p, Vector3 world)
	{
		Vector3 position = p.Position;
		Vector3 vector = ENTITY.GET_ENTITY_FORWARD_VECTOR(p.Handle);
		vector = new Vector3(vector.X, vector.Y, 0f);
		if (vector.Length() < 0.01f)
		{
			vector = new Vector3(0f, 1f, 0f);
		}
		vector.Normalize();
		Vector3 vector2 = new Vector3(vector.Y, 0f - vector.X, 0f);
		Vector3 vector3 = world - position;
		return new Vector3(vector3.X * vector2.X + vector3.Y * vector2.Y, vector3.X * vector.X + vector3.Y * vector.Y, vector3.Z);
	}

	internal unsafe static void Probe(Ped p, Action<string> log)
	{
		if (p == null || !p.Exists() || p.IsHuman || log == null)
		{
			return;
		}
		uint hash = (uint)p.Model.Hash;
		string text = ModelName(p);
		try
		{
			if (_probed.Add(hash))
			{
				List<string> list = new List<string>();
				string[] names = Names;
				foreach (string text2 in names)
				{
					int num = ENTITY.GET_ENTITY_BONE_INDEX_BY_NAME(p.Handle, text2);
					if (num >= 0)
					{
						Vector3 vector = Local(p, ENTITY.GET_WORLD_POSITION_OF_ENTITY_BONE(p.Handle, num));
						list.Add(string.Format("{0}#{1} ({2:0.00},{3:0.00},{4:0.00})", text2.Replace("SKEL_", ""), num, vector.X, vector.Y, vector.Z));
					}
				}
				log($"ANIMAL BONES {text}: {list.Count} named bones found of {Names.Length} tried");
				for (int j = 0; j < list.Count; j += 8)
				{
					log("ANIMAL BONES " + text + ":   " + string.Join("  ", list.Skip(j).Take(8)));
				}
				List<int> list2 = Anatomy.AllBones.Where((int id) => PED.GET_PED_BONE_INDEX(p.Handle, id) >= 0).ToList();
				log(string.Format("ANIMAL BONES {0}: answers to {1} of the X-ray's {2} human bone ids{3}", text, list2.Count, Anatomy.AllBones.Length, (list2.Count > 0) ? (": " + string.Join(", ", list2)) : ""));
			}
			_hitsLogged.TryGetValue(hash, out var value);
			if (value >= 6)
			{
				return;
			}
			_hitsLogged[hash] = value + 1;
			int num2 = 0;
			string text3 = "no damage bone";
			if (PED.GET_PED_LAST_DAMAGE_BONE(p.Handle, &num2))
			{
				int bi = PED.GET_PED_BONE_INDEX(p.Handle, num2);
				string text4 = Names.FirstOrDefault((string x) => ENTITY.GET_ENTITY_BONE_INDEX_BY_NAME(p.Handle, x) == bi && bi >= 0);
				text3 = string.Format("bone {0} = {1} (index {2}{3})", num2, (BoneID)num2, bi, (text4 != null) ? (", " + text4.Replace("SKEL_", "")) : "");
			}
			Vector3 world = p.Position;
			ulong* ptr = stackalloc ulong[3];
			if (WEAPON.GET_PED_LAST_WEAPON_IMPACT_COORD(Game.Player.Character.Handle, (Vector3*)ptr))
			{
				world = new Vector3(*(float*)ptr, *(float*)(ptr + 1), *(float*)(ptr + 2));
			}
			Vector3 vector2 = Local(p, world);
			log(string.Format("ANIMAL HIT {0}: the game says {1}, impact ({2:0.00},{3:0.00},{4:0.00}) local, health {5}/{6}{7}", text, text3, vector2.X, vector2.Y, vector2.Z, p.Health, p.MaxHealth, p.IsDead ? ", dead" : ""));
		}
		catch (Exception ex)
		{
			log("ANIMAL BONES " + text + ": probe failed - " + ex.Message);
		}
	}
}
