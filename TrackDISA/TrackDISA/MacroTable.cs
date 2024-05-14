namespace TrackDISA
{
	public static class MacroTable
	{
		public readonly static TrackEnum[] TrackA =
		{
			TrackEnum.WARP,
			TrackEnum.GOTO_POS,
			TrackEnum.END,
			TrackEnum.REWIND,
			TrackEnum.MARK,
			TrackEnum.SPEED_4,
			TrackEnum.SPEED_5,
			TrackEnum.SPEED_0,
			TrackEnum.UNUSED, //set SPEED to -1
			TrackEnum.ROTATE_X,
			TrackEnum.COLLISION_DISABLE,
			TrackEnum.COLLISION_ENABLE,
			TrackEnum.UNUSED, //do not do anything (increase offset by 2)
			TrackEnum.TRIGGERS_DISABLE,
			TrackEnum.TRIGGERS_ENABLE,
			TrackEnum.WARP_ROT,
			TrackEnum.STORE_POS,
			TrackEnum.STAIRS_X,
			TrackEnum.STAIRS_Z,
			TrackEnum.ROTATE_XYZ
		};

		public readonly static TrackEnum[] TrackB =
		{
			TrackEnum.WARP,
			TrackEnum.GOTO_POS,
			TrackEnum.END,
			TrackEnum.REWIND,
			TrackEnum.MARK,
			TrackEnum.DUMMY, //do not do anything
			TrackEnum.ROTATE_X,
			TrackEnum.COLLISION_DISABLE,
			TrackEnum.COLLISION_ENABLE,
			TrackEnum.UNUSED, //do not do anything (increase offset by 2)
			TrackEnum.TRIGGERS_DISABLE,
			TrackEnum.TRIGGERS_ENABLE
		};
	}

}
