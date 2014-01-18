namespace Sharpen
{
	using System;

	public enum TimeUnit : long
	{
        Milliseconds = 1,
        Seconds = 1000
	}

	internal static class TimeUnitExtensions
	{
		public static long Convert (this TimeUnit thisUnit, long duration, TimeUnit targetUnit)
		{
			return ((duration * (long)targetUnit) / (long)thisUnit);
		}
	}
}
