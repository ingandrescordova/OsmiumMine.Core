﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nancy;
using OsmiumMine.Core.Server.Configuration;
using OsmiumMine.Core.Server.Configuration.Access;
using OsmiumMine.Core.Server.Utilities;
using OsmiumMine.Core.Services.DynDatabase.Access;
using OsmiumMine.Core.Services.Security;
using OsmiumMine.Core.Utilities;
using OsmiumSubstrate.Services.Authentication;
using OsmiumSubstrate.Services.Authentication.Security;

namespace OsmiumMine.Core.Server.Modules.Management
{
    public class RemoteSecurityModule : OsmiumMineServerModule
    {
        public RemoteSecurityModule(IOMServerContext serverContext) : base("/rsec")
        {
            ServerContext = serverContext;

            var accessValidator = new StatelessClientValidator<OMAccessKey, OMApiAccessScope>();
            this.RequiresAllClaims(new[] {accessValidator.GetAccessClaim(OMApiAccessScope.Admin)});

            // Rule management (these can also manage keys)
            Post("/rules/create/{dbid?}", HandleCreateRuleRequestAsync);
            Delete("/rules/clear/{dbid?}", HandleClearRulesRequestAsync);
            Delete("/rules/delete/{dbid?}", HandleDeleteRuleRequestAsync);
            Get("/rules/list/{dbid?}", HandleGetRuleListRequestAsync);
            Get("/rules/get/{dbid?}", HandleGetRuleByIdRequestAsync);

            // API key management
            Post("/keys/create/{keyid}", HandleCreateKeyRequestAsync);
            Get("/keys/get/{keyid}", HandleGetKeyRequestAsync);
            Delete("/keys/delete/{keyid}", HandleDeleteKeyRequestAsync);

            // Persist state after successful request
            After += ctx =>
            {
                if (ctx.Response.StatusCode == HttpStatusCode.OK)
                    ServerContext.ServerState.Persist();
            };
        }

        public IOMServerContext ServerContext { get; set; }

        private async Task<Response> HandleCreateKeyRequestAsync(dynamic args)
        {
            // Parameters:
            var keyid = (string) args.keyid;
            var realmsParam = (string) Request.Query.realms;
            if (realmsParam == null) return HttpStatusCode.BadRequest;
            var realms = realmsParam.Split('|');
            return await Task.Run(() =>
            {
                var key = new OMAccessKey
                {
                    AllowedRealms = realms.ToList(),
                    Key = keyid
                };
                // Store key
                lock (ServerContext.ServerState.ApiKeys)
                {
                    ServerContext.ServerState.ApiKeys.Add(key);
                }
                return Response.AsJsonNet(key);
            });
        }

        private async Task<Response> HandleGetKeyRequestAsync(dynamic args)
        {
            return await Task.Run(() =>
            {
                var keyid = (string) args.keyid;
                var key = ServerContext.ServerState.ApiKeys.FirstOrDefault(x => x.Key == keyid);
                if (key == null) return HttpStatusCode.NotFound;
                return Response.AsJsonNet(key);
            });
        }

        private async Task<Response> HandleDeleteKeyRequestAsync(dynamic args)
        {
            return await Task.Run(() =>
            {
                var keyid = (string) args.keyid;
                var key = ServerContext.ServerState.ApiKeys.FirstOrDefault(x => x.Key == keyid);
                if (key == null) return HttpStatusCode.NotFound;
                lock (ServerContext.ServerState.ApiKeys)
                {
                    ServerContext.ServerState.ApiKeys.Remove(key);
                }
                return HttpStatusCode.OK;
            });
        }

        private static (string, string, string) ParseRulesRequestArgs(dynamic args, Request request,
            bool parsePathPattern = true)
        {
            var dbid = (string) args.dbid;
            var path = (string) request.Query.path;
            var keyid = (string) request.Query.keyid;
            var wc = (string) request.Query.wc == "1"; // whether to parse as a wildcard
            if (string.IsNullOrWhiteSpace(dbid)) dbid = null;
            if (string.IsNullOrWhiteSpace(path)) path = parsePathPattern ? WildcardMatcher.ToRegex("/*") : "/";
            if (wc)
            {
                // convert wildcard to regex
                path = WildcardMatcher.ToRegex(path);
            }
            else
            {
                if (parsePathPattern)
                    try
                    {
                        Regex.IsMatch("", path);
                    }
                    catch
                    {
                        // Path regex was invalid, parse as wildcard
                        path = WildcardMatcher.ToRegex(path);
                    }
            }
            return (dbid, path, keyid);
        }

