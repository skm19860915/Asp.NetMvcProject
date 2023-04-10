using System.Data.Entity;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RainWorx.FrameWorx.MVC.Models
{
    // You can add profile data for the user by adding more properties to your ApplicationUser class, please visit https://go.microsoft.com/fwlink/?LinkID=317594 to learn more.
    public class AuctionWorxUser : IdentityUser<int, AuctionWorxUserLogin, AuctionWorxUserRole, AuctionWorxUserClaim>
    {
        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<AuctionWorxUser, int> manager)
        {
            //when logging in using an external provider, a null exception occurs if the user's SecurityStamp is a null value, so set is here if necessary:
            // source: https://stackoverflow.com/questions/24991212/value-cannot-be-null-parameter-name-value-createidentityasync
            if (this.SecurityStamp == null)
            {
                this.SecurityStamp = Guid.NewGuid().ToString();
            }

            // Note the authenticationType must match the one defined in CookieAuthenticationOptions.AuthenticationType
            var userIdentity = await manager.CreateIdentityAsync(this, DefaultAuthenticationTypes.ApplicationCookie);
            // Add custom user claims here
            return userIdentity;
        }
    }

    public class AuctionWorxUserLogin : IdentityUserLogin<int>
    {
    }

    public class AuctionWorxRole : IdentityRole<int, AuctionWorxUserRole>
    {
    }

    public class AuctionWorxUserRole : IdentityUserRole<int>
    {
    }

    public class AuctionWorxUserClaim : IdentityUserClaim<int>
    {
    }

    public class AuctionWorxUserStore : UserStore<AuctionWorxUser, AuctionWorxRole, int, AuctionWorxUserLogin, AuctionWorxUserRole, AuctionWorxUserClaim>
    {
        public AuctionWorxUserStore(AuctionWorxDbContext dbContext)
            :base(dbContext)
        {
        }
    }

    public class AuctionWorxDbContext : IdentityDbContext<AuctionWorxUser, AuctionWorxRole, int, AuctionWorxUserLogin, AuctionWorxUserRole, AuctionWorxUserClaim>
    {
        public AuctionWorxDbContext()
            : this("name=db_connection")
        {
        }

        public AuctionWorxDbContext(string connStringName)
            : base(connStringName)
        {
            this.Configuration.LazyLoadingEnabled = false;
            this.Configuration.ProxyCreationEnabled = false;
        }

        public static AuctionWorxDbContext Create()
        {
            return new AuctionWorxDbContext();
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // This needs to go before the other rules!

            modelBuilder.Entity<AuctionWorxUser>().ToTable("RWX_Users");
            modelBuilder.Entity<AuctionWorxRole>().ToTable("RWX_Roles");
            modelBuilder.Entity<AuctionWorxUserRole>().ToTable("RWX_UserRoles");
            modelBuilder.Entity<AuctionWorxUserLogin>().ToTable("RWX_UserLogins");
            modelBuilder.Entity<AuctionWorxUserClaim>().ToTable("RWX_UserClaims");
        }
    }

    /// <summary>
    ///     Used to validate some basic password policy like length and number of non alphanumerics
    /// </summary>
    public class AuctionWorxPasswordValidator : IIdentityValidator<string>
    {
        /// <summary>
        ///     Minimum required length
        /// </summary>
        public int RequiredLength { get; set; }

        /// <summary>
        ///     Require a non letter or digit character
        /// </summary>
        public bool RequireNonLetterOrDigit { get; set; }

        /// <summary>
        ///     Require a lower case letter ('a' - 'z')
        /// </summary>
        public bool RequireLowercase { get; set; }

        /// <summary>
        ///     Require an upper case letter ('A' - 'Z')
        /// </summary>
        public bool RequireUppercase { get; set; }

        /// <summary>
        ///     Require a digit ('0' - '9')
        /// </summary>
        public bool RequireDigit { get; set; }

        /// <summary>
        ///     Ensures that the string is of the required length and meets the configured requirements
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public virtual Task<IdentityResult> ValidateAsync(string item)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(item) || item.Length < RequiredLength)
            {
                errors.Add("PasswordTooShort");
            }
            if (RequireNonLetterOrDigit && item.All(IsLetterOrDigit))
            {
                errors.Add("PasswordRequireNonLetterOrDigit");
            }
            if (RequireDigit && item.All(c => !IsDigit(c)))
            {
                errors.Add("PasswordRequireDigit");
            }
            if (RequireLowercase && item.All(c => !IsLower(c)))
            {
                errors.Add("PasswordRequireLower");
            }
            if (RequireUppercase && item.All(c => !IsUpper(c)))
            {
                errors.Add("PasswordRequireUpper");
            }
            if (errors.Count == 0)
            {
                return Task.FromResult(IdentityResult.Success);
            }
            //return Task.FromResult(IdentityResult.Failed(String.Join(" ", errors)));
            return Task.FromResult(IdentityResult.Failed(errors.ToArray()));
        }

        /// <summary>
        ///     Returns true if the character is a digit between '0' and '9'
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public virtual bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        /// <summary>
        ///     Returns true if the character is between 'a' and 'z'
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public virtual bool IsLower(char c)
        {
            return c >= 'a' && c <= 'z';
        }

        /// <summary>
        ///     Returns true if the character is between 'A' and 'Z'
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public virtual bool IsUpper(char c)
        {
            return c >= 'A' && c <= 'Z';
        }

        /// <summary>
        ///     Returns true if the character is upper, lower, or a digit
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public virtual bool IsLetterOrDigit(char c)
        {
            return IsUpper(c) || IsLower(c) || IsDigit(c);
        }
    }

}