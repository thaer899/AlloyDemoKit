using EPiServer.Construction;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.DataAnnotations;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using System;
using System.Collections.Specialized;
using System.Linq;
using EPiServer;
using EPiServer.Configuration;
using EPiServer.Security;

namespace AlloyDemoKit.Business
{
    public class MappedContentProvider : ContentProvider
    {
        private readonly IdentityMappingService _identityMappingService;
        private readonly IContentTypeRepository _contentTypeRepository;
        private readonly IContentFactory _contentFactory;
        private readonly IContentLoader _contentLoader;

        public const string Key = "mapped-content-provider";
        public override string ProviderKey => Key;

        public MappedContentProvider(
            IdentityMappingService identityMappingService,
            IContentTypeRepository contentTypeRepository,
            IContentFactory contentFactory,
            IContentLoader contentLoader)
        {
            _identityMappingService = identityMappingService;
            _contentTypeRepository = contentTypeRepository;
            _contentFactory = contentFactory;
            _contentLoader = contentLoader;
        }

        protected override IContent LoadContent(ContentReference contentLink, ILanguageSelector languageSelector)
        {
            var mappedIdentity = _identityMappingService.Get(contentLink);
            var compositeId = mappedIdentity.ExternalIdentifier.Segments[1];
            var type = _contentTypeRepository.Load(typeof(TestExportData));
            var testExportData = _contentFactory.CreateContent(type, new BuildingContext(type)
            {
                Parent = _contentLoader.Get<ContentFolder>(EntryPoint)
            }) as TestExportData;

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
        private ContentRootService _contentRootService;
        private IContentSecurityRepository _contentSecurityRepository;

        private const string RootName = "Mapped content root";
        private static readonly Guid RootGuid = new Guid("{DBAAAB73-39CA-4F02-A805-9A332EAD6376}");
        public static ContentReference Root;

        /// <summary>
        /// Initialize content provider
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(InitializationEngine context)
        {
            _contentRootService = context.Locate.Advanced.GetInstance<ContentRootService>();
            _contentSecurityRepository = context.Locate.Advanced.GetInstance<IContentSecurityRepository>();

            Root = CreateRootFolder(RootName, RootGuid);

            var providerValues = new NameValueCollection
            {
                { ContentProviderElement.EntryPointString, Root.ToString() }
            };

            var mappedContentProvider = context.Locate.Advanced.GetInstance<MappedContentProvider>();
            var providerManager = context.Locate.Advanced.GetInstance<IContentProviderManager>();

            mappedContentProvider.Initialize(MappedContentProvider.Key, providerValues);
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

        private ContentReference CreateRootFolder(string rootName, Guid rootGuid)
        {
            _contentRootService.Register<ContentFolder>(rootName, rootGuid, ContentReference.RootPage);

            var fieldRoot = _contentRootService.Get(rootName);
            if (!(_contentSecurityRepository.Get(fieldRoot).CreateWritableClone() is IContentSecurityDescriptor
                securityDescriptor))
            {
                return fieldRoot;
            }
            securityDescriptor.IsInherited = false;

            var everyoneEntry = securityDescriptor.Entries.FirstOrDefault(e =>
                e.Name.Equals("everyone", StringComparison.InvariantCultureIgnoreCase));
            if (everyoneEntry == null)
            {
                return fieldRoot;
            }

            securityDescriptor.RemoveEntry(everyoneEntry);
            _contentSecurityRepository.Save(fieldRoot, securityDescriptor, SecuritySaveType.Replace);
            return fieldRoot;
        }

    }
}