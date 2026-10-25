using System;
using System.Collections.Generic;
using System.Linq;

namespace NemesisSystem.BulletImpact;

public static class Anatomy
{
	public abstract class Shape
	{
		public string Key;

		public string Name;

		public Severity Severity;

		public bool IsBone;

		public abstract bool Contains(V3 p);
	}

	public sealed class Capsule : Shape
	{
		public V3 A;

		public V3 B;

		public float R;

		public override bool Contains(V3 p)
		{
			return DistToSegment(p, A, B) <= R;
		}
	}

	public sealed class Sphere : Shape
	{
		public V3 C;

		public float R;

		public override bool Contains(V3 p)
		{
			return (p - C).Length() <= R;
		}
	}

	public sealed class TorsoShell : Shape
	{
		public Body Body;

		public float FracMin;

		public float FracMax;

		public float RadialMin;

		public float RadialMax;

		public Func<float, float, bool> SideTest;

		public override bool Contains(V3 p)
		{
			Body.TorsoCoords(p, out var frac, out var right, out var fwd);
			if (frac < FracMin || frac > FracMax)
			{
				return false;
			}
			float num = (float)Math.Sqrt(right / 1.35f * (right / 1.35f) + fwd * fwd);
			if (num >= RadialMin && num <= RadialMax)
			{
				return SideTest(right, fwd);
			}
			return false;
		}
	}

	public sealed class SoftPart
	{
		public Shape Shape;

		public string Region;
	}

	public sealed class Body
	{
		public readonly Dictionary<int, V3> Bones;

		public V3 PelvisPos;

		public V3 NeckPos;

		public V3 HeadCenter;

		public V3 Up;

		public V3 Fwd;

		public V3 Right;

		public float TorsoLength;

		public readonly List<SoftPart> Soft = new List<SoftPart>();

		public readonly List<Shape> Hard = new List<Shape>();

		public readonly List<Sphere> Organs = new List<Sphere>();

		public bool Animal;

		public float Scale = 0.6f;

		public float Depth;

		public float HalfWidth;

		public float BudgetScale = 1f;

		public V3 Snout;

		public V3 SnoutTip;

		public Body(Dictionary<int, V3> bones)
		{
			Bones = bones;
		}

		public bool Has(int id)
		{
			return Bones.ContainsKey(id);
		}

		public V3 Get(int id)
		{
			return Bones[id];
		}

		public V3 TorsoPoint(float frac, float right, float fwd)
		{
			return PelvisPos + Up * (frac * TorsoLength) + Right * right + Fwd * fwd;
		}

		public void TorsoCoords(V3 p, out float frac, out float right, out float fwd)
		{
			V3 a = p - PelvisPos;
			frac = V3.Dot(a, Up) / TorsoLength;
			right = V3.Dot(a, Right);
			fwd = V3.Dot(a, Fwd);
		}
	}

	public const int Pelvis = 56200;

	public const int Spine0 = 14410;

	public const int Spine1 = 14411;

	public const int Spine2 = 14412;

	public const int Spine3 = 14413;

	public const int Spine4 = 14414;

	public const int Spine5 = 14415;

	public const int Neck0 = 14283;

	public const int Neck1 = 14284;

	public const int Neck2 = 14285;

	public const int Neck3 = 14286;

	public const int Neck4 = 14287;

	public const int Neck5 = 14288;

	public const int Head = 21030;

	public const int LClavicle = 30226;

	public const int LUpperArm = 37873;

	public const int LForearm = 53675;

	public const int LHand = 34606;

	public const int RClavicle = 54802;

	public const int RUpperArm = 46065;

	public const int RForearm = 54187;

	public const int RHand = 22798;

	public const int LThigh = 65478;

	public const int LCalf = 55120;

	public const int LFoot = 45454;

	public const int LToe = 53081;

	public const int RThigh = 6884;

	public const int RCalf = 43312;

	public const int RFoot = 33646;

	public const int RToe = 41273;

	public static readonly int[] SpineChain = new int[14]
	{
		56200, 14410, 14411, 14412, 14413, 14414, 14415, 14283, 14284, 14285,
		14286, 14287, 14288, 21030
	};

