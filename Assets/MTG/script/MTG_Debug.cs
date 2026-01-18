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
				case LogType.Assert:
					return 0xFFA500;
#if UNITY_EDITOR
				case LogType.Editor:
					return 0x00FF00;
#endif
				default:
					return 0xFFFFFF;
			}
		}

        public static void Log(this MTG_Base context, string message)
        {
            Log(message, context, context.GetType().Name);
        }

        public static void Log(string message, Object context = null, string tag = null)
        {
            UnityEngine.Debug.Log(FormatMessage(LogType.Log, message, tag), context);
        }


        public static void Debug(this MTG_Base context, string message)
        {
            Debug(message, context, context.GetType().Name);
        }

        public static void Debug(string message, Object context = null, string tag = null)
        {
            UnityEngine.Debug.Log(FormatMessage(LogType.Debug, message, tag), context);
        }

        private static string FormatMessage(LogType type, string message, string tag) {
            return $"[<color=#{ColorFromTag(tag).ToString("X2")}>{tag}</color>] <color=#{ColorFromType(type).ToString("X2")}>{type.ToString().ToUpper()} {message}</color>";
        }
    }

	public enum LogType {
		Error,
		Assert,
		Warning,
		Log,
		Debug,
#if UNITY_EDITOR
		Editor,
#endif
	}
}
