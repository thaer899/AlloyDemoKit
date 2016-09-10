using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using EPiServer.ServiceLocation;
using Microsoft.Azure.ActiveDirectory.GraphClient;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace AlloyDemoKit.AzureAD
{

    /// <summary>
    /// See http://world.episerver.com/blogs/Kalle-Ljung/Dates/2014/11/using-azure-active-directory-as-identity-provider/
    /// </summary>
    [ServiceConfiguration(typeof(AzureGraphService))]
    public class AzureGraphService
    {
        public async Task CreateRoleClaimsAsync(ClaimsIdentity identity)
        {
            // Get the Windows Azure Active Directory tenantId
            var tenantId = identity.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid").Value;

            // Get the userId
            var currentUserObjectId = identity.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;

            var servicePointUri = new Uri(ConfigurationManager.AppSettings["GraphUrl"]);
            var serviceRoot = new Uri(servicePointUri, tenantId);
            var activeDirectoryClient = new ActiveDirectoryClient(serviceRoot,
                async () => await AcquireTokenAsyncForApplication());

            var userResult = await activeDirectoryClient.Users
                .Where(u => u.ObjectId == currentUserObjectId).ExecuteAsync();
            var currentUser = userResult.CurrentPage.FirstOrDefault() as IUserFetcher;

            var pagedCollection = await currentUser.MemberOf.OfType<Group>().ExecuteAsync();
            do
            {
                var groups = pagedCollection.CurrentPage.ToList();
                foreach (Group role in groups)
                {
                    ((ClaimsIdentity)identity).AddClaim(new Claim(ClaimTypes.Role, role.DisplayName, ClaimValueTypes.String, "AzureGraphService"));

                }
                pagedCollection = pagedCollection.GetNextPageAsync().Result;
            } while (pagedCollection != null && pagedCollection.MorePagesAvailable);
        }

        public async Task<string> AcquireTokenAsyncForApplication()
        {
            var authenticationContext = new AuthenticationContext(string.Format("https://login.windows.net/{0}",
                ConfigurationManager.AppSettings["TenantName"]), false);

            // Config for OAuth client credentials 
            var clientCred = new ClientCredential(ConfigurationManager.AppSettings["ClientId"], ConfigurationManager.AppSettings["ClientSecret"]);
            var authenticationResult = authenticationContext.AcquireToken(ConfigurationManager.AppSettings["GraphUrl"], clientCred);
            return authenticationResult.AccessToken;
        }
    }
}