	public static readonly int[][] LimbChains = new int[4][]
	{
		new int[4] { 30226, 37873, 53675, 34606 },
		new int[4] { 54802, 46065, 54187, 22798 },
		new int[5] { 56200, 65478, 55120, 45454, 53081 },
		new int[5] { 56200, 6884, 43312, 33646, 41273 }
	};

	public const int Jaw = 58559;

	public const int LToe1 = 53082;

	public const int RToe1 = 41274;

	public static readonly int[][] LFingers = new int[5][]
	{
		new int[3] { 41403, 41404, 41405 },
		new int[4] { 41323, 41324, 41325, 41326 },
		new int[4] { 41307, 41308, 41309, 41310 },
		new int[4] { 41355, 41356, 41357, 41358 },
		new int[4] { 41339, 41340, 41341, 41342 }
	};

	public static readonly int[][] RFingers = new int[5][]
	{
		new int[3] { 16827, 16828, 16829 },
		new int[4] { 16747, 16748, 16749, 16750 },
		new int[4] { 16731, 16732, 16733, 16734 },
		new int[4] { 16779, 16780, 16781, 16782 },
		new int[4] { 16763, 16764, 16765, 16766 }
	};

	public static readonly int[] AllBones = SpineChain.Concat(LimbChains.SelectMany((int[] c) => c)).Concat(LFingers.SelectMany((int[] f) => f)).Concat(RFingers.SelectMany((int[] f) => f))
		.Concat(new int[3] { 58559, 53082, 41274 })
		.Distinct()
		.ToArray();

	public static string RegionOfBone(int boneId)
	{
		switch (boneId)
		{
		case 21030:
			return "the head";
		case 14283:
		case 14284:
		case 14285:
		case 14286:
		case 14287:
		case 14288:
			return "the neck";
		case 14413:
		case 14414:
		case 14415:
			return "the chest";
		case 14410:
		case 14411:
		case 14412:
			return "the abdomen";
		case 56200:
			return "the hips";
		case 30226:
		case 37873:
			return "the left shoulder";
		case 46065:
		case 54802:
			return "the right shoulder";
		case 53675:
			return "the left arm";
		case 54187:
			return "the right arm";
		case 34606:
			return "the left hand";
		case 22798:
			return "the right hand";
		case 65478:
			return "the left thigh";
		case 6884:
			return "the right thigh";
		case 55120:
			return "the left leg";
		case 43312:
			return "the right leg";
		case 45454:
		case 53081:
			return "the left foot";
		case 33646:
		case 41273:
			return "the right foot";
		default:
			return "the body";
		}
	}

