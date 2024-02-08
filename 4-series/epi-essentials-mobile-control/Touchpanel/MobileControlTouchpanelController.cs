using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading.Tasks;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.UI;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Core.UI;
using Feedback = PepperDash.Essentials.Core.Feedback;

namespace PepperDash.Essentials.Devices.Common.TouchPanel
{
    public class MobileControlTouchpanelController : TouchpanelBase, IHasFeedback
    {
        private IMobileControlRoomMessenger _bridge;

        private StringFeedback AppUrlFeedback;
        private StringFeedback QrCodeUrlFeedback;
        private StringFeedback McServerUrlFeedback;
        private StringFeedback UserCodeFeedback;

        public FeedbackCollection<Feedback> Feedbacks { get; private set; }

        public string DefaultRoomKey => _config.DefaultRoomKey;

        public MobileControlTouchpanelController(string key, string name, BasicTriListWithSmartObject panel, MobileControlTouchpanelProperties config):base(key, name, panel, config)
        {
            AddPostActivationAction(SubscribeForMobileControlUpdates);

            AppUrlFeedback = new StringFeedback(() => _bridge?.AppUrl);
            QrCodeUrlFeedback = new StringFeedback(() => _bridge?.QrCodeUrl);
            McServerUrlFeedback = new StringFeedback(() => _bridge?.McServerUrl);
            UserCodeFeedback = new StringFeedback(() => _bridge?.UserCode);

            Feedbacks = new FeedbackCollection<Feedback>
            {
                AppUrlFeedback, QrCodeUrlFeedback, McServerUrlFeedback, UserCodeFeedback
            };

            
        }
        protected override void ExtenderSystemReservedSigs_DeviceExtenderSigChange(DeviceExtender currentDeviceExtender, SigEventArgs args)
        {
            Debug.Console(2, this, $"Device Extender args: ${args.Event}:${args.Sig}");
        }

        protected override void SetupPanelDrivers(string roomKey)
        {
            AppUrlFeedback.LinkInputSig(Panel.StringInput[1]);
            QrCodeUrlFeedback.LinkInputSig(Panel.StringInput[2]);
            McServerUrlFeedback.LinkInputSig(Panel.StringInput[3]);
            UserCodeFeedback.LinkInputSig(Panel.StringInput[4]);

            Panel.OnlineStatusChange += (sender, args) =>
            {
                UpdateFeedbacks();

                Panel.StringInput[1].StringValue = AppUrlFeedback.StringValue;
                Panel.StringInput[2].StringValue = QrCodeUrlFeedback.StringValue;
                Panel.StringInput[3].StringValue = McServerUrlFeedback.StringValue;
                Panel.StringInput[4].StringValue = UserCodeFeedback.StringValue;
            };
        }
        
        private void SubscribeForMobileControlUpdates()
        {
            var bridge = DeviceManager.AllDevices.OfType<IMobileControlRoomMessenger>().FirstOrDefault(mcBridge => mcBridge.Key.Contains(_config.DefaultRoomKey));

            if(bridge == null)
            {
                Debug.Console(0, this, $"No Mobile Control controller found for ${_config.DefaultRoomKey}");

                return;                    
            }

            _bridge = bridge;

            _bridge.UserCodeChanged += UpdateFeedbacks;
            _bridge.AppUrlChanged += UpdateFeedbacks;            
        }

        private void UpdateFeedbacks(object sender, EventArgs args)
        {
            UpdateFeedbacks();
        }

        private void UpdateFeedbacks()
        {
            foreach (var feedback in Feedbacks) { feedback.FireUpdate(); }
        }
    }

    public class MobileControlTouchpanelControllerFactory : EssentialsPluginDeviceFactory<MobileControlTouchpanelController>
    {
        public MobileControlTouchpanelControllerFactory()
        {
            TypeNames = new List<string>() { "mccrestronapp", "mctsw550", "mctsw750", "mctsw1050", "mctsw560", "mctsw760", "mctsw1060", "mctsw570", "mctsw770", "mcts770", "mctsw1070", "mcts1070", "mcxpanel" };
            MinimumEssentialsFrameworkVersion = "2.0.0";
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            var comm = CommFactory.GetControlPropertiesConfig(dc);
            var props = JsonConvert.DeserializeObject<MobileControlTouchpanelProperties>(dc.Properties.ToString());

            var panel = GetPanelForType(dc.Type, comm.IpIdInt, props.ProjectName);

            if (panel == null)
            {
                Debug.Console(0, "Unable to create Touchpanel for type {0}. Touchpanel Controller WILL NOT function correctly", dc.Type);                
            }

            Debug.Console(1, "Factory Attempting to create new MobileControlTouchpanelController");

            var panelController = new MobileControlTouchpanelController(dc.Key, dc.Name, panel, props);

            return panelController;
        }

        private BasicTriListWithSmartObject GetPanelForType(string type, uint id, string projectName)
        {
            type = type.ToLower().Replace("mc","");
            try
            {
                if (type == "crestronapp")
                {
                    var app = new CrestronApp(id, Global.ControlSystem);
                    app.ParameterProjectName.Value = projectName;
                    return app;
                }
                else if (type == "xpanel")
                    return new XpanelForSmartGraphics(id, Global.ControlSystem);
                else if (type == "tsw550")
                    return new Tsw550(id, Global.ControlSystem);
                else if (type == "tsw552")
                    return new Tsw552(id, Global.ControlSystem);
                else if (type == "tsw560")
                    return new Tsw560(id, Global.ControlSystem);
                else if (type == "tsw750")
                    return new Tsw750(id, Global.ControlSystem);
                else if (type == "tsw752")
                    return new Tsw752(id, Global.ControlSystem);
                else if (type == "tsw760")
                    return new Tsw760(id, Global.ControlSystem);
                else if (type == "tsw1050")
                    return new Tsw1050(id, Global.ControlSystem);
                else if (type == "tsw1052")
                    return new Tsw1052(id, Global.ControlSystem);
                else if (type == "tsw1060")
                    return new Tsw1060(id, Global.ControlSystem);
                else if (type == "tsw570")
                    return new Tsw570(id, Global.ControlSystem);
                else if (type == "tsw770")
                    return new Tsw770(id, Global.ControlSystem);
                else if (type == "ts770")
                    return new Ts770(id, Global.ControlSystem);
                else if (type == "tsw1070")
                    return new Tsw1070(id, Global.ControlSystem);
                else if (type == "ts1070")
                    return new Ts1070(id, Global.ControlSystem);
                else
                {
                    Debug.Console(0, Debug.ErrorLogLevel.Notice, "WARNING: Cannot create TSW controller with type '{0}'", type);
                    return null;
                }
            }
            catch (Exception e)
            {
                Debug.Console(0, Debug.ErrorLogLevel.Notice, "WARNING: Cannot create TSW base class. Panel will not function: {0}", e.Message);
                return null;
            }
        }
    }
}
