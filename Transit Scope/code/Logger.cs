using Colossal.Logging;

namespace Transit_Scope
{
    public static class Logger
    {
        private static ILog _log = LogManager.GetLogger(nameof(Transit_Scope));
        //SetShowsErrorsInUI(false)就是调用error方法时在游戏中不弹出ui

        // 暴露公开的静态方法
        public static void Info(string message) 
        {
            _log.Info(message);
        }
        
        public static void Error(string message) 
        {
            _log.Error(message);
        }
    }
}