using System;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core.Monitoring;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class SystemMonitorMessenger : MessengerBase
    {
        public SystemMonitorController SysMon { get; private set; }

        public SystemMonitorMessenger(string key, SystemMonitorController sysMon, string messagePath)
            : base(key, messagePath)
        {
            if (sysMon == null)
                throw new ArgumentNullException("sysMon");

            SysMon = sysMon;

            SysMon.SystemMonitorPropertiesChanged += SysMon_SystemMonitorPropertiesChanged;

            foreach (var p in SysMon.ProgramStatusFeedbackCollection)
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
                PostStatusMessage(e.ProgramInfo);
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

            foreach (var p in SysMon.ProgramStatusFeedbackCollection)
            {
                PostStatusMessage(p.Value.ProgramInfo);
            }
        }

        private void SendSystemMonitorStatusMessage()
        {
            Debug.Console(1, "Posting System Monitor Status Message.");

            // This takes a while, launch a new thread
            CrestronInvoke.BeginInvoke(o => PostStatusMessage(new
            {
                timeZone = SysMon.TimeZoneFeedback.IntValue,
                timeZoneName = SysMon.TimeZoneTextFeedback.StringValue,
                ioControllerVersion = SysMon.IoControllerVersionFeedback.StringValue,
                snmpVersion = SysMon.SnmpVersionFeedback.StringValue,
                bacnetVersion = SysMon.BaCnetAppVersionFeedback.StringValue,
                controllerVersion = SysMon.ControllerVersionFeedback.StringValue
            }));
        }

#if SERIES4
        protected override void CustomRegisterWithAppServer(IMobileControl3 appServerController)
#else
        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
#endif
        {
            AppServerController.AddAction(MessagePath + "/fullStatus", new Action(SendFullStatusMessage));
        }
    }
}