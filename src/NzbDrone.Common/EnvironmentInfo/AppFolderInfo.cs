using System;
using System.IO;
using System.Reflection;
using NLog;
using NzbDrone.Common.Instrumentation;

namespace NzbDrone.Common.EnvironmentInfo
{
    public interface IAppFolderInfo
    {
        string AppDataFolder { get; }
        string LegacyAppDataFolder { get; }
        string TempFolder { get; }
        string StartUpFolder { get; }
    }

    public class AppFolderInfo : IAppFolderInfo
    {
        private readonly Environment.SpecialFolder _dataSpecialFolder = Environment.SpecialFolder.CommonApplicationData;

        private static readonly Logger Logger = NzbDroneLogger.GetLogger(typeof(AppFolderInfo));

        public AppFolderInfo(IStartupContext startupContext)
        {
            if (OsInfo.IsNotWindows)
            {
                _dataSpecialFolder = Environment.SpecialFolder.ApplicationData;
            }

            if (startupContext.Args.TryGetValue(StartupContext.APPDATA, out var argsAppDataFolder))
            {
                AppDataFolder = argsAppDataFolder;
                Logger.Info("Data directory is being overridden to [{0}]", AppDataFolder);
            }
            else
            {
                // Podarr has its own data folder and no legacy (NzbDrone) data to migrate, so it never touches a Sonarr install.
                AppDataFolder = Path.Combine(Environment.GetFolderPath(_dataSpecialFolder, Environment.SpecialFolderOption.DoNotVerify), "Podarr");
            }

            StartUpFolder = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory.FullName;
            TempFolder = Path.GetTempPath();
        }

        public string AppDataFolder { get; }
        public string LegacyAppDataFolder { get; }
        public string StartUpFolder { get; }
        public string TempFolder { get; }
    }
}
