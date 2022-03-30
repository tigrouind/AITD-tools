using System;
using Shared;

namespace LifeDISA
{
	public class GameConfig
	{
		public readonly GameVersion Version;
		public readonly LifeEnum[] LifeMacro;
		public readonly EvalEnum[] EvalMacro;
	
		public GameConfig(GameVersion version, LifeEnum[] lifeMacro, EvalEnum[] evalMacro)
		{
			Version = version;
			LifeMacro = lifeMacro;
			EvalMacro = evalMacro;			
		}
	}
}
