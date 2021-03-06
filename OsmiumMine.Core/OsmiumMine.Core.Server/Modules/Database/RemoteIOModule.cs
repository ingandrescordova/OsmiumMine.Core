﻿using System.IO;
using System.Threading.Tasks;
using Nancy;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OsmiumMine.Core.Server.Configuration;
using OsmiumMine.Core.Server.Configuration.Access;
using OsmiumMine.Core.Server.Utilities;
using OsmiumMine.Core.Services.DynDatabase;
using OsmiumMine.Core.Services.DynDatabase.Access;
using OsmiumSubstrate.Services.Authentication;
using OsmiumSubstrate.Services.Authentication.Security;
using static OsmiumMine.Core.Services.DynDatabase.Access.DynDatabaseRequest;
using static OsmiumMine.Core.Services.DynDatabase.DynamicDatabaseService;

namespace OsmiumMine.Core.Server.Modules.Database
{
    public class RemoteIOModule : OsmiumMineServerModule
    {
        public RemoteIOModule(IOMServerContext serverContext) : base("/io")
        {
            ServerContext = serverContext;

            var pathRoutes = new[] {@"^(?:(?<dbid>[\w]+)\/(?<path>.*?)(?=\.json))"};
            foreach (var pathRoute in pathRoutes)
            {
                Put(pathRoute, HandlePutDataAsync);
                Patch(pathRoute, HandlePatchDataAsync);
                Post(pathRoute, HandlePostDataAsync);
                Delete(pathRoute, HandleDeleteDataAsync);
                Get(pathRoute, HandleGetDataAsync);
            }

            // Do some initial setup
            Before += ctx => { return null; };
            // Check requested response type argument
            // Refer to https://firebase.google.com/docs/database/rest/retrieve-data
            After += ctx =>
            {
                var queryData = ctx.Request.Query;
                var printArgument = (string) queryData.print;
                switch (printArgument)
                {
                    case "silent": // User does not want a response.
                        ctx.Response = null;
                        ctx.Response = HttpStatusCode.NoContent;
                        break;

                    default:
                        break;
                }
            };
        }

        public IOMServerContext ServerContext { get; set; }

        private async Task<Response> HandlePutDataAsync(dynamic args)
        {
            var dbRequest = (DynDatabaseRequest) CreateDatabaseRequest(args, DatabaseAction.Put);
            if (dbRequest.State == PermissionState.Denied) return HttpStatusCode.Unauthorized;
            if (!dbRequest.Valid) return HttpStatusCode.BadRequest;
            var path = dbRequest.Path;
            // Deserialize data bundle
            JObject dataBundle;
            try
            {
                using (var sr = new StreamReader(Request.Body))
                {
                    var rawJsonData = await sr.ReadToEndAsync();
                    dataBundle = JObject.Parse(rawJsonData);
                }
            }
            catch (JsonSerializationException)
            {
                return HttpStatusCode.BadRequest;
            }

            // Write data
            var dynDbService = new DynamicDatabaseService(ServerContext.KeyValueDbService);
            var placeResult = await dynDbService.PlaceData(dataBundle, dbRequest, NodeDataOvewriteMode.Put);

            // Return data written
            return Response.FromJsonString(placeResult);
        }

        private async Task<Response> HandlePatchDataAsync(dynamic args)
        {
            var dbRequest = (DynDatabaseRequest) CreateDatabaseRequest(args, DatabaseAction.Update);
            if (dbRequest.State == PermissionState.Denied) return HttpStatusCode.Unauthorized;
            if (!dbRequest.Valid) return HttpStatusCode.BadRequest;
            // Deserialize data bundle
            JObject dataBundle;
            try
            {
                using (var sr = new StreamReader(Request.Body))
                {
                    var rawJsonData = await sr.ReadToEndAsync();
                    dataBundle = JObject.Parse(rawJsonData);
                }
            }
            catch (JsonSerializationException)
            {
                return HttpStatusCode.BadRequest;
            }

            // Write data
            var dynDbService = new DynamicDatabaseService(ServerContext.KeyValueDbService);
            var placeResult = await dynDbService.PlaceData(dataBundle, dbRequest, NodeDataOvewriteMode.Update);

            // Return data written
            return Response.FromJsonString(placeResult);
        }