	public static Body Build(Dictionary<int, V3> bones)
	{
		Body body = new Body(bones);
		body.PelvisPos = (bones.TryGetValue(56200, out var value) ? value : Average(bones.Values));
		body.NeckPos = (bones.TryGetValue(14283, out var value2) ? value2 : (bones.TryGetValue(21030, out var value3) ? (value3 - new V3(0f, 0f, 0.12f)) : (body.PelvisPos + new V3(0f, 0f, 0.5f))));
		body.TorsoLength = Math.Max(0.25f, (body.NeckPos - body.PelvisPos).Length());
		body.Up = (body.NeckPos - body.PelvisPos).Normalized();
		V3 v = new V3(0f, 1f, 0f);
		body.Fwd = (v - body.Up * V3.Dot(v, body.Up)).Normalized();
		if (body.Fwd.Length() < 0.5f)
		{
			body.Fwd = new V3(0f, 1f, 0f);
		}
		body.Right = V3.Cross(body.Fwd, body.Up).Normalized();
		V3 value4;
		V3 v2 = (bones.TryGetValue(21030, out value4) ? value4 : (body.NeckPos + body.Up * 0.1f));
		body.HeadCenter = v2 + body.Up * 0.07f + body.Fwd * 0.02f;
		AddSoft(body, "the head", new Sphere
		{
			C = body.HeadCenter,
			R = 0.11f
		});
		AddSoft(body, "the neck", new Capsule
		{
			A = body.NeckPos,
			B = v2,
			R = 0.065f
		});
		V3 v3 = body.PelvisPos - body.Up * 0.05f;
		V3 v4 = body.NeckPos - body.Up * 0.12f;
		AddSoft(body, "torso", new Capsule
		{
			A = v3 - body.Right * 0.07f,
			B = v4 - body.Right * 0.07f,
			R = 0.13f
		});
		AddSoft(body, "torso", new Capsule
		{
			A = v3 + body.Right * 0.07f,
			B = v4 + body.Right * 0.07f,
			R = 0.13f
		});
		Limb(body, "left", 30226, 37873, 53675, 34606, "shoulder", "upper arm", "forearm", "hand", 0.07f, 0.055f, 0.045f, 0.055f);
		Limb(body, "right", 54802, 46065, 54187, 22798, "shoulder", "upper arm", "forearm", "hand", 0.07f, 0.055f, 0.045f, 0.055f);
		Limb(body, "left", -1, 65478, 55120, 45454, "", "thigh", "leg", "foot", 0f, 0.085f, 0.06f, 0.05f);
		Limb(body, "right", -1, 6884, 43312, 33646, "", "thigh", "leg", "foot", 0f, 0.085f, 0.06f, 0.05f);
		body.Hard.Add(new Sphere
		{
			Key = "skull",
			Name = "SKULL FRACTURED",
			Severity = Severity.Severe,
			IsBone = true,
			C = body.HeadCenter,
			R = 0.105f
		});
		body.Hard.Add(new Sphere
		{
			Key = "jaw",
			Name = "JAW SHATTERED",
			Severity = Severity.Severe,
			IsBone = true,
			C = v2 + body.Fwd * 0.07f,
			R = 0.035f
		});
		body.Hard.Add(new Capsule
		{
			Key = "cervical",
			Name = "NECK VERTEBRAE SHATTERED",
			Severity = Severity.Fatal,
			IsBone = true,
			A = body.NeckPos,
			B = v2,
			R = 0.022f
		});
		body.Hard.Add(new Capsule
		{
			Key = "spine_t",
			Name = "SPINE SEVERED (UPPER BACK)",
			Severity = Severity.Critical,
			IsBone = true,
			A = body.TorsoPoint(0.45f, 0f, -0.03f),
			B = body.TorsoPoint(1f, 0f, -0.03f),
			R = 0.028f
		});
		body.Hard.Add(new Capsule
		{
			Key = "spine_l",
			Name = "SPINE SEVERED (LOWER BACK)",
			Severity = Severity.Critical,
			IsBone = true,
			A = body.TorsoPoint(0.05f, 0f, -0.04f),
			B = body.TorsoPoint(0.45f, 0f, -0.04f),
			R = 0.03f
		});
		body.Hard.Add(new TorsoShell
		{
			Key = "ribs_l",
			Name = "LEFT RIBS SHATTERED",
			Severity = Severity.Moderate,
			IsBone = true,
			Body = body,
			FracMin = 0.42f,
			FracMax = 0.9f,
			RadialMin = 0.085f,
			RadialMax = 0.13f,
			SideTest = (float r, float f) => r < -0.025f
		});
		body.Hard.Add(new TorsoShell
		{
			Key = "ribs_r",
			Name = "RIGHT RIBS SHATTERED",
			Severity = Severity.Moderate,
			IsBone = true,
			Body = body,
			FracMin = 0.42f,
			FracMax = 0.9f,
			RadialMin = 0.085f,
			RadialMax = 0.13f,
			SideTest = (float r, float f) => r > 0.025f
		});
		body.Hard.Add(new TorsoShell
		{
			Key = "sternum",
			Name = "STERNUM CRACKED",
			Severity = Severity.Moderate,
			IsBone = true,
			Body = body,
			FracMin = 0.55f,
			FracMax = 0.9f,
			RadialMin = 0.07f,
			RadialMax = 0.13f,
			SideTest = (float r, float f) => Math.Abs(r) <= 0.03f && f > 0f
		});
		body.Hard.Add(new TorsoShell
		{
			Key = "scapula_l",
			Name = "LEFT SHOULDER BLADE CRACKED",
			Severity = Severity.Moderate,
			IsBone = true,
			Body = body,
			FracMin = 0.7f,
			FracMax = 0.95f,
			RadialMin = 0.07f,
			RadialMax = 0.14f,
			SideTest = (float r, float f) => f < -0.04f && r < -0.04f
		});
		body.Hard.Add(new TorsoShell
		{
			Key = "scapula_r",
			Name = "RIGHT SHOULDER BLADE CRACKED",
			Severity = Severity.Moderate,
			IsBone = true,
			Body = body,
			FracMin = 0.7f,
			FracMax = 0.95f,
			RadialMin = 0.07f,
			RadialMax = 0.14f,
			SideTest = (float r, float f) => f < -0.04f && r > 0.04f
		});
		body.Hard.Add(new Sphere
		{
			Key = "pelvis",
			Name = "PELVIS SHATTERED",
			Severity = Severity.Severe,
			IsBone = true,
			C = body.PelvisPos - body.Up * 0.02f,
			R = 0.11f
		});
		Bone(body, 30226, 37873, "clavicle_l", "LEFT COLLARBONE SNAPPED", Severity.Moderate, 0.02f);
		Bone(body, 54802, 46065, "clavicle_r", "RIGHT COLLARBONE SNAPPED", Severity.Moderate, 0.02f);
		Bone(body, 37873, 53675, "humerus_l", "LEFT HUMERUS BROKEN", Severity.Moderate, 0.022f);
		Bone(body, 46065, 54187, "humerus_r", "RIGHT HUMERUS BROKEN", Severity.Moderate, 0.022f);
		Bone(body, 53675, 34606, "forearm_l", "LEFT FOREARM BONES BROKEN", Severity.Moderate, 0.022f);
		Bone(body, 54187, 22798, "forearm_r", "RIGHT FOREARM BONES BROKEN", Severity.Moderate, 0.022f);
		Ball(body, 55120, "knee_l", "LEFT KNEE SHATTERED", Severity.Severe, 0.042f);
		Ball(body, 43312, "knee_r", "RIGHT KNEE SHATTERED", Severity.Severe, 0.042f);
		Bone(body, 65478, 55120, "femur_l", "LEFT FEMUR SHATTERED", Severity.Severe, 0.03f);
		Bone(body, 6884, 43312, "femur_r", "RIGHT FEMUR SHATTERED", Severity.Severe, 0.03f);
		Shin(body, 55120, 45454, "tibia_l", "LEFT SHIN BONE BROKEN");
		Shin(body, 43312, 33646, "tibia_r", "RIGHT SHIN BONE BROKEN");
		Ball(body, 34606, "hand_l", "LEFT HAND SHATTERED", Severity.Minor, 0.04f);
		Ball(body, 22798, "hand_r", "RIGHT HAND SHATTERED", Severity.Minor, 0.04f);
		Ball(body, 45454, "foot_l", "LEFT FOOT SHATTERED", Severity.Minor, 0.04f);
		Ball(body, 33646, "foot_r", "RIGHT FOOT SHATTERED", Severity.Minor, 0.04f);
		V3 v5 = V3.Lerp(body.NeckPos, v2, 0.5f);
		Organ(body, "brain", "BRAIN DESTROYED", Severity.Fatal, body.HeadCenter, 0.075f);
		Organ(body, "carotid_l", "LEFT CAROTID ARTERY SEVERED", Severity.Critical, v5 - body.Right * 0.03f + body.Fwd * 0.015f, 0.02f);
		Organ(body, "carotid_r", "RIGHT CAROTID ARTERY SEVERED", Severity.Critical, v5 + body.Right * 0.03f + body.Fwd * 0.015f, 0.02f);
		Organ(body, "windpipe", "WINDPIPE PIERCED", Severity.Severe, v5 + body.Fwd * 0.035f, 0.016f);
		Organ(body, "heart", "HEART PIERCED", Severity.Fatal, body.TorsoPoint(0.72f, -0.03f, 0.05f), 0.055f);
		Organ(body, "lung_l", "LEFT LUNG PIERCED", Severity.Critical, body.TorsoPoint(0.7f, -0.075f, 0f), 0.07f);
		Organ(body, "lung_l", "LEFT LUNG PIERCED", Severity.Critical, body.TorsoPoint(0.83f, -0.07f, 0f), 0.06f);
		Organ(body, "lung_r", "RIGHT LUNG PIERCED", Severity.Critical, body.TorsoPoint(0.7f, 0.075f, 0f), 0.07f);
		Organ(body, "lung_r", "RIGHT LUNG PIERCED", Severity.Critical, body.TorsoPoint(0.83f, 0.07f, 0f), 0.06f);
		Organ(body, "liver", "LIVER RUPTURED", Severity.Severe, body.TorsoPoint(0.5f, 0.06f, 0.03f), 0.07f);
		Organ(body, "stomach", "STOMACH PERFORATED", Severity.Severe, body.TorsoPoint(0.5f, -0.05f, 0.05f), 0.055f);
		Organ(body, "kidney_l", "LEFT KIDNEY DESTROYED", Severity.Severe, body.TorsoPoint(0.42f, -0.05f, -0.06f), 0.035f);
		Organ(body, "kidney_r", "RIGHT KIDNEY DESTROYED", Severity.Severe, body.TorsoPoint(0.42f, 0.05f, -0.06f), 0.035f);
		Organ(body, "intestines", "INTESTINES TORN", Severity.Severe, body.TorsoPoint(0.25f, 0f, 0.05f), 0.09f);
		Organ(body, "bladder", "BLADDER RUPTURED", Severity.Moderate, body.TorsoPoint(0.04f, 0f, 0.06f), 0.04f);
		if (body.Has(65478) && body.Has(55120))
		{
			body.Hard.Add(new Capsule
			{
				Key = "femoral_l",
				Name = "LEFT FEMORAL ARTERY SEVERED",
				Severity = Severity.Critical,
				A = body.Get(65478) + body.Fwd * 0.02f,
				B = V3.Lerp(body.Get(65478), body.Get(55120), 0.5f) + body.Fwd * 0.02f,
				R = 0.022f
			});
		}
		if (body.Has(6884) && body.Has(43312))
		{
			body.Hard.Add(new Capsule
			{
				Key = "femoral_r",
				Name = "RIGHT FEMORAL ARTERY SEVERED",
				Severity = Severity.Critical,
				A = body.Get(6884) + body.Fwd * 0.02f,
				B = V3.Lerp(body.Get(6884), body.Get(43312), 0.5f) + body.Fwd * 0.02f,
				R = 0.022f
			});
		}
		return body;
	}

