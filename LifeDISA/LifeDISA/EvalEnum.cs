﻿
using System;

namespace LifeDISA
{
	public enum EvalEnum
	{
		NONE = -1,
		ACTOR_COLLIDER,
		TRIGGER_COLLIDER,
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
		COL_BY,
		ISFOUND,
		ACTION,
		POSREL,
		KEYBOARD_INPUT,
		SPACE,
		CONTACT,
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
		HARD_MAT
		#endif
	}
}
