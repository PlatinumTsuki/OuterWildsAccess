using OWML.Common;

namespace OuterWildsAccess
{
    /// <summary>
    /// Centralized debug logging with categories.
    /// Only active when Main.DebugMode is true (toggle with F12).
    /// Zero overhead otherwise.
    ///
    /// Usage:
    ///   DebugLogger.Log(LogCategory.Input, "F1 appuyé");
    ///   DebugLogger.Log(LogCategory.State, "HandlerName", "Menu ouvert");
    /// </summary>
    public static class DebugLogger
    {
        private static IModHelper _modHelper;

        /// <summary>
        /// Initializes the logger. Call once at mod startup.
        /// </summary>
        public static void Initialize(IModHelper modHelper)
        {
            _modHelper = modHelper;
        }

        /// <summary>
        /// Log a debug message with category.
        /// Only logs when Main.DebugMode is true.
        /// </summary>
        public static void Log(LogCategory category, string message)
        {
            if (!Main.DebugMode) return;
            _modHelper?.Console.WriteLine($"{GetPrefix(category)} {message}", MessageType.Message);
        }

        /// <summary>
        /// Log a debug message with category and source handler name.
        /// </summary>
        public static void Log(LogCategory category, string source, string message)
        {
            if (!Main.DebugMode) return;
            _modHelper?.Console.WriteLine($"{GetPrefix(category)} [{source}] {message}", MessageType.Message);
        }

        /// <summary>
        /// Log screenreader output. Called automatically by ScreenReader.Say().
        /// </summary>
        public static void LogScreenReader(string text)
        {
            if (!Main.DebugMode) return;
            _modHelper?.Console.WriteLine($"[SR] {text}", MessageType.Message);
        }

        /// <summary>
        /// Log a key press event.
        /// </summary>
        public static void LogInput(string keyName, string action = null)
        {
            if (!Main.DebugMode) return;
            string msg = action != null ? $"{keyName} -> {action}" : keyName;
            _modHelper?.Console.WriteLine($"[INPUT] {msg}", MessageType.Message);
        }

        /// <summary>
        /// Log a state change (menu opened/closed, mode changed).
        /// </summary>
        public static void LogState(string description)
        {
            if (!Main.DebugMode) return;
            _modHelper?.Console.WriteLine($"[STATE] {description}", MessageType.Message);
        }

        /// <summary>
        /// Log a game value that was read (for debugging data extraction).
        /// </summary>
        public static void LogGameValue(string name, object value)
        {
            if (!Main.DebugMode) return;
            _modHelper?.Console.WriteLine($"[GAME] {name} = {value}", MessageType.Message);
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
