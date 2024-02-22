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
using PepperDash.Essentials.Touchpanel;
using Feedback = PepperDash.Essentials.Core.Feedback;

namespace PepperDash.Essentials.Devices.Common.TouchPanel
{
    public class MobileControlTouchpanelController : TouchpanelBase, IHasFeedback, ITswAppControl, ITswZoomControl
    {
        private MobileControlTouchpanelProperties localConfig;
        private IMobileControlRoomMessenger _bridge;

        private readonly StringFeedback AppUrlFeedback;
        private readonly StringFeedback QrCodeUrlFeedback;
        private readonly StringFeedback McServerUrlFeedback;
        private readonly StringFeedback UserCodeFeedback;

        private readonly StringFeedback _appPackageFeedback;

        public StringFeedback AppPackageFeedback => _appPackageFeedback;

        private readonly BoolFeedback _appOpenFeedback;

        public BoolFeedback AppOpenFeedback => _appOpenFeedback;

        private readonly BoolFeedback _zoomIncomingCallFeedback;

        public BoolFeedback ZoomIncomingCallFeedback => _zoomIncomingCallFeedback;

        private readonly BoolFeedback _zoomInCallFeedback;

        public BoolFeedback ZoomInCallFeedback => _zoomInCallFeedback;


        public FeedbackCollection<Feedback> Feedbacks { get; private set; }

        public FeedbackCollection<Feedback> ZoomFeedbacks { get; private set; }

        public string DefaultRoomKey => _config.DefaultRoomKey;

        public bool UseDirectServer => localConfig.UseDirectServer;

        public MobileControlTouchpanelController(string key, string name, BasicTriListWithSmartObject panel, MobileControlTouchpanelProperties config):base(key, name, panel, config)
        {
            localConfig = config;

            AddPostActivationAction(SubscribeForMobileControlUpdates);

            AppUrlFeedback = new StringFeedback(() => _bridge?.AppUrl);
            QrCodeUrlFeedback = new StringFeedback(() => _bridge?.QrCodeUrl);
            McServerUrlFeedback = new StringFeedback(() => _bridge?.McServerUrl);
            UserCodeFeedback = new StringFeedback(() => _bridge?.UserCode);

            _appOpenFeedback = new BoolFeedback(() =>
            {
                if (Panel is TswX60BaseClass tsX60)
                {
                    return tsX60.ExtenderApplicationControlReservedSigs.CloseOpenApplicationFeedback.BoolValue;
                }

                if (Panel is TswX70Base tsX70)
                {
                    return tsX70.ExtenderApplicationControlReservedSigs.CloseOpenApplicationFeedback.BoolValue;
                }

                return false;
            });

            _zoomIncomingCallFeedback = new BoolFeedback(() =>
            {
                if (Panel is TswX60WithZoomRoomAppReservedSigs tsX60)
                {
                    return tsX60.ExtenderZoomRoomAppReservedSigs.ZoomRoomIncomingCallFeedback.BoolValue;
                }

                if (Panel is TswX70Base tsX70)
                {
                    return tsX70.ExtenderZoomRoomAppReservedSigs.ZoomRoomIncomingCallFeedback.BoolValue;
                }

                return false;
            });

            _zoomInCallFeedback = new BoolFeedback(() =>
            {
                if (Panel is TswX60WithZoomRoomAppReservedSigs tsX60)
                {
                    return tsX60.ExtenderZoomRoomAppReservedSigs.ZoomRoomActiveFeedback.BoolValue;
                }

                if (Panel is TswX70Base tsX70)
                {
                    return tsX70.ExtenderZoomRoomAppReservedSigs.ZoomRoomActiveFeedback.BoolValue;
                }

                return false;
            });

            Feedbacks = new FeedbackCollection<Feedback>
            {
                AppUrlFeedback, QrCodeUrlFeedback, McServerUrlFeedback, UserCodeFeedback
            };

            ZoomFeedbacks = new FeedbackCollection<Feedback> {
                AppOpenFeedback, _zoomInCallFeedback, _zoomIncomingCallFeedback
            };

            RegisterForExtenders();
        }

