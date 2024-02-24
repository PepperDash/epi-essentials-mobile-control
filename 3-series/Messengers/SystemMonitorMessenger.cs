using System;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core.Monitoring;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class SystemMonitorMessenger : MessengerBase
    {
        private SystemMonitorController systemMonitor;

        private IMobileControl3 appServer;

        public SystemMonitorMessenger(string key, SystemMonitorController sysMon, string messagePath)
            : base(key, messagePath, sysMon)
        {
            if (sysMon == null)
                throw new ArgumentNullException("sysMon");

            this.systemMonitor = sysMon;

            this.systemMonitor.SystemMonitorPropertiesChanged += SysMon_SystemMonitorPropertiesChanged;

            foreach (var p in this.systemMonitor.ProgramStatusFeedbackCollection)
            {
                p.Value.ProgramInfoChanged += ProgramInfoChanged;
            }

            CrestronConsole.AddNewConsoleCommand(s => SendFullStatusMessage(), "SendFullSysMonStatus",
                "Sends the full System Monitor Status", ConsoleAccessLevelEnum.AccessOperator);
        }

        /// <summary>
        /// Posts the program information message
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ProgramInfoChanged(object sender, ProgramInfoEventArgs e)
        {
            if (e.ProgramInfo != null)
            {
                //Debug.Console(1, "Posting Status Message: {0}", e.ProgramInfo.ToString());
                appServer.SendMessageObject(new MobileControlMessage
                {
                    Type = MessagePath,
                    Content = JToken.FromObject(e.ProgramInfo)
                });
            }
        }

        /// <summary>
        /// Posts the system monitor properties
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SysMon_SystemMonitorPropertiesChanged(object sender, EventArgs e)
        {
            SendSystemMonitorStatusMessage();
        }

        private void SendFullStatusMessage()
        {
            SendSystemMonitorStatusMessage();

            foreach (var p in systemMonitor.ProgramStatusFeedbackCollection)
            {
                appServer.SendMessageObject(new MobileControlMessage
                {
                    Type = MessagePath,
                    Content = JToken.FromObject(p.Value.ProgramInfo)
                });                
            }
        }

        private void SendSystemMonitorStatusMessage()
        {
            Debug.Console(1, "Posting System Monitor Status Message.");

            // This takes a while, launch a new thread
            Task.Run(() => appServer.SendMessageObject(new MobileControlMessage
            {
                Type = MessagePath,
                Content = JToken.FromObject(new SystemMonitorStateMessage
                {

                    TimeZone = systemMonitor.TimeZoneFeedback.IntValue,
                    TimeZoneName = systemMonitor.TimeZoneTextFeedback.StringValue,
                    IoControllerVersion = systemMonitor.IoControllerVersionFeedback.StringValue,
                    SnmpVersion = systemMonitor.SnmpVersionFeedback.StringValue,
                    BacnetVersion = systemMonitor.BaCnetAppVersionFeedback.StringValue,
                    ControllerVersion = systemMonitor.ControllerVersionFeedback.StringValue
                })
            }));
        }

#if SERIES4
        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
#else
        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
#endif
        {
            AppServerController.AddAction(MessagePath + "/fullStatus", (id, content) => SendFullStatusMessage());
        }
    }

    public class SystemMonitorStateMessage
    {
        [JsonProperty("timeZone", NullValueHandling = NullValueHandling.Ignore)]
        public int TimeZone { get; set; }

        [JsonProperty("timeZone", NullValueHandling = NullValueHandling.Ignore)]
        public string TimeZoneName { get; set; }

        [JsonProperty("timeZone", NullValueHandling = NullValueHandling.Ignore)]
        public string IoControllerVersion { get; set; }

        [JsonProperty("timeZone", NullValueHandling = NullValueHandling.Ignore)]
        public string SnmpVersion { get; set; }

        [JsonProperty("timeZone", NullValueHandling = NullValueHandling.Ignore)]
        public string BacnetVersion { get; set; }

        [JsonProperty("timeZone", NullValueHandling = NullValueHandling.Ignore)]
        public string ControllerVersion { get; set; }
    }
}