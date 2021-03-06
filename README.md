
# OsmiumMine.Core

AlphaOsmium Data Store Component

[![Build Status](https://travis-ci.org/0xFireball/OsmiumMine.Core.svg?branch=master)](https://travis-ci.org/0xFireball/OsmiumMine.Core)

## Information

The core component of AlphaOsmium Database, OsmiumMine is a Redis/SSDB powered schemaless data store. The provider
allows for multiple "domains," each of which is its own independent data context.

`OsmiumMine.Core` is a portable set of projects that compose AlphaOsmium Database. However,
this project has been specially separated to allow running the core server, `AlphaOsmium.Core.Server`
as a standalone application without AlphaOsmium.

## Documentation

See the `docs/` directory for REST API documentation. Language client API documentation will be available separately.

## Key Features

- Realtime:
  - High-performance cloud hosted NoSQL JSON trees
    - Backed by Redis, the extremely fast yet standard underlying data storage layer
  - Updates are synchronized in milliseconds among all connected devices (*)
    - Client API to stream updates in realtime
    - Local offline cache of data and transactions
- Schemaless
  - Data can be structured in any way inside the database
  - Full support for dynamic data storage
- Security
  - First-class API key support
    - API keys with limited permissions can be created and revoked
  - Security Rules for limiting operations in specified paths
    - Make a section read-only or write-only
    - JS Expressions for dynamic data validation (**)
- Portable and Accessible
  - Built on .NET Core, so easily runs on any platform
  - Fully-featured REST API (see the documentation)
  - Web UI (*)

\* - Only available in AlphaOsmium Edition

\** - Currently only available in AlphaOsmium Edition, but will be ported back to this edition

## Quick Overview

OsmiumMine provides a remotely syncing JSON tree. The documentation contains
details and specifics on consuming the API from an application.

The database is accessed through REST API calls, and authentication and security
are enforced.

### A sample of storing and modifying data

For example, you could store this data in the database:

```JSON
{
    "internet": {
        "year": 2017,
        "map": {
            "foo" : {
                "name": "cookie1",
                "description": "i like pie"
            },
            "bar" : {
                "name": "LOLCODE"
            },
            "baz" : {
                "name": "CHEEZBURGER"
            }
        },
        "websites": [
            {
                "name": "GitHub",
                "url": "https://github.com/"
            },
            {
                "name": "Google",
                "url": "https://google.com/"
            },
            {
                "name": "IridiumIon",
                "url": "https://iridiumion.xyz/"
            }
        ]
    }
}
```

Then, querying `/internet/map/foo.json` would give you the JSON
value of `foo`. Querying `/internet/websites` would give the `websites` array.

After sending an UPDATE request to `/internet/map/foo` with the following data:

```json
{
    "name": "cookieeater123",
    "level": 9001
}
```

Querying `/internet/map/foo.json` would give you the following data:

```json
{
    "name": "cookieeater123",
    "description": "i like pie",
    "level": 9001
}
```

UPDATE merges the JSON objects. There are other ways to add data, including
PUT, which overwrites existing data, and PUSH, which appends the data to a time-indexed
map structure.

OsmiumMine's database feature provides a schemaless dynamic database to store
any kind of data and allows convenient querying and modification.

#### Security rules

If your application requires data in a database
that needs to be write-only, so end users' clients
can write data but the users cannot view other users' data,
a simple security rule can be added that only allows WRITE
access, or more specifically, only allows UPDATE access.

This will allow you to ensure that data integrity is remained,
and allowing only UPDATE access will ensure that no data is overwritten
by a misbehaving client.

See the [REST API documentation](docs/rest_api.md) for detailed information on OsmiumMine's remote database and security rules
and remote API access.

### Planned Features

- Optimization/caching to further improve query performance. Most likely this will use some form of indexing to reduce reads in a property query

### Extensions

OsmiumMine extensions are available in the AlphaOsmium edition, but not in the standalone edition.

AlphaOsmium DbSync extends OsmiumMine by providing a real-time, event-driven data synchronization API, with support for both online and offline transactions.

- Evented data streaming
- Offline caching and synchronization

## License

Copyright &copy; 2016-2017 Nihal Talur (0xFireball), IridiumIon Software. All Rights Reserved.

Licensed under the AGPLv3.