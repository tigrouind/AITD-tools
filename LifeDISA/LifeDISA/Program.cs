using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Shared;

namespace LifeDISA
{
	class Program
	{
		static int pos;
		static byte[] allBytes;

		static readonly Dictionary<int, string> objectsByIndex = new Dictionary<int, string>();
		static Dictionary<int, string> namesByIndex;

		static (GameVersion Version, LifeEnum[] LifeMacro, EvalEnum[] EvalMacro, int Offset) config;
		static readonly LinkedList<Instruction> nodes = new LinkedList<Instruction>();
		static readonly Dictionary<int, LinkedListNode<Instruction>> nodesMap = new Dictionary<int, LinkedListNode<Instruction>>();
		static readonly string[] flagsNames = { "anim", string.Empty, string.Empty, "back", "push", "coll", "trig", "pick", "grav" };
		static readonly string[] foundFlagsNames = { "use", "eat_or_drink", "read", "reload", "fight", "jump", "open_or_search", "close", "push", "throw", "drop_or_put" };

		static readonly VarParserForScript vars = new VarParserForScript();

		static readonly (GameVersion Version, LifeEnum[] LifeMacro, EvalEnum[] EvalMacro, int Offset)[] gameConfigs =
		{
			(GameVersion.AITD1        , MacroTable.LifeA, MacroTable.EvalA, 52),
			(GameVersion.AITD1_FLOPPY , MacroTable.LifeA, MacroTable.EvalA, 52),
			(GameVersion.AITD1_DEMO   , MacroTable.LifeA, MacroTable.EvalA, 52),
			(GameVersion.AITD2        , MacroTable.LifeB, MacroTable.EvalB, 54),
			(GameVersion.AITD2_DEMO   , MacroTable.LifeB, MacroTable.EvalB, 54),
			(GameVersion.AITD3        , MacroTable.LifeB, MacroTable.EvalB, 54),
			(GameVersion.JACK         , MacroTable.LifeB, MacroTable.EvalB, 54),
			(GameVersion.TIMEGATE     , MacroTable.LifeB, MacroTable.EvalB, 54),
			(GameVersion.TIMEGATE_DEMO, MacroTable.LifeB, MacroTable.EvalB, 54)
		};

