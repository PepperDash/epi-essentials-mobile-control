using PepperDash.Core;
using PepperDash.Essentials.AppServer.Messengers;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
#if SERIES4
#endif
namespace PepperDash.Essentials.Room.MobileControl
{
    public class ISetTopBoxControlsMessenger:MessengerBase
    {
        private readonly ISetTopBoxControls stbDevice;
        public ISetTopBoxControlsMessenger(string key, string messagePath, IKeyName device) : base(key, messagePath, device)
        {
            stbDevice = device as ISetTopBoxControls;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();
        
            AddAction("/dvrList", (id, content) => PressAndHoldHandler.HandlePressAndHold(DeviceKey, content, (b) => stbDevice?.DvrList(b)));
            AddAction("/replay", (id, content) => PressAndHoldHandler.HandlePressAndHold(DeviceKey, content, (b) => stbDevice?.Replay(b)));
        }        
    }
}