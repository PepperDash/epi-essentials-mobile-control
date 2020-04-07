using System;
using Crestron.SimplSharp;                          				// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       				// For Basic SIMPL#Pro classes

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PepperDash.Essentials;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Core;

namespace EssentialsPluginTemplateEPI 
{
	public class EssentialsPluginDeviceTemplate : EssentialsDevice
	{

		public EssentialsPluginDeviceTemplate(string key, string name, EssentialsPluginConfigObjectTemplate config)
			: base(key, name)
		{
            Debug.Console(0, this, "Constructing new EssentialsPluginDeviceTemplate instance");
		}
	}
}

