﻿using Nancy;
using Nancy.Authentication.Stateless;
using Nancy.Bootstrapper;
using Nancy.TinyIoc;
using OsmiumMine.Core.Server.Configuration;
using OsmiumMine.Core.Server.Services.Authentication;

namespace OsmiumMine.Core.Server
{
    public class OMCoreServerBootstrapper : DefaultNancyBootstrapper
    {
        public OMServerContext OMServerContext { get; set; }

        public OMCoreServerBootstrapper(OMServerContext serverContext)
        {
            OMServerContext = serverContext;
        }

        protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines)
        {
            OMServerContext.ConnectOsmiumMine();
            
            // Enable authentication
            StatelessAuthentication.Enable(pipelines, new StatelessAuthenticationConfiguration(ctx =>
            {
                // Take API from query string
                var apiKey = (string)ctx.Request.Query.apikey.Value;

                // get user identity
                var authenticator = new ClientAuthenticationService(OMServerContext);
                return authenticator.ResolveClientIdentity(apiKey);
            }));

            // Enable CORS
            pipelines.AfterRequest.AddItemToEndOfPipeline((ctx) =>
            {
                foreach (var origin in OMServerContext.Parameters.CorsOrigins)
                {
                    ctx.Response.WithHeader("Access-Control-Allow-Origin", origin);
                }
                ctx.Response
                    .WithHeader("Access-Control-Allow-Methods", "GET,POST,PUT,DELETE")
                    .WithHeader("Access-Control-Allow-Headers", "Accept, Origin, Content-type");
            });

            base.ApplicationStartup(container, pipelines);
        }

        protected override void ConfigureApplicationContainer(TinyIoCContainer container)
        {
            base.ConfigureApplicationContainer(container);

            container.Register<IOMServerContext>(OMServerContext);
        }
    }
}