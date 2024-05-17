using Crestron.SimplSharp;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
                {"pressed", AddTimer },
                {"held", ResetTimer },
                {"released", StopTimer }
            };
        }

        private static void AddTimer(string type, Action<bool> action)
        {
            Debug.LogMessage(Serilog.Events.LogEventLevel.Debug, "Attempting to add timer for action {type}", type);

            if (_pushedActions.TryGetValue(type, out CTimer cancelTimer))
            {
                Debug.LogMessage(Serilog.Events.LogEventLevel.Debug, "Timer for action {type} already exists", type);
                return;
            }

            Debug.LogMessage(Serilog.Events.LogEventLevel.Debug, "Adding timer for action {type} with due time {dueTime}", type, ButtonHeartbeatInterval);

            cancelTimer = new CTimer(o =>
            {
                Debug.LogMessage(Serilog.Events.LogEventLevel.Debug, "Timer expired for {type}", type);

                action(false);

                _pushedActions.Remove(type);
            }, ButtonHeartbeatInterval);

            _pushedActions.Add(type, cancelTimer);
        }

        private static void ResetTimer(string type, Action<bool> action)
        {
            Debug.LogMessage(Serilog.Events.LogEventLevel.Debug, "Attempting to reset timer for action {type}", type);
            
            if (!_pushedActions.TryGetValue(type, out CTimer cancelTimer))
            {
                Debug.LogMessage(Serilog.Events.LogEventLevel.Debug, "Timer for action {type} not found", type);
                return;
            }

            Debug.LogMessage(Serilog.Events.LogEventLevel.Debug, "Resetting timer for action {type} with due time {dueTime}", type, ButtonHeartbeatInterval);

            cancelTimer.Reset(ButtonHeartbeatInterval);
        }

        private static void StopTimer(string type, Action<bool> action)
        {
            Debug.LogMessage(Serilog.Events.LogEventLevel.Debug, "Attempting to stop timer for action {type}", type);

            if (!_pushedActions.TryGetValue(type, out CTimer cancelTimer)) {
                Debug.LogMessage(Serilog.Events.LogEventLevel.Debug, "Timer for action {type} not found", type);
                return;
            }

            Debug.LogMessage(Serilog.Events.LogEventLevel.Debug, "Stopping timer for action {type} with due time {dueTime}", type, ButtonHeartbeatInterval);

            action(false);
            cancelTimer.Stop();
            _pushedActions.Remove(type);
        }

        public static Action<string, Action<bool>> GetPressAndHoldHandler(string value)
        {
            Debug.LogMessage(Serilog.Events.LogEventLevel.Debug, "Getting press and hold handler for {value}", value);

            if (!_pushedActionHandlers.TryGetValue(value, out Action<string, Action<bool>> handler))
            {
                Debug.LogMessage(Serilog.Events.LogEventLevel.Debug, "Press and hold handler for {value} not found", value);
                return null;
            }

            Debug.LogMessage(Serilog.Events.LogEventLevel.Debug, "Got handler for {value} not found", value);

            return handler;
        }

        public static void HandlePressAndHold(string deviceKey, JToken content, Action<bool> action)
        {
            var msg = content.ToObject<MobileControlSimpleContent<string>>();

            Debug.LogMessage(Serilog.Events.LogEventLevel.Debug, "Handling press and hold message of {type}", msg.Value);

            var timerHandler = GetPressAndHoldHandler(msg.Value);

            if (timerHandler == null)
            {
                return;
            }

            timerHandler(deviceKey, action);

            if (msg.Value.Equals("pressed", StringComparison.InvariantCultureIgnoreCase))
                action(true);
            else if (msg.Value.Equals("released", StringComparison.InvariantCultureIgnoreCase))
                action(false);
        }
    }
}
