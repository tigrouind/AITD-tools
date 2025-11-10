using MoviePlayer;
using System.Threading.Tasks;

public class Movie : IDisposable
{
	readonly byte[] memory = new byte[640 * 1024];

	MovieReader? reader;
	MovieWriter? writer;
	readonly CustomStopwatch stopwatch = new();

	readonly (long Offset, TimeSpan CurrentTime, byte[]? Memory)[] saveStates = new (long Offset, TimeSpan CurrentTime, byte[]? Memory)[9];
	long saveOffset;
	string? lastFilePath;
	TimeSpan currentTime;

	public bool IsLoaded => reader != null;
	public bool IsRunning => reader != null && stopwatch.IsRunning;
	public bool IsRecording => writer != null;
	public TimeSpan CurrentTime => currentTime;

	public TimeSpan TotalTime => reader == null ? TimeSpan.Zero : reader.TotalTime;

	public byte[] Memory => memory;

	public int PlaybackSpeed
	{
		get => stopwatch.Multiplier;
		set
		{
			stopwatch.Multiplier = value;
		}
	}

	public void Resume()
	{
		if (reader != null)
		{
			stopwatch.Start();
		}
	}

	public void Pause()
	{
		if (reader != null)
		{
			stopwatch.Stop();
		}
	}

	public void Load(string filePath)
	{
		if (filePath != lastFilePath)
		{
			lastFilePath = filePath;
			Array.Clear(saveStates);
		}

		Stop();
		reader = new MovieReader(filePath);
		reader.ReadFrame(memory, out currentTime);
	}

	public void Save(string filePath)
	{
		Stop();
		writer = new MovieWriter(filePath);
	}

	public bool Stop()
	{
		Array.Clear(memory);
		stopwatch.Reset();
		saveOffset = 0;
		currentTime = TimeSpan.Zero;

		if (reader != null || writer != null)
		{
			if (reader != null)
			{
				reader.Close();
				reader = null;
			}

			if (writer != null)
			{
				writer.Close();
				writer = null;
			}

			return true;
		}

		return false;
	}

	public void SingleStep()
	{
		if (reader != null)
		{
			stopwatch.Stop();
			stopwatch.Elapsed = currentTime + new TimeSpan(1); //force next frame
		}
	}

	public bool SaveState(int index)
	{
		if (reader != null)
		{
			saveStates[index] = (saveOffset, currentTime, reader.PreviousFrame(memory).ToArray());
			return true;
		}

		return false;
	}

	public bool RestoreState(int index)
	{
		var state = saveStates[index];
		if (reader != null && state.Memory != null)
		{
			(saveOffset, currentTime, var newMemory) = saveStates[index];
			Array.Copy(newMemory!, memory, newMemory!.Length);
			reader.Position = saveOffset;
			stopwatch.Elapsed = currentTime; //force next frame
			return true;
		}

		return false;
	}

	public bool ReadFrame()
	{
		if (reader == null)
		{
			return false;
		}

		var elapsed = stopwatch.Elapsed;
		var enoughTimeElapsed = elapsed > currentTime;
		if (enoughTimeElapsed)
		{
			saveOffset = reader.Position;
			if (!reader.ReadFrame(memory, out var nextTime))
			{
				return false;
			}

			currentTime = nextTime;
		}

		return enoughTimeElapsed;
	}

	public void WriteFrame()
	{
		if (writer == null)
		{
			return;
		}

		if (currentTime == TimeSpan.Zero)
		{
			stopwatch.Restart();
		}

		if (stopwatch.Elapsed > currentTime)
		{
			writer.WriteFrame(memory, currentTime);

			var interval = TimeSpan.FromMilliseconds(1000.0 / 60.0);
			var nearest = stopwatch.Elapsed - new TimeSpan(stopwatch.Elapsed.Ticks % interval.Ticks);
			currentTime = nearest + interval;
		}
	}

	public void Dispose()
	{
		Stop();
	}
}

