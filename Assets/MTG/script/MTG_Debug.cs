using UdonSharp;
using UnityEngine;

namespace MTG
{
    public static class MTG_Debug
    {

        private static int ColorFromTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return 0xFFFFFF;

            var hash = tag.GetHashCode();
            var r = (hash & 0xFF0000) >> 16;
            var g = (hash & 0x00FF00) >> 8;
            var b = (hash & 0x0000FF);
            return (r << 16) | (g << 8) | b;
        }
        private static int ColorFromType(LogType type) {
			switch (type) {
				case LogType.Error:
					return 0xFF0000;
				case LogType.Warning:
					return 0xFFFF00;
				case LogType.Log:
					return 0xFFFFFF;
				case LogType.Debug:
					return 0x00FFFF;
#if UNITY_EDITOR
				case LogType.Editor:
					return 0x00FF00;
#endif
				default:
					return 0xFFFFFF;
			}
		}
        // standard debug methods
        public static void Error(this MTG_Base context, string message)
        {
            if (context.DEBUG == false) return;
            Error(message, context, context.ScriptName);
        }
        public static void Error(string message, Object context = null, string tag = null)
        {
            UnityEngine.Debug.LogError(FormatMessage(LogType.Error, message, tag), context);
        }
        public static void Warning(this MTG_Base context, string message)
        {
            if (context.DEBUG == false) return;
            Warning(message, context, context.ScriptName);
        }
        public static void Warning(string message, Object context = null, string tag = null)
        {
            UnityEngine.Debug.LogWarning(FormatMessage(LogType.Warning, message, tag), context);
        }
        public static void Log(this MTG_Base context, string message)
        {
            if (context.DEBUG == false) return;
            Log(message, context, context.ScriptName);
        }
        public static void Log(string message, Object context = null, string tag = null)
        {
            UnityEngine.Debug.Log(FormatMessage(LogType.Log, message, tag), context);
        }
        public static void Debug(this MTG_Base context, string message)
        {
            if (context.DEBUG == false) return;
            Debug(message, context, context.ScriptName);
        }
        public static void Debug(string message, Object context = null, string tag = null)
        {
            UnityEngine.Debug.Log(FormatMessage(LogType.Debug, message, tag), context);
        }
#if UNITY_EDITOR
        public static void Editor(this MTG_Base context, string message)
        {
            if (context.DEBUG == false) return;
            Editor(message, context, context.ScriptName);
        }
        public static void Editor(string message, Object context = null, string tag = null)
        {
            UnityEngine.Debug.Log(FormatMessage(LogType.Editor, message, tag), context);
        }
#endif
        // verbose debug methods
        public static void VerboseError(this MTG_Base context, string message)
        {
            if (context.VERBOSE_DEBUG == false || context.DEBUG == false) return;
            Error(message, context, context.ScriptName);
        }
        public static void VerboseError(string message, Object context = null, string tag = null)
        {
            UnityEngine.Debug.LogError(FormatMessage(LogType.Error, message, tag), context);
        }
        public static void VerboseWarning(this MTG_Base context, string message)
        {
            if (context.VERBOSE_DEBUG == false || context.DEBUG == false) return;
            Warning(message, context, context.ScriptName);
        }
        public static void VerboseWarning(string message, Object context = null, string tag = null)
        {
            UnityEngine.Debug.LogWarning(FormatMessage(LogType.Warning, message, tag), context);
        }
        public static void VerboseLog(this MTG_Base context, string message)
        {
            if (context.VERBOSE_DEBUG == false || context.DEBUG == false) return;
            Log(message, context, context.ScriptName);
        }
        public static void VerboseLog(string message, Object context = null, string tag = null)
        {
            UnityEngine.Debug.Log(FormatMessage(LogType.Log, message, tag), context);
        }
        public static void VerboseDebug(this MTG_Base context, string message)
        {
            if (context.VERBOSE_DEBUG == false || context.DEBUG == false) return;
            Debug(message, context, context.ScriptName);
        }
        public static void VerboseDebug(string message, Object context = null, string tag = null)
        {
            UnityEngine.Debug.Log(FormatMessage(LogType.Debug, message, tag), context);
        }
#if UNITY_EDITOR
        public static void VerboseEditor(this MTG_Base context, string message)
        {
            if (context.VERBOSE_DEBUG == false || context.DEBUG == false) return;
            Editor(message, context, context.ScriptName);
        }
        public static void VerboseEditor(string message, Object context = null, string tag = null)
        {
            UnityEngine.Debug.Log(FormatMessage(LogType.Editor, message, tag), context);
        }
#endif
        // message formatting
        private static string TypeLabel(LogType type)
        {
            switch (type)
            {
                case LogType.Error:   return "ERROR";
                case LogType.Warning: return "WARN";
                case LogType.Log:     return "LOG";
                case LogType.Debug:   return "DEBUG";
#if UNITY_EDITOR
                case LogType.Editor:  return "EDITOR";
#endif
                default:              return "???";
            }
        }

        private static string FormatMessage(LogType type, string message, string tag) {
            return $"[<color=#{ColorFromTag(tag).ToString("X6")}>{tag}</color>] <color=#{ColorFromType(type).ToString("X6")}>{TypeLabel(type)} {message}</color>";
        }
    }
	public enum LogType {
		Error,
		Warning,
		Log,
		Debug,
#if UNITY_EDITOR
		Editor,
#endif
	}
}
