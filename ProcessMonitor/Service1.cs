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

namespace ProcessMonitor
{
    public partial class Service1 : ServiceBase
    {
        #region SETTINGS

        public bool showWarnings = true;
        //Folderpath of Files --> //"C:\\Users\\XXX\\AppData\\Roaming\\Process Monitor"
        public static string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Process Monitor");
        //public static string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
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

            //OnStart-Callback muss returnen, sonst sieht WIN den Service als 'timed out' an und killt ihn -> Logik muss in eigenem Thread laufen

            log.writeToLogFile("Starting Thread!");

            _thread = new Thread(ThreadWorkerLogic)
            {
                Name = "ProcessMonitor",
                IsBackground = true
            };
            _thread.Start();
        }

        private void ThreadWorkerLogic()
        {
            while (!_shutdownEvent.WaitOne(0))
            {
                startLogic();
            }
        }

        private void startLogic()
        {
            checkOwnFolder();

            handleProcessList();

            initializeTimer();
        }

        private void handleProcessList()
        {
            if (File.Exists(pathStringProcessFile))
            {
                readProcessList();
                log.writeToLogFile("Process List File read from " + pathStringProcessFile + ".");
            }
            else
            {
                createExampleProcessList();
                writeProcessListToFile();
                try
                {
                    MessageBox.Show("Please go to: " + folderPath + " and insert your desired Processes into the JSON-File: " + processFileName + "." + Environment.NewLine + "The Logfile is also located in the same Directory.");
                }
                catch (Exception)
                {
                    //ignore
                }
                log.writeToLogFile("Example Process List File created at " + pathStringProcessFile + ".");
            }
        }

        private void checkOwnFolder()
        {
            if (Directory.Exists(folderPath)) return;
            Directory.CreateDirectory(folderPath);
        }

        private void createExampleProcessList()
        {
            forbiddenProcessesList = new List<MyProcess>();
            var proc1 = new MyProcess()
            {
                name = "Example Process Name 1",
                allowedRunningTime = TimeSpan.FromMinutes(30),
                warningTime = TimeSpan.FromMinutes(5)
            };
            var proc2 = new MyProcess()
            {
                name = "Example Process Name 2",
                allowedRunningTime = TimeSpan.FromMinutes(60),
                warningTime = TimeSpan.FromMinutes(10)
                
            };
            forbiddenProcessesList.Add(proc1);
            forbiddenProcessesList.Add(proc2);     
        }

        private void readProcessList()
        {
            try
            {
                forbiddenProcessesList = new List<MyProcess>();
                var fileStream = File.Open(pathStringProcessFile, FileMode.Open);

                using (StreamReader sr = new StreamReader(fileStream))
                {
                    forbiddenProcessesList = JsonConvert.DeserializeObject<List<MyProcess>>(sr.ReadToEnd());
                }
                fileStream.Close();
            }
            catch (Exception e)
            {
                log.writeToLogFile("Exception happened while reading Process List Json File: " + e);
            }
            
        }

        private void initializeTimer()
        {
            timer.Elapsed += OnElapsedTime;
            timer.Interval = 60000; //60Sekunden
            timer.Enabled = true;
        }

        /// <summary>
        /// DEBUG Method
        /// Not used anymore
        /// </summary>
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

            //toLogFile += "There are " + forbiddenProcessesList.Count + " Processes forbidden:";
            //foreach (var myProcess in forbiddenProcessesList)
            //{
            //    toLogFile += Environment.NewLine + "Name: " + myProcess.name + ". Allowed Runtime: " + myProcess.allowedRunningTime + ".";
            //}
            //writeToLogFile();
        }

        protected override void OnStop()
        {
            timer.Enabled = false;

            log.writeToLogFile("Stopping Thread!");

            _shutdownEvent.Set();
            if (!_thread.Join(5000)) //Der Thread hat 5 Sekunden um sich zu schließen
            {
                _thread.Abort();
                log.writeToLogFile("Thread stopped!");
            }
            log.writeToLogFile("Service stopped!");
        }

        /// <summary>
        /// Method to write Process List to Json File
        /// </summary>
        private void writeProcessListToFile()
        {
            var serializedList = JsonConvert.SerializeObject(forbiddenProcessesList);
            var fileStream = File.Open(pathStringProcessFile, FileMode.OpenOrCreate);

            using (StreamWriter sw = new StreamWriter(fileStream))
            {
                sw.Write(serializedList);
            }
            fileStream.Close();
        }

        #endregion START, STOP, INIT, DEBUG METHODS

        #region HANDLING TIMER AND RELEVANT METHODS

        private void OnElapsedTime(object source, ElapsedEventArgs e)
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
                    forbiddenProcess.actualRunningTime += TimeSpan.FromMinutes(1);
                    log.writeToLogFile("Scanned processes, target process '" + forbiddenProcess.name + "' is running. The process already ran " + forbiddenProcess.actualRunningTime + " Minutes today.");
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
                    log.writeToLogFile("Scanned processes, target process '" + forbiddenProcess.name + "' is not running.");
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
