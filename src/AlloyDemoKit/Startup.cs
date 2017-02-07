using EPiServer.Cms.UI.AspNetIdentity;
using Microsoft.Owin;
using Microsoft.Owin.Security.Cookies;
using Owin;
using System;
using System.Web;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Helpers;
using EPiServer.Security;
using EPiServer.ServiceLocation;
using EPiServer.Web;
using Microsoft.Owin.Extensions;
using Microsoft.Owin.Security.Notifications;
using Microsoft.Owin.Security.OpenIdConnect;
using Microsoft.Owin.Security;

[assembly: OwinStartup(typeof(AlloyDemoKit.Startup))]

namespace AlloyDemoKit
{
    public class Startup
    {

        public void Configuration(Owin.IAppBuilder app)
        {
            // Add CMS integration for ASP.NET Identity
            app.AddCmsAspNetIdentity<ApplicationUser>();
            app.SetDefaultSignInAsAuthenticationType(CookieAuthenticationDefaults.AuthenticationType);
            app.UseCookieAuthentication(new CookieAuthenticationOptions()
            {
                Provider = new CookieAuthenticationProvider
                {
                    OnApplyRedirect = ctx =>
                    {
                        // Do not redirect calls for ajax requests
                        if (!IsAjaxRequest(ctx.Request))
                        {
                            ctx.Response.Redirect(ctx.RedirectUri);
                        }
                    }
                }
            });
            UseEpiOpenIdConnectAuthentication(app);
        }
        // https://brockallen.com/2013/10/27/using-cookie-authentication-middleware-with-web-api-and-401-response-codes/
        private static bool IsAjaxRequest(IOwinRequest request)
        {
            IReadableStringCollection query = request.Query;
            if (query != null && (query["X-Requested-With"] == "XMLHttpRequest" || query["X-Requested-With"] == "Fetch"))
            {
                return true;
            }
            IHeaderDictionary headers = request.Headers;
            return ((headers != null) && (headers["X-Requested-With"] == "XMLHttpRequest" || headers["X-Requested-With"] == "Fetch"));
        }

        public static IAppBuilder UseEpiOpenIdConnectAuthentication(IAppBuilder app)
        {
            try
            {
                app.UseOpenIdConnectAuthentication(new OpenIdConnectAuthenticationOptions
                {
                    ClientId = ConfigurationManager.AppSettings["ida:ClientId"],
                    Authority = string.Format(CultureInfo.InvariantCulture, ConfigurationManager.AppSettings["ida:AADInstance"], ConfigurationManager.AppSettings["ida:Tenant"]),
                    PostLogoutRedirectUri = "asdf",
                    UseTokenLifetime = false,
                    // Should give us control over when to redirect to the AD
                    AuthenticationMode = AuthenticationMode.Active,
                    TokenValidationParameters = new System.IdentityModel.Tokens.TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        RoleClaimType = ClaimTypes.Role
                    },
                    Notifications = new OpenIdConnectAuthenticationNotifications
                    {
                        AuthenticationFailed = context =>
                        {
                            context.HandleResponse();
                            context.Response.Write(context.Exception.Message);
                            return Task.FromResult(0);
                        },
                        RedirectToIdentityProvider = context =>
                        {
                            // Here you can change the return uri based on multisite
                            HandleMultiSitereturnUrl(context);

                            // To avoid a redirect loop to the federation server send 403 
                            // when user is authenticated but does not have access
                            if (context.OwinContext.Response.StatusCode == 401 &&
                                    context.OwinContext.Authentication.User?.Identity != null &&
                                    context.OwinContext.Authentication.User.Identity.IsAuthenticated)
                            {
                                context.OwinContext.Response.StatusCode = 403;
                                context.HandleResponse();
                            }
                            return Task.FromResult(0);
                        },
                        SecurityTokenValidated = async (ctx) =>
                        {
                            var redirectUri = new Uri(ctx.AuthenticationTicket.Properties.RedirectUri, UriKind.RelativeOrAbsolute);
                            if (redirectUri.IsAbsoluteUri)
                            {
                                ctx.AuthenticationTicket.Properties.RedirectUri = redirectUri.PathAndQuery;
                            }

                            var claimsIdentity = ctx.AuthenticationTicket.Identity;

                            // Create role claims for identity
                            claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, "Administrators"));

                            // Sync user and the roles to EPiServer in the background
                            await ServiceLocator.Current.GetInstance<ISynchronizingUserService>().SynchronizeAsync(claimsIdentity, new List<string>());
                        }
                    }
                });

                app.Map("/util/logout.aspx", map => map.Run(ctx =>
                {
                    ctx.Authentication.SignOut(OpenIdConnectAuthenticationDefaults.AuthenticationType, CookieAuthenticationDefaults.AuthenticationType);
                    return Task.Run(() => ctx.Response.Redirect("/"));
                }));

                app.UseStageMarker(PipelineStage.Authenticate);

                //Known problems: http://world.episerver.com/documentation/developer-guides/CMS/security/integrate-azure-ad-using-openid-connect/
                AntiForgeryConfig.UniqueClaimTypeIdentifier = ClaimTypes.NameIdentifier;

                return app;
            }
            catch (Exception ex)
            {
                Debugger.Break();
                throw ex;
            }
        }

        private static void HandleMultiSitereturnUrl(
       RedirectToIdentityProviderNotification<Microsoft.IdentityModel.Protocols.OpenIdConnectMessage,
           OpenIdConnectAuthenticationOptions> context)
        {
            // here you change the context.ProtocolMessage.RedirectUri to corresponding siteurl
            // this is a sample of how to change redirecturi in the multi-tenant environment
            if (context.ProtocolMessage.RedirectUri == null)
            {
                var currentUrl = SiteDefinition.Current.SiteUrl;
                if (currentUrl == null)
                {
                    context.ProtocolMessage.RedirectUri = "http://localhost:51481";
                }
                else
                {
                    context.ProtocolMessage.RedirectUri = new UriBuilder(
                currentUrl.Scheme,
                currentUrl.Host,
                currentUrl.Port,
                HttpContext.Current.Request.Url.AbsolutePath).ToString();
                }
            }
        }
    }
}
