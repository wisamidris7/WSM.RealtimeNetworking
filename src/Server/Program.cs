using WSM.ServerRealtime;
using WSM.ServerRealtime.Scripts;


AppDomain.CurrentDomain.UnhandledException += GlobalUnhandledExceptionHandler;
try
{
    Console.Title = "Server Console";
    Server.Start(Terminal.maxPlayers, Terminal.port);
} catch (Exception ex)
{
    Tools.LogError(ex.Message, ex.StackTrace);
}
Console.ReadKey();

void GlobalUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
{
    Exception ex = (Exception)e.ExceptionObject;
    Tools.LogError(ex.Message, ex.StackTrace, "Unhandled");
}
