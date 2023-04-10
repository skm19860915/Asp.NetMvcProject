using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Web.Http;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers;
using RainWorx.FrameWorx.MVC.Areas.API.Filters;
using RainWorx.FrameWorx.MVC.Areas.API.MessageHandlers;
using RainWorx.FrameWorx.MVC.Areas.API.Models;
using RainWorx.FrameWorx.MVC.Areas.API;
using System.Web.Http.Cors;

namespace RainWorx.FrameWorx.MVC
{
    public static class WebApiConfig
    {
        //public static void Register(HttpConfiguration config)
        //{
        //    config.MapHttpAttributeRoutes();

        //    config.Routes.MapHttpRoute(
        //        name: "DefaultApi",
        //        routeTemplate: "api/{controller}/{id}",
        //        defaults: new { id = RouteParameter.Optional }
        //    );

        //    // Uncomment the following line of code to enable query support for actions with an IQueryable or IQueryable<T> return type.
        //    // To avoid processing unexpected or malicious queries, use the validation settings on QueryableAttribute to validate incoming queries.
        //    // For more information, visit http://go.microsoft.com/fwlink/?LinkId=301869.
        //    //config.EnableQuerySupport();

        //    // To disable tracing in your application, please comment out or remove the following line of code
        //    // For more information, refer to: http://www.asp.net/web-api
        //    config.EnableSystemDiagnosticsTracing();
        //}

        public static void Register(HttpConfiguration config)
        {
            //remove application/x-www-form-urlencoded formatter.
            //var formatter = config.Formatters.FormUrlEncodedFormatter;
            //config.Formatters.Remove(formatter);

            //Handle all 4 possible WCF fault contracts (basically any exception that can be thrown from the Client facade)
            config.Filters.Add(new UnhandledExceptionFilterAttribute()
                    .Register<System.ServiceModel.FaultException<DTO.FaultContracts.AuthorizationFaultContract>>((exception, request) =>
                    {
                        System.ServiceModel.FaultException<DTO.FaultContracts.AuthorizationFaultContract> e = (System.ServiceModel.FaultException<DTO.FaultContracts.AuthorizationFaultContract>)exception;

                        ErrorResponse error = new ErrorResponse();
                        error.Message = e.Detail.Reason.ToString() + ": \"" + e.Detail.ActingUserName +
                                        "\" is not authorized for \"" + e.Detail.RequestedMethod +
                                        "\".  Only specific user(s)[" + e.Detail.RequiredUser + "] or users in role(s)[" +
                                        e.Detail.RequiredRole + "] or administrators are allowed.";

                        var response = request.CreateResponse(HttpStatusCode.Forbidden, error);
                        response.ReasonPhrase = e.Detail.Reason.ToString();
                        return response;
                    })
                    .Register<System.ServiceModel.FaultException<DTO.FaultContracts.InvalidOperationFaultContract>>((exception, request) =>
                    {
                        System.ServiceModel.FaultException<DTO.FaultContracts.InvalidOperationFaultContract> e = (System.ServiceModel.FaultException<DTO.FaultContracts.InvalidOperationFaultContract>)exception;

                        ErrorResponse error = new ErrorResponse();
                        error.Message = e.Detail.Reason.ToString();

                        var response = request.CreateResponse(HttpStatusCode.BadRequest, error);
                        response.ReasonPhrase = e.Detail.Reason.ToString();

                        return response;
                    })
                    .Register<System.ServiceModel.FaultException<DTO.FaultContracts.InvalidArgumentFaultContract>>((exception, request) =>
                    {
                        System.ServiceModel.FaultException<DTO.FaultContracts.InvalidArgumentFaultContract> e = (System.ServiceModel.FaultException<DTO.FaultContracts.InvalidArgumentFaultContract>)exception;

                        ErrorResponse error = new ErrorResponse();
                        error.Message = e.Detail.Reason.ToString();

                        var response = request.CreateResponse(HttpStatusCode.BadRequest, error);
                        response.ReasonPhrase = e.Detail.Reason.ToString();

                        return response;
                    })
                    .Register<System.ServiceModel.FaultException<DTO.FaultContracts.ValidationFaultContract>>((exception, request) =>
                    {
                        System.ServiceModel.FaultException<DTO.FaultContracts.ValidationFaultContract> e = (System.ServiceModel.FaultException<DTO.FaultContracts.ValidationFaultContract>)exception;

                        ErrorResponse error = new ErrorResponse();
                        error.Message = "Validation Issue(s)";
                        error.Details = new string[e.Detail.ValidationIssues.Count];
                        for (int i = 0; i < error.Details.Length; i++)
                        {
                            error.Details[i] = "For: \"" + e.Detail.ValidationIssues[i].Key + "\", \"" + e.Detail.ValidationIssues[i].Message + "\"";
                        }

                        var response = request.CreateResponse(HttpStatusCode.BadRequest, error);
                        response.ReasonPhrase = e.Detail.Message;

                        return response;
                    })
                    .Register<SecurityException>((exception, request) =>
                    {
                        HttpResponseMessage response = request.CreateErrorResponse(HttpStatusCode.Unauthorized, "Unauthorized Access Attempt");
                        switch (ConfigurationManager.AppSettings["WebAPIAuthScheme"])
                        {
                            case "RWX_BASIC":
                                response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue("RWX_BASIC"));
                                break;
                            case "RWX_SECURE":
                                response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue("RWX_SECURE"));
                                break;
                        }                                                       
                        return response;
                    }));

            //provides method override services for facilities such as flash
            config.MessageHandlers.Add(new APIEnabled());
            config.MessageHandlers.Add(new OverrideMethod());
            config.MessageHandlers.Add(new OverrideDate());
            switch (ConfigurationManager.AppSettings["WebAPIAuthScheme"])
            {
                case "RWX_BASIC":
                    config.MessageHandlers.Add(new BasicVerifySignature());
                    break;
                case "RWX_SECURE":
                    config.MessageHandlers.Add(new SecureVerifySignature());
                    break;
            }            

            config.MapHttpAttributeRoutes();

            //CategoryController.RegisterRoutes(config);
            //MediaController.RegisterRoutes(config);
            //CustomFieldController.RegisterRoutes(config);
            //ListingController.RegisterRoutes(config);            

            //default
            //config.Routes.MapHttpRoute(
            //    name: "DefaultApi",
            //    routeTemplate: "api/{controller}/{id}",
            //    defaults: new { id = RouteParameter.Optional }
            //);

            // To disable tracing in your application, please comment out or remove the following line of code
            // For more information, refer to: http://www.asp.net/web-api
            //config.EnableSystemDiagnosticsTracing();

            bool enableCORS = false;
            bool.TryParse(ConfigurationManager.AppSettings["API_CORS_Enabled"], out enableCORS);
            if (enableCORS)
            {
                string corsOrigin = "*";
                if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["API_CORS_AllowedOrigins"]))
                {
                    corsOrigin = ConfigurationManager.AppSettings["API_CORS_AllowedOrigins"];
                }
                config.EnableCors(new EnableCorsAttribute(origins: corsOrigin, headers: "*", methods: "GET"));
            }

        }
    }
}
