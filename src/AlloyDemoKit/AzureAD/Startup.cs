using EPiServer.Security;
using EPiServer.ServiceLocation;
using Microsoft.Owin;
using Microsoft.Owin.Extensions;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.WsFederation;
using Owin;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Helpers;
using AlloyDemoKit.AzureAD;

[assembly: OwinStartup(typeof(Startup))]

namespace AlloyDemoKit.AzureAD
{
    /// <summary>
    /// See: http://world.episerver.com/blogs/Kalle-Ljung/Dates/2014/11/using-azure-active-directory-as-identity-provider/
    /// </summary>
    public class Startup
    {
        const string LogoutUrl = "/util/logout.aspx";

        public void Configuration(IAppBuilder app)
        {
            app.SetDefaultSignInAsAuthenticationType(WsFederationAuthenticationDefaults.AuthenticationType);
            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = WsFederationAuthenticationDefaults.AuthenticationType
            });

            //Enable federated authentication
            app.UseWsFederationAuthentication(new WsFederationAuthenticationOptions()
            {
                //Trusted URL to federation server meta data
                MetadataAddress = ConfigurationManager.AppSettings["MetadataAddress"],
                //Value of Wtreal must *exactly* match what is configured in the federation server
                Wtrealm = ConfigurationManager.AppSettings["Wtrealm"],
                Notifications = new WsFederationAuthenticationNotifications()
                {
                    RedirectToIdentityProvider = (ctx) =>
                    {
                        //To avoid a redirect loop to the federation server send 403 when user is authenticated but does not have access
                        if (ctx.OwinContext.Response.StatusCode == 401 && ctx.OwinContext.Authentication.User.Identity.IsAuthenticated)
                        {
                            ctx.OwinContext.Response.StatusCode = 403;
                            ctx.HandleResponse();
                        }
                        return Task.FromResult(0);
                    },
                    SecurityTokenValidated = async (ctx) =>
                    {
                        //Ignore scheme/host name in redirect Uri to make sure a redirect to HTTPS does not redirect back to HTTP
                        var redirectUri = new Uri(ctx.AuthenticationTicket.Properties.RedirectUri, UriKind.RelativeOrAbsolute);
                        if (redirectUri.IsAbsoluteUri)
                        {
                            ctx.AuthenticationTicket.Properties.RedirectUri = redirectUri.PathAndQuery;
                        }

                        var claimsIdentity = ctx.AuthenticationTicket.Identity;

                        #region Azure

                        // Create claims for roles
                        await ServiceLocator.Current.GetInstance<AzureGraphService>().CreateRoleClaimsAsync(claimsIdentity);

                        #endregion

                        // Make sure we have a name claim
                        if (string.IsNullOrWhiteSpace(claimsIdentity.Name))
                        {
                            var emailClaim = claimsIdentity.FindFirst(ClaimTypes.Email);
                            if (emailClaim != null)
                            {
                                var email = emailClaim.Value;
                                claimsIdentity.AddClaim(new Claim(ClaimTypes.Name, email));
                            }
                        }

                        // Fix, ISynchronizingUserService.SynchronizeAsync needs a list of additional claims
                        var additionalClaims = new List<string>();

                        //Sync user and the roles to EPiServer in the background
                        await ServiceLocator.Current.GetInstance<ISynchronizingUserService>().SynchronizeAsync(ctx.AuthenticationTicket.Identity, additionalClaims);
                    }
                }
            });

            //Always keep the following to properly setup virtual roles for principals, map the logout in backend function and create anti forgery key  
            app.UseStageMarker(PipelineStage.Authenticate);

            app.Map(LogoutUrl, map =>
            {
                map.Run(ctx =>
                {
                    ctx.Authentication.SignOut();
                    return Task.FromResult(0);
                });
            });
            AntiForgeryConfig.UniqueClaimTypeIdentifier = ClaimTypes.Name;
        }
    }
}