	private static void AddSoft(Body b, string region, Shape s)
	{
		b.Soft.Add(new SoftPart
		{
			Shape = s,
			Region = region
		});
	}

	private static void Limb(Body b, string side, int clav, int upper, int lower, int end, string clavName, string upperName, string lowerName, string endName, float rClav, float rUpper, float rLower, float rEnd)
	{
		if (clav >= 0 && b.Has(clav) && b.Has(upper))
		{
			AddSoft(b, "the " + side + " " + clavName, new Capsule
			{
				A = b.Get(clav),
				B = b.Get(upper),
				R = rClav
			});
		}
		if (b.Has(upper) && b.Has(lower))
		{
			AddSoft(b, "the " + side + " " + upperName, new Capsule
			{
				A = b.Get(upper),
				B = b.Get(lower),
				R = rUpper
			});
		}
		if (b.Has(lower) && b.Has(end))
		{
			AddSoft(b, "the " + side + " " + lowerName, new Capsule
			{
				A = b.Get(lower),
				B = b.Get(end),
				R = rLower
			});
		}
		if (b.Has(end))
		{
			AddSoft(b, "the " + side + " " + endName, new Sphere
			{
				C = b.Get(end),
				R = rEnd
			});
		}
	}

	private static void Bone(Body b, int a, int c, string key, string name, Severity sev, float r)
	{
		if (b.Has(a) && b.Has(c))
		{
			b.Hard.Add(new Capsule
			{
				Key = key,
				Name = name,
				Severity = sev,
				IsBone = true,
				A = b.Get(a),
				B = b.Get(c),
				R = r
			});
		}
	}

