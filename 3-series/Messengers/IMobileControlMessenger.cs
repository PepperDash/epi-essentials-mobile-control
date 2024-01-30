using PepperDash.Essentials.Core.DeviceTypeInterfaces;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public interface IMobileControlMessenger
    {
        IMobileControl3 AppServerController { get; }
        string MessagePath { get; }

        void RegisterWithAppServer(IMobileControl3 appServerController);
    }
}