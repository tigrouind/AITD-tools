using System;

namespace VarsViewer
{
	public class GameConfig
	{
		public int VarsAddress;
		public int CvarAddress;
		
		public GameConfig(int varsAddress, int cvarAddress)
		{
			VarsAddress = varsAddress;
			CvarAddress = cvarAddress;
		}
	}
}
