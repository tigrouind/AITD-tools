using MoviePlayer;

public class Movie : IDisposable
{
	readonly byte[] memory = new byte[640 * 1024];

	MovieReader? reader;
	MovieWriter? writer;
	readonly CustomStopwatch stopwatch = new();

	readonly int?[] saveStates = new int?[9];

	public bool IsLoaded => reader != null;
	public bool IsRunning => reader != null && stopwatch.IsRunning;
	public bool IsRecording => writer != null;

	public TimeSpan CurrentTime { get; private set; }
	public int CurrentFrame { get; private set; }
	public TimeSpan TotalTime { get; private set; }
	public int TotalFrames { get; private set; }

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
		Stop();
		reader = new MovieReader(filePath);

		(TotalFrames, TotalTime) = reader.ReadHeader();

		if (reader.ReadFrame(memory, out var nextTime))
		{
			CurrentTime = nextTime;
			CurrentFrame++;
		}
	}

	public void Save(string filePath)
	{
		Stop();
		writer = new MovieWriter(filePath);
		writer.WriteHeader();
	}

	public bool Stop()
	{
		Array.Clear(memory);
		stopwatch.Reset();
		CurrentFrame = 0;
		CurrentTime = TimeSpan.Zero;

		if (reader != null || writer != null)
		{
			if (reader != null)
			{
				reader.Dispose();
				reader = null;
			}

			if (writer != null)
			{
				writer.Dispose();
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
			stopwatch.Elapsed = CurrentTime + new TimeSpan(1); //force next frame
		}
	}

	public bool SaveState(int index)
	{
		if (reader != null && TotalFrames > 0)
		{
			saveStates[index] = CurrentFrame;
			return true;
		}

		return false;
	}

	public bool RestoreState(int index)
	{
		var state = saveStates[index];
		if (reader != null && state != null && state.Value <= TotalFrames)
		{
			CurrentFrame = state.Value;
			if (reader.Seek(CurrentFrame, memory, out var nextTime))
			{
				CurrentTime = nextTime;
			}
			stopwatch.Elapsed = CurrentTime;
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
		var enoughTimeElapsed = elapsed > CurrentTime;
		if (enoughTimeElapsed)
		{
			if (!reader.ReadFrame(memory, out var nextTime))
			{
				return false;
			}

			CurrentFrame++;
			CurrentTime = nextTime;
		}

		return enoughTimeElapsed;
	}

	public void WriteFrame()
	{
		if (writer == null)
		{
			return;
		}

		if (CurrentTime == TimeSpan.Zero)
		{
			stopwatch.Restart();
		}

		if (stopwatch.Elapsed > CurrentTime)
		{
			writer.WriteFrame(memory, CurrentTime);

			var interval = TimeSpan.FromMilliseconds(1000.0 / 60.0);
			var elapsed = stopwatch.Elapsed;
			var nearest = elapsed - new TimeSpan(elapsed.Ticks % interval.Ticks);
			if ((nearest - CurrentTime) < interval)
			{
				CurrentTime = nearest + interval;
			}
			else
			{
				CurrentTime = nearest;
			}

			CurrentFrame++;
		}
	}

	public void Dispose()
	{
		Stop();
	}
}