		public static int Main(string[] args)
		{
			var version = Tools.GetArgument<GameVersion?>(args, "-version");
			bool verbose = Tools.HasArgument(args, "-verbose");
			bool raw = Tools.HasArgument(args, "-raw");
			string outputFile = Tools.GetArgument<string>(args, "-output") ?? "scripts.vb";

			config = gameConfigs.FirstOrDefault(x => x.Version == version);
			if (version == null || config == default)
			{
				var versions = string.Join("|", gameConfigs.Select(x => x.Version.ToString().ToLowerInvariant()));
				Console.WriteLine($"Usage: LifeDISA -version {{{versions}}} [-raw] [-verbose] [-output]");
				return -1;
			}

			Directory.CreateDirectory("GAMEDATA");

			//parse vars
			if (File.Exists(@"GAMEDATA\vars.txt"))
			{
				vars.Load(@"GAMEDATA\vars.txt",
						VarEnum.LIFES,
						VarEnum.BODYS,
						VarEnum.MUSIC,
						VarEnum.ANIMS,
						VarEnum.SPECIAL,
						VarEnum.TRACKS,
						VarEnum.POSREL,
						VarEnum.VARS,
						VarEnum.CVARS,
						VarEnum.SOUNDS,
						VarEnum.ACTIONS,
						VarEnum.KEYBOARD_INPUT,
						VarEnum.TRACKMODE);
			}

			int fileCount = 0;
			if (File.Exists(@"GAMEDATA\LISTLIFE.PAK"))
			{
				using (var pak = new PakArchive(@"GAMEDATA\LISTLIFE.PAK"))
				{
					fileCount = pak.Count;
				}
			}

			namesByIndex = Language.Load();

			if (File.Exists(@"GAMEDATA\OBJETS.ITD"))
			{
				allBytes = File.ReadAllBytes(@"GAMEDATA\OBJETS.ITD");
				int count = allBytes.ReadShort(0);

				for (int i = 0 ; i < count ; i++)
				{
					string name = null;
					int n = i * config.Offset + 2;
					int body = allBytes.ReadShort(n + 2);
					if (body != -1)
					{
						name = vars.GetText(VarEnum.BODYS, body, string.Empty, false);
					}

					if (string.IsNullOrEmpty(name))
					{
						int index = allBytes.ReadShort(n + 10);
						if (index != -1 && index != 0 && namesByIndex.TryGetValue(index, out name))
						{
							name = name.ToLowerInvariant();
							name = string.Join("_", name.Split(new char[] { ' ', '\'' }).Where(x => x != "an" && x != "a").ToArray());
						}
					}

					if (string.IsNullOrEmpty(name))
					{
						int life = allBytes.ReadShort(n + 34);
						if (life != -1)
						{
							name = vars.GetText(VarEnum.LIFES, life, string.Empty, false);
						}
					}

					if (!string.IsNullOrEmpty(name))
					{
						objectsByIndex.Add(i, name.ToLowerInvariant());
					}
				}
			}

			using (var writer = new Writer(outputFile))
			using (var pak = new PakArchive(@"GAMEDATA\LISTLIFE.PAK"))
			{
				//dump all
				foreach (var entry in pak)
				{
					writer.WriteLine("'--------------------------------------------------");
					writer.WriteLine($"'#{entry.Index} {vars.GetText(VarEnum.LIFES, entry.Index, string.Empty, false)}");
					writer.WriteLine("'--------------------------------------------------");
					allBytes = entry.Read();

					try
					{
						ParseFile();
						if (!raw)
						{
							var optimizer = new Optimizer(nodes, nodesMap);
							optimizer.Run();

							if (nodes.Any(x => x.Type == LifeEnum.GOTO))
							{
								throw new Exception("Unexpected gotos");
							}

							ProcessCaseStatements();
						}
						else
						{
							ProcessGoto();
						}

						writer.Dump(nodes, raw, verbose, allBytes);
					}
					catch (Exception ex)
					{
						writer.WriteLine(ex);
					}

					writer.WriteLine();
				}
			}

			return 0;
		}

		static void ProcessCaseStatements()
		{
			foreach (var ins in GetNodes(nodes))
			{
				switch (ins.Type)
				{
					case LifeEnum.CASE:
					case LifeEnum.MULTI_CASE:
					{
						if (ins.Parent != null)
						{
							for (int i = 0 ; i < ins.Arguments.Count ; i++)
							{
								ins.Set(i, GetConditionName(ins.Parent.Value.EvalEnum, ins.Arguments[i]));
							}
						}
					}
					break;
				}
			}

			IEnumerable<Instruction> GetNodes(IEnumerable<Instruction> nodes)
			{
				foreach (var node in nodes)
				{
					yield return node;

					if (node.NodesA != null)
					{
						foreach (var childNode in GetNodes(node.NodesA))
						{
							yield return childNode;
						}
					}

					if (node.NodesB != null)
					{
						foreach (var childNode in GetNodes(node.NodesB))
						{
							yield return childNode;
						}
					}
				}
			}
		}

		static void ProcessGoto()
		{
			foreach (var ins in nodes)
			{
				switch (ins.Type)
				{
					case LifeEnum.IF_EGAL:
					case LifeEnum.IF_DIFFERENT:
					case LifeEnum.IF_SUP_EGAL:
					case LifeEnum.IF_SUP:
					case LifeEnum.IF_INF_EGAL:
					case LifeEnum.IF_INF:
					case LifeEnum.IF_IN:
					case LifeEnum.IF_OUT:
					case LifeEnum.CASE:
					case LifeEnum.MULTI_CASE:
						ins.Add("goto " + ins.Goto);
						break;

					case LifeEnum.GOTO:
						ins.Add(ins.Goto.ToString());
						break;
				}
			}
		}