	private static void Shin(Body b, int calf, int foot, string key, string name)
	{
		if (b.Has(calf) && b.Has(foot))
		{
			b.Hard.Add(new Capsule
			{
				Key = key,
				Name = name,
				Severity = Severity.Moderate,
				IsBone = true,
				A = b.Get(calf),
				B = V3.Lerp(b.Get(calf), b.Get(foot), 0.82f),
				R = 0.025f
			});
		}
	}

	private static void Ball(Body b, int id, string key, string name, Severity sev, float r)
	{
		if (b.Has(id))
		{
			b.Hard.Add(new Sphere
			{
				Key = key,
				Name = name,
				Severity = sev,
				IsBone = true,
				C = b.Get(id),
				R = r
			});
		}
	}

	private static void Organ(Body b, string key, string name, Severity sev, V3 c, float r)
	{
		b.Organs.Add(new Sphere
		{
			Key = key,
			Name = name,
			Severity = sev,
			C = c,
			R = r
		});
	}

	public static float Budget(WeaponClass w)
	{
		return w switch
		{
			WeaponClass.Pistol => 0.29f, 
			WeaponClass.Revolver => 0.32f, 
			WeaponClass.Repeater => 0.36f, 
			WeaponClass.Rifle => 0.75f, 
			WeaponClass.Sniper => 1.2f, 
			WeaponClass.Shotgun => 0.1f, 
			WeaponClass.Stab => 0.17f, 
			WeaponClass.Slash => 0.1f, 
			WeaponClass.Chop => 0.12f, 
			WeaponClass.Blast => 0.07f, 
			WeaponClass.Burn => 0.012f, 
			_ => 0.26f, 
		};
	}

