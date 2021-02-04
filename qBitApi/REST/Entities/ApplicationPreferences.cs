using System;
using System.Collections.Generic;
using System.Text;
using Model = qBitApi.API.AppPreferences;

namespace qBitApi.REST.Entities
{
    public class ApplicationPreferences
    {

        internal ApplicationPreferences()
        {
        }

        internal static ApplicationPreferences Create(Model model)
        {
            return new ApplicationPreferences();
        }
    }
}
