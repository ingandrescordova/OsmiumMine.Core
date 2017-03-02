﻿using OsmiumMine.Core.Services.Security;
using System.Collections.Generic;

namespace OsmiumMine.Core.Configuration
{
    public class OMServiceState
    {
        public Dictionary<string, SecurityRuleCollection> SecurityRuleTable { get; set; } = new Dictionary<string, SecurityRuleCollection>();
    }
}