		static void ParseFile()
		{
			pos = 0;
			nodes.Clear();
			nodesMap.Clear();

			while (pos < allBytes.Length)
			{
				int position = pos;

				int curr = allBytes.ReadShort(pos);
				int actor = -1;
				if ((curr & 0x8000) == 0x8000)
				{
					curr &= 0x7FFF;
					pos += 2;
					actor = allBytes.ReadShort(pos);
				}

				if (curr < 0 || curr >= config.LifeMacro.Length)
				{
					throw new IndexOutOfRangeException(curr.ToString());
				}
				LifeEnum life = config.LifeMacro[curr];

				Instruction ins = new Instruction
				{
					Type = life
				};

				if (actor != -1) ins.Actor = GetObjectName(actor);
				pos += 2;

				ParseArguments(life, ins);
				var node = nodes.AddLast(ins);
				nodesMap.Add(position, node);
				ins.Position = position;
				ins.Size = pos - position;
			}
		}

		static void ParseArguments(LifeEnum life, Instruction ins)
		{
			switch (life)
			{
				case LifeEnum.IF_EGAL:
				case LifeEnum.IF_DIFFERENT:
				case LifeEnum.IF_SUP_EGAL:
				case LifeEnum.IF_SUP:
				case LifeEnum.IF_INF_EGAL:
				case LifeEnum.IF_INF:
					string paramA = Evalvar(out EvalEnum evalEnum);
					string paramB = Evalvar();

					paramB = GetConditionName(evalEnum, paramB);
					ins.Add(paramA);
					ins.Add(paramB);

					ins.Goto = GetParam() * 2 + pos;
					break;

				case LifeEnum.GOTO: //should never be called
					ins.Goto = GetParam() * 2 + pos;
					break;

				case LifeEnum.SWITCH:
					ins.Add(Evalvar(out ins.EvalEnum));
					break;

				case LifeEnum.CASE:
					ins.Add(GetParam());
					ins.Goto = GetParam() * 2 + pos;
					break;

				case LifeEnum.MULTI_CASE:
					int numCases = GetParam();
					for (int n = 0; n < numCases; n++)
					{
						ins.Add(GetParam());
					}

					ins.Goto = GetParam() * 2 + pos;
					break;

				case LifeEnum.IF_IN:
				case LifeEnum.IF_OUT:
					ins.Add(Evalvar());
					ins.Add(Evalvar());
					ins.Add(Evalvar());

					ins.Goto = GetParam() * 2 + pos;
					break;

				case LifeEnum.SOUND:
					if (config.Version == GameVersion.AITD2 || config.Version == GameVersion.AITD3)
					{
						ins.Add(vars.GetText(VarEnum.SOUNDS, GetParam()));
					}
					else if (config.Version == GameVersion.TIMEGATE_DEMO)
					{
						ins.Add(vars.GetText(VarEnum.SOUNDS, GetParam()));
						ins.Add(vars.GetText(VarEnum.SOUNDS, GetParam()));
					}
					else if (config.Version == GameVersion.TIMEGATE)
					{
						ins.Add(vars.GetText(VarEnum.SOUNDS, Evalvar()));
						ins.Add(vars.GetText(VarEnum.SOUNDS, GetParam()));
					}
					else
					{
						ins.Add(vars.GetText(VarEnum.SOUNDS, Evalvar()));
					}
					break;

				case LifeEnum.BODY:
					ins.Add(vars.GetText(VarEnum.BODYS, Evalvar()));
					break;

				case LifeEnum.SAMPLE_THEN:
					if (config.Version == GameVersion.AITD2 || config.Version == GameVersion.AITD3 || config.Version == GameVersion.TIMEGATE || config.Version == GameVersion.TIMEGATE_DEMO)
					{
						ins.Add(vars.GetText(VarEnum.SOUNDS, GetParam()));
						ins.Add(vars.GetText(VarEnum.SOUNDS, GetParam()));
					}
					else
					{
						ins.Add(vars.GetText(VarEnum.SOUNDS, Evalvar()));
						ins.Add(vars.GetText(VarEnum.SOUNDS, Evalvar()));
					}
					break;

				case LifeEnum.DELETE:
				case LifeEnum.IN_HAND:
					if (config.Version == GameVersion.AITD2 || config.Version == GameVersion.AITD3 || config.Version == GameVersion.TIMEGATE || config.Version == GameVersion.TIMEGATE_DEMO)
					{
						ins.Add(GetObjectName(Evalvar()));
					}
					else
					{
						ins.Add(GetObjectName(GetParam()));
					}
					break;

				case LifeEnum.CAMERA_TARGET:
				case LifeEnum.TAKE:
				case LifeEnum.FOUND:
					ins.Add(GetObjectName(GetParam()));
					break;

				case LifeEnum.FOUND_NAME:
				case LifeEnum.MESSAGE:
					ins.Add(GetMessage(GetParam()));
					break;

				case LifeEnum.FOUND_FLAG:
					ins.Add(GetFlags(GetParam(), foundFlagsNames));
					break;

				case LifeEnum.FLAGS:
					ins.Add(GetFlags(GetParam(), flagsNames));
					break;

				case LifeEnum.LIFE:
					ins.Add(vars.GetText(VarEnum.LIFES, GetParam()));
					break;

				case LifeEnum.FOUND_BODY:
					ins.Add(vars.GetText(VarEnum.BODYS, GetParam()));
					break;

				case LifeEnum.NEXT_MUSIC:
				case LifeEnum.MUSIC:
					ins.Add(vars.GetText(VarEnum.MUSIC, GetParam()));
					break;

				case LifeEnum.ANIM_REPEAT:
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					break;

				case LifeEnum.SPECIAL:
					ins.Add(vars.GetText(VarEnum.SPECIAL, GetParam()));
					break;

				case LifeEnum.STOP_SAMPLE:
					if (config.Version == GameVersion.TIMEGATE || config.Version == GameVersion.TIMEGATE_DEMO)
					{
						ins.Add(GetParam());
					}
					break;

				case LifeEnum.READ_ON_PICTURE:
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;

				case LifeEnum.ANIM_RESET:
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;

				case LifeEnum.DO_ROT_CLUT:
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;

				case LifeEnum.PLAY_SEQUENCE:
					if (config.Version == GameVersion.AITD2 || config.Version == GameVersion.AITD3 || config.Version == GameVersion.TIMEGATE || config.Version == GameVersion.TIMEGATE_DEMO)
					{
						ins.Add(GetParam());
						ins.Add(GetParam());
						ins.Add(GetParam());
					}
					else
					{
						ins.Add(GetParam());
					}
					break;

				case LifeEnum.FIRE_UP_DOWN:
					ins.Add(Evalvar());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(Evalvar());
					break;

				case LifeEnum.DEF_ABS_ZV:
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;

				case LifeEnum.STAGE_LIFE:
				case LifeEnum.SET_GROUND:
				case LifeEnum.SET_INVENTORY:
				case LifeEnum.COPY_ANGLE:
				case LifeEnum.TEST_COL:
				case LifeEnum.LIFE_MODE:
				case LifeEnum.FOUND_WEIGHT:
				case LifeEnum.ALLOW_INVENTORY:
				case LifeEnum.WATER:
				case LifeEnum.RND_FREQ:
				case LifeEnum.LIGHT:
				case LifeEnum.SHAKING:
					ins.Add(GetParam());
					break;

				case LifeEnum.FADE_MUSIC:
					ins.Add(GetParam());
					if (config.Version == GameVersion.TIMEGATE || config.Version == GameVersion.TIMEGATE_DEMO)
					{
						ins.Add(GetParam());
					}
					break;

				case LifeEnum.READ:
					ins.Add(GetParam());
					ins.Add(GetParam());
					if (config.Version == GameVersion.AITD1)
					{
						ins.Add(GetParam());
					}
					break;

				case LifeEnum.PUT_AT:
					ins.Add(GetObjectName(GetParam()));
					ins.Add(GetObjectName(GetParam()));
					if (config.Version == GameVersion.TIMEGATE || config.Version == GameVersion.TIMEGATE_DEMO)
					{
						ins.Add(GetObjectName(GetParam()));
					}
					break;

				case LifeEnum.TRACKMODE:
				{
					int curr = GetParam();
					ins.Add(vars.GetText(VarEnum.TRACKMODE, curr));

					switch (curr)
					{
						case 0: //none
						case 1: //manual
							GetParam();
							break;

						case 2: //follow
							ins.Add(GetObjectName(GetParam()));
							break;

						case 3: //track
							ins.Add(vars.GetText(VarEnum.TRACKS, GetParam()));
							break;

						default:
							ins.Add(GetParam());
							break;
					}
					break;
				}

				case LifeEnum.ANIM_ONCE:
				case LifeEnum.ANIM_ALL_ONCE:
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					break;

				case LifeEnum.ANIM_HYBRIDE_REPEAT:
				case LifeEnum.ANIM_HYBRIDE_ONCE:
				case LifeEnum.SET_BETA:
				case LifeEnum.SET_ALPHA:
				case LifeEnum.HIT_OBJECT:
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;

				case LifeEnum.ENDLIFE:
					break;

				case LifeEnum.PLUIE:
				case LifeEnum.DEL_INVENTORY:
				case LifeEnum.CONTINUE_TRACK:
				case LifeEnum.RESET_MOVE_MANUAL:
				case LifeEnum.END_SEQUENCE:
				case LifeEnum.UP_COOR_Y:
				case LifeEnum.GET_HARD_CLIP:
				case LifeEnum.RETURN:
				case LifeEnum.DO_MAX_ZV:
				case LifeEnum.GAME_OVER:
				case LifeEnum.WAIT_GAME_OVER:
				case LifeEnum.STOP_HIT_OBJECT:
				case LifeEnum.START_CHRONO:
				case LifeEnum.DO_MOVE:
				case LifeEnum.MANUAL_ROT:
				case LifeEnum.DO_ROT_ZV:
				case LifeEnum.DO_REAL_ZV:
				case LifeEnum.DO_CARRE_ZV:
				case LifeEnum.DO_NORMAL_ZV:
				case LifeEnum.CALL_INVENTORY:
				case LifeEnum.PROTECT:
				case LifeEnum.STOP_CLUT:
				case LifeEnum.UNKNOWN_6:
					break;

				case LifeEnum.UNKNOWN_5:
					int count = GetParam();
					for (int i = 0 ; i < count ; i++)
					{
						ins.Add(Evalvar());
					}
					break;

				case LifeEnum.SET_VOLUME_SAMPLE:
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;

				case LifeEnum.MUSIC_B:
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;

				case LifeEnum.START_FADE_IN_MUSIC:
				case LifeEnum.START_FADE_IN_MUSIC_LOOP:
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;

				case LifeEnum.MUSIC_AND_LOOP:
				case LifeEnum.FADE_OUT_MUSIC_STOP:
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;

				case LifeEnum.HIT:
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(Evalvar());
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					break;

				case LifeEnum.DEF_ZV:
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;

				case LifeEnum.FIRE:
					bool useEval = config.Version == GameVersion.AITD2 || config.Version == GameVersion.AITD2_DEMO || config.Version == GameVersion.AITD3 || config.Version == GameVersion.TIMEGATE || config.Version == GameVersion.TIMEGATE_DEMO;
					if (useEval)
					{
						ins.Add(vars.GetText(VarEnum.ANIMS, Evalvar()));
					}
					else
					{
						ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					}

					ins.Add(GetParam()); //shoot frame
					ins.Add(GetParam()); //hotpoint
					ins.Add(GetParam()); //range
					ins.Add(GetParam()); //hitforce

					if (useEval)
					{
						ins.Add(GetParam());
						ins.Add(vars.GetText(VarEnum.ANIMS, Evalvar()));
					}
					else
					{
						ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					}
					break;

				case LifeEnum.ANIM_MOVE:
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					break;

				case LifeEnum.THROW:
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetObjectName(GetParam()));
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					break;

				case LifeEnum.ANGLE:
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;

				case LifeEnum.PICTURE:
					if (config.Version == GameVersion.TIMEGATE_DEMO)
					{
						ins.Add(GetParam());
						ins.Add(GetParam());
						ins.Add(Evalvar());
					}
					else if (config.Version == GameVersion.TIMEGATE)
					{
						ins.Add(GetParam());
						ins.Add(GetParam());
						ins.Add(GetParam());
						ins.Add(Evalvar());
					}
					else
					{
						ins.Add(GetParam());
						ins.Add(GetParam());
						ins.Add(GetParam());
					}
					break;

				case LifeEnum.CHANGEROOM:
					ins.Add($"E{GetParam()}R{GetParam()}");
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;

				case LifeEnum.REP_SAMPLE:
					if (config.Version == GameVersion.AITD2 || config.Version == GameVersion.AITD3 || config.Version == GameVersion.TIMEGATE_DEMO)
					{
						ins.Add(vars.GetText(VarEnum.SOUNDS, GetParam()));
					}
					else
					{
						ins.Add(vars.GetText(VarEnum.SOUNDS, Evalvar()));
					}
					ins.Add(GetParam());
					break;

				case LifeEnum.DEF_SEQUENCE_SAMPLE:
					pos = GetParam() * 4 + pos;
					break;

				case LifeEnum.DROP:
					ins.Add(GetObjectName(Evalvar()));
					ins.Add(GetObjectName(GetParam()));
					break;

				case LifeEnum.PUT:
					ins.Add(GetObjectName(GetParam()));
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add($"E{GetParam()}R{GetParam()}");
					ins.Add(GetParam());
					ins.Add(GetParam());
					ins.Add(GetParam());
					break;

				case LifeEnum.ANIM_SAMPLE:
					ins.Add(vars.GetText(VarEnum.SOUNDS, Evalvar()));
					ins.Add(vars.GetText(VarEnum.ANIMS, GetParam()));
					ins.Add(GetParam());
					if (config.Version == GameVersion.TIMEGATE || config.Version == GameVersion.TIMEGATE_DEMO)
					{
						ins.Add(GetParam());
					}
					break;

				case LifeEnum.VAR:
				{
					int curr = GetParam();
					string name = vars.GetText(VarEnum.VARS, curr, "var_" + curr);
					string value = Evalvar();

					if (name == "player_current_action" && int.TryParse(value, out int result))
					{
						value = GetFlags(result, foundFlagsNames);
					}

					ins.Add(name);
					ins.Add(value);
					break;
				}

				case LifeEnum.ADD:
				case LifeEnum.SUB:
				{
					int curr = GetParam();
					ins.Add(vars.GetText(VarEnum.VARS, curr, "var_" + curr));
					ins.Add(Evalvar());
					break;
				}

				case LifeEnum.INC:
				case LifeEnum.DEC:
				{
					int curr = GetParam();
					ins.Add(vars.GetText(VarEnum.VARS, curr, "var_" + curr));
					break;
				}

				case LifeEnum.C_VAR:
				{
					int curr = GetParam();
					ins.Add(vars.GetText(VarEnum.CVARS, curr, "cvar_" + curr));
					ins.Add(Evalvar());
					break;
				}

				case LifeEnum.BODY_RESET:
					ins.Add(Evalvar());
					ins.Add(Evalvar());
					break;

				default:
					throw new NotSupportedException(life.ToString());
			}
		}