        private async Task<Response> HandlePostDataAsync(dynamic args)
        {
            var dbRequest = (DynDatabaseRequest) CreateDatabaseRequest(args, DatabaseAction.Push);
            if (dbRequest.State == PermissionState.Denied) return HttpStatusCode.Unauthorized;
            if (!dbRequest.Valid) return HttpStatusCode.BadRequest;
            // Deserialize data bundle
            JObject dataBundle;
            try
            {
                using (var sr = new StreamReader(Request.Body))
                {
                    var rawJsonData = await sr.ReadToEndAsync();
                    dataBundle = JObject.Parse(rawJsonData);
                }
            }
            catch (JsonSerializationException)
            {
                return HttpStatusCode.BadRequest;
            }

            // Write data
            var dynDbService = new DynamicDatabaseService(ServerContext.KeyValueDbService);
            var pushResult = await dynDbService.PlaceData(dataBundle, dbRequest, NodeDataOvewriteMode.Push);

            // Return push result bundle
            return Response.FromJsonString(pushResult);
        }

        private async Task<Response> HandleDeleteDataAsync(dynamic args)
        {
            var dbRequest = (DynDatabaseRequest) CreateDatabaseRequest(args, DatabaseAction.Delete);
            if (dbRequest.State == PermissionState.Denied) return HttpStatusCode.Unauthorized;
            if (!dbRequest.Valid) return HttpStatusCode.BadRequest;
            var dynDbService = new DynamicDatabaseService(ServerContext.KeyValueDbService);
            await dynDbService.DeleteData(dbRequest);

            return Response.FromJsonString(new JObject().ToString());
        }

        private async Task<Response> HandleGetDataAsync(dynamic args)
        {
            var dbRequest = (DynDatabaseRequest) CreateDatabaseRequest(args, DatabaseAction.Retrieve);
            if (dbRequest.State == PermissionState.Denied) return HttpStatusCode.Unauthorized;
            if (!dbRequest.Valid) return HttpStatusCode.BadRequest;
            // Read query parameters
            var shallow = (string) Request.Query.shallow == "1";
            var dynDbService = new DynamicDatabaseService(ServerContext.KeyValueDbService);
            var dataBundle = await dynDbService.GetData(dbRequest, shallow);

            if (dataBundle == null)
                return Response.FromJsonString("{}");

            return Response.FromJsonString(dataBundle.ToString());
        }

        private DynDatabaseRequest CreateDatabaseRequest(dynamic args, DatabaseAction action)
        {
            var requestProcessor = CreateRequestProcessor(ServerContext);
            var dbRequest = (DynDatabaseRequest) requestProcessor.Process(args, Request.Query, action);
            return dbRequest;
        }

        public static RequestProcessor CreateRequestProcessor(IOMServerContext serverContext)
        {
            var processor = new RequestProcessor(serverContext.OMContext);
            processor.AuthTokenValidator = accessRequest =>
            {
                // get key identity
                var authenticator = new StatelessAuthenticationService<OMAccessKey, OMApiAccessScope>(serverContext);
                var identity = authenticator.ResolveClientIdentity(accessRequest.AuthToken);
                if (identity == null) return false;
                var accessKey = authenticator.ResolveKey(accessRequest.AuthToken);
                // make sure realm is allowed
                if (!accessKey.AllowedRealms.Contains(accessRequest.DatabaseId)) return false;
                // check more rules
                if (processor.ValidateAdditionalRules(accessRequest, accessKey.SecurityRules).Granted) return true;
                // only check admin
                var accessValidator = new StatelessClientValidator<OMAccessKey, OMApiAccessScope>();
                if (identity.EnsureClaim(accessValidator.GetAccessClaim(OMApiAccessScope.Admin)))
                    return true;

                return false;
            };

            return processor;
        }
    }
}