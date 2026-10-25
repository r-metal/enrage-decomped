using System;
using System.Collections.Generic;
using System.Linq;

namespace NemesisSystem.BulletImpact;

public static class AnimalAnatomy
{
	public sealed class TrunkShell : Anatomy.Shape
	{
		public Anatomy.Body Body;

		public float FracMin;

		public float FracMax;

		public float E0;

		public float E1;

		public Func<float, float, bool> SideTest;

		public override bool Contains(V3 p)
		{
			Body.TorsoCoords(p, out var frac, out var right, out var fwd);
			if (frac < FracMin || frac > FracMax)
			{
				return false;
			}
			float num = (float)Math.Sqrt(right / Body.HalfWidth * (right / Body.HalfWidth) + fwd / Body.Depth * (fwd / Body.Depth));
			if (num >= E0 && num <= E1)
			{
				return SideTest(right, fwd);
			}
			return false;
		}
	}

	public static int[] TailIds = new int[0];

	public const int LHoof0 = 41403;

	public const int LHoof1 = 41404;

	public const int RHoof0 = 16827;

	public const int RHoof1 = 16828;

	public static bool LooksFourLegged(ICollection<int> ids)
	{
		if (ids.Contains(56200) && ids.Contains(21030) && (ids.Contains(14410) || ids.Contains(14411)) && (ids.Contains(37873) || ids.Contains(46065)))
		{
			if (!ids.Contains(65478))
			{
				return ids.Contains(6884);
			}
			return true;
		}
		return false;
	}

