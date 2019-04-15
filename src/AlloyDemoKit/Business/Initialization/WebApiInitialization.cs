using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using EPiServer.ServiceLocation;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Linq;
using System.Web.Http;

namespace AlloyDemoKit.Business.Initialization
{
    [InitializableModule]
    public class WebApiInitialization : IConfigurableModule
    {
        public void Initialize(InitializationEngine context)
        {
            var cfg = GlobalConfiguration.Configuration;
            cfg.Formatters.JsonFormatter.SerializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include,
                Formatting = Formatting.Indented,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
            };

            var appXmlType = cfg.Formatters.XmlFormatter.SupportedMediaTypes.FirstOrDefault(t => t.MediaType == "application/xml");
            cfg.Formatters.XmlFormatter.SupportedMediaTypes.Remove(appXmlType);
        }

        public void ConfigureContainer(ServiceConfigurationContext context)
        {
            GlobalConfiguration.Configure(config =>
            {
                config.MapHttpAttributeRoutes();
            });
        }

        public void Uninitialize(InitializationEngine context)
        {

        }
    }
}