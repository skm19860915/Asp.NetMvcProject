using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.Utility;
using RainWorx.FrameWorx.MVC.Helpers;

namespace RainWorx.FrameWorx.MVC.Models
{
    public abstract class ModelStateTempDataTransfer : ActionFilterAttribute
    {
        protected static readonly string Key = typeof(ModelStateTempDataTransfer).FullName;
    }

    //public class ExportModelStateToTempData : ModelStateTempDataTransfer
    //{
    //    public override void OnActionExecuted(ActionExecutedContext filterContext)
    //    {
    //        //Only export when ModelState is not valid
    //        if (!filterContext.Controller.ViewData.ModelState.IsValid)
    //        {
    //            //Export if we are redirecting
    //            if ((filterContext.Result is RedirectResult) || (filterContext.Result is RedirectToRouteResult))
    //            {
    //                filterContext.Controller.TempData[Key] = filterContext.Controller.ViewData.ModelState;
    //            }
    //        }

    //        base.OnActionExecuted(filterContext);
    //    }
    //}

    //public class ImportModelStateFromTempData : ModelStateTempDataTransfer
    //{
    //    public override void OnActionExecuted(ActionExecutedContext filterContext)
    //    {
    //        ModelStateDictionary modelState = filterContext.Controller.TempData[Key] as ModelStateDictionary;

    //        if (modelState != null)
    //        {
    //            //Only Import if we are viewing
    //            if (filterContext.Result is ViewResult)
    //            {
    //                filterContext.Controller.ViewData.ModelState.Merge(modelState);
    //            }
    //            else
    //            {
    //                //Otherwise remove it.
    //                filterContext.Controller.TempData.Remove(Key);
    //            }
    //        }

