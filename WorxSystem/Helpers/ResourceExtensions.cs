using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Compilation;
using System.Web.Mvc;
using System;
using System.Web.WebPages;
using RainWorx.FrameWorx.Clients;
using System.Text;
using System.Linq;
using RainWorx.FrameWorx.Utility;
using RainWorx.FrameWorx.DTO;

namespace RainWorx.FrameWorx.MVC.Helpers
{
    /// <summary>
    /// Provides methods for accessing RESX resources
    /// </summary>
    public static class ResourceExtensions
    {

        #region resource helpers

        //private static bool? _Trace = null;

        private static bool TraceEnabled(Controller controller)
        {
            //if (!_Trace.HasValue)
            //{
            //_Trace = (controller.GlobalResourceString("_trace") == "1");
            //}
            //return _Trace.Value;
            object cacheItem = HttpContext.Current != null ? HttpContext.Current.Cache["resourcetracing"] : null;
            bool retVal;
            if (cacheItem == null)
            {
                retVal = (controller.GlobalResourceString("_trace") == "1");
                if (HttpContext.Current != null)
                {
                    HttpContext.Current.Cache.Insert("resourcetracing", retVal);
                }
            }
            else
            {
                retVal = (bool)cacheItem;
            }
            return retVal;
        }

        private static bool TraceEnabled(HtmlHelper htmlHelper)
        {
            //if (!_Trace.HasValue)
            //{
            //    _Trace = (htmlHelper.GlobalResourceString("_trace") == "1");
            //}
            //return _Trace.Value;
            object cacheItem = HttpContext.Current != null ? HttpContext.Current.Cache["resourcetracing"] : null;
            bool retVal;
            if (cacheItem == null)
            {
                retVal = (htmlHelper.GlobalResourceString("_trace") == "1");
                HttpContext.Current.Cache.Insert("resourcetracing", retVal);
            }
            else
            {
                retVal = (bool)cacheItem;
            }
            return retVal;
        }

        private static string CurrentCulture(this Controller controller)
        {
            if (controller.GetCookie(Strings.MVC.CultureCookie) == null) return SiteClient.SiteCulture;
            else return controller.GetCookie(Strings.MVC.CultureCookie);
        }

        private static string CurrentCulture(this HtmlHelper htmlHelper)
        {
            if (htmlHelper.GetCookie(Strings.MVC.CultureCookie) == null) return SiteClient.SiteCulture;
            else return htmlHelper.GetCookie(Strings.MVC.CultureCookie);
        }

        /// <summary>
        /// Converts the specified expression and path to resource expression fields
        /// </summary>
        /// <param name="expression">a string value which specifies the resource to retrieve</param>
        /// <param name="virtualPath">the path where the RESX resources are located</param>
        /// <returns></returns>
        static ResourceExpressionFields GetResourceFields(string expression, string virtualPath)
        {
            var context = new ExpressionBuilderContext(virtualPath);
            var builder = new ResourceExpressionBuilder();
            return (ResourceExpressionFields)builder.ParseExpression(expression, typeof(string), context);
        }

        /// <summary>
        /// Retrieves a resource string based on the specified expression
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="expression">a string value which specifies the resource to retrieve</param>
        /// <param name="args">optional arguments that will be dynamically inserted into the resoure retrieved if applicable</param>
        public static string ResourceString(this HtmlHelper htmlHelper, string expression, params object[] args)
        {
            try
            {
                ResourceExpressionFields fields = GetResourceFields(expression, Strings.MVC.ResourcePath);
                return GetGlobalResourceString(htmlHelper, fields, args);
            }
            catch (Exception)
            {
                if (expression != "_trace" && TraceEnabled(htmlHelper))
                {
                    //List<string> tokens = (List<string>)htmlHelper.ViewContext.HttpContext.Application["ToLocalize"];
                    //if (!tokens.Contains(expression)) tokens.Add(expression);
                    return (Strings.MVC.ResourceMissingPrefix + expression + Strings.MVC.ResourceMissingSuffix);
                }
                else
                {
                    return expression;
                }
            }
        }

        /// <summary>
        /// Retrieves a resource string based on the specified expression
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="expression">a string value which specifies the resource to retrieve</param>
        /// <param name="args">optional arguments that will be dynamically inserted into the resoure retrieved if applicable</param>
        public static string ResourceString(this Controller controller, string expression, params object[] args)
        {
            try
            {
                ResourceExpressionFields fields = GetResourceFields(expression, Strings.MVC.ResourcePath);
                return GetGlobalResourceString(controller, fields, args);
            }
            catch (Exception)
            {
                if (expression != "_trace" && TraceEnabled(controller))
                {
                    //List<string> tokens = (List<string>)htmlHelper.ViewContext.HttpContext.Application["ToLocalize"];
                    //if (!tokens.Contains(expression)) tokens.Add(expression);
                    return (Strings.MVC.ResourceMissingPrefix + expression + Strings.MVC.ResourceMissingSuffix);
                }
                else
                {
                    return expression;
                }
            }
        }

        /// <summary>
        /// Retrieves a resource string based on the specified expression, converted to an MvcHtmlString
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="expression">a string value which specifies the resource to retrieve</param>
        /// <param name="args">optional arguments that will be dynamically inserted into the resoure retrieved if applicable</param>
        public static MvcHtmlString Resource(this HtmlHelper htmlHelper, string expression, params object[] args)
        {
            return htmlHelper.ResourceString(expression, args).ToMvcHtmlString();
        }


