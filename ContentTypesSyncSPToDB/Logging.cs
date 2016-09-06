using System;
using System.IO;
using System.Security.AccessControl;
using Serilog;
using Serilog.Debugging;

namespace ContentTypesSyncSPToDB
{
    public class Logging
    {
        static ILogger _logger;

        static Logging()
        {
            var file = File.CreateText(Path.Combine(Path.GetTempPath(),"SeriLog.log"));
            SelfLog.Enable(TextWriter.Synchronized(file));
        }

        public static string LogDir { get; set; }

        public static ILogger Logger
        {
            get
            {
                return _logger ?? 
                    (_logger = new LoggerConfiguration()
                           .Enrich.FromLogContext()
                           .MinimumLevel.Debug()
                           .WriteTo
                           .RollingFile(
                               Path.Combine(LogDir ?? @".\Log", @"ContentTypesSync.log"),
                               outputTemplate: "{Timestamp} [{Level}] {Web} {List}: {Message}{NewLine}{Exception}")
                           .CreateLogger());
            }
        }

        public static object SafeParam(object obj)
        {
            return (obj == null) ? "null" : obj;
        }


        public static bool CanWriteOnDir(string path)
        {
            try
            {
                var writeAllow = false;
                var writeDeny = false;
                var accessControlList = Directory.GetAccessControl(path);
                if (accessControlList == null)
                    return false;
                var accessRules = accessControlList.GetAccessRules(true, true,
                                            typeof(System.Security.Principal.SecurityIdentifier));
                if (accessRules == null)
                    return false;

                foreach (FileSystemAccessRule rule in accessRules)
                {
                    if ((FileSystemRights.Write & rule.FileSystemRights) != FileSystemRights.Write)
                        continue;

                    if (rule.AccessControlType == AccessControlType.Allow)
                        writeAllow = true;
                    else if (rule.AccessControlType == AccessControlType.Deny)
                        writeDeny = true;
                }

                return writeAllow && !writeDeny;
            }
            catch (UnauthorizedAccessException uae)
            {
                return false;
            }
        }

    }
}
