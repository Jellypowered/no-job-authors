using System;
using System.Collections.Generic;
using System.Diagnostics;
using Verse;

namespace NoJobAuthors
{
    internal static class NJA_Logging
    {
        private const string Prefix = "[NoJobAuthors]";
        private static readonly HashSet<string> OnceKeys = new HashSet<string>();
        private static readonly Dictionary<string, int> LastTickByKey = new Dictionary<string, int>();

        [Conditional("DEBUG")]
        internal static void Debug(string message)
        {
            SafeLog(Log.Message, $"{Prefix} {message}");
        }

        [Conditional("DEBUG")]
        internal static void Warn(string message)
        {
            SafeLog(Log.Warning, $"{Prefix} {message}");
        }

        [Conditional("DEBUG")]
        internal static void Error(string message)
        {
            SafeLog(Log.Error, $"{Prefix} {message}");
        }

        [Conditional("DEBUG")]
        internal static void DebugOnce(string key, string message)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug(message);
                return;
            }

            if (OnceKeys.Add(key))
                Debug(message);
        }

        [Conditional("DEBUG")]
        internal static void DebugThrottled(string key, string message, int cooldownTicks = 120)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug(message);
                return;
            }

            int now = CurrentTick();
            if (LastTickByKey.TryGetValue(key, out int last) && now - last < cooldownTicks)
                return;

            LastTickByKey[key] = now;
            Debug(message);
        }

        private static int CurrentTick()
        {
            try
            {
                return Find.TickManager?.TicksGame ?? Environment.TickCount;
            }
            catch
            {
                return Environment.TickCount;
            }
        }

        private static void SafeLog(Action<string> logger, string message)
        {
            try
            {
                logger(message);
            }
            catch
            {
                // Ignore logging failures.
            }
        }
    }
}