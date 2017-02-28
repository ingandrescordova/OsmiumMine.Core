﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OsmiumMine.Core.Services.DynDatabase
{
    public static class ServerValueProvider
    {
        public static string ServerValueKeyName => ".sv";

        public static JValue GetValue(string serverValueName)
        {
            switch (serverValueName)
            {
                case "timestamp":
                    return new JValue(DateTimeOffset.Now.ToUnixTimeSeconds());
                default:
                    {
                        return JValue.CreateNull();
                    }
            }
        }
    }
}
