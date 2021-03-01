using System;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.AppServer
{
    public class SimpliRunDirectRouteActionJoinMap:JoinMapBaseAdvanced
    {
        public SimpliRunDirectRouteActionJoinMap(uint joinStart) : base(joinStart)
        {
        }

        public SimpliRunDirectRouteActionJoinMap(uint joinStart, Type type) : base(joinStart, type)
        {
        }
    }
}