		#region Get

		static string GetConditionName(EvalEnum evalEnum, string value)
		{
			switch (evalEnum)
			{
				case EvalEnum.INHAND:
				case EvalEnum.COL_BY:
				case EvalEnum.CONTACT:
				case EvalEnum.HIT_BY:
				case EvalEnum.HIT:
				case EvalEnum.ACTOR_COLLIDER:
					return GetObjectName(value);

				case EvalEnum.POSREL:
					return vars.GetText(VarEnum.POSREL, value);

				case EvalEnum.ACTION:
					return vars.GetText(VarEnum.ACTIONS, value);

				case EvalEnum.ANIM:
					return vars.GetText(VarEnum.ANIMS, value);

				case EvalEnum.BODY:
					return vars.GetText(VarEnum.BODYS, value);

				case EvalEnum.KEYBOARD_INPUT:
					return vars.GetText(VarEnum.KEYBOARD_INPUT, value);

				case EvalEnum.NUM_TRACK:
					return vars.GetText(VarEnum.TRACKS, value);

				case EvalEnum.MUSIC:
					return vars.GetText(VarEnum.MUSIC, value);

				case EvalEnum.LIFE:
					return vars.GetText(VarEnum.LIFES, value);

				default:
					return value;
			}
		}

		static string GetObjectName(string index)
		{
			if (int.TryParse(index, out int value))
			{
				index = GetObjectName(value);
			}

			return index;
		}

