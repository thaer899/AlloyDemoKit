using AlloyDemoKit.Business;
using AlloyDemoKit.Models.Pages;
using AlloyDemoKit.Models.ViewModels;
using EPiServer;
using EPiServer.DataAbstraction;
using EPiServer.DataAccess;
using EPiServer.Security;
using EPiServer.Web;
using EPiServer.Web.Mvc;
using System.Web.Mvc;

namespace AlloyDemoKit.Controllers
{
    public class StartPageController : PageControllerBase<StartPage>
    {
        private readonly IdentityMappingService _identityMappingService;
        private readonly IContentLoader _contentLoader;
        private readonly IContentRepository _contentRepository;

        public StartPageController(
            IdentityMappingService identityMappingService,
            IContentLoader contentLoader, IContentRepository contentRepository)
        {
            _identityMappingService = identityMappingService;
            _contentLoader = contentLoader;
            _contentRepository = contentRepository;
        }

        public ActionResult Index(StartPage currentPage, bool? addMappedContent = false)
        {
            if (addMappedContent.HasValue && addMappedContent.Value)
            {
                var externalIdentifier = MappedIdentity.ConstructExternalIdentifier(
                    MappedContentProvider.Key,
                    "brianweet"
                );
                var identity = _identityMappingService.Get(externalIdentifier, true);
                var content = _contentLoader.Get<TestExportData>(identity.ContentLink);
                var sp = currentPage.CreateWritableClone() as StartPage;
                sp.MappedContent = content.ContentLink;
                _contentRepository.Save(sp, SaveAction.Publish, AccessLevel.NoAccess);
            }

            var model = PageViewModel.Create(currentPage);
            if (SiteDefinition.Current.StartPage.CompareToIgnoreWorkID(currentPage.ContentLink)) // Check if it is the StartPage or just a page of the StartPage type.
            {
                //Connect the view models logotype property to the start page's to make it editable
                var editHints = ViewData.GetEditHints<PageViewModel<StartPage>, StartPage>();
                editHints.AddConnection(m => m.Layout.Logotype, p => p.SiteLogotype);
                editHints.AddConnection(m => m.Layout.ProductPages, p => p.ProductPageLinks);
                editHints.AddConnection(m => m.Layout.CompanyInformationPages, p => p.CompanyInformationPageLinks);
                editHints.AddConnection(m => m.Layout.NewsPages, p => p.NewsPageLinks);
                editHints.AddConnection(m => m.Layout.CustomerZonePages, p => p.CustomerZonePageLinks);
            }

            return View(model);
        }

    }
}
