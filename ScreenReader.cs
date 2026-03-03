using System;
using System.Runtime.InteropServices;
using OWML.Common;

namespace OuterWildsAccess
{
    /// <summary>
    /// Speech priority levels for screen reader announcements.
    /// When NVDA is detected, uses native NVDA priorities via speakSsml.
    /// Fallback to Tolk interrupt/queue for other screen readers.
    /// </summary>
    public enum SpeechPriority
    {
        /// <summary>Queued after all other speech. For prompts, proximity, menu items.</summary>
        Normal = 0,
        /// <summary>Interrupts Normal speech, queues after other Next. For state changes, navigation.</summary>
        Next = 1,
        /// <summary>Interrupts everything, then resumes interrupted speech. For death, hazards, damage.</summary>
        Now = 2
    }

    /// <summary>
    /// Hybrid screen reader wrapper: NVDA direct API with speech priorities
    /// when available, Tolk fallback for JAWS/SAPI/other readers.
    ///
    /// Requirements:
    ///   Tolk.dll and nvdaControllerClient64.dll must be in the game folder
    ///   (C:\Users\assaa\Games\Outer Wilds\)
    /// </summary>
    public static class ScreenReader
    {
        #region Native Imports — Tolk

        [DllImport("Tolk.dll")]
        private static extern void Tolk_Load();

        [DllImport("Tolk.dll")]
        private static extern void Tolk_Unload();

        [DllImport("Tolk.dll")]
        private static extern bool Tolk_IsLoaded();

        [DllImport("Tolk.dll")]
        private static extern bool Tolk_HasSpeech();

        [DllImport("Tolk.dll", CharSet = CharSet.Unicode)]
        private static extern bool Tolk_Output(string text, bool interrupt);

        [DllImport("Tolk.dll")]
        private static extern bool Tolk_Silence();

        [DllImport("Tolk.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr Tolk_DetectScreenReader();

        #endregion

        #region Native Imports — NVDA Controller Client

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode)]
        private static extern int nvdaController_speakText(string text);

        [DllImport("nvdaControllerClient64.dll")]
        private static extern int nvdaController_cancelSpeech();

        [DllImport("nvdaControllerClient64.dll")]
        private static extern int nvdaController_testIfRunning();

        [DllImport("nvdaControllerClient64.dll", CharSet = CharSet.Unicode)]
        private static extern int nvdaController_speakSsml(
            string ssml, int symbolLevel, int priority, bool asynchronous);

        /// <summary>NVDA SSML symbol level: keep current setting.</summary>
        private const int SymbolLevelUnchanged = -1;

        /// <summary>Error code when NVDA version does not support speakSsml (pre-2024.1).</summary>
        private const int RPC_S_UNKNOWN_IF = 1717;

        #endregion

        #region Fields

        private static bool   _available    = false;
        private static bool   _initialized  = false;
        private static IModHelper _modHelper;

        /// <summary>True when the active screen reader is NVDA (enables direct API).</summary>
        private static bool _isNvda = false;

        /// <summary>True until first speakSsml call fails with RPC_S_UNKNOWN_IF.</summary>
        private static bool _nvdaHasSsml = true;

        // Deduplication: skip identical text announced within DedupMs milliseconds
        private static string _lastText;
        private static int    _lastSayTick;
        private const  int    DedupMs = 250;

        // Normal speech protection: prevent cancelSpeech from killing Normal while it's still being read
        private static int _lastNormalSentAt;
        private static int _lastNormalProtectionMs;

        /// <summary>Estimated TTS time per character in ms (conservative for slow readers).</summary>
        private const int MsPerChar = 80;

        /// <summary>Buffer added to estimated duration for TTS startup latency.</summary>
        private const int BufferMs = 300;