	public static float CordMissChance(WeaponClass w)
	{
		return w switch
		{
			WeaponClass.Pistol => 0.6f, 
			WeaponClass.Revolver => 0.55f, 
			WeaponClass.Repeater => 0.35f, 
			WeaponClass.Rifle => 0.1f, 
			WeaponClass.Sniper => 0f, 
			WeaponClass.Shotgun => 0.7f, 
			WeaponClass.Chop => 0.4f, 
			_ => 0.5f, 
		};
	}

	private static float Resistance(Shape s)
	{
		if (!s.IsBone)
		{
			return 0f;
		}
		switch (s.Key)
		{
		case "skull":
			return 0.1f;
		case "femur_r":
		case "femur_l":
		case "pelvis":
			return 0.12f;
		case "spine_t":
		case "spine_l":
		case "cervical":
			return 0.12f;
		case "sternum":
		case "ribs_l":
		case "ribs_r":
		case "scapula_l":
		case "scapula_r":
			return 0.05f;
		default:
			return 0.06f;
		}
	}

	public static PathResult Trace(Body body, V3 origin, V3 dir, WeaponClass weapon, float maxDistance = 25f, AmmoKind ammo = AmmoKind.Regular, float budgetScale = 1f)
	{
		dir = dir.Normalized();
		(float, float, float) tuple = Ammo.Mods(ammo);
		float num = Budget(weapon) * tuple.Item1 * budgetScale * body.BudgetScale;
		PathResult pathResult = new PathResult();
		bool flag = false;
		V3 v = origin;
		float num2 = 0f;
		for (float num3 = 0f; num3 <= maxDistance; num3 += 0.005f)
		{
			V3 p = origin + dir * num3;
			SoftPart softPart = body.Soft.FirstOrDefault((SoftPart s) => s.Shape.Contains(p));
			bool flag2 = softPart != null;
			if (flag2 && !pathResult.HasEntry)
			{
				pathResult.HasEntry = true;
				pathResult.Entry = p;
				pathResult.Start = p - dir * 0.6f;
				pathResult.EntryText = Describe(body, p, softPart.Region, entering: true);
			}
			if (flag2)
			{
				v = p;
				num -= 0.005f;
				foreach (Shape item in (weapon == WeaponClass.Burn) ? Enumerable.Empty<Shape>() : body.Hard.Concat(body.Organs))
				{
					if (pathResult.HitKeys.Contains(item.Key) || !item.Contains(p))
					{
						continue;
					}
					pathResult.HitKeys.Add(item.Key);
					float num4 = Resistance(item) * (IsBlade(weapon) ? 1f : tuple.Item2);
					if (IsBlade(weapon) && item.IsBone)
					{
						bool flag3 = item.Key.StartsWith("ribs") || item.Key == "sternum" || item.Key.StartsWith("scapula");
						if (weapon == WeaponClass.Stab && flag3)
						{
							pathResult.Damage.Add(new DamageItem
							{
								Key = item.Key,
								Name = NickName(item.Key, item.Name),
								Severity = Severity.Minor,
								IsBone = true,
								Chipped = true
							});
							num -= 0.02f;
							continue;
						}
						if (weapon != WeaponClass.Chop)
						{
							pathResult.Damage.Add(new DamageItem
							{
								Key = item.Key,
								Name = NickName(item.Key, item.Name),
								Severity = Severity.Moderate,
								IsBone = true,
								Chipped = true
							});
							num = 0f;
							break;
						}
						num4 *= 0.6f;
					}
					bool num5 = item.Key == "spine_t" || item.Key == "spine_l" || item.Key == "cervical";
					bool flag4 = item.IsBone && num < num4;
					bool flag5 = num5 && !flag4 && Roll(p) < CordMissChance(weapon) * tuple.Item3;
					if (flag4 || flag5)
					{
						pathResult.Damage.Add(new DamageItem
						{
							Key = item.Key,
							Name = ChipName(item.Key, item.Name, flag5),
							Severity = Severity.Moderate,
							IsBone = true,
							Chipped = true
						});
						if (flag4)
						{
							num = 0f;
							break;
						}
						num -= num4;
					}
					else
					{
						pathResult.Damage.Add(new DamageItem
						{
							Key = item.Key,
							Name = item.Name,
							Severity = item.Severity,
							IsBone = item.IsBone
						});
						num -= num4;
					}
				}
				if (num <= 0f)
				{
					pathResult.End = p;
					pathResult.Exited = false;
					pathResult.EndText = (IsBlade(weapon) ? "Blade stopped in " : "Lodged in ") + Describe(body, p, softPart.Region, entering: false);
					return Finish(pathResult);
				}
			}
			else if (flag && pathResult.HasEntry)
			{
				pathResult.End = v;
				pathResult.Exited = true;
				pathResult.EndText = "Exited " + Describe(body, v, RegionAt(body, v), entering: false);
			}
			if (pathResult.HasEntry && !flag2)
			{
				num2 += 0.005f;
			}
			if (pathResult.Exited && num2 > 0.6f)
			{
				break;
			}
			flag = flag2;
		}
		if (!pathResult.HasEntry)
		{
			return null;
		}
		return Finish(pathResult);
	}