        /// <summary>
        /// Retrieves a resource string based on the specified expression, or null if the key does not exist
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="expression">a string value which specifies the resource to retrieve</param>
        /// <param name="args">optional arguments that will be dynamically inserted into the resoure retrieved if applicable</param>
        public static string ResourceOrDefaultString(this HtmlHelper htmlHelper, string expression, params object[] args)
        {
            try
            {
                ResourceExpressionFields fields = GetResourceFields(expression, Strings.MVC.ResourcePath);
                return GetGlobalResourceString(htmlHelper, fields, args);
            }
            catch (Exception)
            {
                //note - in this particular method missing strings are considered normal
                //if (expression != "_trace" && TraceEnabled(htmlHelper))
                //{
                //    //List<string> tokens = (List<string>)htmlHelper.ViewContext.HttpContext.Application["ToLocalize"];
                //    //if (!tokens.Contains(expression)) tokens.Add(expression);
                //    return (Strings.MVC.ResourceMissingPrefix + expression + Strings.MVC.ResourceMissingSuffix);
                //}
                //else
                //{
                //    return expression;
                //}
                return null;
            }
        }

        /// <summary>
        /// Retrieves a resource string based on the specified expression, or null if the key does not exist, converted to an MvcHtmlString
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="expression">a string value which specifies the resource to retrieve</param>
        /// <param name="args">optional arguments that will be dynamically inserted into the resoure retrieved if applicable</param>
        public static MvcHtmlString ResourceOrDefault(this HtmlHelper htmlHelper, string expression, params object[] args)
        {
            return htmlHelper.ResourceOrDefaultString(expression, args).ToMvcHtmlString();
        }

        /// <summary>
        /// Retrieves a resource string based on the specified fields, converted to an MvcHtmlString
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="fields">resource expression fields</param>
        /// <param name="args">optional arguments that will be dynamically inserted into the resoure retrieved if applicable</param>
        static MvcHtmlString GetGlobalResource(HtmlHelper htmlHelper, ResourceExpressionFields fields, object[] args)
        {
            return GetGlobalResourceString(htmlHelper, fields, args).ToMvcHtmlString();
        }

        /// <summary>
        /// Retrieves a resource string based on the specified fields
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="fields">resource expression fields</param>
        /// <param name="args">optional arguments that will be dynamically inserted into the resoure retrieved if applicable</param>
        static string GetGlobalResourceString(HtmlHelper htmlHelper, ResourceExpressionFields fields, object[] args)
        {
            if (args.Length > 0)
                return
                    string.Format(
                        (string)
                            HttpContext.GetGlobalResourceObject(fields.ClassKey, fields.ResourceKey,
                                CultureInfo.GetCultureInfo(CurrentCulture(htmlHelper))), args);
            else
                return
                    (string)
                        HttpContext.GetGlobalResourceObject(fields.ClassKey, fields.ResourceKey,
                            CultureInfo.GetCultureInfo(CurrentCulture(htmlHelper)));
        }

        /// <summary>
        /// Retrieves a resource string based on the specified fields, converted to an MvcHtmlString
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="fields">resource expression fields</param>
        /// <param name="args">optional arguments that will be dynamically inserted into the resoure retrieved if applicable</param>
        static string GetGlobalResourceString(Controller controller, ResourceExpressionFields fields, object[] args)
        {
            if (args.Length > 0)
                return
                    string.Format(
                        (string)
                            HttpContext.GetGlobalResourceObject(fields.ClassKey, fields.ResourceKey,
                                CultureInfo.GetCultureInfo(CurrentCulture(controller))), args);
            else
                return
                    (string)
                        HttpContext.GetGlobalResourceObject(fields.ClassKey, fields.ResourceKey,
                            CultureInfo.GetCultureInfo(CurrentCulture(controller)));
        }

        #endregion

        #region Countries & States

        private static Dictionary<string, string> _localizedCountries = new Dictionary<string, string>();

        public static void FlushCountriesAndStates(this Controller controller)
        {
            _localizedCountries = new Dictionary<string, string>();
            _localizedCountryLists = new Dictionary<string, List<Country>>();
            _localizedStates = new Dictionary<string, string>();
        }

        public static string LocalizeCountry(this HtmlHelper htmlHelper, string country)
        {
            string currentCulture = CurrentCulture(htmlHelper);
            string retVal = string.Empty;
            if (_localizedCountries.TryGetValue(currentCulture + "|" + country, out retVal)) return retVal;

            string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_1234567890";

            StringBuilder sb = new StringBuilder();
            foreach (char c in country)
            {
                if (validChars.Contains(c))
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append('_');
                }
            }

            
            retVal = (string) HttpContext.GetGlobalResourceObject(Strings.MVC.Countries_Resource, sb.ToString(),
                                                            CultureInfo.GetCultureInfo(currentCulture)) ?? country;            

            _localizedCountries.Add(currentCulture + "|" + country, retVal);
            return retVal;
        }
        public static string LocalizeCountry(this Controller controller, string country)
        {
            string currentCulture = CurrentCulture(controller);
            string retVal = string.Empty;
            string cachekey = currentCulture + "|" + country;
            if (_localizedCountries.TryGetValue(cachekey, out retVal)) return retVal;

            string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_1234567890";

            StringBuilder sb = new StringBuilder();
            foreach (char c in country)
            {
                if (validChars.Contains(c))
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append('_');
                }
            }

            retVal = (string)HttpContext.GetGlobalResourceObject(Strings.MVC.Countries_Resource, sb.ToString(),
                                                            CultureInfo.GetCultureInfo(currentCulture)) ?? country;

            if (!_localizedCountries.ContainsKey(cachekey)) _localizedCountries.Add(currentCulture + "|" + country, retVal);

