using Carbon.Runner.Executors;

namespace Carbon.Runner;

public class InternalRunner
{
	public static DotNet DotNet = new();

	public static void Write(object message, ConsoleColor color)
	{
		var originalColor = Console.ForegroundColor;
		Console.ForegroundColor = color;
		Console.WriteLine(message);
		Console.ForegroundColor = originalColor;
	}

	public static void Log(object message) => Write(message, ConsoleColor.DarkGray);
	public static void Warn(object message) => Write(message, ConsoleColor.DarkYellow);
	public static void Error(object message) => Write(message, ConsoleColor.DarkRed);

}
