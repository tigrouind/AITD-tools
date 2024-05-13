namespace VarsViewer
{
	public class GameConfig
	{
		public readonly int VarsAddress;
		public readonly int CvarAddress;

		public GameConfig(int varsAddress, int cvarAddress)
		{
			VarsAddress = varsAddress;
			CvarAddress = cvarAddress;
		}
	}
}
