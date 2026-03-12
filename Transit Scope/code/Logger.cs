using Colossal.Logging;

namespace Transit_Scope
{
    public static class Logger
    {
        // 私有化真实的日志实例
        private static ILog _log = LogManager.GetLogger($"{nameof(Transit_Scope)}.Mod");

        // 暴露公开的静态方法
        public static void Info(string message) 
        {
            _log.Info(message);
        }
    }
}