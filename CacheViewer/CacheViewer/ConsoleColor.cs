using System;

namespace CacheViewer
{
	[Flags]
	public enum ConsoleColor : short
	{
		Black = 0,
		DarkBlue = 1,
		DarkGreen = 2,
		DarkCyan = 3,
		DarkRed = 4,
		DarkMagenta = 5,
		DarkYellow = 6,
		Gray = 7,
		DarkGray = 8,
		Blue = 9,
		Green = 10,
		Cyan = 11,
		Red = 12,
		Magenta = 13,
		Yellow = 14,
		White = 15,

		BackgroundBlack = 0 << 4,
		BackgroundDarkBlue = 1 << 4,
		BackgroundDarkGreen = 2 << 4,
		BackgroundDarkCyan = 3 << 4,
		BackgroundDarkRed = 4 << 4,
		BackgroundDarkMagenta = 5 << 4,
		BackgroundDarkYellow = 6 << 4,
		BackgroundGray = 7 << 4,
		BackgroundDarkGray = 8 << 4,
		BackgroundBlue = 9 << 4,
		BackgroundGreen = 10 << 4,
		BackgroundCyan = 11 << 4,
		BackgroundRed = 12 << 4,
		BackgroundMagenta = 13 << 4,
		BackgroundYellow = 14 << 4,
		BackgroundWhite = 15 << 4
	}
}