		static string GetObjectName(int index)
		{
			if (index == -1)
			{
				return "-1";
			}

			if (index == 1)
			{
				return "player";
			}

			if (objectsByIndex.TryGetValue(index, out string text))
			{
				return text + "_" + index;
			}
			return "obj_" + index;
		}

		static string GetMessage(int index)
		{
			if (namesByIndex.TryGetValue(index, out string text))
			{
				return "\"" + text + "\"";
			}
			return "msg_" + index;
		}

		static int GetParam()
		{
			int curr = allBytes.ReadShort(pos);
			pos += 2;
			return curr;
		}

		static string GetFlags(int flags, string[] names)
		{
			if (flags == 0)
			{
				return "none";
			}

			StringBuilder result = new StringBuilder();
			int flag = 1;
			for (int i = 0 ; i < names.Length ; i++)
			{
				if ((flags & flag) != 0 && !string.IsNullOrEmpty(names[i]))
				{
					if (result.Length > 0)
					{
						result.Append(" ");
					}
					result.Append(names[i]);
				}
				flag <<= 1;
			}

			return result.ToString();
		}

		#endregion

		#region Eval

		static string Evalvar()
		{
			return Evalvar(out _);
		}

		static string Evalvar(out EvalEnum evalEnum)
		{
			evalEnum = EvalEnum.NONE;
			int curr = GetParam();
			if (curr == -1)
			{
				//constant
				return GetParam().ToString();
			}

			if (curr == 0)
			{
				//variable
				curr = GetParam();
				string name =  vars.GetText(VarEnum.VARS, curr, "var_" + curr);
				if (name == "player_current_action")
				{
					evalEnum = EvalEnum.ACTION;
				}

				return name;
			}

			//function
			string result = string.Empty;
			if ((curr & 0x8000) == 0x8000)
			{
				//change actor
				result = GetObjectName(GetParam()) + ".";
			}

			curr &= 0x7FFF;
			curr--;
			if (curr < 0 || curr >= config.EvalMacro.Length)
			{
				throw new IndexOutOfRangeException(curr.ToString());
			}

			evalEnum = config.EvalMacro[curr];

			string parameter = evalEnum.ToString().ToLowerInvariant();

			switch (evalEnum)
			{
				case EvalEnum.DIST:
					parameter += $"({GetObjectName(GetParam())})";
					break;

				case EvalEnum.POSREL:
					if (config.Version == GameVersion.TIMEGATE || config.Version == GameVersion.TIMEGATE_DEMO)
					{
						parameter += $"({Evalvar()})";
					}
					else
					{
						parameter += $"({GetObjectName(GetParam())})";
					}
					break;

				case EvalEnum.OBJECT:
				case EvalEnum.THROW:
					parameter += $"({GetObjectName(GetParam())})";
					break;

				case EvalEnum.ISFOUND:
					parameter += $"({GetObjectName(Evalvar())})";
					break;

				case EvalEnum.RAND:
					parameter += $"({GetParam()})";
					break;

				case EvalEnum.C_VAR:
					parameter = vars.GetText(VarEnum.CVARS, GetParam(), "cvar_" + curr);
					break;

				case EvalEnum.TEST_ZV_END_ANIM:
				case EvalEnum.MATRIX:
					parameter += $"({GetParam()} {GetParam()})";
					break;

				case EvalEnum.DIV_BY_2:
					parameter += $"({GetParam()})";
					break;
			}

			result += parameter;
			return result;
		}

		#endregion
	}
}