        private void RegisterForExtenders()
        {
            if(Panel is TswXX70Base x70Panel) {
                x70Panel.ExtenderApplicationControlReservedSigs.DeviceExtenderSigChange += (e, a) => Debug.Console(2, this, $"X70 App Control Device Extender args: {a.Event}:{a.Sig}:{a.Sig.Type}:{a.Sig.BoolValue}:{a.Sig.UShortValue}:{a.Sig.StringValue}");
                x70Panel.ExtenderApplicationControlReservedSigs.Use();

                x70Panel.ExtenderZoomRoomAppReservedSigs.DeviceExtenderSigChange += (e, a) => Debug.Console(2, this, $"X70 Zoom Room Ap Device Extender args: {a.Event}:{a.Sig}:{a.Sig.Type}:{a.Sig.BoolValue}:{a.Sig.UShortValue}:{a.Sig.StringValue}");
                x70Panel.ExtenderZoomRoomAppReservedSigs.Use();
                return;
            }

            if(Panel is TswX60WithZoomRoomAppReservedSigs x60withZoomApp)
            {
                x60withZoomApp.ExtenderApplicationControlReservedSigs.DeviceExtenderSigChange += (e, a) => Debug.Console(2, this, $"X60 App Control Device Extender args: {a.Event}:{a.Sig}:{a.Sig.Type}:{a.Sig.BoolValue}:{a.Sig.UShortValue}:{a.Sig.StringValue}");
                x60withZoomApp.ExtenderZoomRoomAppReservedSigs.DeviceExtenderSigChange += (e, a) => Debug.Console(2, this, $"X60 Zoom Room App Device Extender args: {a.Event}:{a.Sig}:{a.Sig.Type}:{a.Sig.BoolValue}:{a.Sig.UShortValue}:{a.Sig.StringValue}");

                x60withZoomApp.ExtenderZoomRoomAppReservedSigs.Use();
                x60withZoomApp.ExtenderApplicationControlReservedSigs.Use();                
            }
        }

        public override bool CustomActivate()
        {
            var appMessenger = new ITswAppControlMessenger($"appControlMessenger-{Key}", $"/device/{Key}", this);

            var zoomMessenger = new ITswZoomControlMessenger($"zoomControlMessenger-{Key}", $"/device/{Key}", this);

            var mc = DeviceManager.AllDevices.OfType<IMobileControl3>().FirstOrDefault();

            if(mc == null)
            {
                return base.CustomActivate();
            }

            mc.AddDeviceMessenger(appMessenger);
            mc.AddDeviceMessenger(zoomMessenger);

            return base.CustomActivate();
        }


        protected override void ExtenderSystemReservedSigs_DeviceExtenderSigChange(DeviceExtender currentDeviceExtender, SigEventArgs args)
        {
            Debug.Console(2, this, $"System Device Extender args: ${args.Event}:${args.Sig}");
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
            foreach(var dev in DeviceManager.AllDevices)
            {
                Debug.Console(0, this, $"{dev.Key}:{dev.GetType().Name}");
            }
            
            var mcList = DeviceManager.AllDevices.OfType<MobileControlSystemController>().ToList();

            if(mcList.Count == 0)
            {
                Debug.Console(0, this, $"No Mobile Control controller found");

                return;
            }

            // use first in list, since there should only be one.
             var mc = mcList[0];

            var bridge = mc.GetRoomBridge(_config.DefaultRoomKey);

            if(bridge == null)
            {
                Debug.Console(0, this, $"No Mobile Control bridge for {_config.DefaultRoomKey} found ");
                return;
            }

            _bridge = bridge;

            _bridge.UserCodeChanged += UpdateFeedbacks;
            _bridge.AppUrlChanged += (s, a) => { Debug.Console(0, this, "AppURL changed"); UpdateFeedbacks(s, a); };            
        }

        private void UpdateFeedbacks(object sender, EventArgs args)
        {
            UpdateFeedbacks();
        }

        private void UpdateFeedbacks()
        {
            foreach (var feedback in Feedbacks) { feedback.FireUpdate(); }
        }

        public void HideOpenApp()
        {
            if(Panel is TswX70Base x70Panel )
            {
                x70Panel.ExtenderApplicationControlReservedSigs.HideOpenedApplication();
                return;
            }

            if(Panel is TswX60BaseClass x60Panel)
            {
                x60Panel.ExtenderApplicationControlReservedSigs.HideOpenApplication();
                return;
            }
        }

        public void OpenApp()
        {
            if (Panel is TswX70Base x70Panel)
            {
                x70Panel.ExtenderApplicationControlReservedSigs.OpenApplication();
                return;
            }

            if (Panel is TswX60WithZoomRoomAppReservedSigs x60Panel)
            {
                Debug.Console(0, this, $"X60 panel does not support zoom app");
                return;
            }
        }

        public void CloseOpenApp()
        {
            if (Panel is TswX70Base x70Panel)
            {
                x70Panel.ExtenderApplicationControlReservedSigs.CloseOpenedApplication();
                return;
            }

            if (Panel is TswX60WithZoomRoomAppReservedSigs x60Panel)
            {
                x60Panel.ExtenderApplicationControlReservedSigs.CloseOpenedApplication();
                return;
            }
        }

        public void EndZoomCall()
        {
            if (Panel is TswX70Base x70Panel)
            {
                x70Panel.ExtenderZoomRoomAppReservedSigs.ZoomRoomEndCall();
                return;
            }

            if (Panel is TswX60WithZoomRoomAppReservedSigs x60Panel)
            {
                x60Panel.ExtenderZoomRoomAppReservedSigs.ZoomRoomEndCall();
                return;
            }
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
                    return new XpanelForHtml5(id, Global.ControlSystem);
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
