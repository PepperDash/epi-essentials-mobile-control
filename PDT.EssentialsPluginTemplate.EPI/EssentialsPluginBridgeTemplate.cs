using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;

using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Bridges;
using Crestron.SimplSharp.Reflection;
using Newtonsoft.Json;

namespace EssentialsPluginTemplateEPI
{
	public static class EssentialsPluginBridgeTemplate
	{
		public static void LinkToApiExt(this EssentialsPluginDeviceTemplate device, BasicTriList trilist, uint joinStart, string joinMapKey, EiscApi bridge)
		{
			EssentialsPluginBridgeJoinMapTemplate joinMap = new EssentialsPluginBridgeJoinMapTemplate(joinStart);

            // This adds the join map to the collection on the bridge
            bridge.AddJoinMap(device.Key, joinMap);

            var customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);

            if (customJoins != null)
            {
                joinMap.SetCustomJoinData(customJoins);
            }

            Debug.Console(1, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
            Debug.Console(0, "Linking to Bridge Type {0}", device.GetType().Name.ToString());


            trilist.OnlineStatusChange += new Crestron.SimplSharpPro.OnlineStatusChangeEventHandler((o, a) =>
            {
                if (a.DeviceOnLine)
                {
                    trilist.SetString(joinMap.DeviceName.JoinNumber, device.Name);
                }
            });
		}
	}


	public class EssentialsPluginBridgeJoinMapTemplate : JoinMapBaseAdvanced
	{
        public JoinDataComplete DeviceName = new JoinDataComplete(new JoinData() { JoinNumber = 1, JoinSpan = 1 }, new JoinMetadata() { Label = "Device Name", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });

		public EssentialsPluginBridgeJoinMapTemplate(uint joinStart) 
            :base(joinStart)
		{
		}

	}
}