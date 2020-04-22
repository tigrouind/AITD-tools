﻿using System;

namespace TrackDISA
{
	public enum TrackEnum
	{
		WARP = 0,
		GOTO_POS,
		END,
		REWIND,
		MARK,
		SPEED_4, 
		SPEED_5, 
		SPEED_0, 
		ROTATE_X = 9, 
		COLLISION_DISABLE, 
		COLLISION_ENABLE,
		TRIGGERS_DISABLE = 13,
		TRIGGERS_ENABLE,	
		WARP_ROT,
		STORE_POS, 
		STAIRS_X,
		STAIRS_Z,
		ROTATE_XYZ, 
	}
}
