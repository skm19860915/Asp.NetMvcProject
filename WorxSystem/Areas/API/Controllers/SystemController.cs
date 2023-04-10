using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Http;
using System.Web.Http.Description;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.Filters;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Base;
using RainWorx.FrameWorx.MVC.Areas.API.Models;
using RainWorx.FrameWorx.Unity;
using RainWorx.FrameWorx.Utility;
using User = RainWorx.FrameWorx.DTO.User;
using Microsoft.AspNet.Identity;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers
{    
    /// <summary>
    /// Provides overall API system functionality.
    /// </summary>
    [RoutePrefix("api")]
    [AllowAnonymous]
    public class SystemController : AuctionWorxAPIController
    {
        /// <summary>
        /// Retrieves an authorization token for additional calls to the Web API
        /// Note, this is a "Post" because using GET for sensitive data is a bad idea for several reasons:
        /// 1.) Mostly HTTP referrer leakage (an external image in the target page might leak the password[1])
        /// 2.) Password will be stored in server logs (which is obviously bad)
        /// 3.) History caches in browsers
        /// 
        /// [1] Although I need to note that RFC states that browser should not send referrers from HTTPS to HTTP. But that doesn't mean a bad 3rd party browser toolbar or an external image/flash from an HTTPS site won't leak it.
        /// http://stackoverflow.com/questions/323200/is-an-https-query-string-secure
        /// </summary>        
        /// <param name="request">The request object containing username and password</param>
        /// <returns>The User's current authorization token</returns>
        [Route("login")]
        [ResponseType(typeof(string))]
        public HttpResponseMessage PostRetrieveAuthToken([FromBody] AccountLoginRequest request)
        {
            string actorIP = ((HttpContextBase)Request.Properties["MS_HttpContext"]).Request.UserHostAddress;            

            //this is where we get the authtoken from the db            
            User actingUser = UserClient.GetUserByUserName(request.username, request.username);

            if (actingUser == null)
            {
                LogManager.WriteLog("API Login Failed: User Doesn't Exist", "Authentication", "User", TraceEventType.Warning, actorIP, null, new Dictionary<string, object>() { { "UserName", request.username }, { "Reason", Strings.Messages.UserNotExist } }, 0, 0, Environment.MachineName);
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "Unauthorized Access Attempt");
            }
            else
            {
                if (!actingUser.IsActive)
                {
                    LogManager.WriteLog("API Login Failed: User Is Not Active", "Authentication", "API",
                                        TraceEventType.Warning, actorIP, null,
                                        new Dictionary<string, object>() { { "UserName", request.username }, { "Reason", Strings.Messages.UserIsNotActive } }, 0, 0,
                                        Environment.MachineName);
                    return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User is not active");
                }
                else if (actingUser.IsLockedOut)
                {
                    LogManager.WriteLog("API Login Failed: User Is Locked Out", "Authentication", "API",
                                        TraceEventType.Warning, actorIP, null,
                                        new Dictionary<string, object>() { { "UserName", request.username }, { "Reason", Strings.Messages.UserIsLockedOut } }, 0, 0,
                                        Environment.MachineName);
                    return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User is locked out");
                }
                else if (!actingUser.IsApproved)
                {
                    LogManager.WriteLog("API Login Failed: User Is Not Approved", "Authentication", "API",
                                        TraceEventType.Warning, actorIP, null,
                                        new Dictionary<string, object>() { { "UserName", request.username }, { "Reason", Strings.Messages.UserIsNotApproved } },
                                        0, 0, Environment.MachineName);
                    return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User is not approved");
                }
                else if (!actingUser.IsVerified)
                {
                    LogManager.WriteLog("API Login Failed: User Email Is Not Verified", "Authentication", "API",
                                        TraceEventType.Warning, actorIP, null,
                                        new Dictionary<string, object>() { { "UserName", request.username }, { "Reason", Strings.Messages.UserIsNotVerified } },
                                        0, 0, Environment.MachineName);
                    return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "User is not verified");
                }         
            }            

            bool verify = false;

            //verify = UnityResolver.Get<IEncryptor>("passwordEncryptor").Encrypt(request.password) == actingUser.Password;
            var passwordHasher = new PasswordHasher();
            verify = passwordHasher.VerifyHashedPassword(actingUser.PasswordHash, request.password) != PasswordVerificationResult.Failed;

            if (verify)
            {
                actingUser.LastLoginDate = DateTime.UtcNow;
                actingUser.LastIP = actorIP;
                actingUser.LastActivityDate = DateTime.UtcNow;

                UserClient.UpdateUser(Strings.SystemActors.SystemUserName, actingUser);

                LogManager.WriteLog("API Login Succeeded", "Authentication", "API", TraceEventType.Information, null, null, new Dictionary<string, object>() { { "UserName", request.username } }, 0, 0, Environment.MachineName);
                return Request.CreateResponse(HttpStatusCode.OK, actingUser.ServiceAuthorizationToken);
            }
            else
            {
                LogManager.WriteLog("API Login Failed: Invalid Password", "Authentication", "API", TraceEventType.Warning, null, null, new Dictionary<string, object>() { { "UserName", request.username } }, 0, 0, Environment.MachineName);
                return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "Unauthorized Access Attempt");
            }            
        }

        /// <summary>
        /// Retrieves the current version of the API
        /// </summary>
        /// <returns>The version number as an integer (string)</returns>
        [Route("version")]
        [ResponseType(typeof(string))]
        public HttpResponseMessage GetVersion()
        {            
            return Request.CreateResponse(HttpStatusCode.OK, Version.ToString());
        }

        /// <summary>
        /// Retrieves the current Build number.  (This value is retrieved from the RainWorx.FrameWorx.BLL dll).
        /// </summary>
        /// <returns>The version number as a dotted version number string</returns>
        [Route("build")]
        [ResponseType(typeof(string))]
        public HttpResponseMessage GetBuild()
        {
            Dictionary<string, string> versionInfo = CommonClient.GetVersionInfo();            
            Regex r = new Regex(@"(?:Version=)(?<version>[\d.\.]+)");
            string build = r.Match(versionInfo["RainWorx.FrameWorx.BLL"]).Groups["version"].Value;

            return Request.CreateResponse(HttpStatusCode.OK, build);
        }
    }
}
