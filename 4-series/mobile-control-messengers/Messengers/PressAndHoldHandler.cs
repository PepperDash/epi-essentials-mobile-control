using Crestron.SimplSharp;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using System;
using System.Collections.Generic;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public static class PressAndHoldHandler
    {
        private const long ButtonHeartbeatInterval = 1000;

        private static readonly Dictionary<string, CTimer> _pushedActions = new Dictionary<string, CTimer>();

        private static readonly Dictionary<string, Action<string, Action<bool>>> _pushedActionHandlers;

        static PressAndHoldHandler()
        {
            _pushedActionHandlers = new Dictionary<string, Action<string, Action<bool>>>
            {
                {"true", AddTimer },
                {"held", ResetTimer },
                {"false", StopTimer }
            };
        }

        private static void AddTimer(string type, Action<bool> action)
        {

            if (_pushedActions.TryGetValue(type, out CTimer cancelTimer))
            {
                return;
            }

            cancelTimer = new CTimer(o =>
            {
                action(false);

                _pushedActions.Remove(type);
            }, ButtonHeartbeatInterval);

            _pushedActions.Add(type, cancelTimer);
        }

        private static void ResetTimer(string type, Action<bool> action)
        {

            if (!_pushedActions.TryGetValue(type, out CTimer cancelTimer)) { return; }

            cancelTimer.Reset(ButtonHeartbeatInterval);
        }

        private static void StopTimer(string type, Action<bool> action)
        {

            if (!_pushedActions.TryGetValue(type, out CTimer cancelTimer)) { return; }

            cancelTimer.Stop();
            _pushedActions.Remove(type);
        }

        public static Action<string, Action<bool>> GetPressAndHoldHandler(string value)
        {

            if (!_pushedActionHandlers.TryGetValue(value, out Action<string, Action<bool>> handler))
            {
                Debug.Console(0, "Unable to get Press & Hold handler for {0}", value);
                return null;
            }

            return handler;
        }

        public static void HandlePressAndHold(JToken content, Action<bool> action)
        {
            var state = content.ToObject<MobileControlSimpleContent<string>>();

            var timerHandler = GetPressAndHoldHandler(state.Value);
            if (timerHandler == null)
            {
                return;
            }

            timerHandler(state.Value, action);

            action(state.Value.Equals("true", StringComparison.InvariantCultureIgnoreCase));
        }
    }
}
