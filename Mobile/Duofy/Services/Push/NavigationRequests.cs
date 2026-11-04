namespace Duofy.Services.Push
{
    /// <summary>
    /// Ein Tipp auf eine Benachrichtigung will die App an einer Adresse öffnen (Spec 84, R5). Die Plattformteile melden die
    /// Adresse hier, die Hauptseite reicht sie an die Oberfläche weiter. Kommt der Tipp, bevor die Oberfläche steht, wartet
    /// die Adresse hier, bis jemand zuhört.
    /// </summary>
    public sealed class NavigationRequests
    {
        private string? pending;

        /// <summary>Wird gerufen, wenn eine Adresse zu öffnen ist.</summary>
        public event Action<string>? Requested;

        public void Request(string path)
        {
            if (Requested is { } handler)
            {
                handler(path);
            }
            else
            {
                pending = path;
            }
        }

        /// <summary>Die Adresse, die vor dem Start der Oberfläche angefragt wurde; sie wird nur einmal herausgegeben.</summary>
        public string? TakePending()
        {
            var path = pending;
            pending = null;
            return path;
        }

        /// <summary>
        /// Ob die Adresse zu einer Benachrichtigung gehören darf: nur die Formen, die die Benachrichtigung selbst baut
        /// (der Monat eines Postens), nie eine beliebige Seite oder ein fremder Host.
        /// </summary>
        public static bool IsAppPath(string? path) => Duofy.Push.ReminderPaths.IsAllowed(path);
    }
}
