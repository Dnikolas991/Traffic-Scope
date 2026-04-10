using System;
using Colossal.Logging;

namespace Transit_Scope
{
    public static class Logger
    {
        private static readonly ILog _log = LogManager.GetLogger(nameof(Transit_Scope));

        public static void Info(string message)
        {
            SafeLog(log => log.Info(message));
        }

        public static void Error(string message)
        {
            SafeLog(log => log.Error(message));
        }

        public static void Warning(string message)
        {
            SafeLog(log => log.Warn(message));
        }

        private static void SafeLog(Action<ILog> write)
        {
            if (write == null || _log == null)
            {
                return;
            }

            try
            {
                write(_log);
            }
            catch
            {
                // Colossal.Logging can throw inside its Unity log handler.
                // Logging must never break the simulation update loop.
            }
        }
    }
}
