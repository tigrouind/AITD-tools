using System;

namespace VarsViewer
{
	public interface IWorker
	{
		void Render();
		void ReadMemory();
		void KeyDown(ConsoleKeyInfo keyInfo);
		void MouseMove(int x, int y);
		void MouseDown(int x, int y);
		void Resize(int width, int height);
		void MouseWheel(int delta);
		bool UseMouse { get; }
	}
}
