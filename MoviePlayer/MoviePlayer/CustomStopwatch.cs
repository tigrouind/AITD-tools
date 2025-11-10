using System.Diagnostics;

public class CustomStopwatch
{
	long elapsed;
	long startTimeStamp;
	int multiplier = 1;

	public bool IsRunning { get; private set; }

	public int Multiplier
	{
		get => multiplier;

		set
		{
			if (value != multiplier)
			{
				if (IsRunning)
				{
					Stop();
					multiplier = value;
					Start();
				}
				else
				{
					multiplier = value;
				}
			}
		}
	}

	public void Start()
	{
		if (!IsRunning)
		{
			startTimeStamp = Stopwatch.GetTimestamp();
			IsRunning = true;
		}
	}

	public void Stop()
	{
		if (IsRunning)
		{
			elapsed += (Stopwatch.GetTimestamp() - startTimeStamp) * Multiplier;
			IsRunning = false;
		}
	}

	public void Reset()
	{
		elapsed = 0;
		startTimeStamp = 0;
		IsRunning = false;
	}

	public void Restart()
	{
		elapsed = 0;
		startTimeStamp = Stopwatch.GetTimestamp();
		IsRunning = true;
	}

	public TimeSpan Elapsed
	{
		get
		{
			var time = elapsed;
			if (IsRunning)
			{
				time += (Stopwatch.GetTimestamp() - startTimeStamp) * Multiplier;
			}
			return TimeSpan.FromTicks(time);
		}

		set
		{
			elapsed = value.Ticks;
			startTimeStamp = Stopwatch.GetTimestamp();
		}
	}
}