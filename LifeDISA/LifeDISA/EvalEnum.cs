
using System;
using System.ComponentModel;

namespace LifeDISA
{
	public enum EvalEnum
	{
		NONE = -1,
		ACTOR_COLLIDER,
		[Description("TRIGGER_COLLIDER")]
		TRIGGER_COLLIDER_1,
		HARD_COLLIDER,
		HIT,
		HIT_BY,
		ANIM,
		END_ANIM,
		FRAME,
		END_FRAME,
		BODY,
		MARK,
		NUM_TRACK,
		CHRONO,
		ROOM_CHRONO,
		DIST,
		[Description("COL_BY")]
		COL_BY_1,
		ISFOUND,
		ACTION,
		POSREL,
		KEYBOARD_INPUT,
		SPACE,
		[Description("COL_BY")]
		COL_BY_2,
		ALPHA,
		BETA,
		GAMMA,
		INHAND,
		HITFORCE,
		CAMERA,
		RAND,
		FALLING,
		ROOM,
		LIFE,
		OBJECT,
		#if !JITD
		ROOMY,	
		TEST_ZV_END_ANIM,	
		MUSIC,		
		#else
		MUSIC,	
		TEST_ZV_END_ANIM,
		UNKNOWN,
		#endif
		C_VAR,
		#if !JITD
		STAGE,
		THROW
		#endif
		#if JITD
		MATRIX,
		[Description("TRIGGER_COLLIDER")]
		TRIGGER_COLLIDER_2
		#endif
	}
}
