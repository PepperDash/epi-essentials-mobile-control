using Crestron.SimplSharpPro.DeviceSupport;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class SIMPLDirectRouteMessenger:MessengerBase
    {
        private readonly BasicTriList _eisc;

        public MobileControlSIMPLRunDirectRouteActionJoinMap JoinMap { get; private set; }

        public SIMPLDirectRouteMessenger(string key, BasicTriList eisc, string messagePath) : base(key, messagePath)
        {
            _eisc = eisc;

            JoinMap = new MobileControlSIMPLRunDirectRouteActionJoinMap(1101);
        }

        #region Overrides of MessengerBase

        protected override void CustomRegisterWithAppServer(MobileControlSystemController appServerController)
        {
            throw new System.NotImplementedException();
        }

        #endregion
    }
}