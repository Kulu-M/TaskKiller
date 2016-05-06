using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using Timer = System.Timers.Timer;

namespace ProcessMonitor
{
    public partial class Service1 : ServiceBase
    {
        #region SETTINGS

        public bool showWarnings = true;
        //Folderpath of Logfile
        public static string filepath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        //Filename of Logfile
        public static string filename = "ProcessMonitorLogFile.txt";
        //Complete string to Logfile Location
        readonly string pathstring = Path.Combine(filepath, filename);

        #endregion SETTINGS

        #region VARDEC

        //Auxiliary Variable
        private readonly Timer timer = new Timer();
        int killedProcs;
        string toLogFile = "";
        //Processes
        private List<MyProcess> forbiddenProcessesList;

        #endregion VARDEC

        #region START, STOP, INIT & DEBUG METHODS

        public Service1()
        {
            InitializeComponent();
        }

        public void OnDebug()
        {
            OnStart(null);
        }

        protected override void OnStart(string[] args)
        {
            toLogFile = "";
            toLogFile = "Service started!";
            writeToLogFile();

            initializeProcessList();
            initializeTimer();
        }

        private void initializeTimer()
        {
            timer.Elapsed += OnElapsedTime;
            timer.Interval = 60000; //60Sekunden
            timer.Enabled = true;
        }

        private void initializeProcessList()
        {
            forbiddenProcessesList = new List<MyProcess>();
            var proc1 = new MyProcess()
            {
                name = "atom",
                allowedRunningTime = TimeSpan.FromMinutes(2)
            };
            var proc2 = new MyProcess()
            {
                name = "rainmeter",
            };
            forbiddenProcessesList.Add(proc1);
            forbiddenProcessesList.Add(proc2);

            toLogFile += "There are " + forbiddenProcessesList.Count + " Processes forbidden:";
            foreach (var myProcess in forbiddenProcessesList)
            {
                toLogFile += Environment.NewLine + "Name: " + myProcess.name + ". Allowed Runtime: " + myProcess.allowedRunningTime + ".";
            }
            writeToLogFile();
        }

        protected override void OnStop()
        {
            timer.Enabled = false;
            toLogFile = "Service stopped!";
            writeToLogFile();
        }

        #endregion START, STOP, INIT, DEBUG METHODS

        #region HANDLING TIMER AND RELEVANT METHODS

        private void OnElapsedTime(object source, ElapsedEventArgs e)
        {
            if (nextDay())
            {
                ProcessMonitorSettings.Default.Day = DateTime.Today;
                resetProcessRunTimes();
                toLogFile = "New Day.";
                writeToLogFile();
            }
            var localProcesses = Process.GetProcesses();
            toLogFile += "Number of running processes: " + localProcesses.Count();
            writeToLogFile();
            
            foreach (var forbiddenProcess in forbiddenProcessesList)
            {
                //Raise Runtime if process is running
                if (checkIfProcessIsRunning(forbiddenProcess.name))
                {
                    forbiddenProcess.actualRunningTime += TimeSpan.FromMinutes(1);
                    toLogFile = "Scanned processes, target process '" + forbiddenProcess.name + "' is running. The process already ran " + forbiddenProcess.actualRunningTime + " Minutes today.";
                    writeToLogFile();
                }
                if (showWarnings)
                {
                    //Warn user from upcoming TimeLimit
                    TimeSpan warningTime = forbiddenProcess.allowedRunningTime - forbiddenProcess.warningTime;
                    if (checkIfProcessIsRunning(forbiddenProcess.name) && warningTime == forbiddenProcess.actualRunningTime)
                    {
                        sendWarning(forbiddenProcess);
                    }
                }
                
                //Kill Process if Runtime is up
                if (checkIfProcessIsRunning(forbiddenProcess.name) && forbiddenProcess.actualRunningTime >= forbiddenProcess.allowedRunningTime)
                {
                    killProcess(forbiddenProcess);
                }
                //Write to Log that process is not running
                else if (checkIfProcessIsRunning(forbiddenProcess.name) == false)
                {
                    toLogFile = "Scanned processes, target process '" + forbiddenProcess.name + "' is not running.";
                    writeToLogFile();
                }
            }
            if (killedProcs != 0)
            {
                toLogFile = "Total processes terminated: " + killedProcs;
                writeToLogFile();
                killedProcs = 0;
            }
        }

        private void resetProcessRunTimes()
        {
            foreach (var myProcess in forbiddenProcessesList)
            {
                myProcess.actualRunningTime = TimeSpan.Zero;
            }
        }

        private void sendWarning(MyProcess forbiddenProcess)
        {
            MessageBox.Show("Attention! Process " + forbiddenProcess.name + " will be terminated in " + forbiddenProcess.warningTime + ".");
        }

        private bool nextDay()
        {
            if (ProcessMonitorSettings.Default.Day.Day != DateTime.Today.Day) return true;
            return false;
        }

        private bool checkIfProcessIsRunning(string processName)
        {
            var localProcesses = Process.GetProcessesByName(processName);
            return localProcesses.Length > 0;
        }

        private void killProcess(MyProcess processName)
        {
            var localProcesses = Process.GetProcesses();
            try
            {
                foreach (var proc in localProcesses)
                {
                    if (proc.ProcessName == processName.name)
                    {
                        proc.Kill();
                        processName.processInstances++;
                        killedProcs++;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            if (processName.processInstances > 0)
            {
                toLogFile += "Process terminated: '" + processName.name + "'. The Process had " + processName.processInstances + " Instances.";
                writeToLogFile();
                processName.processInstances = 0;
            }
            else
            {
                toLogFile = "Something went wrong killing the Process '" + processName.name + "'.";
                writeToLogFile();
            }
        }

        #endregion HANDLING TIMER AND RELEVANT METHODS

        #region LOG

        private void writeToLogFile()
        {
            try
            {
                deleteLogFileIfTooBig();
                File.AppendAllText(pathstring, Environment.NewLine + "-----NEW ENTRY-----" + Environment.NewLine + DateTime.Now + Environment.NewLine);
                File.AppendAllText(pathstring, toLogFile);
                toLogFile = "";
                
            }
            catch (Exception e2)
            {
                Console.WriteLine(e2);
            }
        }

        private void deleteLogFileIfTooBig()
        {
            if (File.Exists(pathstring) && new FileInfo(pathstring).Length > 2e+7) //20 Megabyte
            {
                File.Delete(pathstring);
            }  
        }

        #endregion LOG
    }
}
