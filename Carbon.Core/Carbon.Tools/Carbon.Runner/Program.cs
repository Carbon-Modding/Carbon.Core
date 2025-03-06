using System.Reflection;
using Carbon.Runner;

InternalRunner.Run(args.Length > 0 ? args[0] : string.Empty, args, true);
Console.ReadLine();