	public static Anatomy.Body Build(Dictionary<int, V3> bones)
	{
		Anatomy.Body body = new Anatomy.Body(bones)
		{
			Animal = true
		};
		V3 v = G(56200, Mean(bones.Values));
		V3 v2 = (bones.ContainsKey(14283) ? bones[14283] : (bones.ContainsKey(14413) ? bones[14413] : (v + new V3(0f, 0.55f, 0f))));
		V3 v3 = v2 - v;
		float num = Math.Max(0.15f, v3.Length());
		V3 v4 = v3.Normalized();
		if (v4.Length() < 0.5f)
		{
			v4 = new V3(0f, 1f, 0f);
		}
		List<V3> list = (from id in new int[8] { 34606, 22798, 45454, 33646, 53675, 54187, 55120, 43312 }.Where(bones.ContainsKey)
			select bones[id]).ToList();
		V3 v5 = V3.Lerp(v, v2, 0.5f);
		V3 v6 = ((list.Count > 0) ? (Mean(list) - v5) : new V3(0f, 0f, -1f));
		V3 v7 = (v6 - v4 * V3.Dot(v6, v4)).Normalized();
		if (v7.Length() < 0.5f)
		{
			V3 v8 = new V3(0f, 0f, -1f);
			v7 = (v8 - v4 * V3.Dot(v8, v4)).Normalized();
		}
		if (v7.Length() < 0.5f)
		{
			v7 = new V3(0f, 0f, -1f);
		}
		V3 v9 = V3.Cross(v7, v4).Normalized();
		float d = 0.31f * num;
		float a = 0.21f * num;
		body.Scale = num;
		body.Depth = d;
		body.HalfWidth = a;
		body.Up = v4;
		body.Fwd = v7;
		body.Right = v9;
		body.PelvisPos = v + v7 * (d * 0.85f);
		body.NeckPos = v2 + v7 * (d * 0.8f);
		body.TorsoLength = num;
		body.BudgetScale = ((num > 0.95f) ? 0.85f : ((num > 0.55f) ? 0.95f : 1f));
		V3 v10 = G(21030, v2 + v4 * 0.35f * num);
		V3 v11 = (from id in new int[4] { 14286, 14285, 14284, 14283 }.Where(bones.ContainsKey)
			select bones[id]).DefaultIfEmpty(v2).First();
		V3 v12 = (v10 - v11).Normalized();
		V3 v13 = (v12 * 0.3f + v4 + v7 * 0.35f).Normalized();
		if (v13.Length() < 0.5f)
		{
			v13 = v4;
		}
		body.Snout = v13;
		body.SnoutTip = v10 + v13 * (0.42f * num);
		body.HeadCenter = v10 + v13 * (0.08f * num);
		V3 v14 = (v7 * -1f - v13 * V3.Dot(v7 * -1f, v13)).Normalized();
		if (v14.Length() < 0.5f)
		{
			v14 = new V3(0f, 0f, 1f);
		}
		Soft(body, "the head", new Anatomy.Capsule
		{
			A = v10 - v13 * (0.04f * num),
			B = body.SnoutTip,
			R = 0.1f * num
		});
		Soft(body, "the neck", new Anatomy.Capsule
		{
			A = v2,
			B = v10,
			R = 0.13f * num
		});
		(float, float, string)[] array = new(float, float, string)[3]
		{
			(-0.1f, 0.32f, "the hindquarters"),
			(0.32f, 0.62f, "the flank"),
			(0.62f, 1.04f, "the chest")
		};
		for (int num2 = 0; num2 < array.Length; num2++)
		{
			(float, float, string) tuple = array[num2];
			float item = tuple.Item1;
			float item2 = tuple.Item2;
			string item3 = tuple.Item3;
			float[] array2 = new float[2] { -1f, 1f };
			foreach (float num4 in array2)
			{
				Soft(body, item3, new Anatomy.Capsule
				{
					A = body.TorsoPoint(item, 0f, num4 * (d - a)),
					B = body.TorsoPoint(item2, 0f, num4 * (d - a)),
					R = a
				});
			}
		}
		(string, float, int, int, int, int, int, int, int, int, int, int, int)[] array3 = new(string, float, int, int, int, int, int, int, int, int, int, int, int)[2]
		{
			("left", -1f, 30226, 37873, 53675, 34606, 41403, 41404, 65478, 55120, 45454, 53081, 53082),
			("right", 1f, 54802, 46065, 54187, 22798, 16827, 16828, 6884, 43312, 33646, 41273, 41274)
		};
		for (int num2 = 0; num2 < array3.Length; num2++)
		{
			(string, float, int, int, int, int, int, int, int, int, int, int, int) tuple2 = array3[num2];
			string item4 = tuple2.Item1;
			float item5 = tuple2.Item2;
			int item6 = tuple2.Item3;
			int item7 = tuple2.Item4;
			int item8 = tuple2.Item5;
			int item9 = tuple2.Item6;
			int item10 = tuple2.Item7;
			int item11 = tuple2.Rest.Item1;
			int item12 = tuple2.Rest.Item2;
			int item13 = tuple2.Rest.Item3;
			int item14 = tuple2.Rest.Item4;
			int item15 = tuple2.Rest.Item5;
			int item16 = tuple2.Rest.Item6;
			SoftSeg(body, item6, item7, "the " + item4 + " shoulder", 0.1f * num);
			SoftSeg(body, item7, item8, "the " + item4 + " foreleg", 0.07f * num);
			SoftSeg(body, item8, item9, "the " + item4 + " foreleg", 0.045f * num);
			SoftSeg(body, item9, item10, "the " + item4 + " front shin", 0.032f * num);
			SoftSeg(body, item10, item11, "the " + item4 + " front foot", 0.032f * num);
			SoftSeg(body, item12, item13, "the " + item4 + " haunch", 0.11f * num);
			SoftSeg(body, item13, item14, "the " + item4 + " hind leg", 0.06f * num);
			SoftSeg(body, item14, item15, "the " + item4 + " hind shin", 0.035f * num);
			SoftSeg(body, item15, item16, "the " + item4 + " hind foot", 0.033f * num);
			string text = ((item5 < 0f) ? "_l" : "_r");
			string text2 = item4.ToUpperInvariant();
			HardSeg(body, item6, item7, "scapula" + text, text2 + " SHOULDER BLADE BROKEN", Severity.Moderate, 0.05f * num);
			HardSeg(body, item7, item8, "humerus" + text, text2 + " UPPER FORELEG BROKEN", Severity.Severe, 0.03f * num);
			HardSeg(body, item8, item9, "forearm" + text, text2 + " FORELEG BROKEN", Severity.Severe, 0.025f * num);
			HardSeg(body, item9, item10, "cannon_f" + text, text2 + " FRONT SHIN BONE BROKEN", Severity.Severe, 0.02f * num);
			HardSeg(body, item10, item11, "foot_f" + text, text2 + " FRONT FOOT SHATTERED", Severity.Moderate, 0.022f * num);
			HardSeg(body, item12, item13, "femur" + text, text2 + " FEMUR SHATTERED", Severity.Severe, 0.035f * num);
			HardSeg(body, item13, item14, "tibia" + text, text2 + " HIND LEG BROKEN", Severity.Severe, 0.026f * num);
			HardSeg(body, item14, item15, "cannon_h" + text, text2 + " HIND SHIN BONE BROKEN", Severity.Severe, 0.02f * num);
			HardSeg(body, item15, item16, "foot_h" + text, text2 + " HIND FOOT SHATTERED", Severity.Moderate, 0.022f * num);
			if (bones.ContainsKey(item12) && bones.ContainsKey(item13))
			{
				V3 v15 = bones[item12];
				V3 b = bones[item13];
				body.Hard.Add(new Anatomy.Capsule
				{
					Key = "femoral" + text,
					Name = text2 + " FEMORAL ARTERY SEVERED",
					Severity = Severity.Critical,
					A = v15 - v9 * (item5 * 0.04f * num),
					B = V3.Lerp(v15, b, 0.6f) - v9 * (item5 * 0.04f * num),
					R = 0.025f * num
				});
			}
		}
		List<V3> list2 = (from id in TailIds.Where(bones.ContainsKey)
			select bones[id]).ToList();
		if (list2.Count > 0)
		{
			Soft(body, "the tail", new Anatomy.Capsule
			{
				A = v,
				B = list2[list2.Count - 1],
				R = 0.04f * num
			});
			body.Hard.Add(new Anatomy.Capsule
			{
				Key = "tail",
				Name = "TAIL BROKEN",
				Severity = Severity.Minor,
				IsBone = true,
				A = v,
				B = list2[list2.Count - 1],
				R = 0.025f * num
			});
		}
		body.Hard.Add(new Anatomy.Capsule
		{
			Key = "skull",
			Name = "SKULL SHATTERED",
			Severity = Severity.Severe,
			IsBone = true,
			A = v10 - v13 * (0.02f * num),
			B = v10 + v13 * (0.3f * num),
			R = 0.085f * num
		});
		body.Hard.Add(new Anatomy.Capsule
		{
			Key = "jaw",
			Name = "JAW SHATTERED",
			Severity = Severity.Severe,
			IsBone = true,
			A = v10 - v14 * (0.07f * num),
			B = body.SnoutTip - v14 * (0.05f * num),
			R = 0.035f * num
		});
		List<V3> list3 = (from id in new int[5] { 14283, 14284, 14285, 14286, 21030 }.Where(bones.ContainsKey)
			select bones[id]).ToList();
		for (int num5 = 0; num5 + 1 < list3.Count; num5++)
		{
			body.Hard.Add(new Anatomy.Capsule
			{
				Key = "cervical",
				Name = "NECK BROKEN",
				Severity = Severity.Fatal,
				IsBone = true,
				A = list3[num5],
				B = list3[num5 + 1],
				R = 0.045f * num
			});
		}
		List<(int, V3)> list4 = (from id in new int[6] { 56200, 14410, 14411, 14412, 14413, 14283 }.Where(bones.ContainsKey)
			select (id: id, p: bones[id])).ToList();
		for (int num6 = 0; num6 + 1 < list4.Count; num6++)
		{
			bool flag = list4[num6].Item1 == 14412 || list4[num6].Item1 == 14413;
			body.Hard.Add(new Anatomy.Capsule
			{
				Key = (flag ? "spine_t" : "spine_l"),
				Name = (flag ? "SPINE SEVERED (OVER THE SHOULDERS)" : "SPINE SEVERED (LOWER BACK)"),
				Severity = Severity.Critical,
				IsBone = true,
				A = list4[num6].Item2,
				B = list4[num6 + 1].Item2,
				R = 0.04f * num
			});
		}
		body.Hard.Add(new TrunkShell
		{
			Key = "ribs_l",
			Name = "LEFT RIBS SHATTERED",
			Severity = Severity.Moderate,
			IsBone = true,
			Body = body,
			FracMin = 0.48f,
			FracMax = 1.02f,
			E0 = 0.8f,
			E1 = 1.04f,
			SideTest = (float r, float num7) => r < -0.12f * a && num7 < d * 0.85f
		});
		body.Hard.Add(new TrunkShell
		{
			Key = "ribs_r",
			Name = "RIGHT RIBS SHATTERED",
			Severity = Severity.Moderate,
			IsBone = true,
			Body = body,
			FracMin = 0.48f,
			FracMax = 1.02f,
			E0 = 0.8f,
			E1 = 1.04f,
			SideTest = (float r, float num7) => r > 0.12f * a && num7 < d * 0.85f
		});
		body.Hard.Add(new TrunkShell
		{
			Key = "sternum",
			Name = "BREASTBONE CRACKED",
			Severity = Severity.Moderate,
			IsBone = true,
			Body = body,
			FracMin = 0.58f,
			FracMax = 1f,
			E0 = 0.78f,
			E1 = 1.04f,
			SideTest = (float r, float num7) => Math.Abs(r) <= 0.25f * a && num7 > d * 0.7f
		});
		body.Hard.Add(new Anatomy.Sphere
		{
			Key = "pelvis",
			Name = "PELVIS SHATTERED",
			Severity = Severity.Severe,
			IsBone = true,
			C = v + v7 * (0.1f * num) - v4 * (0.02f * num),
			R = 0.14f * num
		});
		V3 v16 = V3.Lerp(Mean((from id in new int[2] { 53675, 54187 }.Where(bones.ContainsKey)
			select bones[id]).DefaultIfEmpty(body.TorsoPoint(0.8f, 0f, d))), body.TorsoPoint(0.78f, 0f, 0.35f * d), 0.55f);
		if (Anatomy.DistToSegment(v16, body.PelvisPos, body.NeckPos) > d * 1.1f)
		{
			v16 = body.TorsoPoint(0.8f, -0.05f * a, 0.45f * d);
		}
		Organ(body, "brain", "BRAIN DESTROYED", Severity.Fatal, v10 + v13 * (0.04f * num) + v14 * (0.03f * num), 0.075f * num);
		Organ(body, "heart", "HEART SHOT", Severity.Fatal, v16, 0.1f * num);
		Organ(body, "lung_l", "LEFT LUNG HIT", Severity.Critical, body.TorsoPoint(0.84f, -0.5f * a, -0.15f * d), 0.16f * num);
		Organ(body, "lung_l", "LEFT LUNG HIT", Severity.Critical, body.TorsoPoint(0.68f, -0.45f * a, -0.08f * d), 0.14f * num);
		Organ(body, "lung_r", "RIGHT LUNG HIT", Severity.Critical, body.TorsoPoint(0.84f, 0.5f * a, -0.15f * d), 0.16f * num);
		Organ(body, "lung_r", "RIGHT LUNG HIT", Severity.Critical, body.TorsoPoint(0.68f, 0.45f * a, -0.08f * d), 0.14f * num);
		Organ(body, "liver", "LIVER HIT", Severity.Severe, body.TorsoPoint(0.58f, 0.25f * a, 0.15f * d), 0.13f * num);
		Organ(body, "stomach", "STOMACH HIT", Severity.Severe, body.TorsoPoint(0.47f, -0.25f * a, 0.25f * d), 0.17f * num);
		Organ(body, "intestines", "INTESTINES TORN", Severity.Severe, body.TorsoPoint(0.28f, 0f, 0.3f * d), 0.17f * num);
		Organ(body, "kidney_l", "LEFT KIDNEY HIT", Severity.Severe, body.TorsoPoint(0.4f, -0.3f * a, -0.7f * d), 0.055f * num);
		Organ(body, "kidney_r", "RIGHT KIDNEY HIT", Severity.Severe, body.TorsoPoint(0.4f, 0.3f * a, -0.7f * d), 0.055f * num);
		Organ(body, "bladder", "BLADDER RUPTURED", Severity.Moderate, body.TorsoPoint(0.06f, 0f, 0.25f * d), 0.07f * num);
		V3 v17 = (v7 - v12 * V3.Dot(v7, v12)).Normalized();
		if (v17.Length() < 0.5f)
		{
			v17 = v7;
		}
		V3 v18 = V3.Lerp(v2, v10, 0.45f) + v17 * (0.07f * num);
		Organ(body, "carotid_l", "LEFT JUGULAR SEVERED", Severity.Critical, v18 - v9 * (0.045f * num), 0.035f * num);
		Organ(body, "carotid_r", "RIGHT JUGULAR SEVERED", Severity.Critical, v18 + v9 * (0.045f * num), 0.035f * num);
		Organ(body, "windpipe", "WINDPIPE HIT", Severity.Severe, v18 + v17 * (0.03f * num), 0.03f * num);
		return body;
		V3 G(int id, V3 fallback)
		{
			if (!bones.TryGetValue(id, out var value))
			{
				return fallback;
			}
			return value;
		}
	}

