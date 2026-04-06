using System;
using System.IO;
using OWML.Common;

namespace OuterWildsAccess
{
    /// <summary>
    /// Centralized debug logging with categories.
    /// Only active when Main.DebugMode is true (toggle with F12).
    /// Zero overhead otherwise.
    ///
    /// Writes to two sinks:
    ///  1. The OWML console (live, shared with all mods).
    ///  2. A dedicated file `OuterWildsAccess.log.txt` inside the mod folder,
    ///     intended for bug reports — users can attach it to a GitHub issue
    ///     without having to untangle the combined OWML.Log.txt.
    ///
    /// The file is truncated at every mod start so each session is
    /// self-contained. A session header is always written (mod version,
    /// UTC timestamp), even with debug mode off, so users can confirm
    /// the mod loaded successfully.
    ///
    /// Usage:
    ///   DebugLogger.Log(LogCategory.Input, "F1 appuyé");
    ///   DebugLogger.Log(LogCategory.State, "HandlerName", "Menu ouvert");
    /// </summary>
    public static class DebugLogger
    {
        private static IModHelper _modHelper;
        private static string     _logFilePath;
        private static bool       _fileSinkReady;

        /// <summary>
        /// Initializes the logger. Call once at mod startup.
        /// Opens (and truncates) the dedicated log file and writes a header.
        /// </summary>
        public static void Initialize(IModHelper modHelper)
        {
            _modHelper = modHelper;

            try
            {
                string folder = modHelper?.Manifest?.ModFolderPath;
                if (!string.IsNullOrEmpty(folder))
                {
                    _logFilePath = Path.Combine(folder, "OuterWildsAccess.log.txt");
                    // Truncate and write header
                    string version = modHelper.Manifest.Version ?? "?";
                    string header =
                        "=== OuterWildsAccess v" + version + " ===" + Environment.NewLine +
                        "Session started: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" + Environment.NewLine +
                        "Debug mode is OFF by default. Press F12 in-game to enable detailed logging." + Environment.NewLine +
                        "Attach this file to a GitHub issue to report bugs." + Environment.NewLine +
                        "---" + Environment.NewLine;
                    File.WriteAllText(_logFilePath, header);
                    _fileSinkReady = true;
                }
            }
            catch (Exception ex)
            {
                _fileSinkReady = false;
                _modHelper?.Console.WriteLine(
                    "[DebugLogger] Failed to open log file: " + ex.Message,
                    MessageType.Warning);
            }
        }

        /// <summary>
        /// Appends a line to the dedicated log file. Silent on failure —
        /// logging must never crash the mod.
        /// </summary>
        private static void WriteToFile(string line)
        {
            if (!_fileSinkReady) return;
            try
            {
                File.AppendAllText(_logFilePath,
                    DateTime.UtcNow.ToString("HH:mm:ss.fff") + " " + line + Environment.NewLine);
            }
            catch
            {
                // Disable the sink on any write failure to avoid spamming errors.
                _fileSinkReady = false;
            }
        }

        /// <summary>
        /// Log a debug message with category.
        /// Only logs when Main.DebugMode is true.
        /// </summary>
        public static void Log(LogCategory category, string message)
        {
            if (!Main.DebugMode) return;
            string line = $"{GetPrefix(category)} {message}";
            _modHelper?.Console.WriteLine(line, MessageType.Message);
            WriteToFile(line);
        }

        /// <summary>
        /// Log a debug message with category and source handler name.
        /// </summary>
        public static void Log(LogCategory category, string source, string message)
        {
            if (!Main.DebugMode) return;
            string line = $"{GetPrefix(category)} [{source}] {message}";
            _modHelper?.Console.WriteLine(line, MessageType.Message);
            WriteToFile(line);
        }

        /// <summary>
        /// Log screenreader output. Called automatically by ScreenReader.Say().
        /// </summary>
        public static void LogScreenReader(string text)
        {
            if (!Main.DebugMode) return;
            string line = $"[SR] {text}";
            _modHelper?.Console.WriteLine(line, MessageType.Message);
            WriteToFile(line);
        }

        /// <summary>
        /// Log a key press event.
        /// </summary>
        public static void LogInput(string keyName, string action = null)
        {
            if (!Main.DebugMode) return;
            string msg = action != null ? $"{keyName} -> {action}" : keyName;
            string line = $"[INPUT] {msg}";
            _modHelper?.Console.WriteLine(line, MessageType.Message);
            WriteToFile(line);
        }

        /// <summary>
        /// Log a state change (menu opened/closed, mode changed).
        /// </summary>
        public static void LogState(string description)
        {
            if (!Main.DebugMode) return;
            string line = $"[STATE] {description}";
            _modHelper?.Console.WriteLine(line, MessageType.Message);
            WriteToFile(line);
        }

        /// <summary>
        /// Log a game value that was read (for debugging data extraction).
        /// </summary>
        public static void LogGameValue(string name, object value)
        {
            if (!Main.DebugMode) return;
            string line = $"[GAME] {name} = {value}";
            _modHelper?.Console.WriteLine(line, MessageType.Message);
            WriteToFile(line);
        }

        private static string GetPrefix(LogCategory category)
        {
            switch (category)
            {
                case LogCategory.ScreenReader: return "[SR]";
                case LogCategory.Input:        return "[INPUT]";
                case LogCategory.State:        return "[STATE]";
                case LogCategory.Handler:      return "[HANDLER]";
                case LogCategory.Game:         return "[GAME]";
                default:                       return "[DEBUG]";
            }
        }
    }

    /// <summary>
    /// Categories for debug logging.
    /// </summary>
    public enum LogCategory
    {
        /// <summary>What the screenreader announces</summary>
        ScreenReader,
        /// <summary>Key presses and input events</summary>
        Input,
        /// <summary>Screen/menu state changes</summary>
        State,
        /// <summary>Handler decisions and processing</summary>
        Handler,
        /// <summary>Values read from the game</summary>
        Game
    }
}
