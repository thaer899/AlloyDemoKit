using EPiServer.Construction;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.DataAnnotations;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using System;
using System.Collections.Specialized;

namespace AlloyDemoKit.Business
{
    public class MappedContentProvider : ContentProvider
    {
        private readonly IdentityMappingService _identityMappingService;
        private readonly IContentTypeRepository _contentTypeRepository;
        private readonly IContentFactory _contentFactory;

        public const string Key = "mapped-content-provider";
        public override string ProviderKey => Key;

        public MappedContentProvider(
            IdentityMappingService identityMappingService,
            IContentTypeRepository contentTypeRepository,
            IContentFactory contentFactory)
        {
            _identityMappingService = identityMappingService;
            _contentTypeRepository = contentTypeRepository;
            _contentFactory = contentFactory;
        }

        protected override IContent LoadContent(ContentReference contentLink, ILanguageSelector languageSelector)
        {
            var mappedIdentity = _identityMappingService.Get(contentLink);
            var compositeId = mappedIdentity.ExternalIdentifier.Segments[1];
            var type = _contentTypeRepository.Load(typeof(TestExportData));
            var testExportData = _contentFactory.CreateContent(type) as TestExportData;
            testExportData.ContentTypeID = type.ID;
            testExportData.ContentLink = mappedIdentity.ContentLink;
            testExportData.ContentGuid = mappedIdentity.ContentGuid;
            testExportData.Status = VersionStatus.Published;
            testExportData.IsPendingPublish = false;
            testExportData.StartPublish = DateTime.Now.Subtract(TimeSpan.FromDays(1));
            testExportData.Name =
                testExportData.FullName = GetName(compositeId);
            testExportData.MakeReadOnly();
            return testExportData;
        }

        private string GetName(string compositeId)
        {
            return $"Hi, my name is: {compositeId}";
        }
    }

    [ContentType(
        DisplayName = "Test export data",
        GUID = "{BD00D891-217E-4F67-B7A3-ACABA393085B}",
        Description = "")]
    public class TestExportData : ContentBase
    {
        public virtual string FullName { get; set; }
    }

    [ModuleDependency(typeof(EPiServer.Web.InitializationModule))]
    public class MappedContentInitialization : IInitializableModule
    {
        /// <summary>
        /// Initialize content provider
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(InitializationEngine context)
        {
            var mappedContentProvider = context.Locate.Advanced.GetInstance<MappedContentProvider>();
            var providerManager = context.Locate.Advanced.GetInstance<IContentProviderManager>();

            mappedContentProvider.Initialize(MappedContentProvider.Key, new NameValueCollection());
            providerManager.ProviderMap.AddProvider(mappedContentProvider);
        }

        /// <summary>
        /// Remove content provider
        ///  
        /// </summary>
        /// <param name="context"></param>
        public void Uninitialize(InitializationEngine context)
        {
            var providerManager = context.Locate.Advanced.GetInstance<IContentProviderManager>();

            providerManager.ProviderMap.RemoveProvider(MappedContentProvider.Key);
        }
    }
}