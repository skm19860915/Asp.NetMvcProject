using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.DTO.FaultContracts;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Base;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Helpers;
using RainWorx.FrameWorx.MVC.Areas.API.Models;
using System.Threading.Tasks;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers
{
    /// <summary>
    /// Provides services to Create/Update/Get/Delete Users
    /// </summary>
    [RoutePrefix("api/User")]
    public class UserController : AuctionWorxAPIController
    {
        /// <summary>
        /// Gets a User by UserName
        /// </summary>
        /// <param name="username">The UserName of the User to get</param>
        /// <returns>An HTTP Status code of 200 (OK) and the User on success.  Otherwise, HTTP Status code 404 (Not Found) if the User is not found.</returns>
        [Route("{username}")]
        [ResponseType(typeof(APIUser))]
        public HttpResponseMessage Get(string username)
        {
            try
            {
                DTO.User user = UserClient.GetUserByUserName(Strings.SystemActors.SystemUserName, username);

                if (user == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "User not found");
                }
                else
                {
                    return Request.CreateResponse<APIUser>(HttpStatusCode.OK, APIUser.FromDTOUser(user));
                }
            }
            catch (System.ServiceModel.FaultException<InvalidArgumentFaultContract> /*iafc*/)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "User not found");
            }
        }

        /// <summary>
        /// Processes request to register a new user.
        /// </summary>
        /// <param name="input">a collection of user input values</param>
        /// <remarks>
        /// See https://www.rainworx.com/dev-docs/#userclient_userinput_parameters.htm#RegisterUser for a list of all applicable input keys.
        /// </remarks>
        /// <returns>An HTTP Status code of 200 (OK) and the User on success.  Otherwise, HTTP Status code 404 (Not Found) if the User is not found.</returns>
        [Route("Register")]
        [ResponseType(typeof(string))]
        [AllowAnonymous]
        public async Task<HttpResponseMessage> RegisterUserAsync([FromBody] UserInput input)
        {
            if (input == null)
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "UserInput is null");

            string requestedUserName = input.Items.ContainsKey(Strings.Fields.UserName) ? input.Items[Strings.Fields.UserName].Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(requestedUserName))
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "UserName is required");

            string requestedPassword = input.Items.ContainsKey(Strings.Fields.Password) ? input.Items[Strings.Fields.Password] : string.Empty;
            if (string.IsNullOrWhiteSpace(requestedPassword))
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Password is required");

            User existingUser = null;
            existingUser = UserClient.GetUserByUserName(Strings.SystemActors.SystemUserName, requestedUserName);

            if (existingUser == null)
            {
                UserClient.RegisterUser(requestedUserName, input);
                var newUser = UserClient.GetUserByUserName(Strings.SystemActors.SystemUserName, requestedUserName);
                var result = await UserManager.AddPasswordAsync(newUser.ID, requestedPassword);

                if (!result.Succeeded)
                {
                    UserClient.DeleteUser(Strings.SystemActors.SystemUserName, newUser.ID);
                    return Request.CreateResponse(HttpStatusCode.OK, "Invalid password");
                }
                else
                {
                    if (SiteClient.BoolSetting(Strings.SiteProperties.UserApprovalRequired))
                    {
                        UserClient.SendNeedsAdminApprovalEmail(Strings.SystemActors.SystemUserName, newUser.UserName);
                    }
                    if (SiteClient.VerifyUserEmail)
                    {
                        UserClient.SendUserVerificationEmail(Strings.SystemActors.SystemUserName, newUser.UserName);
                    }
                }
                return Request.CreateResponse(HttpStatusCode.OK, newUser.ID);
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.OK, "UserName taken, please enter another name");
            }
        }
    }

}
