using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharp;

using PepperDash.Core;

namespace PepperDash.Essentials.AppServer.Messengers
{
    public class PropertyCopier<TParent, TChild>
        where TParent : VideoCodecBaseStateMessage
        where TChild : ZoomRoomStateMessage
    {
        public static void Copy(TParent parent, TChild child)
        {
            var parentProperties = parent.GetCType().GetProperties();
            var childProperties = child.GetCType().GetProperties();

            foreach (var parentProperty in parentProperties)
            {
                foreach (var childProperty in childProperties)
                {
                    if (parentProperty.Name == childProperty.Name && parentProperty.PropertyType == childProperty.PropertyType && parentProperty.CanWrite)
                    {
                        childProperty.SetValue(child, parentProperty.GetValue(parent, null), null);
                        //Debug.Console(2, "Copying property: {0} value: {1}", parentProperty.Name, parentProperty.GetValue(parent, null));
                        break;
                    }
                }
            }
        }
    }
}