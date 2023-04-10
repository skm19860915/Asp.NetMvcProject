using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.Filters;
using RainWorx.FrameWorx.Unity;
using Microsoft.AspNet.Identity;

namespace RainWorx.FrameWorx.MVC.Areas.API.MessageHandlers
{
    public class BasicVerifySignature : DelegatingHandler
    {
        public BasicVerifySignature()
        {
            //_encryptor = UnityResolver.Get<IEncryptor>("passwordEncryptor");
        }

        public BasicVerifySignature(HttpMessageHandler inner)
            : base(inner)
        {
            //_encryptor = UnityResolver.Get<IEncryptor>("passwordEncryptor");
        }

        private const string Scheme = "RWX_BASIC";
        //private readonly IEncryptor _encryptor;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            //skip authentication for requests without an Authorization header
            if (request.Headers.Authorization == null)
            {
                return base.SendAsync(request, cancellationToken);
            }

            //Make sure correct Authorization Scheme is present
            if (!request.Headers.Authorization.Scheme.Equals(Scheme, StringComparison.Ordinal))
            {
                var tsc = new TaskCompletionSource<HttpResponseMessage>();
                tsc.SetResult(request.CreateErrorResponse(HttpStatusCode.BadRequest, "Authentication Scheme INVALID (must be \"" + Scheme + "\")"));
                return tsc.Task;
            }

            //Make sure Authorization Parameter is present
            if (string.IsNullOrEmpty(request.Headers.Authorization.Parameter))
            {
                var tsc = new TaskCompletionSource<HttpResponseMessage>();
                tsc.SetResult(request.CreateErrorResponse(HttpStatusCode.BadRequest, "Authentication Parameter MISSING (must be {username" + Utilities.SignatureSeparatorCharacter + "password})"));
                return tsc.Task;
            }
           
            //get actual signature and calculate expected signature
            char[] signatureSplitter = new char[] { Utilities.SignatureSeparatorCharacter };
            string[] splitAuthParameter = request.Headers.Authorization.Parameter.Split(signatureSplitter, 2);
            string username = splitAuthParameter[0];
            string password = splitAuthParameter[1];

            //this is where we get the user from the db            
            User actingUser = UserClient.GetUserByUserName(username, username);

            if (actingUser == null)
            {
                var tsc = new TaskCompletionSource<HttpResponseMessage>();
                HttpResponseMessage response = request.CreateErrorResponse(HttpStatusCode.Unauthorized, "Unauthorized Access Attempt");
                response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(Scheme));
                tsc.SetResult(response);
                return tsc.Task;
            }

            if (actingUser.IsLockedOut || !actingUser.IsApproved || !actingUser.IsActive || !actingUser.IsVerified || !actingUser.WebAPIEnabled)
            {
                var tsc = new TaskCompletionSource<HttpResponseMessage>();
                HttpResponseMessage response = request.CreateErrorResponse(HttpStatusCode.Unauthorized, "Unauthorized Access Attempt");
                response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(Scheme));
                tsc.SetResult(response);
                return tsc.Task;
            }

            //compare signatures, return if unauthorized
            //if (_encryptor.Encrypt(password) != actingUser.Password)
            var passwordHasher = new PasswordHasher();
            if (passwordHasher.VerifyHashedPassword(actingUser.PasswordHash, password) == PasswordVerificationResult.Failed)
            {
                var tsc = new TaskCompletionSource<HttpResponseMessage>();
                HttpResponseMessage response = request.CreateErrorResponse(HttpStatusCode.Unauthorized, "Unauthorized Access Attempt");
                response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(Scheme));
                tsc.SetResult(response);
                return tsc.Task;
            }            

            return base.SendAsync(request, cancellationToken);
        }       
    }
}