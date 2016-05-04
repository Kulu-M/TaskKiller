using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace ProcessMonitor
{
    public partial class Service1 : ServiceBase
    {
        //Auxiliary Variable
        Timer timer = new Timer();
        int killedProcs = 0;
        string toLogFile = "";

        //Folderpath of Logfile
        public static string filepath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        //Filename of Logfile
        public static string filename = "ProcessMonitorLogFile.txt";

        //Process allowed Time
        TimeSpan timeAllowedToRun = TimeSpan.FromHours(2);

        //Process name
        string processName = "atom";

        public Service1()
        {
            InitializeComponent();
        }

        private void OnElapsedTime(object source, ElapsedEventArgs e)
        {
            if (DateTime.Now == DateTime()
            {

            }

            if (checkIfProcessIsRunning(processName))
            {
                ProcessMonitorSettings.Default.Runtime += TimeSpan.FromMinutes(2);
            }

            if (checkIfProcessIsRunning(processName) && ProcessMonitorSettings.Default.Runtime > timeAllowedToRun)
            {
                killProcess(processName);
            }
        }

        private bool checkIfProcessIsRunning(string processName)
        {
            Process[] localProcesses = Process.GetProcessesByName(processName);
            if (localProcesses.Length > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private void killProcess(string processName)
        {
            Process[] localProcesses = Process.GetProcesses();
            Console.WriteLine("Found: " + localProcesses.Count() + " processes.");
            toLogFile += Environment.NewLine + "Found: " + localProcesses.Count() + " processes.";
            try
            {
                foreach (Process proc in localProcesses)
                {
                    if (proc.ProcessName == processName)
                    {
                        proc.Kill();
                        killedProcs++;
                        Console.WriteLine("Process terminated: " + proc.ProcessName);
                        toLogFile += Environment.NewLine + "Process terminated: " + proc.ProcessName;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            if (killedProcs == 0)
            {
                Console.WriteLine("Wanted process not running!");
                toLogFile += Environment.NewLine + "Wanted process not running!";
            }
            else
            {
                Console.WriteLine("Total processes terminated: " + killedProcs);
                toLogFile += Environment.NewLine + "Total processes terminated: " + killedProcs;
            }
            killedProcs = 0;
            writeToLogFile(toLogFile);
        }

        private void writeToLogFile(string toLogFile)
        {
            try
            {
                var pathstring = Path.Combine(filepath, filename);
                File.AppendAllText(pathstring, Environment.NewLine + "-----NEW ENTRY-----" + Environment.NewLine + DateTime.Now + Environment.NewLine);
                File.AppendAllText(pathstring, toLogFile);
                toLogFile = "";
            }
            catch (Exception e2)
            {
                Console.WriteLine(e2);
            }
        }

        public void OnDebug()
        {
            OnStart(null);
        }

        protected override void OnStart(string[] args)
        {
            timer.Elapsed += new ElapsedEventHandler(OnElapsedTime);
            timer.Interval = 60000; //60Sekunden
            timer.Enabled = true;

            Console.WriteLine("Started");
        }

        protected override void OnStop()
        {
            timer.Enabled = false;

            Console.WriteLine("Stopped");
        }



        
    }
}
