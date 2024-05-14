using Shared;

namespace TrackDISA
{
	public class GameConfig
	{
		public readonly GameVersion Version;
		public readonly TrackEnum[] TrackMacro;

		public GameConfig(GameVersion version, TrackEnum[] trackMacro)
		{
			Version = version;
			TrackMacro = trackMacro;
		}
	}
}