	public static string Describe(Anatomy.Body b, V3 p, string region)
	{
		switch (region)
		{
		case "the chest":
		case "the flank":
		case "the hindquarters":
		{
			b.TorsoCoords(p, out var _, out var right, out var fwd);
			if (fwd > b.Depth * 0.6f)
			{
				if (!(region == "the chest"))
				{
					return "the belly";
				}
				return "the brisket";
			}
			if (fwd < (0f - b.Depth) * 0.6f)
			{
				if (!(region == "the hindquarters"))
				{
					return "the back";
				}
				return "the rump";
			}
			string text = ((right < -0.15f * b.HalfWidth) ? "left" : ((right > 0.15f * b.HalfWidth) ? "right" : null));
			if (text != null)
			{
				return "the " + text + " side of " + region.Substring(4);
			}
			return region;
		}
		default:
			return region ?? "the body";
		}
	}

	public static string Drop(IEnumerable<DamageItem> damage)
	{
		HashSet<string> hashSet = new HashSet<string>(from d in damage
			where !d.Chipped
			select d.Key);
		if (hashSet.Contains("brain"))
		{
			return "brain";
		}
		if (hashSet.Contains("heart"))
		{
			return "heart";
		}
		if (hashSet.Contains("cervical"))
		{
			return "neck";
		}
		if (hashSet.Contains("spine_t") || hashSet.Contains("spine_l"))
		{
			return "spine";
		}
		if (hashSet.Contains("lung_l") && hashSet.Contains("lung_r"))
		{
			return "lungs";
		}
		return null;
	}