        private async Task<Response> HandleClearRulesRequestAsync(dynamic args)
        {
            return await Task.Run(() =>
            {
                (string dbid, string path, string keyid) =
                    ParseRulesRequestArgs((DynamicDictionary) args, Request, false);
                var ruleList = GetCurrentRuleList(dbid, keyid);
                if (ruleList == null) return HttpStatusCode.BadRequest;
                var dbRules = ruleList;
                // remove all rules that match the given path
                ruleList.RemoveAll(x => x.PathRegex.IsMatch(path));
                return HttpStatusCode.OK;
            });
        }

        private async Task<Response> HandleDeleteRuleRequestAsync(dynamic args)
        {
            return await Task.Run(() =>
            {
                // Path is not necessary for this operation
                (string dbid, string path, string keyid) =
                    ParseRulesRequestArgs((DynamicDictionary) args, Request, false);
                var ruleList = GetCurrentRuleList(dbid, keyid);
                if (ruleList == null) return HttpStatusCode.BadRequest;
                var ruleId = (string) Request.Query.id;
                if (string.IsNullOrWhiteSpace(ruleId)) return HttpStatusCode.BadRequest;
                var dbRules = ruleList;
                // remove all rules that match the given path
                var pLen = dbRules.Count;
                dbRules.RemoveAll(x => x.Id == ruleId);
                var removed = pLen > dbRules.Count;
                return removed ? HttpStatusCode.OK : HttpStatusCode.NotFound;
            });
        }

        private async Task<Response> HandleGetRuleByIdRequestAsync(dynamic args)
        {
            return await Task.Run(() =>
            {
                // Path is not necessary for this operation
                (string dbid, string path, string keyid) =
                    ParseRulesRequestArgs((DynamicDictionary) args, Request, false);
                var ruleList = GetCurrentRuleList(dbid, keyid);
                if (ruleList == null) return HttpStatusCode.BadRequest;
                var ruleId = (string) Request.Query.id;
                if (string.IsNullOrWhiteSpace(ruleId)) return HttpStatusCode.BadRequest;
                var dbRules = ruleList;
                var foundRule = dbRules.FirstOrDefault(x => x.Id == ruleId);
                return foundRule != null ? Response.AsJsonNet(foundRule) : HttpStatusCode.NotFound;
            });
        }

        private async Task<Response> HandleGetRuleListRequestAsync(dynamic args)
        {
            return await Task.Run(() =>
            {
                (string dbid, string path, string keyid) =
                    ParseRulesRequestArgs((DynamicDictionary) args, Request, false);
                var ruleList = GetCurrentRuleList(dbid, keyid);
                if (ruleList == null) return HttpStatusCode.BadRequest;

                var matchingRules = ruleList.Where(x => x.PathRegex.IsMatch(path));
                return Response.AsJsonNet(matchingRules);
            });
        }

        private async Task<Response> HandleCreateRuleRequestAsync(dynamic args)
        {
            return await Task.Run(() =>
            {
                (string dbid, string path, string keyid) = ParseRulesRequestArgs((DynamicDictionary) args, Request);
                var ruleList = GetCurrentRuleList(dbid, keyid);
                if (ruleList == null) return HttpStatusCode.BadRequest;
                var allowRule = (string) Request.Query.allow == "1"; // whether the rule should set allow to true
                var priority = -1;
                int.TryParse((string) Request.Query.priority, out priority);
                DatabaseAction ruleFlags = 0;
                try
                {
                    var ruleTypes = ((string) Request.Query.type).Split('|')
                        .Select(x => (DatabaseAction) Enum.Parse(typeof(DatabaseAction), x));
                    foreach (var ruleType in ruleTypes)
                        ruleFlags |= ruleType;
                }
                catch
                {
                    return HttpStatusCode.BadRequest;
                }
                var rule = new SecurityRule(new Regex(path), ruleFlags, allowRule, priority);
                ruleList.Add(rule);
                return Response.AsJsonNet(rule);
            });
        }

        private List<SecurityRule> GetCurrentRuleList(string dbid, string keyid)
        {
            List<SecurityRule> ruleList = null;
            if (dbid != null)
            {
                // Get database rules
                if (!ServerContext.OMContext.DbServiceState.SecurityRuleTable.ContainsKey(dbid))
                    ServerContext.OMContext.DbServiceState.SecurityRuleTable.Add(dbid, new List<SecurityRule>());
                ruleList = ServerContext.OMContext.DbServiceState.SecurityRuleTable[dbid];
            }
            else if (keyid != null)
            {
                // Get key rules
                var authenticator = new StatelessAuthenticationService<OMAccessKey, OMApiAccessScope>(ServerContext);
                var identity = authenticator.ResolveClientIdentity(keyid);
                if (identity == null) return null;
                var accessKey = authenticator.ResolveKey(keyid);
                ruleList = accessKey.SecurityRules;
            }
            return ruleList;
        }
    }
}