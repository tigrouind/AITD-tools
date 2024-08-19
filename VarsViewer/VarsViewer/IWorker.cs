using System;

namespace VarsViewer
{
	public interface IWorker
	{
		void Render(int view);
		bool ReadMemory();
		void KeyDown(ConsoleKeyInfo keyInfo);
		void MouseMove(int x, int y);
		void MouseDown(int x, int y);
		bool UseMouse { get; }
	}
}
