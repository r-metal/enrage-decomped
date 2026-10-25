using System;
using RDR2.Native;

namespace NemesisSystem;

public static class GameDate
{
	public static readonly DateTime Epoch = new DateTime(1890, 1, 1);

	public static readonly string[] Months = new string[12]
	{
		"Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct",
		"Nov", "Dec"
	};

	public static string Month(int month1)
	{
		return Months[Math.Max(1, Math.Min(12, month1)) - 1];
	}

	public static long ToMinutes(DateTime d)
	{
		return (long)(d - Epoch).TotalMinutes;
	}

	public static DateTime FromMinutes(long m)
	{
		return Epoch.AddMinutes(m);
	}

	public static int DayKey(DateTime d)
	{
		return d.Year * 10000 + (d.Month - 1) * 100 + d.Day;
	}

	public static DateTime FromDayKey(int key)
	{
		int year = Math.Max(1, key / 10000);
		int month = Math.Max(1, Math.Min(12, key / 100 % 100 + 1));
		return new DateTime(year, month, Math.Max(1, Math.Min(DateTime.DaysInMonth(year, month), key % 100)));
	}

	public static int TodayKey()
	{
		return CLOCK.GET_CLOCK_YEAR() * 10000 + CLOCK.GET_CLOCK_MONTH() * 100 + CLOCK.GET_CLOCK_DAY_OF_MONTH();
	}

	public static string NowText()
	{
		int cLOCK_HOURS = CLOCK.GET_CLOCK_HOURS();
		int cLOCK_MINUTES = CLOCK.GET_CLOCK_MINUTES();
		return string.Format("{0} {1}, {2}  {3}:{4:00} {5}", Months[Math.Max(0, Math.Min(11, CLOCK.GET_CLOCK_MONTH()))], CLOCK.GET_CLOCK_DAY_OF_MONTH(), CLOCK.GET_CLOCK_YEAR(), (cLOCK_HOURS % 12 == 0) ? 12 : (cLOCK_HOURS % 12), cLOCK_MINUTES, (cLOCK_HOURS < 12) ? "AM" : "PM");
	}

	public static string TodayShort()
	{
		return $"{Months[Math.Max(0, Math.Min(11, CLOCK.GET_CLOCK_MONTH()))]} {CLOCK.GET_CLOCK_DAY_OF_MONTH()}";
	}
}
