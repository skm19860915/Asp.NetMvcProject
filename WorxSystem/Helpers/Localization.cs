using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Compilation;
using System.Globalization;
using RainWorx.FrameWorx.Clients;

namespace RainWorx.FrameWorx.MVC.Helpers
{
    public static class Localization
    {        
        public static SelectList Cultures(this HtmlHelper htmlHelper)
        {
            List<CultureInfo> cultures;
            //string selectedValue = "en-US";
            if (SiteClient.BoolSetting(Strings.SiteProperties.ShowLanguageRegionOptions))
            {
                cultures = SiteClient.SupportedCultures.Values
                    .Where(ci => !ci.Name.Equals("en-us", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(ci => ci.EnglishName).ToList();
                var enUS =
                    SiteClient.SupportedCultures.Values.Where(
                        ci => ci.Name.Equals("en-us", StringComparison.OrdinalIgnoreCase)).SingleOrDefault();
                if (enUS != null) cultures.Insert(0, enUS);
            }
            else
            {
                cultures = SiteClient.SupportedCultures.Values
                    .Where(ci => !ci.Name.Equals("en", StringComparison.OrdinalIgnoreCase) && ci.IsNeutralCulture)
                    .OrderBy(ci => ci.EnglishName).ToList();
                var en =
                    SiteClient.SupportedCultures.Values.Where(
                        ci => ci.Name.Equals("en", StringComparison.OrdinalIgnoreCase)).SingleOrDefault();
                if (en != null) cultures.Insert(0, en);
            }
            SelectList retVal = new SelectList(cultures, Strings.Fields.Name, Strings.Fields.NativeName);            
            return retVal;
        }

        public static SelectList Currencies(this HtmlHelper htmlHelper)
        {
            string defaultCurrency = (!string.IsNullOrEmpty(htmlHelper.GetCookie("currency"))) ? htmlHelper.GetCookie("currency") : SiteClient.Settings[Strings.SiteProperties.SiteCurrency];
            SelectList retVal = new SelectList(SiteClient.SupportedCurrencyRegions.OrderBy(ci => ci.Value.CurrencyNativeName), Strings.Fields.Key, Strings.Fields.ValuesCurrencyNativeName, defaultCurrency);
            return retVal;
        }

        //public static string Resource(this HtmlHelper htmlhelper, string expression, params object[] args)
        //{
        //    string virtualPath = GetVirtualPath(htmlhelper);

        //    try
        //    {               
        //        return GetResourceString(htmlhelper.ViewContext.HttpContext, expression, virtualPath, args);
        //    }
        //    catch (Exception)
        //    {
        //        return (string)htmlhelper.ViewContext.HttpContext.GetGlobalResourceObject("Strings", expression) ?? expression;                
        //    }
        //}

        //public static string Resource(this Controller controller, string expression, params object[] args)
        //{            
        //    return (string)controller.HttpContext.GetGlobalResourceObject("Strings", expression) ?? expression;
        //}

        //public static string ResourceOrDefault(this HtmlHelper htmlhelper, string expression, params object[] args)
        //{
        //    string virtualPath = GetVirtualPath(htmlhelper);

        //    try
        //    {
        //        return GetResourceString(htmlhelper.ViewContext.HttpContext, expression, virtualPath, args);
        //    }
        //    catch (Exception)
        //    {
        //        return (string)htmlhelper.ViewContext.HttpContext.GetGlobalResourceObject("Strings", expression) ?? string.Empty;
        //    }
        //}

        //public static string ResourceOrDefault(this Controller controller, string expression, params object[] args)
        //{
        //    return (string)controller.HttpContext.GetGlobalResourceObject("Strings", expression) ?? string.Empty;
        //}

        //private static string GetResourceString(HttpContextBase httpContext, string expression, string virtualPath, object[] args)
        //{
        //    ExpressionBuilderContext context = new ExpressionBuilderContext(virtualPath);
        //    ResourceExpressionBuilder builder = new ResourceExpressionBuilder();
        //    ResourceExpressionFields fields = (ResourceExpressionFields)builder.ParseExpression(expression, typeof(string), context);

        //    if (!string.IsNullOrEmpty(fields.ClassKey))
        //        return string.Format((string)httpContext.GetGlobalResourceObject(
        //        fields.ClassKey,
        //        fields.ResourceKey,
        //        CultureInfo.CurrentUICulture),
        //        args);

        //    return string.Format((string)httpContext.GetLocalResourceObject(
        //    virtualPath,
        //    fields.ResourceKey,
        //    CultureInfo.CurrentUICulture),
        //    args);
        //}

        //private static string GetVirtualPath(HtmlHelper htmlHelper)
        //{
        //    string virtualPath = null;
        //    WebFormView view = htmlHelper.ViewContext.View as WebFormView;

        //    if (view != null)
        //    {
        //        virtualPath = view.ViewPath;
        //    }
        //    return virtualPath;
        //}        
       
    }
}
