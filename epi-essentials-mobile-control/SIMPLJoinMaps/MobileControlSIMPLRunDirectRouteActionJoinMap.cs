using System;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.AppServer
{
    public class MobileControlSIMPLRunDirectRouteActionJoinMap:JoinMapBaseAdvanced
    {
        [JoinName("SetAdvancedSharingMode")]
        public JoinDataComplete SetAdvancedSharingMode =
            new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Use Advanced Sharing Mode",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("SetSimpleSharingMode")]
        public JoinDataComplete SetSimpleSharingMode =
            new JoinDataComplete(new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Use Simple Sharing Mode",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        

        [JoinName("ShowDestination")]
        public JoinDataComplete SourceForDestination =
            new JoinDataComplete(new JoinData { JoinNumber = 51, JoinSpan = 10 },
            new JoinMetadata
            {
                Description = "Source to Route to Destination",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        public MobileControlSIMPLRunDirectRouteActionJoinMap(uint joinStart)
            : base(joinStart, typeof(MobileControlSIMPLRunDirectRouteActionJoinMap))
        {
        }
    }
}