    //        base.OnActionExecuted(filterContext);
    //    }
    //}


    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class GoUnsecure : FilterAttribute, IAuthorizationFilter
    {
        public static bool EnableSSL = bool.Parse(ConfigurationManager.AppSettings["EnableSSL"]);
        public static bool AlwaysSSL = bool.Parse(ConfigurationManager.AppSettings.AllKeys.Contains("AlwaysSSL") ? ConfigurationManager.AppSettings["AlwaysSSL"] : "False");

        public void OnAuthorization(AuthorizationContext filterContext)
        {
            if (!EnableSSL && !AlwaysSSL)
            {                
                return;
            }

            // abort if it's not a secure connection AND AlwaysSSL is false
            if (!AlwaysSSL && !filterContext.HttpContext.Request.IsSecureConnection) return;

            // abort if it's a secure connection AND AlwaysSSL is true
            if (AlwaysSSL && filterContext.HttpContext.Request.IsSecureConnection) return;

            // abort if a [GoSecure] attribute is applied to action
            //if (filterContext.ActionDescriptor.ControllerDescriptor.GetCustomAttributes(typeof(GoSecure), true).Length > 0) return;
            if (filterContext.ActionDescriptor.GetCustomAttributes(typeof(GoSecure), true).Length > 0) return;

            //// abort if a [RetainHttps] attribute is applied to controller or action  
            //if (filterContext.ActionDescriptor.ControllerDescriptor.GetCustomAttributes(typeof(RetainHttpsAttribute), true).Length > 0) return;
            //if (filterContext.ActionDescriptor.GetCustomAttributes(typeof(RetainHttpsAttribute), true).Length > 0) return;

            // abort if it's not a GET request - we don't want to be redirecting on a form post  
            if (!String.Equals(filterContext.HttpContext.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)) return;

            if (AlwaysSSL && !filterContext.HttpContext.Request.IsSecureConnection)
            {
                // redirect to HTTPS              
                Uri urlBuilder = new Uri(SiteClient.Settings[Strings.SiteProperties.SecureURL]);

                //abort if "SecureURL" is not an "HTTPS" value
                if (!urlBuilder.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)) return;

                string url = urlBuilder.Scheme + "://" + urlBuilder.Authority + filterContext.HttpContext.Request.RawUrl;
                filterContext.Result = new RedirectResult(url);
            }
            else
            {
                // redirect to HTTP  
                Uri urlBuilder = new Uri(SiteClient.Settings[Strings.SiteProperties.URL]);

                //abort if "URL" is not an "HTTP" value
                if (!urlBuilder.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)) return;

                string url = urlBuilder.Scheme + "://" + urlBuilder.Authority + filterContext.HttpContext.Request.RawUrl;
                filterContext.Result = new RedirectResult(url);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class GoSecure : FilterAttribute, IAuthorizationFilter
    {
        public static bool EnableSSL = bool.Parse(ConfigurationManager.AppSettings["EnableSSL"]);
        public static bool AlwaysSSL = bool.Parse(ConfigurationManager.AppSettings.AllKeys.Contains("AlwaysSSL") ? ConfigurationManager.AppSettings["AlwaysSSL"] : "False");

        public void OnAuthorization(AuthorizationContext filterContext)
        {
            if (!EnableSSL && !AlwaysSSL)
            {                
                return;
            }

            // abort if it is a secure connection  
            if (filterContext.HttpContext.Request.IsSecureConnection) return;

            if (!AlwaysSSL) // skip these checks if the AlwaysSSL is true
            {
                // abort if a [GoUnsecure] attribute is applied to action
                //if (filterContext.ActionDescriptor.ControllerDescriptor.GetCustomAttributes(typeof(GoUnsecure), true).Length > 0) return;
                if (filterContext.ActionDescriptor.GetCustomAttributes(typeof(GoUnsecure), true).Length > 0) return;

                // abort if a [RequireHttps] attribute is applied to controller or action  
                //if (filterContext.ActionDescriptor.ControllerDescriptor.GetCustomAttributes(typeof(RequireHttpsAttribute), true).Length > 0) return;
                //if (filterContext.ActionDescriptor.GetCustomAttributes(typeof(RequireHttpsAttribute), true).Length > 0) return;

                // abort if a [RetainHttps] attribute is applied to controller or action  
                //if (filterContext.ActionDescriptor.ControllerDescriptor.GetCustomAttributes(typeof(RetainHttpsAttribute), true).Length > 0) return;
                //if (filterContext.ActionDescriptor.GetCustomAttributes(typeof(RetainHttpsAttribute), true).Length > 0) return;
            }

            // abort if it's not a GET request - we don't want to be redirecting on a form post  
            if (!String.Equals(filterContext.HttpContext.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)) return;

            // redirect to HTTPS              
            Uri urlBuilder = new Uri(SiteClient.Settings[Strings.SiteProperties.SecureURL]);

            //abort if "SecureURL" is not an "HTTPS" value
            if (!urlBuilder.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)) return;

            string url = urlBuilder.Scheme + "://" + urlBuilder.Authority + filterContext.HttpContext.Request.RawUrl;
            filterContext.Result = new RedirectResult(url);         
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class Authenticate : FilterAttribute, IAuthorizationFilter
    {        
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            //check for skip
            ActionDescriptor action = filterContext.ActionDescriptor;
            bool isNoAuthenticate = action.GetCustomAttributes(typeof(NoAuthenticate), true).Any();
            if (isNoAuthenticate) return;

            if (SiteClient.MaintenanceMode)
            {                
                if (filterContext.HttpContext.User.IsInRole(Strings.Roles.Admin))
                {
                    return;
                }
                else
                {
                    // redirect to Maintenance CMS                          
                    filterContext.Result = new RedirectResult(SiteClient.Settings[Strings.SiteProperties.URL] + "/Home/Maintenance");        
                }
            }
            else
            {
                if (!SiteClient.RequireAuthentication) return;

                if (filterContext.HttpContext.User.Identity.IsAuthenticated) return;

                // redirect to login                          
                filterContext.Result = new RedirectResult(SiteClient.Settings[Strings.SiteProperties.SecureURL] + "/Account/LogOn?ReturnUrl=" + filterContext.HttpContext.Request.RawUrl.Replace("/default.aspx?", "/"));    
            }                        
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class NoAuthenticate : FilterAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            return;            
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class ActiveUserChecking : FilterAttribute, IAuthorizationFilter
    {
        public static bool StrictActiveUserChecking = bool.Parse(ConfigurationManager.AppSettings.AllKeys.Contains("StrictActiveUserChecking")
            ? ConfigurationManager.AppSettings["StrictActiveUserChecking"] : "False");

        public void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                //check for skip
                ActionDescriptor action = filterContext.ActionDescriptor;
                bool isSkipActiveUserCheck = action.GetCustomAttributes(typeof(SkipActiveUserCheck), true).Any();
                if (isSkipActiveUserCheck) return;

                if (StrictActiveUserChecking && !UserClient.IsActiveUser(filterContext.HttpContext.User.Identity.Name))
                {
                    if (filterContext.ActionDescriptor.ActionName.ToLower() != "LogOff".ToLower())
                    {
                        // redirect to logoff
                        string logoffUri = SiteClient.Settings[Strings.SiteProperties.SecureURL];
                        if (logoffUri.Right(1) != "/") logoffUri += "/";
                        logoffUri += "Account/LogOff";
                        filterContext.Result = new RedirectResult(logoffUri);
                    }
                }
            }
            return;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class SkipActiveUserCheck : FilterAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            return;
        }
    }

    //public class LogTraffic : ActionFilterAttribute
    //{
    //    private static bool? trafficLoggingEnabled = null;
    //    private Stopwatch stopWatch = null;

    //    public LogTraffic()
    //    {
    //        if (trafficLoggingEnabled == null)
    //        {
    //            bool temp1;
    //            if (bool.TryParse(ConfigurationManager.AppSettings["Log_MVC_Actions"], out temp1))
    //            {
    //                trafficLoggingEnabled = temp1;
    //            }
    //        }
    //    }

    //    public override void OnActionExecuting(ActionExecutingContext filterContext)
    //    {
    //        if (!trafficLoggingEnabled ?? false) return;

    //        //check for skip
    //        ActionDescriptor action = filterContext.ActionDescriptor;
    //        bool isNoLoggingAction = action.GetCustomAttributes(typeof(NoTrafficLogging), true).Any();
    //        if (isNoLoggingAction) return;

    //        stopWatch = new Stopwatch();
    //        stopWatch.Start();
    //    }

    //    public override void OnResultExecuted(ResultExecutedContext filterContext)
    //    {
    //        if (stopWatch == null) return;

    //        stopWatch.Stop();
    //        long executionMS = stopWatch.ElapsedMilliseconds;

    //        //generate log entry
    //        var controller = filterContext.RequestContext.RouteData.Values["controller"];
    //        var action = filterContext.RequestContext.RouteData.Values["action"];
    //        var title = "/" + action;

    //        string message = string.Empty;
    //        foreach(string key in filterContext.RequestContext.RouteData.Values.Keys.Where(
    //            k => k != null && 
    //            !k.Equals("controller", StringComparison.OrdinalIgnoreCase) && 
    //            !k.Equals("action", StringComparison.OrdinalIgnoreCase)))
    //        {
    //            string routeValue = filterContext.RequestContext.RouteData.Values[key].ToString();
    //            if (!string.IsNullOrEmpty(routeValue)) message += "/" + routeValue;
    //        }
    //        var queryString = filterContext.HttpContext.Request.QueryString.ToString();
    //        message += string.IsNullOrEmpty(queryString) ? "" : "?" + queryString.Replace("&", " &");

    //        var functionalArea = "Traffic: /" + controller;
    //        var actor = filterContext.HttpContext.User.Identity.IsAuthenticated ? filterContext.HttpContext.User.Identity.Name : "";
    //        var logProperties = new Dictionary<string, object>()
    //        {
    //            { "HttpMethod", filterContext.HttpContext.Request.HttpMethod },
    //            { "HttpStatus", filterContext.HttpContext.Response?.StatusCode.ToString() ?? "" },
    //            { "executionMS", executionMS }
    //        };
    //        var machineName = filterContext.HttpContext.Request.UserHostAddress;
    //        LogManager.WriteLog(message, title, functionalArea, TraceEventType.Verbose, actor, null, logProperties, 0, 0, machineName);
    //    }
    //}

    //public class NoTrafficLogging : ActionFilterAttribute
    //{
    //    public override void OnActionExecuting(ActionExecutingContext filterContext)
    //    {
    //        return;
    //    }
    //}

    public class LogStats : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            if (!(context.Result is ViewResult))
            {
                context.LogPageRenderStats();

            }
        }
    }

}