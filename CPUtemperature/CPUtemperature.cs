using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.Timers;
using System.Runtime.InteropServices;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using OpenHardwareMonitor.Hardware;

namespace CPUtemperature
{
    public partial class CPUtemperature : ServiceBase
    {
        // EventLog
        static EventLog eventLog1 = new System.Diagnostics.EventLog();
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(System.IntPtr handle, ref ServiceStatus serviceStatus);

        // MQTT
        static MqttClient client;
        static string clientId;
        static string[] BrokerAddress = { "192.168.198.227", "192.168.2.111" };
        static int broker_entries = BrokerAddress.Length;
        static int broker_used = 0;


        public CPUtemperature()
        {
            InitializeComponent();
            
            if (!System.Diagnostics.EventLog.SourceExists("MySource"))
            {
                System.Diagnostics.EventLog.CreateEventSource(
                    "CPUtemperature", "MyLogs");
            }
            eventLog1.Source = "CPUtemperature";
            eventLog1.Log = "MyLogs";

            client = new MqttClient(BrokerAddress[broker_used]);    // Connect to the default server
        }

        protected override void OnStart(string[] args)
        {
            eventLog1.WriteEntry("In OnStart.");
            // Update the service state to Start Pending.
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            // Set up a timer that triggers every minute.
            Timer timer = new Timer();
            timer.Interval = 60000; // 60 seconds
            timer.Elapsed += new ElapsedEventHandler(this.OnTimer);
            timer.Start();

            // Update the service state to Running.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
        }

        protected override void OnStop()
        {
            eventLog1.WriteEntry("In OnStop.");
        }

        protected override void OnContinue()
        {
            eventLog1.WriteEntry("In OnContinue.");
        }

        public class UpdateVisitor : IVisitor
        {
            public void VisitComputer(IComputer computer)
            {
                computer.Traverse(this);
            }
            public void VisitHardware(IHardware hardware)
            {
                hardware.Update();
                foreach (IHardware subHardware in hardware.SubHardware) subHardware.Accept(this);
            }
            public void VisitSensor(ISensor sensor) { }
            public void VisitParameter(IParameter parameter) { }
        }

        public float getSystemInfo()
        {
            float temp = 0;
            UpdateVisitor updateVisitor = new UpdateVisitor();
            Computer computer = new Computer();
            computer.Open();
            computer.CPUEnabled = true;
            computer.Accept(updateVisitor);
            for (int i = 0; i < computer.Hardware.Length; i++)
            {
                if (computer.Hardware[i].HardwareType == HardwareType.CPU)
                {
                    for (int j = 0; j < computer.Hardware[i].Sensors.Length; j++)
                    {
                        if (computer.Hardware[i].Sensors[j].SensorType == SensorType.Temperature)
                        {
                            String temp0;
                            temp0 = computer.Hardware[i].Sensors[j].Name;
                            String temp1 = temp0.Replace(' ', '_');
                            String temp2 = temp1.Replace('#', '0');
                            
                            temp = (float)computer.Hardware[i].Sensors[j].Value;
                        }
                    }
                }
            }
            computer.Close();
            return temp;
        }

        /*
         * MQTT functions
         */
        static void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string ReceivedMessage = Encoding.UTF8.GetString(e.Message);
            string ReceivedTopic = e.Topic;
        }

        public static void Client_ConnectionClosed(object sender, EventArgs e)
        {
            eventLog1.WriteEntry("The connection has been closed!");
        }

        private static void Client_Reconnect()
        {
            while (!client.IsConnected)
            {
                eventLog1.WriteEntry("Called Client_Reconnect.");
                client = new MqttClient(BrokerAddress[broker_used]);
                // register a callback-function (we have to implement, see below) which is called by the library when a message was received
                client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
                client.ConnectionClosed += Client_ConnectionClosed;

                // use a unique id as client id, each time we start the application
                clientId = Guid.NewGuid().ToString();
                try
                {
                    client.Connect(clientId);
                }
                catch
                {
                    broker_used++;
                    if (broker_used >= broker_entries)
                        broker_used = 0;

                }
                
            }
        }

        public void OnTimer(object sender, ElapsedEventArgs args)
        {
            float temperature;

            if (!client.IsConnected)
            {
                Client_Reconnect();
            }

            temperature = getSystemInfo();
            string Topic = "Asus/Temp/CPU";
            client.Publish(Topic, Encoding.UTF8.GetBytes(temperature.ToString()), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
        }

        public enum ServiceState
        {
            SERVICE_STOPPED = 0x00000001,
            SERVICE_START_PENDING = 0x00000002,
            SERVICE_STOP_PENDING = 0x00000003,
            SERVICE_RUNNING = 0x00000004,
            SERVICE_CONTINUE_PENDING = 0x00000005,
            SERVICE_PAUSE_PENDING = 0x00000006,
            SERVICE_PAUSED = 0x00000007,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ServiceStatus
        {
            public int dwServiceType;
            public ServiceState dwCurrentState;
            public int dwControlsAccepted;
            public int dwWin32ExitCode;
            public int dwServiceSpecificExitCode;
            public int dwCheckPoint;
            public int dwWaitHint;
        };

    }
}
