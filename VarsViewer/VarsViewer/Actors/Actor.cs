namespace VarsViewer
{
	public class Actor
	{
		public int Id;
		public byte[] Values;

		public long CreationTime;
		public long DeletionTime;
		public long[] UpdateTime;

		public bool Created;
		public bool Deleted;
		public bool[] Updated;
	}
}
