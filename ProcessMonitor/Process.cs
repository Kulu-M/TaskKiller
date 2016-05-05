using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessMonitor
{
    public class MyProcess
    {
        public string name { get; set; }
        public TimeSpan allowedRunningTime { get; set; }
        public TimeSpan actualRunningTime { get; set; }
        public TimeSpan warningTime { get; set; }
        public int processInstances { get; set; }

        public MyProcess()
        {
            allowedRunningTime = TimeSpan.FromMinutes(3);
            warningTime = TimeSpan.FromMinutes(1);
        }
    }
}