	public static void AddVerdicts(PathResult m, IEnumerable<PathResult> extra, bool diedOnHit = false, float hitShare = 0f)
	{
		if (m != null)
		{
			List<PathResult> source = new PathResult[1] { m }.Concat(extra ?? Enumerable.Empty<PathResult>()).ToList();
			HashSet<string> hashSet = new HashSet<string>(source.SelectMany((PathResult x) => x.HitKeys));
			HashSet<string> hashSet2 = new HashSet<string>(from d in source.SelectMany((PathResult x) => x.Damage)
				where !d.Chipped
				select d.Key);
			bool num = hashSet.Contains("brain");
			bool flag = hashSet.Contains("heart");
			bool flag2 = hashSet.Contains("lung_l") && hashSet.Contains("lung_r");
			bool flag3 = hashSet2.Contains("cervical") || hashSet2.Contains("spine_t") || hashSet2.Contains("spine_l");
			bool flag4 = hashSet.Contains("stomach") || hashSet.Contains("intestines") || hashSet.Contains("liver");
			if (num)
			{
				m.Damage.Insert(0, new DamageItem
				{
					Key = "verdict",
					Name = "BRAIN SHOT - DROPPED ON THE SPOT",
					Severity = Severity.Fatal
				});
			}
			else if (flag)
			{
				m.Damage.Insert(0, new DamageItem
				{
					Key = "verdict",
					Name = "HEART SHOT - A CLEAN KILL",
					Severity = Severity.Fatal
				});
			}
			else if (flag2)
			{
				m.Damage.Insert(0, new DamageItem
				{
					Key = "verdict",
					Name = "BOTH LUNGS - A QUICK DEATH",
					Severity = Severity.Fatal
				});
			}
			else if (flag3)
			{
				m.Damage.Insert(0, new DamageItem
				{
					Key = "verdict",
					Name = "SPINE SHOT - DROPPED WHERE IT STOOD",
					Severity = Severity.Critical
				});
			}
			else if (diedOnHit)
			{
				m.Damage.Insert(0, new DamageItem
				{
					Key = "verdict",
					Name = ((hitShare >= 0.5f) ? "KILLED OUTRIGHT - THE SHOCK OF THE HIT" : "FINISHED OFF - ITS WOUNDS ADDED UP"),
					Severity = Severity.Fatal
				});
			}
			else if (flag4)
			{
				m.Damage.Insert(0, new DamageItem
				{
					Key = "verdict",
					Name = "GUT SHOT - A SLOW DEATH",
					Severity = Severity.Severe
				});
			}
		}
	}

