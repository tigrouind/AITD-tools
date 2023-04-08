using System;
using Shared;

namespace LifeDISA
{
	public class GameConfig
	{
		public readonly GameVersion Version;
		public readonly LifeEnum[] LifeMacro;
		public readonly EvalEnum[] EvalMacro;
		public int Offset;

		public GameConfig(GameVersion version, LifeEnum[] lifeMacro, EvalEnum[] evalMacro, int offset)
		{
			Version = version;
			LifeMacro = lifeMacro;
			EvalMacro = evalMacro;
			Offset = offset;
		}
	}
}