	private static float Roll(V3 p)
	{
		int num = ((int)(p.X * 1000f) * 73856093) ^ ((int)(p.Y * 1000f) * 19349663) ^ ((int)(p.Z * 1000f) * 83492791);
		int num2 = (num ^ (num >> 13)) * 1540483477;
		return (float)(((num2 ^ (num2 >> 15)) & 0x7FFFFFFF) % 1000) / 1000f;
	}

	public static bool IsBlade(WeaponClass w)
	{
		if (w != WeaponClass.Stab && w != WeaponClass.Slash)
		{
			return w == WeaponClass.Chop;
		}
		return true;
	}

	public static bool IsArea(WeaponClass w)
	{
		if (w != WeaponClass.Blast)
		{
			return w == WeaponClass.Burn;
		}
		return true;
	}

	public static bool IsBlunt(WeaponClass w)
	{
		return w == WeaponClass.Blunt;
	}

	public static string RegionOf(Body b, V3 p)
	{
		return RegionAt(b, p);
	}

	private static string NickName(string key, string name)
	{
		switch (key)
		{
		case "spine_t":
			return "VERTEBRA NICKED (UPPER BACK) - CORD INTACT";
		case "spine_l":
			return "VERTEBRA NICKED (LOWER BACK) - CORD INTACT";
		case "cervical":
			return "NECK VERTEBRA NICKED - CORD INTACT";
		default:
		{
			int num = name.LastIndexOf(' ');
			return ((num > 0) ? name.Substring(0, num) : name) + " NICKED";
		}
		}
	}

	private static string ChipName(string key, string name, bool deflected)
	{
		switch (key)
		{
		case "spine_t":
			if (!deflected)
			{
				return "BULLET LODGED IN SPINE (UPPER BACK) - CORD INTACT";
			}
			return "VERTEBRA CHIPPED (UPPER BACK) - CORD INTACT";
		case "spine_l":
			if (!deflected)
			{
				return "BULLET LODGED IN SPINE (LOWER BACK) - CORD INTACT";
			}
			return "VERTEBRA CHIPPED (LOWER BACK) - CORD INTACT";
		case "cervical":
			return "NECK VERTEBRA CHIPPED - CORD INTACT";
		default:
		{
			int num = name.LastIndexOf(' ');
			return ((num > 0) ? name.Substring(0, num) : name) + " CHIPPED";
		}
		}
	}

	private static PathResult Finish(PathResult r)
	{
		if (r.Damage.Count == 0)
		{
			r.Damage.Add(new DamageItem
			{
				Key = "flesh",
				Name = "FLESH WOUND",
				Severity = Severity.Minor
			});
		}
		r.Damage.Sort((DamageItem a, DamageItem b) => b.Severity.CompareTo(a.Severity));
		return r;
	}