	private static void Soft(Anatomy.Body b, string region, Anatomy.Shape s)
	{
		b.Soft.Add(new Anatomy.SoftPart
		{
			Shape = s,
			Region = region
		});
	}

	private static void SoftSeg(Anatomy.Body b, int a, int c, string region, float r)
	{
		if (b.Has(a) && b.Has(c))
		{
			Soft(b, region, new Anatomy.Capsule
			{
				A = b.Get(a),
				B = b.Get(c),
				R = r
			});
		}
	}

	private static void HardSeg(Anatomy.Body b, int a, int c, string key, string name, Severity sev, float r)
	{
		if (b.Has(a) && b.Has(c))
		{
			b.Hard.Add(new Anatomy.Capsule
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

	private static void Organ(Anatomy.Body b, string key, string name, Severity sev, V3 c, float r)
	{
		b.Organs.Add(new Anatomy.Sphere
		{
			Key = key,
			Name = name,
			Severity = sev,
			C = c,
			R = r
		});
	}

	private static V3 Mean(IEnumerable<V3> pts)
	{
		List<V3> list = pts.ToList();
		if (list.Count == 0)
		{
			return new V3(0f, 0f, 0f);
		}
		V3 v = new V3(0f, 0f, 0f);
		foreach (V3 item in list)
		{
			v += item;
		}
		return v * (1f / (float)list.Count);
	}
}