        /// <summary>Maximum protection window in ms, regardless of text length.</summary>
        private const int MaxProtectionMs = 5000;

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes Tolk and detects NVDA for direct API access.
        /// Call once at mod startup.
        /// </summary>
        public static void Initialize(IModHelper modHelper)
        {
            if (_initialized) return;
            _modHelper = modHelper;

            try
            {
                Tolk_Load();
                _available = Tolk_IsLoaded() && Tolk_HasSpeech();

                if (_available)
                {
                    IntPtr srNamePtr = Tolk_DetectScreenReader();
                    string srName = srNamePtr != IntPtr.Zero
                        ? Marshal.PtrToStringUni(srNamePtr)
                        : "Inconnu";
                    _modHelper.Console.WriteLine($"Lecteur d'écran détecté: {srName}", MessageType.Success);

                    // Check if active reader is NVDA — enables direct API with priorities
                    _isNvda = srName != null
                        && srName.IndexOf("NVDA", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (_isNvda)
                    {
                        // Probe speakSsml support (NVDA 2024.1+)
                        try
                        {
                            int probe = nvdaController_speakSsml("<speak></speak>",
                                SymbolLevelUnchanged, (int)SpeechPriority.Normal, true);
                            _nvdaHasSsml = (probe != RPC_S_UNKNOWN_IF);
                        }
                        catch { _nvdaHasSsml = false; }

                        _modHelper.Console.WriteLine(
                            _nvdaHasSsml
                                ? "NVDA direct API: priorités vocales activées (speakSsml)"
                                : "NVDA direct API: version ancienne, fallback Tolk (pas de priorités)",
                            MessageType.Info);
                    }
                }
                else
                {
                    _modHelper.Console.WriteLine("Aucun lecteur d'écran détecté ou Tolk non disponible", MessageType.Warning);
                }
            }
            catch (DllNotFoundException)
            {
                _modHelper.Console.WriteLine("Tolk.dll introuvable ! Placer Tolk.dll dans le dossier du jeu.", MessageType.Error);
                _available = false;
            }
            catch (Exception ex)
            {
                _modHelper.Console.WriteLine($"Échec initialisation Tolk: {ex.Message}", MessageType.Error);
                _available = false;
            }

            _initialized = true;
        }

        /// <summary>
        /// Announces text via the screen reader with a speech priority.
        /// Identical text within 250 ms is silently dropped (deduplication).
        /// When NVDA is detected, uses native NVDA priorities.
        /// Fallback: Tolk interrupt (Now/Next) or queue (Normal).
        /// </summary>
        /// <param name="text">Text to speak</param>
        /// <param name="priority">Speech priority level (default: Next)</param>
        public static void Say(string text, SpeechPriority priority = SpeechPriority.Next)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!Main.ModEnabled) return;

            // Deduplicate: drop if same text was just spoken within DedupMs
            int now = Environment.TickCount;
            if (text == _lastText && Math.Abs(now - _lastSayTick) < DedupMs)
            {
                DebugLogger.LogScreenReader($"[DEDUP] {text}");
                return;
            }

            _lastText    = text;
            _lastSayTick = now;

            DebugLogger.LogScreenReader($"[{priority}] {text}");

            if (!_available) return;

            try
            {
                // NVDA direct path
                if (_isNvda && _nvdaHasSsml && ModSettings.NvdaDirectEnabled)
                {
                    // Now = speakSsml with priority 2 (interrupt + resume after alert)
                    // Next = cancel + speakText, BUT skip cancel if Normal is still being read
                    // Normal = speakText (queue, appears in NVDA history) + set protection window
                    if (priority == SpeechPriority.Now)
                    {
                        string ssml = "<speak>" + EscapeXml(text) + "</speak>";
                        int result = nvdaController_speakSsml(ssml,
                            SymbolLevelUnchanged, (int)SpeechPriority.Now, true);
                        _lastNormalSentAt = 0; // Now overrides everything, reset protection
                        if (result == 0) return;
                        if (result == RPC_S_UNKNOWN_IF) _nvdaHasSsml = false;
                        // fall through to Tolk on failure
                    }
                    else if (priority == SpeechPriority.Next)
                    {
                        // Only skip cancel if Normal speech is likely still being read.
                        // Once one Next has been queued (protecting Normal), consume the
                        // protection so subsequent Nexts cancel each other normally.
                        if (IsNormalProtected())
                            _lastNormalSentAt = 0; // protection consumed
                        else
                            nvdaController_cancelSpeech();
                        int result = nvdaController_speakText(text);
                        if (result == 0) return;
                    }
                    else // Normal
                    {
                        int result = nvdaController_speakText(text);
                        // Start protection window so Next won't cancel this speech
                        _lastNormalSentAt = Environment.TickCount;
                        _lastNormalProtectionMs = Math.Min(
                            text.Length * MsPerChar + BufferMs, MaxProtectionMs);
                        if (result == 0) return;
                    }
                }

                // Tolk fallback: Now/Next = interrupt, Normal = queue
                bool interrupt = priority >= SpeechPriority.Next;
                Tolk_Output(text, interrupt);
            }
            catch (Exception ex)
            {
                _modHelper?.Console.WriteLine($"ScreenReader.Say failed: {ex.Message}", MessageType.Warning);
            }
        }