	private static string RegionAt(Body b, V3 p)
	{
		return (from s in b.Soft
			where s.Shape.Contains(p)
			select s.Region).FirstOrDefault() ?? b.Soft.OrderBy((SoftPart s) => Dist(s.Shape, p)).First().Region;
	}

	private static float Dist(Shape s, V3 p)
	{
		if (!(s is Capsule capsule))
		{
			if (!(s is Sphere sphere))
			{
				return 1000f;
			}
			return (p - sphere.C).Length() - sphere.R;
		}
		return DistToSegment(p, capsule.A, capsule.B) - capsule.R;
	}

	private static string Describe(Body b, V3 p, string region, bool entering)
	{
		if (b.Animal)
		{
			return AnimalAnatomy.Describe(b, p, region);
		}
		if (region != "torso")
		{
			if (region == "the head")
			{
				b.TorsoCoords(p, out var _, out var _, out var fwd);
				if (!(fwd > 0.03f))
				{
					if (!(fwd < -0.03f))
					{
						return "the side of the head";
					}
					return "the back of the skull";
				}
				return "the face";
			}
			return region;
		}
		b.TorsoCoords(p, out var frac2, out var right2, out var fwd2);
		string text = ((frac2 > 0.55f) ? "chest" : ((frac2 > 0.18f) ? "stomach" : "hips"));
		if (fwd2 < -0.03f)
		{
			text = ((frac2 > 0.55f) ? "upper back" : ((frac2 > 0.18f) ? "lower back" : "buttocks"));
		}
		string text2 = ((right2 < -0.04f) ? ", left side" : ((right2 > 0.04f) ? ", right side" : ""));
		return ((fwd2 < -0.03f) ? "the " : "the front of the ") + text + text2;
	}

	public static float SegmentDistance(V3 p0, V3 p1, V3 q0, V3 q1, out V3 onP)
	{
		V3 v = p1 - p0;
		V3 v2 = q1 - q0;
		V3 b = p0 - q0;
		float num = V3.Dot(v, v);
		float num2 = V3.Dot(v2, v2);
		float num3 = V3.Dot(v2, b);
		if (num < 1E-09f && num2 < 1E-09f)
		{
			onP = p0;
			return b.Length();
		}
		float num4;
		float num5;
		if (num < 1E-09f)
		{
			num4 = 0f;
			num5 = Clamp01(num3 / num2);
		}
		else
		{
			float num6 = V3.Dot(v, b);
			if (num2 < 1E-09f)
			{
				num5 = 0f;
				num4 = Clamp01((0f - num6) / num);
			}
			else
			{
				float num7 = V3.Dot(v, v2);
				float num8 = num * num2 - num7 * num7;
				num4 = ((num8 > 1E-09f) ? Clamp01((num7 * num3 - num6 * num2) / num8) : 0f);
				num5 = (num7 * num4 + num3) / num2;
				if (num5 < 0f)
				{
					num5 = 0f;
					num4 = Clamp01((0f - num6) / num);
				}
				else if (num5 > 1f)
				{
					num5 = 1f;
					num4 = Clamp01((num7 - num6) / num);
				}
			}
		}
		onP = p0 + v * num4;
		return (onP - (q0 + v2 * num5)).Length();
	}

	private static float Clamp01(float v)
	{
		if (!(v < 0f))
		{
			if (!(v > 1f))
			{
				return v;
			}
			return 1f;
		}
		return 0f;
	}

	public static float DistToSegment(V3 p, V3 a, V3 b)
	{
		V3 v = b - a;
		float num = V3.Dot(v, v);
		float num2 = ((num < 1E-08f) ? 0f : Math.Max(0f, Math.Min(1f, V3.Dot(p - a, v) / num)));
		return (p - (a + v * num2)).Length();
	}

	private static V3 Average(IEnumerable<V3> pts)
	{
		List<V3> list = pts.ToList();
		if (list.Count == 0)
		{
			return new V3(0f, 0f, 1f);
		}
		V3 v = new V3(0f, 0f, 0f);
		foreach (V3 item in list)
		{
			v += item;
		}
		return v * (1f / (float)list.Count);
	}
}
