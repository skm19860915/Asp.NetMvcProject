using System;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.Google;
using Microsoft.Owin.Security.Facebook;
using Owin;
using RainWorx.FrameWorx.MVC.Models;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.Strings;
using System.Collections.Generic;
using RainWorx.FrameWorx.Utility;
using System.Configuration;

namespace RainWorx.FrameWorx.MVC
{
    public partial class Startup
    {
        // For more information on configuring authentication, please visit https://go.microsoft.com/fwlink/?LinkId=301864
        public void ConfigureAuth(IAppBuilder app)
        {
            // Configure the db context, user manager and signin manager to use a single instance per request
            app.CreatePerOwinContext(AuctionWorxDbContext.Create);
            app.CreatePerOwinContext<AuctionWorxUserManager>(AuctionWorxUserManager.Create);
            app.CreatePerOwinContext<AuctionWorxSignInManager>(AuctionWorxSignInManager.Create);

            // Enable the application to use a cookie to store information for the signed in user
            // and to use a cookie to temporarily store information about a user logging in with a third party login provider
            // Configure the sign in cookie
            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = DefaultAuthenticationTypes.ApplicationCookie,
                LoginPath = new PathString("/Account/Logon")
                //,Provider = new CookieAuthenticationProvider
                //{
                //    // Enables the application to validate the security stamp when the user logs in.
                //    // This is a security feature which is used when you change a password or add an external login to your account.  
                //    OnValidateIdentity = SecurityStampValidator.OnValidateIdentity<ApplicationUserManager, ApplicationUser>(
                //        validateInterval: TimeSpan.FromMinutes(30),
                //        regenerateIdentity: (manager, user) => user.GenerateUserIdentityAsync(manager))
                //}
                ,CookieSecure = CookieSecureOption.Never
            });
            app.UseExternalSignInCookie(DefaultAuthenticationTypes.ExternalCookie);

            //// Enables the application to temporarily store user information when they are verifying the second factor in the two-factor authentication process.
            //app.UseTwoFactorSignInCookie(DefaultAuthenticationTypes.TwoFactorCookie, TimeSpan.FromMinutes(5));

            //// Enables the application to remember the second login verification factor such as phone or email.
            //// Once you check this option, your second step of verification during the login process will be remembered on the device where you logged in from.
            //// This is similar to the RememberMe option when you log in.
            //app.UseTwoFactorRememberBrowserCookie(DefaultAuthenticationTypes.TwoFactorRememberBrowserCookie);

            // Uncomment the following lines to enable logging in with third party login providers
            //app.UseMicrosoftAccountAuthentication(
            //    clientId: "",
            //    clientSecret: "");

            //app.UseTwitterAuthentication(
            //   consumerKey: "",
            //   consumerSecret: "");

            bool Facebook_Authentication_Enabled = false;
            bool.TryParse(ConfigurationManager.AppSettings["Facebook_Authentication_Enabled"], out Facebook_Authentication_Enabled);
            string Facebook_App_ID = ConfigurationManager.AppSettings["Facebook_App_ID"];
            string Facebook_App_Secret = ConfigurationManager.AppSettings["Facebook_App_Secret"];
            if (Facebook_Authentication_Enabled)
            {
                app.UseFacebookAuthentication(
                    appId: Facebook_App_ID,
                    appSecret: Facebook_App_Secret);
            }

            bool Google_Authentication_Enabled = false;
            bool.TryParse(ConfigurationManager.AppSettings["Google_Authentication_Enabled"], out Google_Authentication_Enabled);
            string Google_Client_ID = ConfigurationManager.AppSettings["Google_Client_ID"];
            string Google_Client_Secret = ConfigurationManager.AppSettings["Google_Client_Secret"];
            if (Google_Authentication_Enabled)
            {
                app.UseGoogleAuthentication(new GoogleOAuth2AuthenticationOptions()
                {
                    ClientId = Google_Client_ID,
                    ClientSecret = Google_Client_Secret
                });
            }

        }
    }
}