            return retVal;
        }

        private static Dictionary<string, string> _localizedStates = new Dictionary<string, string>();
        public static string LocalizeState(this HtmlHelper htmlHelper, string state)
        {
            string currentCulture = CurrentCulture(htmlHelper);
            string retVal = string.Empty;
            if (_localizedStates.TryGetValue(currentCulture + "|" + state, out retVal)) return retVal;

            string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_1234567890";

            StringBuilder sb = new StringBuilder();
            foreach (char c in state)
            {
                if (validChars.Contains(c))
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append('_');
                }
            }
            
            retVal = (string)HttpContext.GetGlobalResourceObject(Strings.MVC.States_Resource, sb.ToString(),
                    CultureInfo.GetCultureInfo(currentCulture)) ?? state;

            _localizedStates.Add(currentCulture + "|" + state, retVal);
            return retVal;
        }
        public static string LocalizeState(this Controller controller, string state)
        {
            string currentCulture = CurrentCulture(controller);
            string retVal = string.Empty;
            if (_localizedStates.TryGetValue(currentCulture + "|" + state, out retVal)) return retVal;

            string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_1234567890";

            StringBuilder sb = new StringBuilder();
            foreach (char c in state)
            {
                if (validChars.Contains(c))
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append('_');
                }
            }

            retVal = (string)HttpContext.GetGlobalResourceObject(Strings.MVC.States_Resource, sb.ToString(),
                    CultureInfo.GetCultureInfo(currentCulture)) ?? state;

            _localizedStates.Add(currentCulture + "|" + state, retVal);
            return retVal;        
        }

        public static State Clone(this State state)
        {
            return new State
            {
                Code = state.Code,
                CountryID = state.CountryID,
                Enabled = state.Enabled,
                ID = state.ID,
                Name = state.Name
            };            
        }

        public static Country Clone(this Country country)
        {            
            return new Country
            {
                Enabled = country.Enabled,
                ID = country.ID,
                Name = country.Name,
                Code = country.Code,
                StateRequired = country.StateRequired
            };
        }

        private static Dictionary<string, List<Country>> _localizedCountryLists = new Dictionary<string, List<Country>>();

        public static List<Country> Countries(this Controller controller)
        {
            string currentCulture = CurrentCulture(controller);
            List<Country> retVal = null;
            if (_localizedCountryLists.TryGetValue(currentCulture, out retVal)) return retVal;

            var defaultCountryId = SiteClient.IntSetting(Strings.SiteProperties.SiteDefaultCountry);
            var results = new List<Country>();
            if (SiteClient.Countries.Any(c => c.ID == defaultCountryId))
            {
                results.Add(SiteClient.Countries.First(c => c.ID == defaultCountryId));
            }
            results.AddRange(SiteClient.Countries.Where(c => c.Enabled && c.ID != defaultCountryId).OrderBy(c => c.ID));

            List<Country> localizedCountries = new List<Country>(results.Count);
            foreach (Country country in results)
            {
                Country clone = country.Clone();
                clone.Name = LocalizeCountry(controller, clone.Name);
                localizedCountries.Add(clone);
            }

            _localizedCountryLists.Add(currentCulture, localizedCountries);
            return localizedCountries;
        }

        public static List<Country> Countries(this HtmlHelper htmlHelper)
        {            
            var defaultCountryId = SiteClient.IntSetting(Strings.SiteProperties.SiteDefaultCountry);
            var results = new List<Country>();
            if (SiteClient.Countries.Any(c => c.ID == defaultCountryId))
            {
                results.Add(SiteClient.Countries.First(c => c.ID == defaultCountryId));
            }
            results.AddRange(SiteClient.Countries.Where(c => c.Enabled && c.ID != defaultCountryId).OrderBy(c => c.ID));

            List<Country> localizedCountries = new List<Country>(results.Count);
            foreach (Country country in results)
            {
                Country clone = country.Clone();
                clone.Name = LocalizeCountry(htmlHelper, clone.Name);
                localizedCountries.Add(clone);
            }
            
            return localizedCountries;            
        }

        #endregion

        #region GlobalStrings

        public static string GlobalResourceString(this Controller controller, string resourceKey, params object[] args)
        {
            try
            {
                string resourceRetrieved = (string)
                        HttpContext.GetGlobalResourceObject(Strings.MVC.GlobalStrings_Resource, resourceKey,
                                                            CultureInfo.GetCultureInfo(
                                                                CurrentCulture(controller)));
                if (resourceRetrieved == null)
                {
                    if (resourceKey != "_trace" && TraceEnabled(controller))
                    {
                        //List<string> tokens = (List<string>)controller.HttpContext.Application["ToLocalize"];
                        //if (!tokens.Contains(Strings.MVC.GlobalStrings_Resource + ", " + resourceKey)) tokens.Add(Strings.MVC.GlobalStrings_Resource + ", " + resourceKey);
                        return Strings.MVC.ResourceMissingPrefix + Strings.MVC.GlobalStrings_Resource + ", " + resourceKey + Strings.MVC.ResourceMissingSuffix;
                    }
                    else
                    {
                        return string.Format(resourceKey, args);
                    }
                }

                return string.Format(resourceRetrieved, args);
            }
            catch
            {
                if (resourceKey != "_trace" && TraceEnabled(controller))
                {
                    //List<string> tokens = (List<string>)controller.HttpContext.Application["ToLocalize"];
                    //if (!tokens.Contains(Strings.MVC.GlobalStrings_Resource + ", " + resourceKey)) tokens.Add(Strings.MVC.GlobalStrings_Resource + ", " + resourceKey);
                    return Strings.MVC.ResourceMissingPrefix + Strings.MVC.GlobalStrings_Resource + ", " + resourceKey + Strings.MVC.ResourceMissingSuffix;
                }
                else
                {
                    try
                    {
                        return string.Format(resourceKey ?? string.Empty, args);
                    }
                    catch
                    {
                        return resourceKey ?? string.Empty;
                    }
                }
            }
        }

        public static MvcHtmlString GlobalResource(this Controller controller, string resourceKey, params object[] args)
        {
            return controller.GlobalResourceString(resourceKey, args).ToMvcHtmlString();
        }

        public static string GlobalResourceString(this HtmlHelper htmlHelper, string resourceKey, params object[] args)
        {
            try
            {
                string resourceRetrieved = (string)
                        HttpContext.GetGlobalResourceObject(Strings.MVC.GlobalStrings_Resource, resourceKey,
                                                            CultureInfo.GetCultureInfo(
                                                                CurrentCulture(htmlHelper)));
                if (resourceRetrieved == null)
                {
                    if (resourceKey != "_trace" && TraceEnabled(htmlHelper))
                    {
                        //List<string> tokens = (List<string>)htmlHelper.ViewContext.HttpContext.Application["ToLocalize"];
                        //if (!tokens.Contains(Strings.MVC.GlobalStrings_Resource + ", " + resourceKey)) tokens.Add(Strings.MVC.GlobalStrings_Resource + ", " + resourceKey);
                        return Strings.MVC.ResourceMissingPrefix + Strings.MVC.GlobalStrings_Resource + ", " + resourceKey + Strings.MVC.ResourceMissingSuffix;
                    }
                    else
                    {
                        return string.Format(resourceKey, args);
                    }
                }

                return string.Format(resourceRetrieved, args);
            }
            catch
            {
                if (resourceKey != "_trace" && TraceEnabled(htmlHelper))
                {
                    //List<string> tokens = (List<string>)htmlHelper.ViewContext.HttpContext.Application["ToLocalize"];
                    //if (!tokens.Contains(Strings.MVC.GlobalStrings_Resource + ", " + resourceKey)) tokens.Add(Strings.MVC.GlobalStrings_Resource + ", " + resourceKey);
                    return Strings.MVC.ResourceMissingPrefix + Strings.MVC.GlobalStrings_Resource + ", " + resourceKey + Strings.MVC.ResourceMissingSuffix;
                }
                else
                {
                    try
                    {
                        return string.Format(resourceKey ?? string.Empty, args);
                    }
                    catch
                    {
                        return resourceKey ?? string.Empty;
                    }
                }
            }
        }

        public static MvcHtmlString GlobalResource(this HtmlHelper htmlHelper, string resourceKey, params object[] args)
        {
            return htmlHelper.GlobalResourceString(resourceKey, args).ToMvcHtmlString();
        }

        #endregion

        #region CustomFields

        public static string CustomFieldResourceString(this HtmlHelper htmlHelper, string resourceKey, string culture, params object[] args)
        {
            try
            {
                //generate resource key to attempt
                resourceKey = resourceKey ?? string.Empty;
                Regex r = new Regex(@"[^a-zA-Z0-9_]");
                string attemptKey = r.Replace(resourceKey, "_");
                if (attemptKey[0] >= '0' && attemptKey[0] <= '9')
                {
                    attemptKey = "_" + attemptKey;
                }

                return
                    string.Format(
                        (string)
                        HttpContext.GetGlobalResourceObject(Strings.MVC.CustomFieldStrings_Resource, attemptKey,
                                                            CultureInfo.GetCultureInfo(culture)), args);
            }
            catch
            {
                if (resourceKey != "_trace" && TraceEnabled(htmlHelper))
                {
                    //List<string> tokens = (List<string>)htmlHelper.ViewContext.HttpContext.Application["ToLocalize"];
                    //if (!tokens.Contains(Strings.MVC.CustomFieldStrings_Resource + ", " + resourceKey)) tokens.Add(Strings.MVC.GlobalStrings_Resource + ", " + resourceKey);
                    return Strings.MVC.ResourceMissingPrefix + Strings.MVC.CustomFieldStrings_Resource + ", " + resourceKey + Strings.MVC.ResourceMissingSuffix;
                }
                else
                {
                    try
                    {
                        return string.Format(resourceKey, args);
                    }
                    catch
                    {
                        return resourceKey;
                    }
                }
            }
        }

        public static string CustomFieldResourceString(this HtmlHelper htmlHelper, string resourceKey, params object[] args)
        {
            try
            {
                //generate resource key to attempt
                resourceKey = resourceKey ?? string.Empty;
                Regex r = new Regex(@"[^a-zA-Z0-9_]");
                string attemptKey = r.Replace(resourceKey, "_");
                if (attemptKey[0] >= '0' && attemptKey[0] <= '9')
                {
                    attemptKey = "_" + attemptKey;
                }

                return
                    string.Format(
                        (string)
                        HttpContext.GetGlobalResourceObject(Strings.MVC.CustomFieldStrings_Resource, attemptKey,
                                                            CultureInfo.GetCultureInfo(
                                                                CurrentCulture(htmlHelper))), args);
            }
            catch
            {
                if (resourceKey != "_trace" && TraceEnabled(htmlHelper))
                {
                    //List<string> tokens = (List<string>)htmlHelper.ViewContext.HttpContext.Application["ToLocalize"];
                    //if (!tokens.Contains(Strings.MVC.CustomFieldStrings_Resource + ", " + resourceKey)) tokens.Add(Strings.MVC.GlobalStrings_Resource + ", " + resourceKey);
                    return Strings.MVC.ResourceMissingPrefix + Strings.MVC.CustomFieldStrings_Resource + ", " + resourceKey + Strings.MVC.ResourceMissingSuffix;
                }
                else
                {
                    try
                    {
                        return string.Format(resourceKey, args);
                    }
                    catch
                    {
                        return resourceKey;
                    }
                }
            }
        }

        public static MvcHtmlString CustomFieldResource(this HtmlHelper htmlHelper, string resourceKey, params object[] args)
        {
            return htmlHelper.CustomFieldResourceString(resourceKey, args).ToMvcHtmlString();
        }

        public static MvcHtmlString CustomFieldResource(this HtmlHelper htmlHelper, string resourceKey, string culture, params object[] args)
        {
            return htmlHelper.CustomFieldResourceString(resourceKey, culture, args).ToMvcHtmlString();
        }

        public static string CustomFieldResourceOrDefaultString(this HtmlHelper htmlHelper, string resourceKey, params object[] args)
        {
            try
            {
                //generate resource key to attempt
                resourceKey = resourceKey ?? string.Empty;
                Regex r = new Regex(@"[^a-zA-Z0-9_]");
                string attemptKey = r.Replace(resourceKey, "_");
                if (attemptKey[0] >= '0' && attemptKey[0] <= '9')
                {
                    attemptKey = "_" + attemptKey;
                }

                return
                    string.Format(
                        (string)
                        HttpContext.GetGlobalResourceObject(Strings.MVC.CustomFieldStrings_Resource, attemptKey,
                                                            CultureInfo.GetCultureInfo(
                                                                CurrentCulture(htmlHelper))), args);
            }
            catch
            {
                //note - in this particular method missing strings are considered normal
                //if (resourceKey != "_trace" && TraceEnabled(htmlHelper))
                //{
                //    //List<string> tokens = (List<string>)htmlHelper.ViewContext.HttpContext.Application["ToLocalize"];
                //    //if (!tokens.Contains(Strings.MVC.CustomFieldStrings_Resource + ", " + resourceKey)) tokens.Add(Strings.MVC.GlobalStrings_Resource + ", " + resourceKey);
                //    return Strings.MVC.ResourceMissingPrefix + Strings.MVC.CustomFieldStrings_Resource + ", " + resourceKey + Strings.MVC.ResourceMissingSuffix;
                //}
                //else
                //{
                return null;
                //}
            }
        }

        public static string CustomFieldResourceOrDefaultString(this Controller controller, string resourceKey, params object[] args)
        {
            try
            {
                //generate resource key to attempt
                resourceKey = resourceKey ?? string.Empty;
                Regex r = new Regex(@"[^a-zA-Z0-9_]");
                string attemptKey = r.Replace(resourceKey, "_");
                if (attemptKey[0] >= '0' && attemptKey[0] <= '9')
                {
                    attemptKey = "_" + attemptKey;
                }

                return
                    string.Format(
                        (string)
                        HttpContext.GetGlobalResourceObject(Strings.MVC.CustomFieldStrings_Resource, attemptKey,
                                                            CultureInfo.GetCultureInfo(
                                                                CurrentCulture(controller))), args);
            }
            catch
            {
                //note - in this particular method missing strings are considered normal
                //if (resourceKey != "_trace" && TraceEnabled(htmlHelper))
                //{
                //    //List<string> tokens = (List<string>)htmlHelper.ViewContext.HttpContext.Application["ToLocalize"];
                //    //if (!tokens.Contains(Strings.MVC.CustomFieldStrings_Resource + ", " + resourceKey)) tokens.Add(Strings.MVC.GlobalStrings_Resource + ", " + resourceKey);
                //    return Strings.MVC.ResourceMissingPrefix + Strings.MVC.CustomFieldStrings_Resource + ", " + resourceKey + Strings.MVC.ResourceMissingSuffix;
                //}
                //else
                //{
                return null;
                //}
            }
        }

        public static MvcHtmlString CustomFieldResourceOrDefault(this HtmlHelper htmlHelper, string resourceKey, params object[] args)
        {
            return htmlHelper.CustomFieldResourceOrDefaultString(resourceKey, args).ToMvcHtmlString();
        }

        public static string CustomFieldResourceString(this Controller controller, string resourceKey, params object[] args)
        {
            try
            {
                //generate resource key to attempt
                resourceKey = resourceKey ?? string.Empty;
                Regex r = new Regex(@"[^a-zA-Z0-9_]");
                string attemptKey = r.Replace(resourceKey, "_");
                if (attemptKey[0] >= '0' && attemptKey[0] <= '9')
                {
                    attemptKey = "_" + attemptKey;
                }

                return
                    string.Format(
                        (string)
                        HttpContext.GetGlobalResourceObject(Strings.MVC.CustomFieldStrings_Resource, attemptKey,
                                                            CultureInfo.GetCultureInfo(
                                                                CurrentCulture(controller))), args);
            }
            catch
            {
                if (resourceKey != "_trace" && TraceEnabled(controller))
                {
                    //List<string> tokens = (List<string>)htmlHelper.ViewContext.HttpContext.Application["ToLocalize"];
                    //if (!tokens.Contains(Strings.MVC.CustomFieldStrings_Resource + ", " + resourceKey)) tokens.Add(Strings.MVC.GlobalStrings_Resource + ", " + resourceKey);
                    return Strings.MVC.ResourceMissingPrefix + Strings.MVC.CustomFieldStrings_Resource + ", " + resourceKey + Strings.MVC.ResourceMissingSuffix;
                }
                else
                {
                    try
                    {
                        return string.Format(resourceKey, args);
                    }
                    catch
                    {
                        return resourceKey;
                    }
                }
            }
        }

        #endregion

        #region AdminStrings

        public static string AdminResourceString(this Controller controller, string resourceKey, params object[] args)
        {
            try
            {
                return
                    string.Format(
                        (string)
                        HttpContext.GetGlobalResourceObject(Strings.MVC.AdminStrings_Resource, resourceKey,
                                                            CultureInfo.GetCultureInfo(
                                                                CurrentCulture(controller))), args);
            }
            catch
            {
                if (resourceKey != "_trace" && TraceEnabled(controller))
                {
                    //List<string> tokens = (List<string>)controller.HttpContext.Application["ToLocalize"];
                    //if (!tokens.Contains(Strings.MVC.AdminStrings_Resource + ", " + resourceKey)) tokens.Add(Strings.MVC.AdminStrings_Resource + ", " + resourceKey);
                    return Strings.MVC.ResourceMissingPrefix + Strings.MVC.AdminStrings_Resource + ", " + resourceKey + Strings.MVC.ResourceMissingSuffix;
                }
                else
                {
                    try
                    {
                        return string.Format(resourceKey, args);
                    }
                    catch
                    {
                        return resourceKey;
                    }
                }
            }
        }

        public static string AdminResourceString(this HtmlHelper htmlHelper, string resourceKey, params object[] args)
        {
            try
            {
                return string.Format((string)HttpContext.GetGlobalResourceObject(Strings.MVC.AdminStrings_Resource, resourceKey, CultureInfo.GetCultureInfo(CurrentCulture(htmlHelper))), args);
            }
            catch
            {
                if (resourceKey != "_trace" && TraceEnabled(htmlHelper))
                {
                    //List<string> tokens = (List<string>)htmlHelper.ViewContext.HttpContext.Application["ToLocalize"];
                    //if (!tokens.Contains(Strings.MVC.AdminStrings_Resource + ", " + resourceKey)) tokens.Add(Strings.MVC.AdminStrings_Resource + ", " + resourceKey);
                    return (Strings.MVC.ResourceMissingPrefix + Strings.MVC.AdminStrings_Resource + ", " + resourceKey + Strings.MVC.ResourceMissingSuffix);
                }
                else
                {
                    try
                    {
                        return string.Format(resourceKey, args);
                    }
                    catch
                    {
                        return resourceKey;
                    }
                }
            }
        }

        public static MvcHtmlString AdminResource(this HtmlHelper htmlHelper, string resourceKey, params object[] args)
        {
            return htmlHelper.AdminResourceString(resourceKey, args).ToMvcHtmlString();
        }

        #endregion

        #region Validation

        public static string ValidationResourceString(this Controller controller, string resourceKey, params object[] args)
        {
            try
            {
                return
                    string.Format(
                        (string)
                        HttpContext.GetGlobalResourceObject(Strings.MVC.ValidationStrings_Resource, resourceKey,
                                                            CultureInfo.GetCultureInfo(
                                                                CurrentCulture(controller))), args);
            }
            catch
            {
                if (resourceKey != "_trace" && TraceEnabled(controller))
                {
                    //List<string> tokens = (List<string>)htmlHelper.ViewContext.HttpContext.Application["ToLocalize"];
                    //if (!tokens.Contains(Strings.MVC.ValidationStrings_Resource + ", " + resourceKey)) tokens.Add(Strings.MVC.ValidationStrings_Resource + ", " + resourceKey);
                    return (Strings.MVC.ResourceMissingPrefix + Strings.MVC.ValidationStrings_Resource + ", " + resourceKey + Strings.MVC.ResourceMissingSuffix);
                }
                else
                {
                    try
                    {
                        return string.Format(resourceKey, args);
                    }
                    catch
                    {
                        return resourceKey;
                    }
                }
            }
        }

        public static string ValidationResourceString(this HtmlHelper htmlHelper, string resourceKey, params object[] args)
        {
            try
            {
                return
                    string.Format(
                        (string)
                        HttpContext.GetGlobalResourceObject(Strings.MVC.ValidationStrings_Resource, resourceKey,
                                                            CultureInfo.GetCultureInfo(
                                                                CurrentCulture(htmlHelper))), args);
            }
            catch
            {
                if (resourceKey != "_trace" && TraceEnabled(htmlHelper))
                {
                    //List<string> tokens = (List<string>)htmlHelper.ViewContext.HttpContext.Application["ToLocalize"];
                    //if (!tokens.Contains(Strings.MVC.ValidationStrings_Resource + ", " + resourceKey)) tokens.Add(Strings.MVC.ValidationStrings_Resource + ", " + resourceKey);
                    return (Strings.MVC.ResourceMissingPrefix + Strings.MVC.ValidationStrings_Resource + ", " + resourceKey + Strings.MVC.ResourceMissingSuffix);
                }
                else
                {
                    try
                    {
                        return string.Format(resourceKey, args);
                    }
                    catch
                    {
                        return resourceKey;
                    }
                }
            }
        }

        public static MvcHtmlString ValidationResource(this HtmlHelper htmlHelper, string resourceKey, params object[] args)
        {
            return htmlHelper.ValidationResourceString(resourceKey, args).ToMvcHtmlString();
        }

        #endregion

        #region AriaStrings

        public static string AriaResourceString(this HtmlHelper htmlHelper, string resourceKey, params object[] args)
        {
            try
            {
                return
                    string.Format(
                        (string)
                        HttpContext.GetGlobalResourceObject(Strings.MVC.AriaStrings_Resource, resourceKey,
                                                            CultureInfo.GetCultureInfo(
                                                                CurrentCulture(htmlHelper))), args);
            }
            catch
            {
                if (resourceKey != "_trace" && TraceEnabled(htmlHelper))
                {
                    //List<string> tokens = (List<string>)htmlHelper.ViewContext.HttpContext.Application["ToLocalize"];
                    //if (!tokens.Contains(Strings.MVC.AriaStrings_Resource + ", " + resourceKey)) tokens.Add(Strings.MVC.AriaStrings_Resource + ", " + resourceKey);
                    return (Strings.MVC.ResourceMissingPrefix + Strings.MVC.AriaStrings_Resource + ", " + resourceKey + Strings.MVC.ResourceMissingSuffix);
                }
                else
                {
                    try
                    {
                        return string.Format(resourceKey, args);
                    }
                    catch
                    {
                        return resourceKey;
                    }
                }
            }
        }

        public static MvcHtmlString AriaResource(this HtmlHelper htmlHelper, string resourceKey, params object[] args)
        {
            return htmlHelper.AriaResourceString(resourceKey, args).ToMvcHtmlString();
        }

        #endregion

        #region TimeZones

        public static MvcHtmlString TimeZoneName(this HtmlHelper helper, string timeZoneId)
        {
            return TimeZoneNameString(helper, timeZoneId).ToMvcHtmlString();
        }

        public static string TimeZoneNameString(this HtmlHelper helper, string timeZoneId)
        {
            string resourceKey = timeZoneId.SimplifyForURL("_");
            string result;
            try
            {
                result = (string)HttpContext.GetGlobalResourceObject(Strings.MVC.TimeZone_Resource, resourceKey, CultureInfo.GetCultureInfo(CurrentCulture(helper)));
            }
            catch
            {
                if (resourceKey != "_trace" && TraceEnabled(helper))
                {
                    result = Strings.MVC.ResourceMissingPrefix + Strings.MVC.TimeZone_Resource + ", " + resourceKey + Strings.MVC.ResourceMissingSuffix;
                }
                else
                {
                    result = resourceKey;
                }
            }
            return result;
        }

        public static MvcHtmlString TimeZoneAbbreviation(this HtmlHelper helper, string timeZoneId)
        {
            return TimeZoneAbbreviationString(helper, timeZoneId).ToMvcHtmlString();
        }
        public static string TimeZoneAbbreviationString(this HtmlHelper helper, string timeZoneId)
        {
            string resourceKey = timeZoneId.SimplifyForURL("_") + "_Short";
            string result;
            try
            {
                result = (string)HttpContext.GetGlobalResourceObject(Strings.MVC.TimeZone_Resource, resourceKey, CultureInfo.GetCultureInfo(CurrentCulture(helper)));
            }
            catch
            {
                if (resourceKey != "_trace" && TraceEnabled(helper))
                {
                    result = Strings.MVC.ResourceMissingPrefix + Strings.MVC.TimeZone_Resource + ", " + resourceKey + Strings.MVC.ResourceMissingSuffix;
                }
                else
                {
                    result = resourceKey;
                }
            }
            return result;
        }

        public static string TimeZoneAbbreviationString(this Controller controller, string timeZoneId)
        {
            string resourceKey = timeZoneId.SimplifyForURL("_") + "_Short";
            string result;
            try
            {
                result = (string)HttpContext.GetGlobalResourceObject(Strings.MVC.TimeZone_Resource, resourceKey, CultureInfo.GetCultureInfo(CurrentCulture(controller)));
            }
            catch
            {
                if (resourceKey != "_trace" && TraceEnabled(controller))
                {
                    result = Strings.MVC.ResourceMissingPrefix + Strings.MVC.TimeZone_Resource + ", " + resourceKey + Strings.MVC.ResourceMissingSuffix;
                }
                else
                {
                    result = resourceKey;
                }
            }
            return result;
        }

        #endregion

        #region Categories & Regions

        public static string ToLocalizedLineageString(this Hierarchy<int, Category> h, HtmlHelper htmlHelper, string seperator, string[] toRemove)
        {
            var sb = new StringBuilder();
            string unlocalizedCategoryName = h.Current.Name;
            if (h.Current.ID == 18 && SiteClient.EnableEvents)
            {
                unlocalizedCategoryName = "Auctions";
            }
            sb.Append(htmlHelper.GlobalResourceString(unlocalizedCategoryName));
            Hierarchy<int, Category> currentParent = h.Parent;
            while (currentParent != null)
            {
                string newToken = currentParent.Current.Name;
                if (currentParent.Current.ID == 18 && SiteClient.EnableEvents)
                {
                    newToken = "Auctions";
                }
                if (!toRemove.Contains(newToken))
                {
                    sb.Insert(0, seperator);
                    sb.Insert(0, htmlHelper.GlobalResourceString(newToken));
                }
                currentParent = currentParent.Parent;
            }
            return sb.ToString();
        }

        public static string ToLocalizedLineageString(this Hierarchy<int, Category> h, Controller controller, string seperator, string[] toRemove)
        {
            var sb = new StringBuilder();
            string unlocalizedCategoryName = h.Current.Name;
            if (h.Current.ID == 18 && SiteClient.EnableEvents)
            {
                unlocalizedCategoryName = "Auctions";
            }
            sb.Append(controller.GlobalResourceString(unlocalizedCategoryName));
            Hierarchy<int, Category> currentParent = h.Parent;
            while (currentParent != null)
            {
                string newToken = currentParent.Current.Name;
                if (currentParent.Current.ID == 18 && SiteClient.EnableEvents)
                {
                    newToken = "Auctions";
                }
                if (!toRemove.Contains(newToken))
                {
                    sb.Insert(0, seperator);
                    sb.Insert(0, controller.GlobalResourceString(newToken));
                }
                currentParent = currentParent.Parent;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Retrieves the localized category or region name
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="categoryName">the original category name</param>
        public static string LocalizedCategoryName(this HtmlHelper htmlHelper, string categoryName)
        {
            string resourceKey = categoryName.SimplifyForURL("_");
            try
            {
                //generate resource key to attempt
                string result = (string)HttpContext.GetGlobalResourceObject(Strings.MVC.CategoryAndRegionNames_Resource, resourceKey,
                    CultureInfo.GetCultureInfo(CurrentCulture(htmlHelper)));
                if (result == null)
                {
                    //throw new NullReferenceException();
                    if (resourceKey != "_trace" && TraceEnabled(htmlHelper))
                    {
                        return Strings.MVC.ResourceMissingPrefix + Strings.MVC.CategoryAndRegionNames_Resource + ", " + resourceKey + Strings.MVC.ResourceMissingSuffix;
                    }
                    else
                    {
                        return categoryName;
                    }
                }
                return result;
            }
            catch
            {
                if (resourceKey != "_trace" && TraceEnabled(htmlHelper))
                {
                    return Strings.MVC.ResourceMissingPrefix + Strings.MVC.CategoryAndRegionNames_Resource + ", " + resourceKey + Strings.MVC.ResourceMissingSuffix;
                }
                else
                {
                    return categoryName;
                }
            }
        }

        /// <summary>
        /// Retrieves the localized category or region name
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="categoryName">the original category name</param>
        public static string LocalizedCategoryName(this Controller controller, string categoryName)
        {
            string resourceKey = categoryName.SimplifyForURL("_");
            try
            {
                //generate resource key to attempt
                string result = (string)HttpContext.GetGlobalResourceObject(Strings.MVC.CategoryAndRegionNames_Resource, resourceKey,
                    CultureInfo.GetCultureInfo(CurrentCulture(controller)));
                if (result == null)
                {
                    //throw new NullReferenceException();
                    if (resourceKey != "_trace" && TraceEnabled(controller))
                    {
                        return Strings.MVC.ResourceMissingPrefix + Strings.MVC.CategoryAndRegionNames_Resource + ", " + resourceKey + Strings.MVC.ResourceMissingSuffix;
                    }
                    else
                    {
                        return categoryName;
                    }
                }
                return result;
            }
            catch
            {
                if (resourceKey != "_trace" && TraceEnabled(controller))
                {
                    return Strings.MVC.ResourceMissingPrefix + Strings.MVC.CategoryAndRegionNames_Resource + ", " + resourceKey + Strings.MVC.ResourceMissingSuffix;
                }
                else
                {
                    return categoryName;
                }
            }
        }

        /// <summary>
        /// converts the category hierarchy to a string with localized category names
        /// </summary>
        /// <param name="h">the category hierarchy</param>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="seperator">the string to inert between each category name</param>
        /// <param name="toRemove">categoroes to exclude from the result</param>
        public static string LocalizedCategoryLineageString(this Hierarchy<int, Category> h, Controller controller, string seperator, string[] toRemove)
        {
            var sb = new StringBuilder();
            string unlocalizedCategoryName = h.Current.Name;
            sb.Append(controller.LocalizedCategoryName(unlocalizedCategoryName));
            Hierarchy<int, Category> currentParent = h.Parent;
            while (currentParent != null)
            {
                string newToken = currentParent.Current.Name;
                if (!toRemove.Contains(newToken))
                {
                    sb.Insert(0, seperator);
                    sb.Insert(0, controller.LocalizedCategoryName(newToken));
                }
                currentParent = currentParent.Parent;
            }
            return sb.ToString();
        }

        /// <summary>
        /// converts the category hierarchy to a string with localized category names
        /// </summary>
        /// <param name="h">the category hierarchy</param>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="seperator">the string to inert between each category name</param>
        /// <param name="toRemove">categoroes to exclude from the result</param>
        public static string LocalizedCategoryLineageString(this Hierarchy<int, Category> h, HtmlHelper htmlHelper, string seperator, string[] toRemove)
        {
            var sb = new StringBuilder();
            string unlocalizedCategoryName = h.Current.Name;
            sb.Append(htmlHelper.LocalizedCategoryName(unlocalizedCategoryName));
            Hierarchy<int, Category> currentParent = h.Parent;
            while (currentParent != null)
            {
                string newToken = currentParent.Current.Name;
                if (!toRemove.Contains(newToken))
                {
                    sb.Insert(0, seperator);
                    sb.Insert(0, htmlHelper.LocalizedCategoryName(newToken));
                }
                currentParent = currentParent.Parent;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Returns a string representing the category hierarchy of the primary category of the specified listing
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="listing">the specified listing</param>
        /// <returns>string</returns>
        public static string LocalizedCategoryLineageString(this HtmlHelper htmlHelper, Listing listing)
        {
            //int catID = listing.PrimaryCategory.ID;
            //Hierarchy<int, Category> cats = CommonClient.GetCategoryPath(catID).Trees[catID];
            //return cats.ToLineageString(Strings.Fields.Name, Strings.MVC.LineageSeperator, new string[] { "Root", "Items" });
            return LocalizedCategoryLineageString(htmlHelper, listing, Strings.MVC.LineageSeperator);
        }

        /// <summary>
        /// Returns a string representing the category hierarchy of the primary category of the specified listing
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="listing">the specified listing</param>
        /// <param name="separator">the specified separator to insert between each category name</param>
        /// <returns>string</returns>
        public static string LocalizedCategoryLineageString(this HtmlHelper htmlHelper, Listing listing, string separator)
        {
            int catID = listing.PrimaryCategory.ID;
            Hierarchy<int, Category> cats = CommonClient.GetCategoryPath(catID).Trees[catID];
            return cats.LocalizedCategoryLineageString(htmlHelper, separator, new string[] { "Root", "Items" });
        }

        #endregion

    }
}
