using WSM.ServerRealtime;
using WSM.ServerRealtime.Scripts;

bool isRunning = false;
float updatePeriod = 1000f / Terminal.updatesPerSecond;

AppDomain.CurrentDomain.UnhandledException += GlobalUnhandledExceptionHandler;
try
{
    Console.Title = "Server Console";
    isRunning = true;
    Server.Start(Terminal.maxPlayers, Terminal.port);
} 
catch (Exception ex)
{
    Tools.LogError(ex.Message, ex.StackTrace);
}


void GlobalUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
{
    Exception ex = (Exception)e.ExceptionObject;
    Tools.LogError(ex.Message, ex.StackTrace, "Unhandled");
}