        /// <summary>
        /// Announces text bypassing the ModEnabled check.
        /// Used only for the F5 toggle announcement.
        /// </summary>
        public static void SayForce(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!_available) return;
            try
            {
                if (_isNvda && ModSettings.NvdaDirectEnabled)
                {
                    nvdaController_cancelSpeech();
                    int result = nvdaController_speakText(text);
                    if (result == 0) return;
                }
                Tolk_Output(text, true);
            }
            catch { }
        }

        /// <summary>
        /// Queued announcement — Normal priority, waits for current speech.
        /// Use for secondary info after a main announcement.
        /// </summary>
        public static void SayQueued(string text)
        {
            Say(text, SpeechPriority.Normal);
        }



        /// <summary>
        /// Repeats the last announced text immediately, bypassing deduplication.
        /// Map to a hotkey (Delete) so the user can recall a missed announcement.
        /// </summary>
        public static void RepeatLast()
        {
            if (string.IsNullOrEmpty(_lastText)) return;

            DebugLogger.LogScreenReader($"[REPEAT] {_lastText}");

            if (!_available) return;

            try
            {
                if (_isNvda && ModSettings.NvdaDirectEnabled)
                {
                    nvdaController_cancelSpeech();
                    int result = nvdaController_speakText(_lastText);
                    if (result == 0) return;
                }
                Tolk_Output(_lastText, true);
            }
            catch { }
        }

        /// <summary>
        /// Stops current speech immediately.
        /// </summary>
        public static void Stop()
        {
            if (!_available) return;
            try
            {
                if (_isNvda && ModSettings.NvdaDirectEnabled)
                    nvdaController_cancelSpeech();
                Tolk_Silence();
            }
            catch { }
        }

        /// <summary>
        /// Shuts down Tolk. Call when the game closes.
        /// </summary>
        public static void Shutdown()
        {
            if (!_initialized) return;
            try { Tolk_Unload(); } catch { }
            _initialized = false;
            _available = false;
        }

        /// <summary>
        /// Returns true if a screen reader is available.
        /// </summary>
        public static bool IsAvailable => _available;

        /// <summary>
        /// Returns true if the active screen reader is NVDA with priority support.
        /// </summary>
        public static bool HasPriorities => _isNvda && _nvdaHasSsml;

        #endregion

        #region Private Helpers

        /// <summary>
        /// Returns true if a Normal-priority speech is likely still being read by NVDA.
        /// Uses a time-based estimate (text length * MsPerChar + buffer).
        /// When true, cancelSpeech is skipped to avoid killing the Normal speech.
        /// </summary>
        private static bool IsNormalProtected()
        {
            if (_lastNormalSentAt == 0) return false;
            int elapsed = Math.Abs(Environment.TickCount - _lastNormalSentAt);
            return elapsed < _lastNormalProtectionMs;
        }

        /// <summary>
        /// Escapes text for use inside SSML elements.
        /// </summary>
        private static string EscapeXml(string text)
        {
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        #endregion
    }
}
