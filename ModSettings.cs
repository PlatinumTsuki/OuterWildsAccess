using System.IO;

namespace OuterWildsAccess
{
    /// <summary>
    /// Central source of truth for all accessibility feature toggles.
    /// Settings are persisted to accessibility_settings.ini in Application.persistentDataPath.
    /// Format: one key=value per line (e.g. beacon=true).
    /// </summary>
    public static class ModSettings
    {
        // ── Feature flags ──────────────────────────────────────────────────────

        /// <summary>Whether the audio beacon towards the navigation target is enabled.</summary>
        public static bool BeaconEnabled     = true;

        /// <summary>Whether navigation scanning and direction are enabled.</summary>
        public static bool NavigationEnabled = true;

        /// <summary>Whether the collision beep (obstacle feedback while walking) is enabled.</summary>
        public static bool CollisionEnabled  = true;

        /// <summary>Whether the F9 auto-walk feature is enabled.</summary>
        public static bool AutoWalkEnabled   = true;

        /// <summary>Whether nearby NPCs and interactables are announced during manual walk.</summary>
        public static bool ProximityEnabled = true;

        /// <summary>Whether oxygen and fuel threshold warnings are announced.</summary>
        public static bool GaugeEnabled = true;

        /// <summary>Whether the G-key path guidance (audio ticks along A* path) is enabled.</summary>
        public static bool GuidanceEnabled = true;

        /// <summary>Whether the meditation button is unlocked from the start (no need to find Gabbro).</summary>
        public static bool MeditationEnabled = true;

        /// <summary>Whether ghost matter protection is active (proximity warnings + damage immunity).</summary>
        public static bool GhostMatterProtectionEnabled = true;

        /// <summary>Whether the F3 ship recall feature is enabled.</summary>
        public static bool ShipRecallEnabled = true;

        /// <summary>Whether the F4 autopilot to planet feature is enabled.</summary>
        public static bool AutopilotEnabled = true;

        /// <summary>Whether DLC ghosts are pacified (no hostile AI).</summary>
        public static bool PeacefulGhostsEnabled = true;

        /// <summary>Whether the NVDA direct API is used instead of Tolk (requires NVDA 2024.1+).</summary>
        public static bool NvdaDirectEnabled = true;

        // ── Persistence ────────────────────────────────────────────────────────

        private static string _path;

        /// <summary>
        /// Loads settings from disk. Call once at mod startup with Application.persistentDataPath.
        /// </summary>
        public static void Initialize(string persistentDataPath)
        {
            _path = Path.Combine(persistentDataPath, "accessibility_settings.ini");
            Load();
        }

        /// <summary>
        /// Writes the current settings to disk.
        /// </summary>
        public static void Save()
        {
            if (_path == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("beacon="            + BoolStr(BeaconEnabled));
            sb.AppendLine("navigation="        + BoolStr(NavigationEnabled));
            sb.AppendLine("collision="         + BoolStr(CollisionEnabled));
            sb.AppendLine("autowalk="          + BoolStr(AutoWalkEnabled));
            sb.AppendLine("proximity="         + BoolStr(ProximityEnabled));
            sb.AppendLine("gauge="             + BoolStr(GaugeEnabled));
            sb.AppendLine("guidance="          + BoolStr(GuidanceEnabled));
            sb.AppendLine("meditation="        + BoolStr(MeditationEnabled));
            sb.AppendLine("ghostmatterprotection=" + BoolStr(GhostMatterProtectionEnabled));
            sb.AppendLine("shiprecall="        + BoolStr(ShipRecallEnabled));
            sb.AppendLine("autopilot="         + BoolStr(AutopilotEnabled));
            sb.AppendLine("peacefulghosts="    + BoolStr(PeacefulGhostsEnabled));
            sb.AppendLine("nvdadirect="        + BoolStr(NvdaDirectEnabled));

            try   { File.WriteAllText(_path, sb.ToString()); }
            catch (System.Exception ex)
            { DebugLogger.LogState("[ModSettings] Save failed: " + ex.Message); }
        }

        // ── Private ────────────────────────────────────────────────────────────

        private static void Load()
        {
            if (!File.Exists(_path)) return;

            try
            {
                string[] lines = File.ReadAllLines(_path);
                foreach (string line in lines)
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;

                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    bool   val = line.Substring(eq + 1).Trim().ToLowerInvariant() == "true";

                    switch (key)
                    {
                        case "beacon":              BeaconEnabled              = val; break;
                        case "navigation":          NavigationEnabled          = val; break;
                        case "collision":           CollisionEnabled           = val; break;
                        case "autowalk":            AutoWalkEnabled            = val; break;
                        case "proximity":           ProximityEnabled           = val; break;
                        case "gauge":               GaugeEnabled               = val; break;
                        case "guidance":            GuidanceEnabled            = val; break;
                        case "meditation":          MeditationEnabled          = val; break;
                        case "ghostmatterprotection": GhostMatterProtectionEnabled = val; break;
                        case "shiprecall":          ShipRecallEnabled          = val; break;
                        case "autopilot":           AutopilotEnabled           = val; break;
                        case "peacefulghosts":      PeacefulGhostsEnabled      = val; break;
                        case "nvdadirect":          NvdaDirectEnabled          = val; break;
                    }
                }
                DebugLogger.LogState("[ModSettings] Loaded from " + _path);
            }
            catch (System.Exception ex)
            {
                DebugLogger.LogState("[ModSettings] Load failed: " + ex.Message);
            }
        }

        private static string BoolStr(bool v) => v ? "true" : "false";
    }
}
