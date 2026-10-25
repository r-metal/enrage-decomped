namespace NemesisSystem;

public class TownRow
{
	public string Name { get; set; }

	public float[] Doctor { get; set; }

	public float[] Saloon { get; set; }

	public bool? DoctorOpen { get; set; }

	public float CoreRadius { get; set; } = 110f;

	public float[] Centre { get; set; }
}
