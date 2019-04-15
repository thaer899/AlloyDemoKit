using AlloyDemoKit.Business.Data;
using AlloyDemoKit.Business.Rendering;
using EPiServer.Cms.TinyMce.Core;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using EPiServer.ServiceLocation;
using EPiServer.Web.Mvc;
using EPiServer.Web.Mvc.Html;
using System;
using System.Web.Mvc;

namespace AlloyDemoKit.Business.Initialization
{
    [InitializableModule]
    public class DependencyResolverInitialization : IConfigurableModule
    {
        public void ConfigureContainer(ServiceConfigurationContext context)
        {

            //Implementations for custom interfaces can be registered here.

            context.ConfigurationComplete += (o, e) =>
            {
                //Register custom implementations that should be used in favour of the default implementations
                context.Services.AddTransient<IContentRenderer, ErrorHandlingContentRenderer>()
                    .AddTransient<ContentAreaRenderer, AlloyContentAreaRenderer>()
                    .AddTransient<IFileDataImporter, FileDataImporter>();

                // Init
                context.Services.Configure<TinyMceConfiguration>(config =>
                {
                    config.Default()
                        .AddExternalPlugin("epi-tinymce-ai-plugin", "/AI/epi-tinymce-ai-plugin/dist/epi-tinymce-ai-plugin/plugin.js")
                        .AppendToolbar("epi-tinymce-ai-plugin");
                });
            };
        }

        public void Initialize(InitializationEngine context)
        {
            DependencyResolver.SetResolver(new ServiceLocatorDependencyResolver(context.Locate.Advanced));
        }

        public void Uninitialize(InitializationEngine context)
        {
        }

        public void Preload(string[] parameters)
        {
        }
    }
}
