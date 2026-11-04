namespace Duofy.Services.Push
{
    /// <summary>
    /// Der Zugang der Plattformteile zu den Diensten der App. Ein Signal kann die App starten, ohne dass je eine Oberfläche
    /// da war; dann baut Android nur die Anwendung (MauiProgram) und ruft den Empfänger. Jeder Aufruf bekommt seinen
    /// eigenen Bereich, denn die API-Clients sind pro Bereich.
    /// </summary>
    public static class PushServices
    {
        public static IServiceProvider? Root => IPlatformApplication.Current?.Services;

        /// <summary>Führt die Arbeit in einem eigenen Bereich aus; ohne Anwendung (nicht gestartet) geschieht nichts.</summary>
        public static async Task RunAsync(Func<IServiceProvider, Task> work)
        {
            if (Root is not { } root)
            {
                return;
            }
            using var scope = root.CreateScope();
            await work(scope.ServiceProvider);
        }
    }
}
