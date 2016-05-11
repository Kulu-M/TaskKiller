using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessMonitor
{
    public class Logger
    {
        //Folderpath of Logfile
        public static string filepath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
        //Filename of Logfile
        public static string filename = "ProcessMonitorLogfile.1337";
        //Complete string to Logfile Location
        public static readonly string pathToLogFile = Path.Combine(filepath, filename);

        //public static string pathToLogFile = "LDAP2HCP-LogFile.1337";

        public void writeToLogFile(string toLogFile)
        {
            //if (!Directory.Exists(filepath))
            //{
            //    Directory.CreateDirectory(filepath);
            //}
            try
            {
                if (deleteLogFileIfTooBig(pathToLogFile))
                {
                    writeToLogFile("LOGGER: Logfile exceeded 20MB and got resettet.");
                }
                File.AppendAllText(pathToLogFile, Environment.NewLine + "-----NEW ENTRY-----" + Environment.NewLine + DateTime.Now + Environment.NewLine);
                File.AppendAllText(pathToLogFile, toLogFile);
            }
            catch (Exception e2)
            {
                Console.WriteLine(e2);
            }
        }

        private bool deleteLogFileIfTooBig(string pathToLogFile)
        {
            if (File.Exists(pathToLogFile) && new FileInfo(pathToLogFile).Length > 2e+7) //20 Megabyte
            {
                File.Delete(pathToLogFile);
                Console.WriteLine("LOGGER: Logfile exceeded 20MB and got resettet.");
                return true;
            }
            return false;
        }
    }
}
