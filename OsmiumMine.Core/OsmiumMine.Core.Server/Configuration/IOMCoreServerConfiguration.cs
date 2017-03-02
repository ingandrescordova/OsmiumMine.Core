﻿using OsmiumMine.Core.Services.Database;

namespace OsmiumMine.Core.Server.Configuration
{
    public interface IOMCoreServerConfiguration
    {
        OMServerParameters Parameters { get; set; }
        OsmiumMineContext OMContext { get; set; }
        KeyValueDatabaseService KeyValueDbService { get; set; }
    }
}