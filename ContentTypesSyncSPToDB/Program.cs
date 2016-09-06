using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using ContentTypesSyncSPToDB;
using NDesk.Options;
using Serilog.Core;

namespace ContentTypesSyncSPToDB
{
    class Program
    {
        static void Main(string[] args)
        {
            string configFileName = null;
            string loggingDir = null;
            Configuration.Configuration config = null;

            var p = new OptionSet()
            {
                {
                    "c=|config=|configuration=", "Path to the configuration file",
                    v => configFileName = v ?? "configuration.xml"
                },
                {
                    "l=|log=|logdir=",
                    "Path to the logging dir",
                    v => loggingDir = v ?? @".\Log"
                },
                {
                    "?|help",
                    "Help",
                    v =>
                    {
                        Console.WriteLine($"Usage: ContentTypesSyncSPToDB.exe -config=CONFIGFILENAME [-logdir=LOGDIR]");
                        return;
                    }
                }
            };

            try
            {
                var messages = p.Parse(args);
                Console.WriteLine(string.Join("\n", messages));
            }
            catch (OptionException e)
            {
                Console.WriteLine();
            }

            if (configFileName == null)
                throw new InvalidOperationException("Missing required option -config=CONFIGFILENAME");
            if (!File.Exists(configFileName))
                throw new InvalidOperationException($"File {configFileName} does not exist");
            try
            {
                config = (Configuration.Configuration)new XmlSerializer(typeof(Configuration.Configuration))
                            .Deserialize(
                                new XmlTextReader(
                                        configFileName));
            }
            catch (Exception)
            {
                throw new InvalidOperationException($"File {configFileName} is not a valid config file");
            }

            Logging.LogDir = loggingDir;

            Logging.Logger.Information("Start Sync");

            var dbWriter = new DBWriter();

            Logging.Logger.Debug("Start reading SharePoint");
            var data = SharePointReader.Read(config);
            Logging.Logger.Debug("Stop  reading SharePoint");

            var script = new List<string>();
            script.AddRange(dbWriter.GenerateDropTableScript(data));
            script.AddRange(dbWriter.GenerateTableCreationScript(data));
            script.AddRange(dbWriter.GenerateInsertScript(data));

            Logging.Logger.Debug("Start writing to database");
            dbWriter.ExecuteScript(script, config.DBConnectionString);
            Logging.Logger.Debug("Stop  writing to database");

            Logging.Logger.Information("Stop Sync");
        }
    }
}