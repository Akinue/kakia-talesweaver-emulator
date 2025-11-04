



using kakia_talesweaver_emulator.Network;
using kakia_talesweaver_logging;

TalesServer server = new("127.0.0.1", 20000);
CancellationTokenSource ct = new();

Task serverTask = server.Run(ct.Token);

Logger.Log("==== [Server is now running, press Q to quit gracefully] ====", LogLevel.Information);
while (Console.ReadKey().KeyChar.ToString().ToLower() != "q")
{

	await Task.Delay(1000);
}
//server.Stop();
ct.Cancel();
Task.WaitAll(serverTask);