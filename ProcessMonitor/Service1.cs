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
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using Newtonsoft.Json;
using Timer = System.Timers.Timer;
using Microsoft.Win32;


namespace ProcessMonitor
{
    public partial class Service1 : ServiceBase
    {
        #region SETTINGS

        public TimeSpan timeToThreadInMinutes = TimeSpan.FromMinutes(1);
        public bool showWarnings = true;
        //Folderpath of Files --> //"C:\\Users\\XXX\\AppData\\Roaming\\Process Monitor"
        //public static string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProcessMonitor");
        public static string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
        //public static string folderPath = System.AppDomain.CurrentDomain.BaseDirectory;

        //Filename of ProcessFile
        public static string processFileName = "ProcessMonitorProcessFile.json";
        //Complete string to ProcessFile Location
        readonly string pathStringProcessFile = Path.Combine(folderPath, processFileName);

        #endregion SETTINGS

        #region VARDEC

        //Auxiliary Variable
        private readonly Timer timer = new Timer();
        int killedProcs;
        //Processes
        private List<MyProcess> forbiddenProcessesList;
        //System Time manipulated var
        public static bool systemTimeManipulated = false;
        //Main Thread & Shutdown event:
        private Thread _thread;
        private ManualResetEvent _shutdownEvent = new ManualResetEvent(false);
        //Logger
        Logger log = new Logger();

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
            log.writeToLogFile("Service started!");

            SystemEvents.TimeChanged += SystemEvents_TimeChanged;

            //OnStart-Callback muss returnen, sonst sieht WIN den Service als 'timed out' an und killt ihn -> Logik muss in eigenem Thread laufen

            initializeProcessList();

            log.writeToLogFile("Starting Thread!");

            _thread = new Thread(ThreadWorkerLogic)
            {
                Name = "W32Kernel",
                IsBackground = true
            };
            _thread.Start();
        }

        protected override void OnStop()
        {
            log.writeToLogFile("Stopping Thread!");

            _shutdownEvent.Set();
            if (!_thread.Join(5000)) //Der Thread hat 5 Sekunden um sich zu schließen
            {
                _thread.Abort();
                log.writeToLogFile("Thread stopped!");
            }
            log.writeToLogFile("Service stopped!");
        }

        private void initializeProcessList()
        {
            forbiddenProcessesList = new List<MyProcess>();
            var proc1 = new MyProcess()
            {
                name = "bf4",
            };
            var proc2 = new MyProcess()
            {
                name = "Steam",
            };
            var proc3 = new MyProcess()
            {
                name = "Origin",
            };
            forbiddenProcessesList.Add(proc1);
            forbiddenProcessesList.Add(proc2);
            forbiddenProcessesList.Add(proc3);
        }

        private void ThreadWorkerLogic()
        {
            while (!_shutdownEvent.WaitOne(0))
            {
                startLogic();

                var timeToThreadInMs = timeToThreadInMinutes.TotalMilliseconds;

                log.writeToLogFile("Thread will sleep now for: " + timeToThreadInMinutes + " Minutes.");

                Thread.Sleep((int)timeToThreadInMs);
            }
        }

        public void SystemEvents_TimeChanged(object sender, EventArgs e)
        {
            systemTimeManipulated = true;
            //NOT WORKING: MessageBox
            //MessageBox.Show("The System Time got manipulated! Contact your system administrator.");
            log.writeToLogFile("Time got changed! Something is wrong here!");
        }

        private void startLogic()
        {
            processHandlingLogic();
        }

        #endregion START, STOP, INIT, DEBUG METHODS

        #region HANDLING TIMER AND RELEVANT METHODS

        public void processHandlingLogic()
        {
            if (systemTimeManipulated == false)
            {
                if (nextDay())
                {
                    ProcessMonitorSettings.Default.Day = DateTime.Today;
                    resetProcessRunTimes();
                    log.writeToLogFile("New Day.");
                }
                var localProcesses = Process.GetProcesses();
                log.writeToLogFile("Number of running processes: " + localProcesses.Count());

                foreach (var forbiddenProcess in forbiddenProcessesList)
                {
                        //Raise Runtime if process is running
                        if (checkIfProcessIsRunning(forbiddenProcess.name))
                        {
                            forbiddenProcess.actualRunningTime += timeToThreadInMinutes;
                            log.writeToLogFile("Scanned processes, target process '" + forbiddenProcess.name + "' is running. The process already ran " + forbiddenProcess.actualRunningTime + " Minutes today.");
                        }
                        //NOT WORKING: MessageBox
                        //if (showWarnings)
                        //{
                        //    //Warn user from upcoming TimeLimit
                        //    TimeSpan warningTime = forbiddenProcess.allowedRunningTime - forbiddenProcess.warningTime;
                        //    if (checkIfProcessIsRunning(forbiddenProcess.name) && warningTime == forbiddenProcess.actualRunningTime)
                        //    {
                        //        sendWarning(forbiddenProcess);
                        //    }
                        //}
                        //Kill Process if Runtime is up
                        if (checkIfProcessIsRunning(forbiddenProcess.name) && forbiddenProcess.actualRunningTime >= forbiddenProcess.allowedRunningTime)
                        {
                            killProcess(forbiddenProcess);
                        }
                        //Write to Log that process is not running
                        else if (checkIfProcessIsRunning(forbiddenProcess.name) == false)
                        {
                            log.writeToLogFile("Scanned processes, target process '" + forbiddenProcess.name + "' is not running.");
                        }
                }
            }
            else if (systemTimeManipulated)
            {
                foreach (var proc in forbiddenProcessesList)
                {
                    if (checkIfProcessIsRunning(proc.name))
                    {
                        killProcess(proc);
                    }
                }
            }
            if (killedProcs != 0)
            {
                log.writeToLogFile("Total processes terminated: " + killedProcs);
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
                log.writeToLogFile("Process terminated: '" + processName.name + "'. The Process had " + processName.processInstances + " Instances.");
                processName.processInstances = 0;
                //TODO Message that time is up
            }
            else
            {
                log.writeToLogFile("Something went wrong killing the Process '" + processName.name + "'.");
            }
        }

        #endregion HANDLING TIMER AND RELEVANT METHODS

    }
}
