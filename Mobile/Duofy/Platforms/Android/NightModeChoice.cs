namespace Duofy
{
    /// <summary>
    /// Spec 124, R5: Welchen Nachtmodus Android für die App nimmt. Die Fenster des Systems (Datums- und Zeitwähler im WebView)
    /// richten sich nach dem Nachtmodus der Activity, nicht nach dem Theme der Web-Oberfläche. Wählt die Person in der App hell oder
    /// dunkel, gilt das auch für diese Fenster, egal wie das Gerät eingestellt ist; ohne Wahl folgt die App dem Gerät.
    /// Die Zahlen sind die Konstanten von AppCompatDelegate (MODE_NIGHT_FOLLOW_SYSTEM, MODE_NIGHT_NO, MODE_NIGHT_YES); die Klasse kennt
    /// Android nicht, damit sie ohne die MAUI-Workload getestet werden kann.
    /// </summary>
    internal static class NightModeChoice
    {
        public const int FollowSystem = -1;
        public const int No = 1;
        public const int Yes = 2;

        /// <param name="darkChoice">Die Wahl der Person: true dunkel, false hell, null keine Wahl (wie das Gerät).</param>
        public static int For(bool? darkChoice) => darkChoice switch
        {
            true => Yes,
            false => No,
            null => FollowSystem,
        };
    }
}
