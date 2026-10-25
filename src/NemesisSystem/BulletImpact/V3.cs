using System;

namespace NemesisSystem.BulletImpact;

public struct V3
{
	public float X;

	public float Y;

	public float Z;

	public V3(float x, float y, float z)
	{
		X = x;
		Y = y;
		Z = z;
	}

	public static V3 operator +(V3 a, V3 b)
	{
		return new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
	}

	public static V3 operator -(V3 a, V3 b)
	{
		return new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
	}

	public static V3 operator *(V3 a, float s)
	{
		return new V3(a.X * s, a.Y * s, a.Z * s);
	}

	public static float Dot(V3 a, V3 b)
	{
		return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
	}

	public static V3 Cross(V3 a, V3 b)
	{
		return new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
	}

	public float Length()
	{
		return (float)Math.Sqrt(X * X + Y * Y + Z * Z);
	}

	public V3 Normalized()
	{
		float num = Length();
		if (!(num > 1E-06f))
		{
			return new V3(0f, 0f, 0f);
		}
		return this * (1f / num);
	}

	public static V3 Lerp(V3 a, V3 b, float t)
	{
		return a + (b - a) * t;
	}

	public override string ToString()
	{
		return $"({X:0.00},{Y:0.00},{Z:0.00})";
	}
}
