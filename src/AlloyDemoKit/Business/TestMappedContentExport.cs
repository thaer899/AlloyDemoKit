using EPiServer.Construction;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.DataAnnotations;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Security;
using System.Xml;
using System.Xml.Linq;
using EPiServer;
using EPiServer.Configuration;
using EPiServer.Core.Internal;
using EPiServer.Core.Transfer;
using EPiServer.Data;
using EPiServer.Data.Dynamic;
using EPiServer.DataAccess;
using EPiServer.Enterprise;
using EPiServer.Enterprise.Transfer;
using EPiServer.Security;
using EPiServer.ServiceLocation;
using EPiServer.Web;

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

        protected ContentResolveResult ResolveContent(MappedIdentity identity)
        {
            if (identity == null)
            {
                return null;
            }

            var data = LoadContent(identity.ContentLink, new NullLanguageSelector());
            if (data == null)
            {
                return null;
            }

            return new ContentResolveResult
            {
                ContentLink = identity.ContentLink,
                UniqueID = identity.ContentGuid,
                ContentUri =
                    this.ConstructContentUri(data.ContentTypeID, identity.ContentLink, identity.ContentGuid)
            };
        }

        public override ContentReference Save(IContent content, SaveAction action)
        {
            if (!(content is TestExportData testExportData))
            {
                throw new NotImplementedException();
            }

            // We need to support Save to support import
            // We only have content.contentGuid here?
            // PROBLEM: the content does not contain any information to construct the external identifier

            // This is what we need to do:
            var externalIdentifier = MappedIdentity.ConstructExternalIdentifier(
                MappedContentProvider.Key,
                "brianweet"
            );

            // Create a content link somehow?
            // WRONG?: mappedIdentity.contentGuid is randomly generated
            var mappedIdentity = _identityMappingService.Get(externalIdentifier, true);
            content.ContentLink = mappedIdentity.ContentLink;

            // Recreate mapped identity now we have a content link
            // Necessary to maintain correct contentGuid
            // HACKY: delete and map to correct contentGuid
            _identityMappingService.Delete(externalIdentifier);
            _identityMappingService.MapContent(externalIdentifier, content);

            return mappedIdentity.ContentLink;
        }

        // Do we need to implement this?
        protected override ContentResolveResult ResolveContent(Guid contentGuid)
        {
            var identity = _identityMappingService.Get(contentGuid);
            return ResolveContent(identity);
        }

        protected override ContentResolveResult ResolveContent(
            ContentReference contentLink)
        {
            var identity = _identityMappingService.Get(contentLink);
            return ResolveContent(identity);
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
                { ContentProviderElement.CapabilitiesString, "Create,Edit" },
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

    [InitializableModule,
     ModuleDependency(typeof(DataInitialization)),
     ModuleDependency(typeof(DynamicDataTransferHandler))]
    public class ContentTransferModule : IInitializableModule
    {
        private IdentityMappingService _identityMappingService;
        private IContentLoader _contentLoader;
        private Injected<IDataExportEvents> DataExportEvents { get; set; }
        private Injected<IDataImportEvents> DataImportEvents { get; set; }

        public void Initialize(InitializationEngine context)
        {
            _identityMappingService = context.Locate.Advanced.GetInstance<IdentityMappingService>();
            _contentLoader = context.Locate.Advanced.GetInstance<IContentLoader>();
            DataExportEvents.Service.ContentExporting += DataExportEvents_ContentExporting;
            
            // Any suggestion what to do with these events?
            DataImportEvents.Service.Starting += (transferContext, args) =>
            {
                Debugger.Break();
            };
            DataImportEvents.Service.BlobImporting += (transferContext, args) =>
            {
                Debugger.Break();
            };
            DataImportEvents.Service.ContentImporting += (transferContext, args) =>
            {
                Debugger.Break();
            };
            DataImportEvents.Service.PropertyImporting += (transferContext, args) =>
            {
                Debugger.Break();
            };
            RegisterTransferHandler.RegisterTransferHandlers += RegisterTransferHandlers;
        }

        public void Uninitialize(InitializationEngine context)
        {
            DataExportEvents.Service.ContentExporting -= DataExportEvents_ContentExporting;
            RegisterTransferHandler.RegisterTransferHandlers -= RegisterTransferHandlers;
        }

        void RegisterTransferHandlers(object sender, RegisterTransferHandlerEventArgs e)
        {
            e.RegisteredHandlers.Add(new MappedContentTransfer(_identityMappingService, _contentLoader));
        }

        private void DataExportEvents_ContentExporting(ITransferContext transferContext, ContentExportingEventArgs e)
        {
            if (!(transferContext is ITransferHandlerContext exporter)
                || exporter.TransferType != TypeOfTransfer.MirroringExporting)
            {
                return;
            }
        }
    }

    public class MappedContentTransfer : TransferHandlerBase
    {
        private readonly IdentityMappingService _identityMappingService;
        private readonly IContentLoader _contentLoader;

        public MappedContentTransfer(
            IdentityMappingService identityMappingService,
            IContentLoader contentLoader)
        {
            _identityMappingService = identityMappingService;
            _contentLoader = contentLoader;
        }

        // We can export custom data if we want
        public override void Write(Stream writer)
        {
            using (var xmlWriter = new XmlTextWriter(writer, System.Text.Encoding.UTF8))
            {
                xmlWriter.WriteStartElement("root");

                xmlWriter.WriteStartElement("mappedIdentities");
                foreach (var mappedIdentity in _identityMappingService.List(MappedContentProvider.Key))
                {
                    xmlWriter.WriteStartElement("identity");
                    xmlWriter.WriteElementString("contentGuid", mappedIdentity.ContentGuid.ToString());
                    xmlWriter.WriteElementString("external", mappedIdentity.ExternalIdentifier.ToString());
                    xmlWriter.WriteEndElement();//identity
                }
                xmlWriter.WriteEndElement();//mappedIdentities

                xmlWriter.WriteEndElement();//root
            }
        }

        // PROBLEM: This is called AFTER import (after contentRepository.Save etc)
        public override void Read(Stream reader)
        {
            
            using (var xmlReader = new XmlTextReader(reader))
            {
                xmlReader.MoveToContent();
                xmlReader.ReadToFollowing("identity");
                while (string.Equals(xmlReader.LocalName, "identity"))
                {
                    var identityElement = XNode.ReadFrom(xmlReader) as XElement;
                    var externalIdentifierString = identityElement?.Elements("external").FirstOrDefault()?.Value;
                    var contentGuidString = identityElement?.Elements("contentGuid").FirstOrDefault()?.Value;
                    if (string.IsNullOrWhiteSpace(externalIdentifierString) || 
                        string.IsNullOrWhiteSpace(contentGuidString) ||
                        !Guid.TryParse(contentGuidString, out var contentGuid))
                    {
                        continue;
                    }

                    // Here we can do the same hacky thing, delete and map content
                    var externalUri = new Uri(externalIdentifierString);

                    var mapping = _identityMappingService.Get(externalUri);
                    var content = _contentLoader.Get<TestExportData>(mapping.ContentLink).CreateWritableClone() as IContent;

                    content.ContentGuid = contentGuid;

                    _identityMappingService.Delete(externalUri);
                    _identityMappingService.MapContent(externalUri, content);
                }
            }
        }
    }
}