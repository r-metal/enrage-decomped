using Newtonsoft.Json;

namespace NemesisSystem.BulletImpact;

public sealed class Blow
{
	public int Bone;

	public V3 Dir;

	public bool Butt;

	public bool Kick;

	public bool Choke;

	public int HeldMs;

	public bool Stomp;

	public bool Down;

	[JsonIgnore]
	public BlowKind Kind
	{
		get
		{
			if (!Choke)
			{
				if (!Butt)
				{
					if (!Kick && !Stomp)
					{
						return BlowKind.Punch;
					}
					return BlowKind.Kick;
				}
				return BlowKind.Gun;
			}
			return BlowKind.Choke;
		}
	}

	[JsonIgnore]
	public int Weight
	{
		get
		{
			if (!Choke)
			{
				if (!Stomp)
				{
					if (!Butt && !Kick)
					{
						return 1;
					}
					return 2;
				}
				return 3;
			}
			return 0;
		}
	}
}
