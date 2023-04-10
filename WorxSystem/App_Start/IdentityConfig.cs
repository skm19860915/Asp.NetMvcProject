using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using RainWorx.FrameWorx.MVC.Models;
using System.Configuration;

namespace RainWorx.FrameWorx.MVC
{
    public class EmailService : IIdentityMessageService
    {
        public Task SendAsync(IdentityMessage message)
        {
            // Plug in your email service here to send an email.
            return Task.FromResult(0);
        }
    }

    public class SmsService : IIdentityMessageService
    {
        public Task SendAsync(IdentityMessage message)
        {
            // Plug in your SMS service here to send a text message.
            return Task.FromResult(0);
        }
    }

    // Configure the application user manager used in this application. UserManager is defined in ASP.NET Identity and is used by the application.
    public class AuctionWorxUserManager : UserManager<AuctionWorxUser, int>
    {
        public AuctionWorxUserManager(AuctionWorxUserStore store)
            : base(store)
        {
        }

        public static AuctionWorxUserManager Create(IdentityFactoryOptions<AuctionWorxUserManager> options, IOwinContext context) 
        {
            var manager = new AuctionWorxUserManager(new AuctionWorxUserStore(context.Get<AuctionWorxDbContext>()));
            // Configure validation logic for usernames
            manager.UserValidator = new UserValidator<AuctionWorxUser, int>(manager)
            {
                AllowOnlyAlphanumericUserNames = false,
                RequireUniqueEmail = true
            };

            int passwordRequiredLength = 6;
            int.TryParse(ConfigurationManager.AppSettings["Password_RequiredLength"], out passwordRequiredLength);
            bool passwordRequireNonLetterOrDigit = false;
            bool.TryParse(ConfigurationManager.AppSettings["Password_RequireNonLetterOrDigit"], out passwordRequireNonLetterOrDigit);
            bool passwordRequireDigit = false;
            bool.TryParse(ConfigurationManager.AppSettings["Password_RequireDigit"], out passwordRequireDigit);
            bool passwordRequireLowercase = false;
            bool.TryParse(ConfigurationManager.AppSettings["Password_RequireLowercase"], out passwordRequireLowercase);
            bool passwordRequireUppercase = false;
            bool.TryParse(ConfigurationManager.AppSettings["Password_RequireUppercase"], out passwordRequireUppercase);

            // Configure validation logic for passwords
            manager.PasswordValidator = new AuctionWorxPasswordValidator
            {
                RequiredLength = passwordRequiredLength,
                RequireNonLetterOrDigit = passwordRequireNonLetterOrDigit,
                RequireDigit = passwordRequireDigit,
                RequireLowercase = passwordRequireLowercase,
                RequireUppercase = passwordRequireUppercase,
            };

            bool userLockoutEnabledByDefault = false;
            bool.TryParse(ConfigurationManager.AppSettings["UserLockoutEnabledByDefault"], out userLockoutEnabledByDefault);
            int defaultAccountLockout_Minutes = 5;
            int.TryParse(ConfigurationManager.AppSettings["DefaultAccountLockout_Minutes"], out defaultAccountLockout_Minutes);
            int maxFailedAccessAttemptsBeforeLockout = 5;
            int.TryParse(ConfigurationManager.AppSettings["MaxFailedAccessAttemptsBeforeLockout"], out maxFailedAccessAttemptsBeforeLockout);
            // Configure user lockout defaults
            manager.UserLockoutEnabledByDefault = userLockoutEnabledByDefault;
            manager.DefaultAccountLockoutTimeSpan = TimeSpan.FromMinutes(5);
            manager.MaxFailedAccessAttemptsBeforeLockout = maxFailedAccessAttemptsBeforeLockout;

            //// Register two factor authentication providers. This application uses Phone and Emails as a step of receiving a code for verifying the user
            //// You can write your own provider and plug it in here.
            //manager.RegisterTwoFactorProvider("Phone Code", new PhoneNumberTokenProvider<AuctionWorxUser, int>
            //{
            //    MessageFormat = "Your security code is {0}"
            //});
            //manager.RegisterTwoFactorProvider("Email Code", new EmailTokenProvider<AuctionWorxUser, int>
            //{
            //    Subject = "Security Code",
            //    BodyFormat = "Your security code is {0}"
            //});
            //manager.EmailService = new EmailService();
            //manager.SmsService = new SmsService();

            var dataProtectionProvider = options.DataProtectionProvider;
            if (dataProtectionProvider != null)
            {
                manager.UserTokenProvider = 
                    new DataProtectorTokenProvider<AuctionWorxUser, int>(dataProtectionProvider.Create("ASP.NET Identity"));
            }
            return manager;
        }
    }

    // Configure the application sign-in manager which is used in this application.
    public class AuctionWorxSignInManager : SignInManager<AuctionWorxUser, int>
    {
        public AuctionWorxSignInManager(AuctionWorxUserManager userManager, IAuthenticationManager authenticationManager)
            : base(userManager, authenticationManager)
        {
        }

        public override Task<ClaimsIdentity> CreateUserIdentityAsync(AuctionWorxUser user)
        {
            return user.GenerateUserIdentityAsync((AuctionWorxUserManager)UserManager);
        }

        public static AuctionWorxSignInManager Create(IdentityFactoryOptions<AuctionWorxSignInManager> options, IOwinContext context)
        {
            return new AuctionWorxSignInManager(context.GetUserManager<AuctionWorxUserManager>(), context.Authentication);
        }
    }
}
