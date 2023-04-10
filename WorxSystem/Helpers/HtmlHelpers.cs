using System;
using System.Configuration;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Web.Routing;
using System.Web.UI.WebControls;
using Mindscape.LightSpeed;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.DTO.Media;
using RainWorx.FrameWorx.Providers.MediaAsset;
using RainWorx.FrameWorx.Providers.MediaSaver;
using RainWorx.FrameWorx.Providers.MediaLoader;
using RainWorx.FrameWorx.Strings;
using RainWorx.FrameWorx.Utility;
using ListItem = RainWorx.FrameWorx.DTO.ListItem;
using System.Data.SqlClient;
using System.Data;
using Newtonsoft.Json;


namespace RainWorx.FrameWorx.MVC.Helpers
{
    /// <summary>
    /// Extensions for the standard System.Web,Mvc.HtmlHelper class
    /// </summary>
    public static class HtmlHelpers
    {
        //public static string ImageRoot(this HtmlHelper htmlHelper)
        //{
        //    return ConfigurationManager.AppSettings["ImageLoadURI"];
        //}

        //public static string ImageSource(this HtmlHelper htmlHelper, Image image)
        //{
        //    return ConfigurationManager.AppSettings["ImageLoadURI"] + image.Folder + "/" + image.URL;
        //}       

        /// <summary>
        /// Returns a list of SelectListItem options for each month of the year.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        public static List<SelectListItem> MonthOptions(this HtmlHelper htmlHelper)
        {
            List<SelectListItem> list = new List<SelectListItem>();
            for (int i = 1; i <= 12; i++)
            {
                string monthText = ("0" + i.ToString()).Right(2);
                list.Add(new SelectListItem() { Text = monthText, Value = monthText });
            }
            return list;
        }

        /// <summary>
        /// Returns a single-selection element using the specified HTML helper, the name of the form field, 
        ///  an option label, and the specified HTML attributes, with an option for each month of the year.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="name">the value to use for the &quot;name&quot; and &quot;id&quot; html attibutes</param>
        /// <param name="optionLabel">The text for a deafult empty item.  This parameter can be null.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        public static MvcHtmlString MonthDropDownList(this HtmlHelper htmlHelper, string name,
            string optionLabel, object htmlAttributes)
        {
            return htmlHelper.DropDownList(name, MonthOptions(htmlHelper), optionLabel, htmlAttributes);
        }

        /// <summary>
        /// Returns a list of SelectListItem options with an option for the current year and one for each of the next 15 years
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        public static List<SelectListItem> YearOptions(this HtmlHelper htmlHelper)
        {
            List<SelectListItem> list = new List<SelectListItem>();
            for (int i = DateTime.Now.Year; i <= DateTime.UtcNow.Year + 15; i++)
            {
                string yearText = i.ToString();
                list.Add(new SelectListItem() { Text = yearText, Value = yearText });
            }
            return list;
        }

        /// <summary>
        /// Returns a single-selection element using the specified HTML helper, the name of the form field, 
        ///  an option label, and the specified HTML attributes, with an option for the current year and one for each of the next 15 years
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="name">the value to use for the &quot;name&quot; and &quot;id&quot; html attibutes</param>
        /// <param name="optionLabel">The text for a deafult empty item.  This parameter can be null.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        public static MvcHtmlString YearDropDownList(this HtmlHelper htmlHelper, string name,
            string optionLabel, object htmlAttributes)
        {
            return htmlHelper.DropDownList(name, YearOptions(htmlHelper), optionLabel, htmlAttributes);
        }

        /// <summary>
        /// Returns true if the specified listing is eligible to be edited
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="listing">the specified listing</param>
        public static bool IsEditable(this HtmlHelper htmlHelper, Listing listing)
        {
            return listing.Status.Equals(Strings.ListingStatuses.Active) ||
                   listing.Status.Equals(Strings.ListingStatuses.Pending) ||
                   listing.Status.Equals(Strings.ListingStatuses.AwaitingPayment) ||
                   listing.Status.Equals(Strings.ListingStatuses.Draft) ||
                   listing.Status.Equals(Strings.ListingStatuses.Validated);
        }

        /// <summary>
        /// Returns a string properly escaped string for use inside a javascript string
        /// </summary>
        /// <param name="myString">An un-escaped string</param>
        public static string ToJavascriptSafeString(this string myString)
        {
            return (myString.Replace(@"\", @"\\").Replace(@"""", @"\""").Replace(@"'", @"\'").Replace("\r", "").Replace("\n", ""));
        }

        /// <summary>
        /// Returns a string properly escaped string for use inside a javascript string
        /// </summary>
        /// <param name="myString">An un-escaped string</param>
        public static MvcHtmlString ToJavascriptSafeString(this MvcHtmlString myString)
        {
            return (myString.ToString().Replace(@"\", @"\\").Replace(@"""", @"\""").Replace(@"'", @"\'").Replace("\r", "").Replace("\n", "").ToMvcHtmlString());
        }

        /// <summary>
        /// Returns a URL to the specified action and all current route values, except those specified 
        /// </summary>
        /// <param name="urlHelper">an instance of the UrlHelper class</param>
        /// <param name="action">the specified MVC action</param>
        /// <param name="routeValuesToExclude">an array of strings, one for each route key to be excluded from the result</param>
        public static string WithoutRouteValues(this UrlHelper urlHelper, string action, params string[] routeValuesToExclude)
        {
            var rv = urlHelper.RequestContext.RouteData.Values;
            var ignoredValues = rv.Where(x => routeValuesToExclude.Any(z => z == x.Key)).ToList();
            foreach (var ignoredValue in ignoredValues)
                rv.Remove(ignoredValue.Key);
            var res = urlHelper.Action(action);
            foreach (var ignoredValue in ignoredValues)
                rv.Add(ignoredValue.Key, ignoredValue.Value);
            return res;
        }

        /// <summary>
        /// Returns a multi-selection element using the specified HTML helper, the name of the form field, 
        ///  the specified HTML attributes, with an option for each category contained in the specified category heirarchy
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="name">the value to use for the &quot;name&quot; html attibute</param>
        /// <param name="hierarchy">an instance of Hierarchy&lt;int, Category&gt; containing the specified category hierarchy</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        /// <param name="disabledFieldID">if greater than zero, all categories associated with this custom field will be rendered with the &quot;disabled&quot; HTML attribute</param>
        /// <param name="enabledFieldID">if greater than zero, all categories NOT associated with this custom field will be rendered with the &quot;disabled&quot; HTML attribute</param>
        /// <returns></returns>
        public static MvcHtmlString HierarchyComboBox(this HtmlHelper htmlHelper, string name, Hierarchy<int, Category> hierarchy, object htmlAttributes, int disabledFieldID, int enabledFieldID)
        {
            TagBuilder builder = new TagBuilder("select");
            builder.MergeAttribute("multiple", "multiple");
            builder.MergeAttribute("name", name);
            builder.MergeAttributes(((IDictionary<string, object>)new RouteValueDictionary(htmlAttributes)));
            builder.InnerHtml = string.Empty;

            if (hierarchy != null)
            {
                foreach (Hierarchy<int, Category> child in hierarchy.ChildHierarchies)
                {
                    builder.InnerHtml += HierarchyOption(child, disabledFieldID, enabledFieldID);
                }
            }
            return builder.ToMvcHtmlString(TagRenderMode.Normal);
        }

        private static MvcHtmlString HierarchyOption(Hierarchy<int, Category> hierarchy, int disabledFieldID, int enabledFieldID)
        {
            if (hierarchy == null) return MvcHtmlString.Empty;
            string retVal = string.Empty;
            TagBuilder builder = new TagBuilder("option");
            builder.MergeAttribute("value", hierarchy.Current.ID.ToString());
            if (disabledFieldID > 0)
            {
                if (hierarchy.Current.CustomFieldIDs.Contains(disabledFieldID)) builder.MergeAttribute("disabled", "disabled");
            }
            if (enabledFieldID > 0)
            {
                if (!hierarchy.Current.CustomFieldIDs.Contains(enabledFieldID)) builder.MergeAttribute("disabled", "disabled");
            }
            builder.InnerHtml = string.Empty;
            for (int i = 1; i < hierarchy.Depth; i++)
            {
                builder.InnerHtml += "&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;";
            }
            builder.InnerHtml += hierarchy.Current.Name;
            retVal = builder.ToString(TagRenderMode.Normal);
            foreach (Hierarchy<int, Category> child in hierarchy.ChildHierarchies)
            {
                retVal += HierarchyOption(child, disabledFieldID, enabledFieldID);
            }
            return retVal.ToMvcHtmlString();
        }

        /// <summary>
        /// Returns an element containin all success, neutral and/or error messages resulting from a previous request
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        public static MvcHtmlString SystemMessages(this HtmlHelper htmlHelper)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(htmlHelper.SuccessMessage());
            sb.Append(htmlHelper.NeutralMessage());
            sb.Append(htmlHelper.ErrorMessage());
            return sb.ToMvcHtmlString();
        }

        private static MvcHtmlString SuccessMessage(this HtmlHelper htmlHelper)
        {
            if (htmlHelper.ViewContext.TempData[Strings.MVC.SuccessMessage] == null) return MvcHtmlString.Empty;

            TagBuilder builder = new TagBuilder("div");
            builder.MergeAttribute("class", "alert alert-success");

            builder.InnerHtml = "<a class=\"close\" onclick=\"$(this).parents('div').first().slideUp();\">×</a>";
            if (((string)htmlHelper.ViewContext.RouteData.Values["controller"]).Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                builder.InnerHtml += htmlHelper.AdminResourceString((string)htmlHelper.ViewContext.TempData[Strings.MVC.SuccessMessage]);
            }
            else
            {
                builder.InnerHtml += htmlHelper.GlobalResourceString((string)htmlHelper.ViewContext.TempData[Strings.MVC.SuccessMessage]);
            }
            return builder.ToMvcHtmlString(TagRenderMode.Normal);
        }

        private static MvcHtmlString NeutralMessage(this HtmlHelper htmlHelper)
        {
            if (htmlHelper.ViewContext.TempData[Strings.MVC.NeutralMessage] == null) return MvcHtmlString.Empty;

            TagBuilder builder = new TagBuilder("div");
            builder.MergeAttribute("class", "alert alert-info");

            builder.InnerHtml = "<a class=\"close\" onclick=\"$(this).parents('div').first().slideUp();\">×</a>";
            if (((string)htmlHelper.ViewContext.RouteData.Values["controller"]).Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                builder.InnerHtml += htmlHelper.AdminResource((string)htmlHelper.ViewContext.TempData[Strings.MVC.NeutralMessage]).ToString();
            }
            else
            {
                builder.InnerHtml += htmlHelper.GlobalResource((string)htmlHelper.ViewContext.TempData[Strings.MVC.NeutralMessage]).ToString();
            }
            return builder.ToMvcHtmlString(TagRenderMode.Normal);
        }

        private static MvcHtmlString ErrorMessage(this HtmlHelper htmlHelper)
        {
            if (htmlHelper.ViewContext.TempData[Strings.MVC.ErrorMessage] == null) return MvcHtmlString.Empty;

            TagBuilder builder = new TagBuilder("div");
            builder.MergeAttribute("class", "alert alert-danger");

            builder.InnerHtml = "<a class=\"close\" onclick=\"$(this).parents('div').first().slideUp();\">×</a>";
            if (((string)htmlHelper.ViewContext.RouteData.Values["controller"]).Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                builder.InnerHtml += htmlHelper.AdminResource((string)htmlHelper.ViewContext.TempData[Strings.MVC.ErrorMessage]).ToString();
            }
            else
            {
                builder.InnerHtml += htmlHelper.GlobalResource((string)htmlHelper.ViewContext.TempData[Strings.MVC.ErrorMessage]).ToString();
            }
            return builder.ToMvcHtmlString(TagRenderMode.Normal);
        }

        /// <summary>
        /// Returns a checkbox element which will is disabled if the initial value is "checked"
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="name">the value to use for the &quot;name&quot; and &quot;id&quot; html attibutes</param>
        /// <returns></returns>
        public static MvcHtmlString CheckBoxEnableOnly(this HtmlHelper htmlHelper, string name)
        {
            return (htmlHelper.CheckBoxEnableOnly(name, new Dictionary<string, object>()));
        }

        /// <summary>
        /// Returns a checkbox element which will is disabled if the initial value is "checked", with the specified HTML attributes
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="name">the value to use for the &quot;name&quot; and &quot;id&quot; html attibutes</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        public static MvcHtmlString CheckBoxEnableOnly(this HtmlHelper htmlHelper, string name, IDictionary<string, object> htmlAttributes)
        {
            string retVal = string.Empty;

            //determine if this checkbox is already checked
            bool existingValueChecked = false;
            ModelState state;
            if (htmlHelper.ViewData.ModelState.TryGetValue(name, out state))
                existingValueChecked = (state.Value.AttemptedValue == Strings.MVC.TrueValue);
            else if (htmlHelper.ViewData.ContainsKey(name))
                existingValueChecked = ((string)htmlHelper.ViewData[name] == Strings.MVC.TrueValue);

            if (existingValueChecked)
            {
                //build tags with disabled, checked checkbox
                TagBuilder newCheckbox = new TagBuilder("input");
                newCheckbox.MergeAttribute("type", "checkbox");
                newCheckbox.MergeAttribute("id", name);
                newCheckbox.MergeAttribute("name", "x_" + name + "_disabled");
                newCheckbox.MergeAttributes<string, object>(htmlAttributes);
                newCheckbox.MergeAttribute("checked", "checked");
                newCheckbox.MergeAttribute("disabled", "disabled");

                TagBuilder newHiddenField = new TagBuilder("input");
                newHiddenField.MergeAttribute("type", "hidden");
                newHiddenField.MergeAttribute("name", name);
                newHiddenField.MergeAttribute("value", Strings.MVC.TrueFormValue);

                retVal += newCheckbox.ToString(TagRenderMode.SelfClosing);
                retVal += newHiddenField.ToString(TagRenderMode.SelfClosing);
            }
            else
            {
                //build checkbox and hidden tags normally
                TagBuilder newCheckbox = new TagBuilder("input");
                newCheckbox.MergeAttribute("type", "checkbox");
                newCheckbox.MergeAttribute("id", name);
                newCheckbox.MergeAttribute("name", name);
                newCheckbox.MergeAttributes<string, object>(htmlAttributes);
                newCheckbox.MergeAttribute("value", "true");

                TagBuilder newHiddenField = new TagBuilder("input");
                newHiddenField.MergeAttribute("type", "hidden");
                newHiddenField.MergeAttribute("name", name);
                newHiddenField.MergeAttribute("value", "false");

                retVal += newCheckbox.ToString(TagRenderMode.SelfClosing);
                retVal += newHiddenField.ToString(TagRenderMode.SelfClosing);
            }
            return retVal.ToMvcHtmlString();
        }

        /// <summary>
        /// Gets the decimal thousands separator character used by the culture of the user currently logged in (e.g. "," in "1,234.56" for en-US)
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        public static MvcHtmlString GetCurrencyGroupChar(this HtmlHelper htmlHelper)
        {
            CultureInfo numberCulture = SiteClient.SupportedCultures[htmlHelper.GetCookie(Strings.MVC.CultureCookie) ?? Strings.FieldDefaults.Culture];
            return numberCulture.NumberFormat.CurrencyGroupSeparator.Replace((char)160, (char)32).ToMvcHtmlString(); // replace nbsp char (160) with space char (32)
        }

        /// <summary>
        /// Gets the decimal character used by the culture of the user currently logged in (e.g. "." in "1,234.56" for en-US)
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        public static MvcHtmlString GetCurrencyDecimalChar(this HtmlHelper htmlHelper)
        {
            CultureInfo numberCulture = SiteClient.SupportedCultures[htmlHelper.GetCookie(Strings.MVC.CultureCookie) ?? Strings.FieldDefaults.Culture];
            return numberCulture.NumberFormat.CurrencyDecimalSeparator.Replace((char)160, (char)32).ToMvcHtmlString(); // replace nbsp char (160) with space char (32)
        }

        /// <summary>
        /// Returns the CultureInfo of the current user, or the site CultureInfo if not set
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        public static CultureInfo GetCultureInfo(this HtmlHelper htmlHelper)
        {
            string cultureCode = htmlHelper.GetCookie(Strings.MVC.CultureCookie);
            if (string.IsNullOrEmpty(cultureCode))
            {
                cultureCode = SiteClient.SiteCulture;
            }
            if (string.IsNullOrEmpty(cultureCode))
            {
                cultureCode = FieldDefaults.Culture;
            }
            if (SiteClient.SupportedCultures.ContainsKey(cultureCode))
            {
                return SiteClient.SupportedCultures[cultureCode];
            }
            else if (SiteClient.SupportedCultures.Keys.Count > 0)
            {
                return SiteClient.SupportedCultures[SiteClient.SupportedCultures.Keys.First()];
            }
            return new CultureInfo(FieldDefaults.Culture);
        }

        /// <summary>
        /// Returns the CultureInfo of the current user, or the site CultureInfo if not set
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        public static CultureInfo GetCultureInfo(this Controller controller)
        {
            string cultureCode = controller.GetCookie(Strings.MVC.CultureCookie);
            if (string.IsNullOrEmpty(cultureCode))
            {
                cultureCode = SiteClient.SiteCulture;
            }
            if (string.IsNullOrEmpty(cultureCode))
            {
                cultureCode = FieldDefaults.Culture;
            }
            if (SiteClient.SupportedCultures.ContainsKey(cultureCode))
            {
                return SiteClient.SupportedCultures[cultureCode];
            }
            else if (SiteClient.SupportedCultures.Keys.Count > 0)
            {
                return SiteClient.SupportedCultures[SiteClient.SupportedCultures.Keys.First()];
            }
            return new CultureInfo(FieldDefaults.Culture);
        }

        /// <summary>
        /// Formats a decimal value as a currency string, except without the currency symbol, using the culture of the user currently logged in
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="decValue">a decimal value</param>
        /// <remarks>
        ///     In some cultures, such as en-ZA (English, South Africa) the decimal format is defined as "1 234.56" while the currency format is "1 234,56", 
        ///     so it is sometimes necessary to distinguish between the two types for parsing/validation purposes.
        ///     (see also: DecimalToPlainCurrencyMvcHtmlString)
        /// </remarks>
        /// <returns></returns>
        public static string DecimalToPlainCurrencyString(this HtmlHelper htmlHelper, decimal decValue)
        {
            CultureInfo numberCulture = SiteClient.SupportedCultures[htmlHelper.GetCookie(Strings.MVC.CultureCookie) ?? Strings.FieldDefaults.Culture];
            numberCulture.NumberFormat.CurrencySymbol = string.Empty; // prevent $ or other currency symbol from being rendered
            return decValue.ToString("F2", numberCulture.NumberFormat).Trim();
        }

        /// <summary>
        /// Formats a decimal value as a currency MvcHtmlString, except without the currency symbol, using the culture of the user currently logged in
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="decValue">a decimal value</param>
        /// <remarks>
        ///     In some cultures, such as en-ZA (English, South Africa) the decimal format is defined as "1 234.56" while the currency format is "1 234,56", 
        ///     so it is sometimes necessary to distinguish between the two types for parsing/validation purposes.
        ///     (see also: DecimalToPlainCurrencyString)
        /// </remarks>
        /// <returns></returns>
        public static MvcHtmlString DecimalToPlainCurrencyMvcHtmlString(this HtmlHelper htmlHelper, decimal decValue)
        {
            return htmlHelper.DecimalToPlainCurrencyString(decValue).ToMvcHtmlString();
        }

        /// <summary>
        /// Returns the CultureInfo of the current user, or the site CultureInfo if not set
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        public static CultureInfo GetCurrentCultureInfo(this HtmlHelper htmlHelper)
        {
            CultureInfo retVal = null;
            string culture = htmlHelper.GetCookie("culture");
            if (SiteClient.SupportedCultures.ContainsKey(culture))
            {
                retVal = SiteClient.SupportedCultures[htmlHelper.GetCookie("culture")];
            }
            if (retVal == null)
            {
                retVal = SiteClient.SupportedCultures[SiteClient.SiteCulture];
            }
            return retVal;
        }

        /// <summary>
        /// Returns an input element with the currency symbol and/or code code displayed next to it as configured in the admin control panel
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="name">the value to use for the &quot;name&quot; and &quot;id&quot; html attibutes</param>
        /// <param name="value">a string representation of the current value, expected to be formatted with the CultureInfo of the current user or the site culture, if not set</param>
        /// <param name="currencyISO">the ISO currency code of the specified currency</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        public static MvcHtmlString CurrencyBox(this HtmlHelper htmlHelper, string name, string value, string currencyISO, object htmlAttributes)
        {
            return CurrencyBox(htmlHelper, name, value, currencyISO, htmlAttributes, null);
        }

        /// <summary>
        /// Returns an input element with the currency symbol and/or code code displayed next to it as configured in the admin control panel
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="name">the value to use for the &quot;name&quot; and &quot;id&quot; html attibutes</param>
        /// <param name="value">a string representation of the current value, expected to be formatted with the CultureInfo of the current user or the site culture, if not set</param>
        /// <param name="currencyISO">the ISO currency code of the specified currency</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        /// <param name="addOnText">optional text to add onto the end of the text box</param>
        public static MvcHtmlString CurrencyBox(this HtmlHelper htmlHelper, string name, string value, string currencyISO, object htmlAttributes, string addOnText)
        {
            /*
                <add key="EnableHtml5NumberInputs" value="true" /><!-- Note: this may need to be disabled for certain cultures -->
                <add key="Html5NumberInputStepValue" value="0.01" />
            */
            bool enableHtml5NumberInputs = true;
            bool tempBool1;
            if (bool.TryParse(ConfigurationManager.AppSettings["EnableHtml5NumberInputs"], out tempBool1))
            {
                enableHtml5NumberInputs = tempBool1;
            }
            string html5NumberInputStepValue = "0.01";
            if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["Html5NumberInputStepValue"]))
            {
                html5NumberInputStepValue = ConfigurationManager.AppSettings["Html5NumberInputStepValue"];
            }

            CultureInfo numberCulture = SiteClient.SupportedCultures[htmlHelper.GetCookie(Strings.MVC.CultureCookie) ?? Strings.FieldDefaults.Culture];
            numberCulture.NumberFormat.CurrencySymbol = string.Empty;

            TagBuilder builder = new TagBuilder("input");

            builder.MergeAttribute("name", name, true);

            if (enableHtml5NumberInputs)
            {
                builder.MergeAttribute("type", "number");
                builder.MergeAttribute("step", "0.01");
            }
            else
            {
                builder.MergeAttribute("type", "text");
            }

            if (htmlAttributes != null)
            {
                var attribsToMerge = htmlAttributes.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(htmlAttributes, null));
                builder.MergeAttributes<string, object>(attribsToMerge, true);
            }
            if (!builder.Attributes.Any(attr => attr.Key.Equals("id", StringComparison.OrdinalIgnoreCase)))
            {
                builder.MergeAttribute("id", name, true);
            }
            if (!builder.Attributes.Any(attr => attr.Key.Equals("class", StringComparison.OrdinalIgnoreCase) && attr.Value.Contains("form-control")))
            {
                builder.AddCssClass("form-control");
            }

            //determine if error class required
            ModelState state;
            if (htmlHelper.ViewData.ModelState.TryGetValue(name, out state) && (state.Errors.Count > 0))
            {
                builder.AddCssClass(HtmlHelper.ValidationInputCssClassName);
            }

            //Try to get value from ModelState, ViewData, and finally the value parameter, in that order
            string strValue = string.Empty;
            if (state != null && state.Value != null)
            {
                strValue = state.Value.AttemptedValue;
            }
            else if (htmlHelper.ViewData.ContainsKey(name))
            {
                strValue = htmlHelper.ViewData[name].ToString();
            }
            else
            {
                strValue = value;
            }

            decimal decValue;
            if (decimal.TryParse(strValue, NumberStyles.Currency, numberCulture, out decValue))
                strValue = htmlHelper.DecimalToPlainCurrencyString(decValue);

            builder.MergeAttribute("value", strValue);

            string textBox = builder.ToString(TagRenderMode.SelfClosing);

            string retVal = string.Empty;

            if (SiteClient.BoolSetting(Strings.SiteProperties.ShowCurrencySymbol))
            {
                if (SiteClient.BoolSetting(Strings.SiteProperties.ShowCurrencyCodes))
                {
                    //display currency code and symbol
                    switch (numberCulture.NumberFormat.CurrencyPositivePattern)
                    {
                        case 0:
                            retVal = "<div class='input-group'>" + "<span class='input-group-addon'>" + 
                                SiteClient.SupportedCurrencyRegions[currencyISO].CurrencySymbol + "</span>" + 
                                textBox + "<span class='input-group-addon'>" + currencyISO + 
                                (!string.IsNullOrWhiteSpace(addOnText) ? " " +  addOnText : string.Empty) +
                                "</span>" + "</div>";
                            break;
                        case 1:
                            retVal = "<div class='input-group'>" + textBox + "<span class='input-group-addon'>" + 
                                SiteClient.SupportedCurrencyRegions[currencyISO].CurrencySymbol + "&nbsp; " + currencyISO +
                                (!string.IsNullOrWhiteSpace(addOnText) ? " " + addOnText : string.Empty) +
                                "</span>" + "</div>";
                            break;
                        case 2:
                            retVal = "<div class='input-group'><span class='input-group-addon'>" + 
                                SiteClient.SupportedCurrencyRegions[currencyISO].CurrencySymbol + "</span> " + 
                                textBox + "<span class='input-group-addon'> " + currencyISO +
                                (!string.IsNullOrWhiteSpace(addOnText) ? " " + addOnText : string.Empty) +
                                "</span>" + "</div>";
                            break;
                        case 3:
                            retVal = "<div class='input-group'>" + textBox + "<span class='input-group-addon'>" + 
                                SiteClient.SupportedCurrencyRegions[currencyISO].CurrencySymbol + "&nbsp;  " + currencyISO +
                                (!string.IsNullOrWhiteSpace(addOnText) ? " " + addOnText : string.Empty) +
                                "</span>" + "</div>";
                            break;
                    }
                }
                else
                {
                    //do not display currency code but do display symbol
                    switch (numberCulture.NumberFormat.CurrencyPositivePattern)
                    {
                        case 0:
                            retVal = "<div class='input-group'><span class='input-group-addon'>" + 
                                SiteClient.SupportedCurrencyRegions[currencyISO].CurrencySymbol + "</span>" + textBox +
                                (!string.IsNullOrWhiteSpace(addOnText) ? "<span class='input-group-addon'>" + addOnText + "</span>" : string.Empty) +
                                "</div>";
                            break;
                        case 1:
                            retVal = "<div class='input-group'>" + textBox + "<span class='input-group-addon'>" + 
                                SiteClient.SupportedCurrencyRegions[currencyISO].CurrencySymbol +
                                (!string.IsNullOrWhiteSpace(addOnText) ? " " + addOnText : string.Empty) +
                                "</span>" + "</div>";
                            break;
                        case 2:
                            retVal = "<div class='input-group'>" + "<span class='input-group-addon'>" + 
                                SiteClient.SupportedCurrencyRegions[currencyISO].CurrencySymbol +
                                (!string.IsNullOrWhiteSpace(addOnText) ? " " + addOnText : string.Empty) +
                                "</span>" + textBox + "</div>";
                            break;
                        case 3:
                            retVal = "<div class='input-group'>" + textBox + "<span class='input-group-addon'> " + 
                                SiteClient.SupportedCurrencyRegions[currencyISO].CurrencySymbol +
                                (!string.IsNullOrWhiteSpace(addOnText) ? " " + addOnText : string.Empty) +
                                "</span>" + "</div>";
                            break;
                    }
                }
            }
            else
            {
                if (SiteClient.BoolSetting(Strings.SiteProperties.ShowCurrencyCodes))
                {
                    //display currency code, but not symbol
                    retVal = "<div class='input-group'>" + textBox + "<span class='input-group-addon'>" + currencyISO +
                        (!string.IsNullOrWhiteSpace(addOnText) ? " " + addOnText : string.Empty) +
                        "</span>" + "</div>";
                }
                else
                {
                    //do not display currency code or symbol
                    if (!string.IsNullOrWhiteSpace(addOnText))
                    {
                        retVal = "<div class='input-group'>" + textBox + "<span class='input-group-addon'>" +  addOnText  + "</span>" + "</div>";
                    }
                    else
                    {
                        retVal = textBox;
                    }
                }
            }

            return retVal.ToMvcHtmlString();
        }

        /// <summary>
        /// Returns an element showing all validation messages, localized using the relevant .resx resource file
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="modelName">name of the specified element</param>
        public static MvcHtmlString LocalizedValidationMessage(this HtmlHelper htmlHelper, string modelName)
        {
            return htmlHelper.LocalizedValidationMessage(modelName, null);
        }

        /// <summary>
        /// Returns an element showing all validation messages, localized using the relevant .resx resource file
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="modelName">name of the specified element</param>
        /// <param name="validationMessage">message key to be displayed</param>
        public static MvcHtmlString LocalizedValidationMessage(this HtmlHelper htmlHelper, string modelName, string validationMessage)
        {
            return htmlHelper.LocalizedValidationMessage(modelName, validationMessage, null);
        }

        /// <summary>
        /// Returns an element showing all validation messages, localized using the relevant .resx resource file
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="modelName">name of the specified element</param>
        /// <param name="validationMessage">message key to be displayed</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        public static MvcHtmlString LocalizedValidationMessage(this HtmlHelper htmlHelper, string modelName, string validationMessage, IDictionary<string, object> htmlAttributes)
        {
            if (modelName == null)
            {
                throw new ArgumentNullException("modelName");
            }
            if (!htmlHelper.ViewData.ModelState.ContainsKey(modelName))
            {
                return null;
            }
            ModelState modelState = htmlHelper.ViewData.ModelState[modelName];
            ModelErrorCollection errors = (modelState == null) ? null : modelState.Errors;
            ModelError error = ((errors == null) || (errors.Count == 0)) ? null : errors[0];
            if (error == null)
            {
                return null;
            }
            TagBuilder builder = new TagBuilder("span");
            builder.MergeAttributes<string, object>(htmlAttributes);
            builder.MergeAttribute("class", HtmlHelper.ValidationMessageCssClassName);
            builder.SetInnerText(string.IsNullOrEmpty(validationMessage) ? htmlHelper.ValidationResourceString(error.ErrorMessage) : htmlHelper.ValidationResourceString(validationMessage));
            return builder.ToMvcHtmlString(TagRenderMode.Normal);
        }

        /// <summary>
        /// Returns an element containign all applicable validation messages
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        public static MvcHtmlString LocalizedValidationSummary(this HtmlHelper htmlHelper)
        {
            return htmlHelper.LocalizedValidationSummary(null, null);
        }

        /// <summary>
        /// Returns an element containign all applicable validation messages
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="message">if not null or blank, a validation message with this resx key will also be included</param>
        public static MvcHtmlString LocalizedValidationSummary(this HtmlHelper htmlHelper, string message)
        {
            return htmlHelper.LocalizedValidationSummary(message, null);
        }

        /// <summary>
        /// Returns an element containign all applicable validation messages
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="message">if not null or blank, a validation message with this resx key will also be included</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        public static MvcHtmlString LocalizedValidationSummary(this HtmlHelper htmlHelper, string message, IDictionary<string, object> htmlAttributes)
        {
            string str;
            if (htmlHelper.ViewData.ModelState.IsValid)
            {
                return null;
            }
            if (!string.IsNullOrEmpty(message))
            {
                TagBuilder builder = new TagBuilder("span");
                builder.MergeAttributes<string, object>(htmlAttributes);
                builder.MergeAttribute("class", HtmlHelper.ValidationSummaryCssClassName);
                builder.SetInnerText(htmlHelper.ValidationResourceString(message));
                str = builder.ToString(TagRenderMode.Normal) + Environment.NewLine;
            }
            else
            {
                str = null;
            }
            StringBuilder builder2 = new StringBuilder();
            TagBuilder builder3 = new TagBuilder("ul");
            builder3.MergeAttributes<string, object>(htmlAttributes);
            builder3.MergeAttribute("class", HtmlHelper.ValidationSummaryCssClassName);
            foreach (string inputKey in htmlHelper.ViewData.ModelState.Keys)
            {
                ModelState state = htmlHelper.ViewData.ModelState[inputKey];
                foreach (ModelError error in state.Errors)
                {
                    string str2 = error.ErrorMessage;
                    if (!string.IsNullOrEmpty(str2))
                    {
                        string localizedMessage;
                        if (str2.StartsWith(Strings.ValidationMessages.CustomFieldValidationPrefix))
                        {
                            localizedMessage =
                                str2.Substring(Strings.ValidationMessages.CustomFieldValidationPrefix.Length);
                        }
                        else
                        {
                            localizedMessage = htmlHelper.ValidationResourceString(str2);
                        }
                        TagBuilder builder4 = new TagBuilder("li");
                        //builder4.SetInnerText(localizedMessage);
                        builder4.InnerHtml = localizedMessage;
                        builder4.MergeAttribute("data-input-key", inputKey);
                        builder2.AppendLine(builder4.ToString(TagRenderMode.Normal));
                    }
                }
            }
            builder3.InnerHtml = builder2.ToString();
            return (str + builder3.ToString(TagRenderMode.Normal)).ToMvcHtmlString();
        }

        /// <summary>
        /// Returns an element showing the currently authenticated user's username, and also an element showing the user being impersonated, if applicable
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        public static MvcHtmlString DisplayUserName(this HtmlHelper htmlHelper)
        {
            string retVal = string.Empty;

            if (htmlHelper.ViewContext.HttpContext.Request.Cookies[Strings.MVC.FBOUserName] != null && !string.IsNullOrEmpty(htmlHelper.ViewContext.HttpContext.Request.Cookies[Strings.MVC.FBOUserName].Value))
            {
                retVal = htmlHelper.ViewContext.HttpContext.User.Identity.Name
                    + " <span class=\"label label-success\">("
                    + htmlHelper.GlobalResource("Impersonating") + " \""
                    + htmlHelper.ViewContext.HttpContext.Request.Cookies[Strings.MVC.FBOUserName].Value
                    + "\")"
                    + "</span>";
            }
            else
            {
                retVal = htmlHelper.ViewContext.HttpContext.User.Identity.Name;
            }
            return retVal.ToMvcHtmlString();
        }

        /// <summary>
        /// Returns the username of the user being impersonated, if applicable, otherwise returns the username of the authenticated user
        /// </summary>
        public static string FBOUserName()
        {
            if (HttpContext.Current.Request.Cookies[Strings.MVC.FBOUserName] != null && !string.IsNullOrEmpty(HttpContext.Current.Request.Cookies[Strings.MVC.FBOUserName].Value))
            {
                if (HttpContext.Current.User.IsInRole(Strings.Roles.Admin))
                {
                    return HttpContext.Current.Request.Cookies[Strings.MVC.FBOUserName].Value;
                }
            }

            return HttpContext.Current.User.Identity.Name;
        }

        /// <summary>
        /// Returns the username of the user being impersonated, if applicable, otherwise returns the username of the authenticated user
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        public static string FBOUserName(this Controller controller)
        {
            if (controller.HttpContext.Request.Cookies[Strings.MVC.FBOUserName] != null && !string.IsNullOrEmpty(controller.HttpContext.Request.Cookies[Strings.MVC.FBOUserName].Value))
            {
                if (controller.User.IsInRole(Strings.Roles.Admin))
                {
                    return controller.HttpContext.Request.Cookies[Strings.MVC.FBOUserName].Value;
                }
            }

            return controller.User.Identity.Name;
        }

        /// <summary>
        /// Returns the username of the user being impersonated, if applicable, otherwise returns the username of the authenticated user
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        public static string FBOUserName(this HtmlHelper htmlHelper)
        {

            if (htmlHelper.ViewContext.HttpContext.Request.Cookies[Strings.MVC.FBOUserName] != null && !string.IsNullOrEmpty(htmlHelper.ViewContext.HttpContext.Request.Cookies[Strings.MVC.FBOUserName].Value))
            {
                if (htmlHelper.ViewContext.HttpContext.User.IsInRole(Strings.Roles.Admin))
                {
                    return htmlHelper.ViewContext.HttpContext.Request.Cookies[Strings.MVC.FBOUserName].Value;
                }
            }
            return htmlHelper.ViewContext.HttpContext.User.Identity.Name;
        }

        /// <summary>
        /// If the currently authenticated user is an admin user, sets a cookie indicating the user to be impersonated
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="userName">username of the user to be impersonated</param>
        public static void ImpersonateUser(this Controller controller, string userName)
        {
            if (HttpContext.Current.User.IsInRole(Strings.Roles.Admin))
            {
                controller.SetCookie(Strings.MVC.FBOUserName, userName);
            }
        }

        /// <summary>
        /// Returns a truncated version of the spcieifed string, with an ellipsis  (...) appended when the string is longer than the specified truncation length
        /// </summary>
        /// <param name="value">the specified string</param>
        /// <param name="length">if the specified string is longer than this value, truncation will occur</param>
        public static string Ellipsize(this string value, int length)
        {
            if (value.Length <= length) return value;

            return value.Substring(0, length - 3) + Strings.MVC.Ellipses;
        }

        /// <summary>
        /// Returns a truncated version of the spcieifed string, with an ellipsis (...) appended when the string is longer than the specified truncation length
        /// </summary>
        /// <param name="value">the specified string</param>
        /// <param name="length">if the specified string is longer than this value, truncation will occur</param>
        /// <remarks>adds a bracket around the ellipsis to prevent confustion with numbers</remarks>
        public static string BracketEllipsize(this string value, int length)
        {
            if (value.Length <= length) return value;

            return value.Substring(0, length - 5) + "[" + Strings.MVC.Ellipses + "]";
        }

        /// <summary>
        /// Returns an element showing the amount of time remaining between the specified date/time value and &quot;Now&quot;.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="value">a UTC date/time value</param>
        public static MvcHtmlString RemainingTime(this HtmlHelper htmlHelper, DateTime value)
        {
            return RemainingTime(htmlHelper, value, -1, string.Empty);
        }

        /// <summary>
        /// Returns an element showing the amount of time that has passed between &quot;Now&quot; and the specified date/time value.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="value">a UTC date/time value</param>
        public static MvcHtmlString TimeSince(this HtmlHelper htmlHelper, DateTime value)
        {
            return TimeSince(htmlHelper, value, -1, string.Empty);
        }

        /// <summary>
        /// Returns an element showing the amount of time that has passed between &quot;Now&quot; and the specified date/time value.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="value">a UTC date/time value</param>
        /// <param name="alertMinutes">if the result is less than or equal to this number of minutes then the result will be formatted using the specified &quot;alertFormatString&quot;</param>
        /// <param name="alertFormatString">the format string to apply to the result if it is less than &quot;alertMinutes&quot;</param>
        public static MvcHtmlString TimeSince(this HtmlHelper htmlHelper, DateTime value, int alertMinutes, string alertFormatString)
        {
            TimeSpan timeSince = DateTime.UtcNow.Subtract(value);
            return TimeDifferenceToString(timeSince, htmlHelper, alertMinutes, alertFormatString);
        }

        /// <summary>
        /// Returns an element showing the amount of time remaining between &quot;Now&quot; and the specified date/time value.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="value">a UTC date/time value</param>
        /// <param name="alertMinutes">if the result is less than or equal to this number of minutes then the result will be formatted using the specified &quot;alertFormatString&quot;</param>
        /// <param name="alertFormatString">the format string to apply to the result if it is less than &quot;alertMinutes&quot;</param>
        public static MvcHtmlString RemainingTime(this HtmlHelper htmlHelper, DateTime value, int alertMinutes, string alertFormatString)
        {
            if (value.Year >= (DateTime.UtcNow.Year + 10)) return htmlHelper.GlobalResource("Infinity");
            TimeSpan remainingTime = value.Subtract(DateTime.UtcNow);
            return TimeDifferenceToString(remainingTime, htmlHelper, alertMinutes, alertFormatString);
        }

        private static MvcHtmlString TimeDifferenceToString(TimeSpan time, HtmlHelper htmlHelper, int alertMinutes, string alertFormatString)
        {
            bool alert = time.TotalMinutes <= alertMinutes;

            StringBuilder sb = new StringBuilder();
            if (SiteClient.Settings[Strings.SiteProperties.TimeRemainingStyle] == "Active")
            {
                sb.Append(time.Days);
                if (time.Days > 1)
                {
                    sb.Append(" ");
                    sb.Append(htmlHelper.GlobalResource("Days"));
                    sb.Append(Strings.MVC.TimeUnitSuffix);
                }
                else
                {
                    sb.Append(" ");
                    sb.Append(htmlHelper.GlobalResource("Day"));
                    sb.Append(Strings.MVC.TimeUnitSuffix);
                }
                sb.Append(" " + time.Hours.ToString("00") + ":" + time.Minutes.ToString("00") + ":" + time.Seconds.ToString("00"));
                return alert ? string.Format(alertFormatString, sb).ToMvcHtmlString() : sb.ToMvcHtmlString();
            }
            else //if (SiteClient.Settings[Strings.SiteProperties.TimeRemainingStyle] == "Classic")
            {
                if (time.Days > 0)
                {
                    sb.Append(time.Days);
                    if (time.Days > 1)
                    {
                        sb.Append(" ");
                        sb.Append(htmlHelper.GlobalResource("Days"));
                        sb.Append(Strings.MVC.TimeUnitSuffix);
                    }
                    else
                    {
                        sb.Append(" ");
                        sb.Append(htmlHelper.GlobalResource("Day"));
                        sb.Append(Strings.MVC.TimeUnitSuffix);
                    }
                }

                if (time.Hours > 0)
                {
                    sb.Append(time.Hours);
                    if (time.Hours > 1)
                    {
                        sb.Append(" ");
                        sb.Append(htmlHelper.GlobalResource("Hours"));
                        sb.Append(Strings.MVC.TimeUnitSuffix);
                    }
                    else
                    {
                        sb.Append(" ");
                        sb.Append(htmlHelper.GlobalResource("Hour"));
                        sb.Append(Strings.MVC.TimeUnitSuffix);
                    }

                    if (time.Days > 0)
                    {
                        sb.Remove(sb.Length - 2, 2);
                        return alert ? string.Format(alertFormatString, sb).ToMvcHtmlString() : sb.ToMvcHtmlString();
                    }
                }

                if (time.Minutes > 0)
                {
                    sb.Append(time.Minutes);
                    if (time.Minutes > 1)
                    {
                        sb.Append(" ");
                        sb.Append(htmlHelper.GlobalResource("Minutes"));
                        sb.Append(Strings.MVC.TimeUnitSuffix);
                    }
                    else
                    {
                        sb.Append(" ");
                        sb.Append(htmlHelper.GlobalResource("Minute"));
                        sb.Append(Strings.MVC.TimeUnitSuffix);
                    }

                    if (time.Hours > 0 || time.Days > 0)
                    {
                        sb.Remove(sb.Length - 2, 2);
                        return alert ? string.Format(alertFormatString, sb).ToMvcHtmlString() : sb.ToMvcHtmlString();
                    }
                }

                if (time.Seconds > 0)
                {
                    sb.Append(time.Seconds);
                    if (time.Seconds > 1)
                    {
                        sb.Append(" ");
                        sb.Append(htmlHelper.GlobalResource("Seconds"));
                        sb.Append(Strings.MVC.TimeUnitSuffix);
                    }
                    else
                    {
                        sb.Append(" ");
                        sb.Append(htmlHelper.GlobalResource("Second"));
                        sb.Append(Strings.MVC.TimeUnitSuffix);
                    }
                }

                if (sb.ToString().EndsWith(Strings.MVC.TimeUnitSuffix))
                {
                    sb.Remove(sb.Length - Strings.MVC.TimeUnitSuffix.Length, Strings.MVC.TimeUnitSuffix.Length);
                }

                return alert ? string.Format(alertFormatString, sb).ToMvcHtmlString() : sb.ToMvcHtmlString();
            }
        }

        /// <summary>
        /// returns a string representation of a UTC date/time value, adjusted for the specified timezone and with the specified format string
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="utcDateTime">a UTC date value</param>
        /// <param name="targetTimeZoneId">the timezone Id to display in</param>
        /// <param name="format">a standard or custom format string</param>
        /// <returns>string</returns>
        public static MvcHtmlString LocalDTTM(this HtmlHelper htmlHelper, DateTime utcDateTime, string targetTimeZoneId, string format)
        {
            if (utcDateTime.Year >= (DateTime.UtcNow.Year + 10)) return htmlHelper.GlobalResource("Infinity");
            string culture = htmlHelper.GetCookie(Strings.MVC.CultureCookie);
            if (culture != null && !SiteClient.SupportedCultures.ContainsKey(culture))
            {
                htmlHelper.SetCookie(Strings.MVC.CultureCookie, SiteClient.Settings[Strings.SiteProperties.SiteCulture]);
                culture = SiteClient.Settings[Strings.SiteProperties.SiteCulture];
            }
            TimeZoneInfo targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(targetTimeZoneId);
            var localizedDateTime = TimeZoneInfo.ConvertTime(utcDateTime, TimeZoneInfo.Utc, targetTimeZone)
                .ToString(format, SiteClient.SupportedCultures[culture ?? SiteClient.Settings[Strings.SiteProperties.SiteCulture]]);
            //localizedDateTime += !string.IsNullOrEmpty(SiteClient.TextSetting(Strings.SiteProperties.TimeZoneLabel))
            //    ? " " + SiteClient.TextSetting(Strings.SiteProperties.TimeZoneLabel)
            //    : string.Empty;
            if (SiteClient.BoolSetting(Strings.SiteProperties.ShowTimeZoneLabel))
            {
                localizedDateTime += " " + htmlHelper.TimeZoneAbbreviationString(targetTimeZoneId);
            }
            return localizedDateTime.ToMvcHtmlString();
        }

        /// <summary>
        /// Converts the specified UTC date/time to the site timezone
        /// </summary>
        /// <param name="utcDateTime">a UTC date value</param>
        /// <returns>converted DateTime</returns>
        public static DateTime ToLocalDTTM(this DateTime utcDateTime)
        {
            TimeZoneInfo siteTimeZone = TimeZoneInfo.FindSystemTimeZoneById(SiteClient.SiteTimeZone);
            return TimeZoneInfo.ConvertTime(utcDateTime, TimeZoneInfo.Utc, siteTimeZone);
        }

        /// <summary>
        /// Converts the specified UTC date/time to the site timezone
        /// </summary>
        /// <param name="utcDateTime">a UTC date value</param>
        /// <param name="targetTimeZoneId">the timezone to convert to</param>
        /// <returns>converted DateTime</returns>
        public static DateTime ToLocalDTTM(this DateTime utcDateTime, string targetTimeZoneId)
        {
            TimeZoneInfo targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(targetTimeZoneId);
            return TimeZoneInfo.ConvertTime(utcDateTime, TimeZoneInfo.Utc, targetTimeZone);
        }

        /// <summary>
        /// returns a string representation of a UTC date/time value, adjusted for the specified timezone and with the specified format string
        /// </summary>
        /// <param name="controller">an instance of the Mvc Controller class</param>
        /// <param name="utcDateTime">a UTC date value</param>
        /// <param name="targetTimeZoneId">the timezone to display in</param>
        /// <param name="format">a standard or custom format string</param>
        /// <returns>MvcHtmlString</returns>
        public static MvcHtmlString LocalDTTM(this Controller controller, DateTime utcDateTime, string targetTimeZoneId, string format)
        {
            if (utcDateTime.Year >= (DateTime.UtcNow.Year + 10)) return controller.GlobalResource("Infinity");
            string culture = controller.GetCookie(Strings.MVC.CultureCookie);
            if (culture != null && !SiteClient.SupportedCultures.ContainsKey(culture))
            {
                controller.SetCookie(Strings.MVC.CultureCookie, SiteClient.Settings[Strings.SiteProperties.SiteCulture]);
                culture = SiteClient.Settings[Strings.SiteProperties.SiteCulture];
            }
            TimeZoneInfo targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(targetTimeZoneId);
            var localizedDateTime = TimeZoneInfo.ConvertTime(utcDateTime, TimeZoneInfo.Utc, targetTimeZone)
                .ToString(format, SiteClient.SupportedCultures[culture ?? SiteClient.Settings[Strings.SiteProperties.SiteCulture]]);
            //localizedDateTime += !string.IsNullOrEmpty(SiteClient.TextSetting(Strings.SiteProperties.TimeZoneLabel))
            //    ? " " + SiteClient.TextSetting(Strings.SiteProperties.TimeZoneLabel)
            //    : string.Empty;
            return localizedDateTime.ToMvcHtmlString();
        }

        /// <summary>
        /// returns a string representation of a UTC date/time value, adjusted for the site timezone and with the specified format string
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="utcDateTime">a UTC date value</param>
        /// <param name="format">a standard or custom format string</param>
        /// <returns>MvcHtmlString</returns>
        public static MvcHtmlString LocalDTTM(this HtmlHelper htmlHelper, DateTime utcDateTime, string format)
        {
            if (utcDateTime.Year >= (DateTime.UtcNow.Year + 10)) return htmlHelper.GlobalResource("Infinity");
            string culture = htmlHelper.GetCookie(Strings.MVC.CultureCookie);
            if (culture != null && !SiteClient.SupportedCultures.ContainsKey(culture))
            {
                htmlHelper.SetCookie(Strings.MVC.CultureCookie, SiteClient.Settings[Strings.SiteProperties.SiteCulture]);
                culture = SiteClient.Settings[Strings.SiteProperties.SiteCulture];
            }
            //return dateTime.AddHours(SiteClient.TimeZoneOffset).ToString(format, SiteClient.SupportedCultures[culture ?? SiteClient.Settings[Strings.SiteProperties.SiteCulture]]).ToMvcHtmlString();
            TimeZoneInfo siteTimeZone = TimeZoneInfo.FindSystemTimeZoneById(SiteClient.SiteTimeZone);
            var localizedDateTime = TimeZoneInfo.ConvertTime(utcDateTime, TimeZoneInfo.Utc, siteTimeZone)
                .ToString(format, SiteClient.SupportedCultures[culture ?? SiteClient.Settings[Strings.SiteProperties.SiteCulture]]);
            //localizedDateTime += !string.IsNullOrEmpty(SiteClient.TextSetting(Strings.SiteProperties.TimeZoneLabel))
            //    ? " " + SiteClient.TextSetting(Strings.SiteProperties.TimeZoneLabel)
            //    : string.Empty;
            if (SiteClient.BoolSetting(Strings.SiteProperties.ShowTimeZoneLabel))
            {
                localizedDateTime += " " + htmlHelper.TimeZoneAbbreviationString(SiteClient.SiteTimeZone);
            }
            return localizedDateTime.ToMvcHtmlString();
        }

        /// <summary>
        /// returns a string representation of a UTC date/time value, adjusted for the site timezone and with the specified format string
        /// </summary>
        /// <param name="controller">an instance of the Mvc Controller class</param>
        /// <param name="utcDateTime">a UTC date value</param>
        /// <param name="format">a standard or custom format string</param>
        /// <returns>MvcHtmlString</returns>
        public static MvcHtmlString LocalDTTM(this Controller controller, DateTime utcDateTime, string format)
        {
            if (utcDateTime.Year >= (DateTime.UtcNow.Year + 10)) return controller.GlobalResource("Infinity");
            string culture = controller.GetCookie(Strings.MVC.CultureCookie);
            if (culture != null && !SiteClient.SupportedCultures.ContainsKey(culture))
            {
                controller.SetCookie(Strings.MVC.CultureCookie, SiteClient.Settings[Strings.SiteProperties.SiteCulture]);
                culture = SiteClient.Settings[Strings.SiteProperties.SiteCulture];
            }
            //return dateTime.AddHours(SiteClient.TimeZoneOffset).ToString(format, SiteClient.SupportedCultures[culture ?? SiteClient.Settings[Strings.SiteProperties.SiteCulture]]).ToMvcHtmlString();
            TimeZoneInfo siteTimeZone = TimeZoneInfo.FindSystemTimeZoneById(SiteClient.SiteTimeZone);
            var localizedDateTime = TimeZoneInfo.ConvertTime(utcDateTime, TimeZoneInfo.Utc, siteTimeZone)
                .ToString(format, SiteClient.SupportedCultures[culture ?? SiteClient.Settings[Strings.SiteProperties.SiteCulture]]);
            //localizedDateTime += !string.IsNullOrEmpty(SiteClient.TextSetting(Strings.SiteProperties.TimeZoneLabel)) 
            //    ? " " + SiteClient.TextSetting(Strings.SiteProperties.TimeZoneLabel) 
            //    : string.Empty;
            if (SiteClient.BoolSetting(Strings.SiteProperties.ShowTimeZoneLabel))
            {
                localizedDateTime += " " + controller.TimeZoneAbbreviationString(SiteClient.SiteTimeZone);
            }
            return localizedDateTime.ToMvcHtmlString();
        }

        /// <summary>
        /// returns a string representation of a UTC date/time value, adjusted for the site timezone and with the specified format string
        /// </summary>
        /// <param name="controller">an instance of the Mvc Controller class</param>
        /// <param name="utcDateTime">a UTC date value</param>
        /// <param name="format">a standard or custom format string</param>
        /// <returns>string</returns>
        public static string LocalDTTMString(this Controller controller, DateTime utcDateTime, string format)
        {
            if (utcDateTime.Year >= (DateTime.UtcNow.Year + 10)) return controller.GlobalResourceString("Infinity");
            string culture = controller.GetCookie(Strings.MVC.CultureCookie);
            if (culture != null && !SiteClient.SupportedCultures.ContainsKey(culture))
            {
                controller.SetCookie(Strings.MVC.CultureCookie, SiteClient.Settings[Strings.SiteProperties.SiteCulture]);
                culture = SiteClient.Settings[Strings.SiteProperties.SiteCulture];
            }
            //return dateTime.AddHours(SiteClient.TimeZoneOffset).ToString(format, SiteClient.SupportedCultures[culture ?? SiteClient.Settings[Strings.SiteProperties.SiteCulture]]).ToMvcHtmlString();
            TimeZoneInfo siteTimeZone = TimeZoneInfo.FindSystemTimeZoneById(SiteClient.SiteTimeZone);
            var localizedDateTime = TimeZoneInfo.ConvertTime(utcDateTime, TimeZoneInfo.Utc, siteTimeZone)
                .ToString(format, SiteClient.SupportedCultures[culture ?? SiteClient.Settings[Strings.SiteProperties.SiteCulture]]);
            //localizedDateTime += !string.IsNullOrEmpty(SiteClient.TextSetting(Strings.SiteProperties.TimeZoneLabel)) 
            //    ? " " + SiteClient.TextSetting(Strings.SiteProperties.TimeZoneLabel) 
            //    : string.Empty;
            if (SiteClient.BoolSetting(Strings.SiteProperties.ShowTimeZoneLabel))
            {
                localizedDateTime += " " + controller.TimeZoneAbbreviationString(SiteClient.SiteTimeZone);
            }
            return localizedDateTime;
        }

        /// <summary>
        /// returns a string representation of a UTC date/time value, adjusted for the site timezone and with format string &quot;F&quot;
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="utcDateTime">a UTC date value</param>
        /// <returns>MvcHtmlString</returns>
        public static MvcHtmlString LocalDTTM(this HtmlHelper htmlHelper, DateTime utcDateTime)
        {
            return LocalDTTM(htmlHelper, utcDateTime, Strings.Formats.DateTime);
        }

        /// <summary>
        /// returns a string representation of a UTC date/time value, adjusted for the site timezone and with format string &quot;F&quot;
        /// </summary>
        /// <param name="controller">an instance of the Mvc Controller class</param>
        /// <param name="utcDateTime">a UTC date value</param>
        /// <returns>MvcHtmlString</returns>
        public static MvcHtmlString LocalDTTM(this Controller controller, DateTime utcDateTime)
        {
            return LocalDTTM(controller, utcDateTime, Strings.Formats.DateTime);
        }

        /// <summary>
        /// returns a string representation of a date/time value, adjusted for the site timezone and formatted as a culture-invariant value
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="utcDateTime">a UTC date value</param>
        /// <returns>MvcHtmlString</returns>
        public static MvcHtmlString CultureInvariantLocalDTTM(this HtmlHelper htmlHelper, DateTime utcDateTime)
        {
            if (utcDateTime.Year >= (DateTime.UtcNow.Year + 10)) return htmlHelper.GlobalResource("Infinity");
            //return dateTime.AddHours(SiteClient.TimeZoneOffset).ToString(CultureInfo.InvariantCulture).ToMvcHtmlString();
            TimeZoneInfo siteTimeZone = TimeZoneInfo.FindSystemTimeZoneById(SiteClient.SiteTimeZone);
            return TimeZoneInfo.ConvertTime(utcDateTime, TimeZoneInfo.Utc, siteTimeZone)
                .ToString(CultureInfo.InvariantCulture).ToMvcHtmlString();
        }

        /// <summary>
        /// returns a string representation of a date/time value, adjusted for the site timezone and formatted as a culture-invariant value
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="utcDateTime">a UTC date value</param>
        /// <param name="targetTimeZoneId">the timezone to display in</param>
        /// <returns>MvcHtmlString</returns>
        public static MvcHtmlString CultureInvariantLocalDTTM(this HtmlHelper htmlHelper, DateTime utcDateTime, string targetTimeZoneId)
        {
            if (utcDateTime.Year >= (DateTime.UtcNow.Year + 10)) return htmlHelper.GlobalResource("Infinity");
            //return dateTime.AddHours(SiteClient.TimeZoneOffset).ToString(CultureInfo.InvariantCulture).ToMvcHtmlString();
            TimeZoneInfo targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(targetTimeZoneId);
            return TimeZoneInfo.ConvertTime(utcDateTime, TimeZoneInfo.Utc, targetTimeZone)
                .ToString(CultureInfo.InvariantCulture).ToMvcHtmlString();
        }

        /// <summary>
        /// returns a string representation of an integer value formatted with the specified format string
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="data">the integer value to be converted</param>
        /// <param name="format">a standard or custom format string</param>
        /// <returns>string</returns>
        public static MvcHtmlString LocalInteger(this HtmlHelper htmlHelper, int data, string format)
        {
            string culture = htmlHelper.GetCookie(Strings.MVC.CultureCookie);
            if (culture != null && !SiteClient.SupportedCultures.ContainsKey(culture))
            {
                htmlHelper.SetCookie(Strings.MVC.CultureCookie, SiteClient.Settings[Strings.SiteProperties.SiteCulture]);
                culture = SiteClient.Settings[Strings.SiteProperties.SiteCulture];
            }
            return data.ToString(format, SiteClient.SupportedCultures[culture ?? SiteClient.Settings[Strings.SiteProperties.SiteCulture]]).ToMvcHtmlString();
        }

        /// <summary>
        /// returns a string representation of an integer value formatted for the culture specified for the current user, with the format string &quot;N0&quot;
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="data">the integer value to be converted</param>
        /// <returns>string</returns>
        public static MvcHtmlString LocalInteger(this HtmlHelper htmlHelper, int data)
        {
            return LocalInteger(htmlHelper, data, Strings.Formats.Integer);
        }

        /// <summary>
        /// Renders a element containing page links
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="page">an instance of a Page&lt;T&gt; page container object</param>
        public static void RenderPageNumberLinks<T>(this HtmlHelper htmlHelper, Page<T> page)
        {
            RenderPageNumberLinks(htmlHelper, page.PageIndex, page.TotalPageCount, null, null, null, null, null);
        }

        /// <summary>
        /// Renders a element containing page links
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="page">an instance of a Page&lt;T&gt; page container object</param>
        /// <param name="actionName">the name of the MVC action to be used for the page links</param>
        /// <param name="controllerName">the name of the MVC controller to be used for the page links</param>
        /// <param name="routeValues">An object that contains the route values to be used for each page link element.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for each page link element.</param>
        public static void RenderPageNumberLinks<T>(this HtmlHelper htmlHelper, Page<T> page, string actionName, string controllerName, object routeValues, object htmlAttributes)
        {
            RenderPageNumberLinks(htmlHelper, page.PageIndex, page.TotalPageCount, actionName, controllerName,
                                  routeValues, htmlAttributes, null);
        }

        /// <summary>
        /// Renders a element containing page links
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="page">an instance of a Page&lt;T&gt; page container object</param>
        /// <param name="actionName">the name of the MVC action to be used for the page links</param>
        /// <param name="controllerName">the name of the MVC controller to be used for the page links</param>
        /// <param name="routeValues">An object that contains the route values to be used for each page link element.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for each page link element.</param>
        /// <param name="containerHtmlAttribs">An object that contains the HTML attributes to set for the container element.</param>
        public static void RenderPageNumberLinks<T>(this HtmlHelper htmlHelper, Page<T> page, string actionName, string controllerName, object routeValues, object htmlAttributes, object containerHtmlAttribs)
        {
            RenderPageNumberLinks(htmlHelper, page.PageIndex, page.TotalPageCount, actionName, controllerName,
                                  routeValues, htmlAttributes, containerHtmlAttribs);
        }

        /// <summary>
        /// Renders a element containing page links
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="currentPage">0-based index of the page currently being viewed</param>
        /// <param name="totalPages">the total number of pages available</param>
        /// <param name="actionName">the name of the MVC action to be used for the page links</param>
        /// <param name="controllerName">the name of the MVC controller to be used for the page links</param>
        /// <param name="routeValues">An object that contains the route values to be used for each page link element.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for each page link element.</param>
        /// <param name="containerHtmlAttribs">An object that contains the HTML attributes to set for the container element.</param>
        public static void RenderPageNumberLinks(this HtmlHelper htmlHelper, int currentPage, int totalPages, string actionName, string controllerName, object routeValues, object htmlAttributes, object containerHtmlAttribs)
        {
            if (currentPage >= totalPages)
            {
                currentPage = totalPages;
            }

            RouteValueDictionary routes = new RouteValueDictionary(routeValues);
            if (routeValues == null)
            {
                foreach (string key in htmlHelper.ViewContext.HttpContext.Request.Form.AllKeys.Where(k => k != null))
                {
                    if (!routes.ContainsKey(key)) routes.Add(key, htmlHelper.ViewContext.HttpContext.Request.Form[key]);
                }
                foreach (string key in htmlHelper.ViewContext.HttpContext.Request.QueryString.AllKeys.Where(k => k != null))
                {
                    if (!routes.ContainsKey(key)) routes.Add(key, htmlHelper.ViewContext.HttpContext.Request.QueryString[key]);
                }
            }
            if (!routes.ContainsKey("page")) routes.Add("page", "0");

            if (string.IsNullOrEmpty(actionName))
            {
                actionName = (String)htmlHelper.ViewContext.RouteData.Values["action"];
            }

            TagBuilder tb = new TagBuilder("ul");
            tb.MergeAttribute("class", "pagination");
            tb.MergeAttributes((Dictionary<string, object>)containerHtmlAttribs, true);

            if (totalPages <= 0)
            {
                htmlHelper.ViewContext.Writer.Write(tb.ToString(TagRenderMode.Normal));
                return;
            }

            StringBuilder pagerlinks = new StringBuilder();

            //Start
            if (currentPage > 0)
            {
                pagerlinks.Append(EnabledPagerButton(htmlHelper, actionName, controllerName, routes, htmlAttributes,
                    currentPage - 1, "&laquo;"));
            }
            else
            {
                pagerlinks.Append(DisabledPagerButton(htmlHelper, htmlAttributes, "&laquo;"));
            }

            //First 2 Pages
            if (currentPage >= 2)
            {
                pagerlinks.Append(EnabledPagerButton(htmlHelper, actionName, controllerName, routes, htmlAttributes, 0, "1"));
                pagerlinks.Append(EnabledPagerButton(htmlHelper, actionName, controllerName, routes, htmlAttributes, 1, "2"));
            }

            //Left Partition
            if (currentPage >= 4)
            {
                if (currentPage == 4)
                {
                    pagerlinks.Append(EnabledPagerButton(htmlHelper, actionName, controllerName, routes, htmlAttributes, currentPage / 2, ((currentPage / 2) + 1).ToString()));
                }
                else if (currentPage == 5)
                {
                    pagerlinks.Append(EnabledPagerButton(htmlHelper, actionName, controllerName, routes, htmlAttributes, currentPage / 2, ((currentPage / 2) + 1).ToString()));
                    pagerlinks.Append(EnabledPagerButton(htmlHelper, actionName, controllerName, routes, htmlAttributes, (currentPage / 2) + 1, ((currentPage / 2) + 2).ToString()));
                }
                else
                {
                    pagerlinks.Append(DisabledPagerButton(htmlHelper, htmlAttributes, "..."));
                    pagerlinks.Append(EnabledPagerButton(htmlHelper, actionName, controllerName, routes, htmlAttributes, currentPage / 2, ((currentPage / 2) + 1).ToString()));
                    pagerlinks.Append(DisabledPagerButton(htmlHelper, htmlAttributes, "..."));
                }
            }

            //Current Page                              
            if (currentPage != 2 && currentPage != 0)
            {
                pagerlinks.Append(EnabledPagerButton(htmlHelper, actionName, controllerName, routes, htmlAttributes, currentPage - 1, currentPage.ToString()));
            }
            pagerlinks.Append(ActivePagerButton(htmlHelper, htmlAttributes, (currentPage + 1).ToString()));
            if (currentPage != (totalPages - 3) && currentPage != (totalPages - 1) && (currentPage < totalPages))
            {
                pagerlinks.Append(EnabledPagerButton(htmlHelper, actionName, controllerName, routes, htmlAttributes, currentPage + 1, (currentPage + 2).ToString()));
            }

            //Right Partition
            if (currentPage <= (totalPages - 5))
            {
                if (totalPages - currentPage == 5)
                {
                    pagerlinks.Append(EnabledPagerButton(htmlHelper, actionName, controllerName, routes, htmlAttributes, ((totalPages - currentPage) / 2) + currentPage, (((totalPages - currentPage) / 2) + currentPage + 1).ToString()));
                }
                else if (totalPages - currentPage == 6)
                {
                    pagerlinks.Append(EnabledPagerButton(htmlHelper, actionName, controllerName, routes, htmlAttributes, ((totalPages - currentPage) / 2) + currentPage - 1, (((totalPages - currentPage) / 2) + currentPage).ToString()));
                    pagerlinks.Append(EnabledPagerButton(htmlHelper, actionName, controllerName, routes, htmlAttributes, ((totalPages - currentPage) / 2) + currentPage, (((totalPages - currentPage) / 2) + currentPage + 1).ToString()));
                }
                else
                {
                    pagerlinks.Append(DisabledPagerButton(htmlHelper, htmlAttributes, "..."));
                    pagerlinks.Append(EnabledPagerButton(htmlHelper, actionName, controllerName, routes, htmlAttributes, ((totalPages - currentPage) / 2) + currentPage, (((totalPages - currentPage) / 2) + currentPage + 1).ToString()));
                    pagerlinks.Append(DisabledPagerButton(htmlHelper, htmlAttributes, "..."));
                }
            }

            //Last 2 Pages
            if (currentPage < totalPages - 2)
            {
                pagerlinks.Append(EnabledPagerButton(htmlHelper, actionName, controllerName, routes, htmlAttributes, totalPages - 2, (totalPages - 1).ToString()));
                pagerlinks.Append(EnabledPagerButton(htmlHelper, actionName, controllerName, routes, htmlAttributes, (totalPages - 1), totalPages.ToString()));
            }

            //End
            if (currentPage < (totalPages - 1))
            {
                pagerlinks.Append(EnabledPagerButton(htmlHelper, actionName, controllerName, routes, htmlAttributes, currentPage + 1, "&raquo;"));
            }
            else
            {
                pagerlinks.Append(DisabledPagerButton(htmlHelper, htmlAttributes, "&raquo;"));
            }

            tb.InnerHtml = pagerlinks.ToString();
            htmlHelper.ViewContext.Writer.Write(tb.ToString(TagRenderMode.Normal));
        }

        private static string EnabledPagerButton(HtmlHelper htmlHelper, string actionName, string controllerName, object routeValues, object htmlAttributes, int page, string display)
        {
            UrlHelper urlhelper = new UrlHelper(htmlHelper.ViewContext.RequestContext);
            string url;
            if (routeValues == null)
                url = urlhelper.Action(actionName, controllerName);
            else if (routeValues.GetType().Name == "RouteValueDictionary")
                url = urlhelper.Action(actionName, controllerName, (RouteValueDictionary)routeValues);
            else
                url = urlhelper.Action(actionName, controllerName, routeValues);

            url = Regex.Replace(url, @"page=\d+", "page=" + page.ToString());

            TagBuilder li = new TagBuilder("li");
            TagBuilder anchor = new TagBuilder("a");
            anchor.MergeAttribute("href", url);
            if (htmlAttributes != null) anchor.MergeAttributes((IDictionary<string, object>)htmlAttributes);
            anchor.InnerHtml = display;
            li.InnerHtml = anchor.ToString(TagRenderMode.Normal);
            return li.ToString(TagRenderMode.Normal);
        }

        private static string DisabledPagerButton(HtmlHelper htmlHelper, object htmlAttributes, string display)
        {
            TagBuilder li = new TagBuilder("li");
            li.MergeAttribute("class", "disabled");
            TagBuilder anchor = new TagBuilder("a");
            //anchor.MergeAttribute("href", "#");
            if (htmlAttributes != null) anchor.MergeAttributes((IDictionary<string, object>)htmlAttributes);
            anchor.InnerHtml = display;
            li.InnerHtml = anchor.ToString(TagRenderMode.Normal);
            return li.ToString(TagRenderMode.Normal);
        }

        private static string ActivePagerButton(HtmlHelper htmlHelper, object htmlAttributes, string display)
        {
            TagBuilder li = new TagBuilder("li");
            li.MergeAttribute("class", "active");
            TagBuilder anchor = new TagBuilder("a");
            //anchor.MergeAttribute("href", "#");
            if (htmlAttributes != null) anchor.MergeAttributes((IDictionary<string, object>)htmlAttributes);
            anchor.InnerHtml = display;
            li.InnerHtml = anchor.ToString(TagRenderMode.Normal);
            return li.ToString(TagRenderMode.Normal);
        }

        //as of v2.0/Razor these helper methods are obsolete
        //public static MvcHtmlString RenderDecorations(this HtmlHelper htmlHelper, Listing listing, string fieldName)
        //{
        //    return RenderDecorations(htmlHelper, listing, fieldName,
        //                             typeof(Listing).GetProperty(fieldName).GetValue(listing, null).ToString());
        //}

        //public static MvcHtmlString RenderDecorations(this HtmlHelper htmlHelper, Listing listing, string fieldName, string value)
        //{
        //    value = htmlHelper.Encode(value);

        //    foreach (Decoration decoration in listing.Decorations)
        //    {
        //        List<string> fieldsToDecorate = new List<string>(decoration.ValidFields.Split(','));
        //        if (fieldsToDecorate.Contains(fieldName)) value = string.Format(decoration.FormatString, value);
        //    }            
        //    return value.ToMvcHtmlString();
        //}

        /// <summary>
        /// Generates the absolute url, including SEO data, to the Detail page of this Listing
        /// </summary>
        /// <param name="listing">this Listing</param>
        /// <returns>a string representing the resulting URL</returns>
        public static string GetDetailUrl(this Listing listing)
        {
            var writer = new StringWriter();
            writer.Write(VirtualPathUtility.ToAbsolute("~/"));
            if (listing.Lot != null)
            {
                writer.Write("Event/LotDetails/");
                writer.Write(listing.Lot.ID);
            }
            else
            {
                writer.Write("Listing/Details/");
                writer.Write(listing.ID);
            }
            writer.Write("/");
            writer.Write(listing.Title.SimplifyForURL("-"));
            return writer.ToString();
        }

        /// <summary>
        /// Generates the Beginning HTML of an anchor tag with the absolute url, including SEO data, to the Detail page of this Listing
        /// </summary>
        /// <param name="listing">this Listing</param>
        /// <returns>a string representing the resulting HTML</returns>
        public static MvcHtmlString BeginDetailLink(this Listing listing)
        {
            //null check added to prevent a runtime error when viewing line items where the related listing has been permanently deleted
            if (listing == null) return MvcHtmlString.Empty;

            var writer = new StringWriter();
            writer.Write("<a href=\"");
            writer.Write(listing.GetDetailUrl());
            writer.Write("\">");
            return writer.ToMvcHtmlString();
        }

        /// <summary>
        /// Generates the Ending HTML of an anchor tag with the absolute url, including SEO data, to the Detail page of this Listing
        /// </summary>
        /// <param name="listing">this Listing</param>
        /// <returns>a string representing the resulting HTML</returns>
        public static MvcHtmlString EndDetailLink(this Listing listing)
        {
            //null check added to prevent a runtime error when viewing line items where the related listing has been permanently deleted
            if (listing == null) return MvcHtmlString.Empty;

            return ("</a>").ToMvcHtmlString();
        }

        /// <summary>
        /// Generates the HTML of an anchor tag with the absolute url, including SEO data, to the Detail page of this Listing, wrapping the specified value
        /// </summary>
        /// <param name="listing">this Listing</param>
        /// <param name="value">the specified value</param>
        /// <returns>a string representing the resulting HTML</returns>
        public static MvcHtmlString DetailLink(this Listing listing, string value)
        {
            var writer = new StringWriter();
            writer.Write(listing.BeginDetailLink());
            writer.Write(HttpUtility.HtmlEncode(value));
            writer.Write(listing.EndDetailLink());
            return writer.ToMvcHtmlString();
        }

        /// <summary>
        /// Generates the absolute url, including SEO data, to the Detail page of this Lot
        /// </summary>
        /// <param name="lot">this Lot</param>
        /// <returns>a string representing the resulting URL</returns>
        public static string GetDetailUrl(this Lot lot)
        {
            var writer = new StringWriter();
            writer.Write(VirtualPathUtility.ToAbsolute("~/"));
            writer.Write("Event/LotDetails/");
            writer.Write(lot.ID);
            writer.Write("/");
            writer.Write(lot.Listing.Title.SimplifyForURL("-"));
            return writer.ToString();
        }

        /// <summary>
        /// Retrieves the localized concise context message and the associated disposition (Positive, Neutral or Negative)
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="listing">A DTO.Listing</param>
        /// <param name="message">Outputs the short form of the status message for the applicable context.  If the context is not filled then it outputs empty string.</param>
        /// <param name="disposition">outputs either Positive, Neutral or Negative. If the context is not filled then it outputs Neutral.</param>
        public static void GetConciseBiddingContext(this HtmlHelper htmlHelper, Listing listing, out string message, out ContextDispositionType disposition)
        {
            message = "";
            disposition = ContextDispositionType.Neutral;
            if (listing.Context != null)
            {
                if (listing.OwnerUserName == htmlHelper.FBOUserName())
                    return;

                switch (listing.Context.Status)
                {
                    case "WINNING":
                        message = htmlHelper.ResourceString("AuctionListing, Winning_Concise");
                        disposition = ContextDispositionType.Positive;
                        break;
                    case "WON":
                    case "YOUR_OFFER_ACCEPTED":
                        message = htmlHelper.ResourceString("AuctionListing, Won_Concise");
                        disposition = ContextDispositionType.Positive;
                        break;
                    case "CURRENT_HIGH_BIDDER_RESERVE_NOT_MET":
                    case "LOSING":
                    case "RESERVE_NOT_MET":
                    case "NOTCURRENTLISTINGACTIONUSER":
                        message = htmlHelper.ResourceString("AuctionListing, NotWinning_Concise");
                        disposition = ContextDispositionType.Negative;
                        break;
                    case "LOST":
                    case "LOST_RESERVE_NOT_MET":
                    case "YOUR_OFFER_DECLINED":
                        message = htmlHelper.ResourceString("AuctionListing, Lost_Concise");
                        disposition = ContextDispositionType.Negative;
                        break;
                    default:
                        message = "";
                        disposition = ContextDispositionType.Neutral;
                        break;
                }

            }
        }

        /// <summary>
        /// Generates the absolute url, including SEO data, to the Detail page of this Event
        /// </summary>
        /// <param name="auctionEvent">this Event</param>
        /// <returns>a string representing the resulting URL</returns>
        public static string GetDetailUrl(this Event auctionEvent)
        {
            var writer = new StringWriter();
            writer.Write(VirtualPathUtility.ToAbsolute("~/"));
            writer.Write("Event/Details/");
            writer.Write(auctionEvent.ID);
            writer.Write("/");
            writer.Write(auctionEvent.Title.SimplifyForURL("-"));
            return writer.ToString();
        }

        /// <summary>
        /// Generates the Beginning HTML of an anchor tag with the absolute url, including SEO data, to the Detail page of this Event
        /// </summary>
        /// <param name="auctionEvent">this Event</param>
        /// <returns>a string representing the resulting HTML</returns>
        public static MvcHtmlString BeginDetailLink(this Event auctionEvent)
        {
            var writer = new StringWriter();
            writer.Write("<a href=\"");
            writer.Write(auctionEvent.GetDetailUrl());
            writer.Write("\">");
            return writer.ToMvcHtmlString();
        }

        /// <summary>
        /// Generates the Ending HTML of an anchor tag with the absolute url, including SEO data, to the Detail page of this Event
        /// </summary>
        /// <param name="auctionEvent">this Event</param>
        /// <returns>a string representing the resulting HTML</returns>
        public static MvcHtmlString EndDetailLink(this Event auctionEvent)
        {
            return ("</a>").ToMvcHtmlString();
        }

        /// <summary>
        /// Generates the HTML of an anchor tag with the absolute url, including SEO data, to the Detail page of this Event, wrapping the specified value
        /// </summary>
        /// <param name="auctionEvent">this Event</param>
        /// <param name="value">the specified value</param>
        /// <returns>a string representing the resulting HTML</returns>
        public static MvcHtmlString DetailLink(this Event auctionEvent, string value)
        {
            var writer = new StringWriter();
            writer.Write(auctionEvent.BeginDetailLink());
            writer.Write(HttpUtility.HtmlEncode(value));
            writer.Write(auctionEvent.EndDetailLink());
            return writer.ToMvcHtmlString();
        }

        /// <summary>
        /// Returns a string representation of the specified numeric value formatted as the specified currency.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="rawNumericValue">a string representing a numeric value, expected to be formatted in the current user's culture, or site culture if missing</param>
        /// <param name="currencyISOCode">the ISO currency code of the specified currency</param>
        /// <remarks>If &quot;rawNumericValue&quot; is not parsable as a numeric value, it is returned as-is with no additional formatting</remarks>
        public static string LocalCurrency(this HtmlHelper htmlHelper, string rawNumericValue, string currencyISOCode)
        {
            //999998888877777666665555544444333332222211111 should not crash...
            string culture = htmlHelper.GetCookie(Strings.MVC.CultureCookie);
            var currentCultureInfo = new CultureInfo(culture);
            decimal tempDecVal;
            if (decimal.TryParse(rawNumericValue, NumberStyles.Currency, currentCultureInfo, out tempDecVal))
                return LocalCurrency(htmlHelper, tempDecVal, currencyISOCode);
            else
                return rawNumericValue;
        }

        /// <summary>
        /// Returns a string representation of the specified numeric value formatted as the specified currency.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="amount">the numeric value to be formatted</param>
        /// <param name="currencyISOCode">the ISO currency code of the specified currency</param>
        public static string LocalCurrency(this HtmlHelper htmlHelper, decimal amount, string currencyISOCode)
        {
            //return amount.ToString("c", CultureInfo.GetCultureInfo(htmlHelper.GetCookie("culture")));      
            string culture = htmlHelper.GetCookie(Strings.MVC.CultureCookie);
            if (culture != null && !SiteClient.SupportedCultures.ContainsKey(culture))
            {
                htmlHelper.SetCookie(Strings.MVC.CultureCookie, SiteClient.Settings[Strings.SiteProperties.SiteCulture]);
                culture = SiteClient.Settings[Strings.SiteProperties.SiteCulture];
            }
            return SiteClient.FormatCurrency(amount, currencyISOCode, culture ?? SiteClient.Settings[Strings.SiteProperties.SiteCulture]);
        }

        /// <summary>
        /// Returns HTML showing the specified numeric value formatted as the specified currency.
        /// The numeric portion will be wrapped in a &lt;span&gt; tag with css class &quot;NumberPart&quot; applied
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="amount">the numeric value to be formatted</param>
        /// <param name="currencyISOCode">the ISO currency code of the specified currency</param>
        public static HtmlString LocalCurrencyWithNumberTags(this HtmlHelper htmlHelper, decimal amount, string currencyISOCode)
        {
            return LocalCurrencyWithNumberTags(htmlHelper, amount, currencyISOCode, "<span class=\"NumberPart\">", "</span>");
        }

        /// <summary>
        /// Returns HTML showing the specified numeric value formatted as a currency value.
        /// The numeric portion will be wrapped in the tags specified by &quot;prefix&quot; and &quot;postfix&quot;
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="amount">the numeric value to be formatted</param>
        /// <param name="currencyISOCode">the ISO currency code of the specified currency</param>
        /// <param name="prefix">this tag will be inserted immediately before the numeric portion of the result</param>
        /// <param name="postfix">this tag will be inserted immediately after the numeric portion of the result</param>
        public static HtmlString LocalCurrencyWithNumberTags(this HtmlHelper htmlHelper, decimal amount, string currencyISOCode, string prefix, string postfix)
        {
            //return amount.ToString("c", CultureInfo.GetCultureInfo(htmlHelper.GetCookie("culture")));      
            string culture = htmlHelper.GetCookie(Strings.MVC.CultureCookie);
            if (culture != null && !SiteClient.SupportedCultures.ContainsKey(culture))
            {
                htmlHelper.SetCookie(Strings.MVC.CultureCookie, SiteClient.Settings[Strings.SiteProperties.SiteCulture]);
                culture = SiteClient.Settings[Strings.SiteProperties.SiteCulture];
            }
            return new HtmlString(SiteClient.FormatCurrencyWithNumberTags(amount, currencyISOCode, culture ?? SiteClient.Settings[Strings.SiteProperties.SiteCulture],
                prefix, postfix));
        }

        /// <summary>
        /// Returns HTML showing the specified numeric value formatted as the specified currency.
        /// The numeric portion will be wrapped in a &lt;span&gt; tag with css class &quot;NumberPart&quot; applied
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="amount">the numeric value to be formatted</param>
        /// <param name="currencyISOCode">the ISO currency code of the specified currency</param>
        public static string LocalCurrency(this Controller controller, decimal amount, string currencyISOCode)
        {
            //return amount.ToString("c", CultureInfo.GetCultureInfo(htmlHelper.GetCookie("culture")));      
            string culture = controller.GetCookie(Strings.MVC.CultureCookie);
            if (culture != null && !SiteClient.SupportedCultures.ContainsKey(culture))
            {
                controller.SetCookie(Strings.MVC.CultureCookie, SiteClient.Settings[Strings.SiteProperties.SiteCulture]);
                culture = SiteClient.Settings[Strings.SiteProperties.SiteCulture];
            }
            return SiteClient.FormatCurrency(amount, currencyISOCode, culture ?? SiteClient.Settings[Strings.SiteProperties.SiteCulture]);
        }

        /// <summary>
        /// Returns a string representation of the specified numeric value formatted as the site currency.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="amount">the numeric value to be formatted</param>
        public static string SiteCurrency(this HtmlHelper htmlHelper, decimal amount)
        {
            return LocalCurrency(htmlHelper, amount, SiteClient.Settings[Strings.SiteProperties.SiteCurrency]);
        }

        /// <summary>
        /// Returns a string representation of the specified numeric value formatted as the site currency.
        /// If the specified value is less than or equal to zero then the localized version of &quot;Free&quot; is returned instead.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="amount">the numeric value to be formatted</param>
        public static string SiteCurrencyOrFree(this HtmlHelper htmlHelper, decimal amount)
        {
            if (amount > 0)
            {
                return LocalCurrency(htmlHelper, amount, SiteClient.Settings[Strings.SiteProperties.SiteCurrency]);
            }
            else
            {
                return htmlHelper.GlobalResource("Free").ToString();
            }
        }

        /// <summary>
        /// Returns a string representation of the specified numeric value formatted as the site currency.
        /// If the specified value is less than or equal to zero then the localized version of &quot;Free&quot; is returned instead.
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="amount">the numeric value to be formatted</param>
        public static string SiteCurrencyOrFree(this Controller controller, decimal amount)
        {
            if (amount > 0)
            {
                return LocalCurrency(controller, amount, SiteClient.Settings[Strings.SiteProperties.SiteCurrency]);
            }
            else
            {
                return controller.GlobalResource("Free").ToString();
            }
        }

        /// <summary>
        /// Sets a cookie for the current user with the specified key and value
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="key">the specified cookie key</param>
        /// <param name="value">the specified cookie value</param>
        public static void SetCookie(this Controller controller, string key, string value)
        {
            if (key == null || value == null) throw new ArgumentNullException();
            controller.HttpContext.Response.Cookies.Set(new HttpCookie(key, value) { Expires = DateTime.UtcNow.AddMonths(1) });
        }

        /// <summary>
        /// Sets a cookie for the current user with the specified key and value
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="key">the specified cookie key</param>
        /// <param name="value">the specified cookie value</param>
        public static void SetCookie(this HtmlHelper htmlHelper, string key, string value)
        {
            if (key == null || value == null) throw new ArgumentNullException();
            htmlHelper.ViewContext.HttpContext.Response.Cookies.Set(new HttpCookie(key, value) { Expires = DateTime.UtcNow.AddMonths(1) });
        }

        /// <summary>
        /// Returns a string representation of the browser cookie value associated with the specified key
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="key">the specified cookie key</param>
        public static string GetCookie(this Controller controller, string key)
        {
            if (key == null) throw new ArgumentNullException();

            if (controller.HttpContext.Request.Cookies[key] == null)
            {
                if (key.Equals("culture")) return SiteClient.SiteCulture;
                else if (key.Equals("currency")) return SiteClient.SiteCurrency;
                else return null;
            }
            else
            {
                return controller.HttpContext.Request.Cookies[key].Value;
            }

            //return controller.HttpContext.Request.Cookies[key] != null ? (controller.HttpContext.Request.Cookies[key].Value) : null;
        }

        /// <summary>
        /// Returns a string representation of the browser cookie value associated with the specified key
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="key">the specified cookie key</param>
        public static string GetCookie(this HtmlHelper htmlHelper, string key)
        {
            if (key == null) throw new ArgumentNullException();

            if (htmlHelper.ViewContext.HttpContext.Request.Cookies[key] == null)
            {
                if (key.Equals("culture")) return SiteClient.SiteCulture;
                else if (key.Equals("currency")) return SiteClient.SiteCurrency;
                else return null;
            }
            else
            {
                return htmlHelper.ViewContext.HttpContext.Request.Cookies[key].Value;
            }

            //return htmlHelper.ViewContext.HttpContext.Request.Cookies[key] != null ? (htmlHelper.ViewContext.HttpContext.Request.Cookies[key].Value) : null;
        }

        /// <summary>
        /// Renders a form input for the specified custom field
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="field">the specified custom field</param>
        /// <param name="htmlAttributes">An optional object that contains the HTML attributes to set for each page link element.</param>
        /// <param name="showDefaultValue">if true, the default value assigned to this field will be pre-filled in the result</param>
        public static void RenderCustomField(this HtmlHelper htmlHelper, CustomField field, object htmlAttributes = null, bool showDefaultValue = false)
        {
            string defaultValue = null;
            if (showDefaultValue)
            {
                defaultValue = field.DefaultValue;
            }

            switch (field.Type)
            {
                case CustomFieldType.Boolean:
                    bool boolValue = false;
                    if (showDefaultValue)
                    {
                        bool.TryParse(defaultValue, out boolValue);
                    }
                    htmlHelper.ViewContext.Writer.Write("<div class='checkbox'>");
                    htmlHelper.ViewContext.Writer.Write("<label>");
                    htmlHelper.ViewContext.Writer.Write(htmlHelper.CheckBox(field.Name, boolValue));
                    htmlHelper.ViewContext.Writer.Write(htmlHelper.CustomFieldResourceString(field.Name));
                    htmlHelper.ViewContext.Writer.Write("</label>");
                    htmlHelper.ViewContext.Writer.Write("</div>");
                    break;
                case CustomFieldType.Enum:
                    if (field.Enumeration.Count > 3)
                    {
                        //automatic drop down
                        List<ListItem> localizedList = new List<ListItem>(field.Enumeration.Count);
                        localizedList.Add(new ListItem(0, htmlHelper.CustomFieldResourceString("PleaseSelect_DropDownEmptyOptionLabel"), true, string.Empty));
                        foreach (ListItem li in field.Enumeration)
                        {
                            localizedList.Add(new ListItem(li.ID, htmlHelper.CustomFieldResourceString(li.Name), li.Enabled, li.Value));
                        }
                        if (showDefaultValue && localizedList.Any(li => li.Value == defaultValue))
                        {
                            htmlHelper.ViewContext.Writer.Write(htmlHelper.DropDownList(field.Name,
                                new SelectList(localizedList, Strings.Fields.Value, Strings.Fields.Name, defaultValue), new { @class = "form-control" }));
                        }
                        else
                        {
                            htmlHelper.ViewContext.Writer.Write(htmlHelper.DropDownList(field.Name,
                                new SelectList(localizedList, Strings.Fields.Value, Strings.Fields.Name), new { @class = "form-control" }));
                        }
                    }
                    else
                    {
                        //automatic radio buttons
                        int enumIndex = 0;
                        foreach (ListItem item in field.Enumeration)
                        {
                            htmlHelper.ViewContext.Writer.Write("<div class='radio'>");
                            htmlHelper.ViewContext.Writer.Write("<label>");
                            htmlHelper.ViewContext.Writer.Write(htmlHelper.RadioButton(field.Name, item.Value, (item.Value == defaultValue), new { @id = field.Name + "_" + enumIndex++ }));
                            htmlHelper.ViewContext.Writer.Write(htmlHelper.CustomFieldResourceString(item.Name));
                            htmlHelper.ViewContext.Writer.Write("</label>");
                            htmlHelper.ViewContext.Writer.Write("</div>");
                        }
                    }

                    break;
                case CustomFieldType.DateTime:

                    DateTime dateValue;
                    if (showDefaultValue && DateTime.TryParse(defaultValue, out dateValue))
                    {
                        defaultValue = dateValue.ToString("d", htmlHelper.GetCultureInfo());
                    }

                    RenderDateInput(htmlHelper, field.Name, defaultValue, new { @class = "form-control" });
                    break;
                case CustomFieldType.Decimal:
                    {
                        if (htmlAttributes == null)
                        {
                            htmlAttributes = new { @class = "form-control" };
                        }

                        if (!string.IsNullOrEmpty(field.DefaultValue))
                        {
                            if (showDefaultValue)
                            {
                                defaultValue = decimal.Parse(field.DefaultValue).ToString(Strings.Formats.Decimal, htmlHelper.GetCultureInfo());
                            }
                        }
                        else
                        {
                            defaultValue = string.Empty;
                        }

                        htmlHelper.ViewContext.Writer.Write(htmlHelper.TextBox(field.Name, defaultValue, htmlAttributes));

                        break;
                    }
                case CustomFieldType.Int:
                    {
                        if (htmlAttributes == null)
                        {
                            htmlAttributes = new { @class = "form-control" };
                        }

                        int intValue;
                        if (showDefaultValue && int.TryParse(defaultValue, out intValue))
                        {
                            defaultValue = intValue.ToString("N0", htmlHelper.GetCultureInfo());
                        }

                        htmlHelper.ViewContext.Writer.Write(htmlHelper.TextBox(field.Name, defaultValue, htmlAttributes));

                        break;
                    }
                default:
                    if (htmlAttributes == null)
                    {
                        htmlAttributes = new { @class = "form-control" };
                    }

                    htmlHelper.ViewContext.Writer.Write(htmlHelper.TextBox(field.Name, defaultValue, htmlAttributes));

                    break;
            }
        }

        /// <summary>
        /// Renders a form input, for use with an admin page, for the specified custom field
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="field">the specified custom field</param>
        /// <param name="htmlAttributes">An optional object that contains the HTML attributes to set for each page link element.</param>
        /// <param name="showDefaultValue">if true, the default value assigned to this field will be pre-filled in the result</param>
        public static void RenderCustomField_Admin(this HtmlHelper htmlHelper, CustomField field, object htmlAttributes = null, bool showDefaultValue = false)
        {
            string defaultValue = null;
            if (showDefaultValue)
            {
                defaultValue = field.DefaultValue;
            }

            switch (field.Type)
            {
                case CustomFieldType.Boolean:
                    bool boolValue = false;
                    if (showDefaultValue)
                    {
                        bool.TryParse(defaultValue, out boolValue);
                    }
                    //htmlHelper.ViewContext.Writer.Write("<div class='checkbox'>");
                    htmlHelper.ViewContext.Writer.Write(htmlHelper.CheckBox(field.Name, boolValue));
                    //htmlHelper.ViewContext.Writer.Write("</div>");
                    break;
                case CustomFieldType.Enum:
                    if (field.Enumeration.Count > 3)
                    {
                        //automatic drop down
                        List<ListItem> localizedList = new List<ListItem>(field.Enumeration.Count);
                        localizedList.Add(new ListItem(0, htmlHelper.CustomFieldResourceString("PleaseSelect_DropDownEmptyOptionLabel"), true, string.Empty));
                        foreach (ListItem li in field.Enumeration)
                        {
                            localizedList.Add(new ListItem(li.ID, htmlHelper.CustomFieldResourceString(li.Name), li.Enabled, li.Value));
                        }
                        if (showDefaultValue && localizedList.Any(li => li.Value == defaultValue))
                        {
                            htmlHelper.ViewContext.Writer.Write(htmlHelper.DropDownList(field.Name,
                                new SelectList(localizedList, Strings.Fields.Value, Strings.Fields.Name, defaultValue), new { @class = "form-control" }));
                        }
                        else
                        {
                            htmlHelper.ViewContext.Writer.Write(htmlHelper.DropDownList(field.Name,
                                new SelectList(localizedList, Strings.Fields.Value, Strings.Fields.Name), new { @class = "form-control" }));
                        }
                    }
                    else
                    {
                        //automatic radio buttons
                        int enumIndex = 0;
                        foreach (ListItem item in field.Enumeration)
                        {
                            htmlHelper.ViewContext.Writer.Write("<div class='radio'>");
                            htmlHelper.ViewContext.Writer.Write("<label>");
                            htmlHelper.ViewContext.Writer.Write(htmlHelper.RadioButton(field.Name, item.Value, (item.Value == defaultValue), new { @id = field.Name + "_" + enumIndex++ }));
                            htmlHelper.ViewContext.Writer.Write(htmlHelper.CustomFieldResourceString(item.Name));
                            htmlHelper.ViewContext.Writer.Write("</label>");
                            htmlHelper.ViewContext.Writer.Write("</div>");
                        }
                    }

                    break;
                case CustomFieldType.DateTime:

                    DateTime dateValue;
                    if (showDefaultValue && DateTime.TryParse(defaultValue, out dateValue))
                    {
                        defaultValue = dateValue.ToString("G", htmlHelper.GetCultureInfo());
                    }

                    RenderDateInput(htmlHelper, field.Name, defaultValue, new { @class = "form-control" });
                    break;
                case CustomFieldType.Decimal:
                    {
                        if (htmlAttributes == null)
                        {
                            htmlAttributes = new { @class = "form-control" };
                        }

                        if (!string.IsNullOrEmpty(field.DefaultValue))
                        {
                            if (showDefaultValue)
                            {
                                defaultValue = decimal.Parse(field.DefaultValue).ToString(Strings.Formats.Decimal, htmlHelper.GetCultureInfo());
                            }
                        }
                        else
                        {
                            defaultValue = string.Empty;
                        }

                        htmlHelper.ViewContext.Writer.Write(htmlHelper.TextBox(field.Name, defaultValue, htmlAttributes));

                        break;
                    }
                case CustomFieldType.Int:
                    {
                        if (htmlAttributes == null)
                        {
                            htmlAttributes = new { @class = "form-control" };
                        }

                        int intValue;
                        if (showDefaultValue && int.TryParse(defaultValue, out intValue))
                        {
                            defaultValue = intValue.ToString("N0", htmlHelper.GetCultureInfo());
                        }

                        htmlHelper.ViewContext.Writer.Write(htmlHelper.TextBox(field.Name, defaultValue, htmlAttributes));

                        break;
                    }
                default:
                    if (htmlAttributes == null)
                    {
                        htmlAttributes = new { @class = "form-control" };
                    }

                    htmlHelper.ViewContext.Writer.Write(htmlHelper.TextBox(field.Name, defaultValue, htmlAttributes));

                    break;
            }
        }

        /// <summary>
        /// Renders a form input element and associated javascript for the date picker widget
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="fieldName">the value to use for the &quot;name&quot; and &quot;id&quot; html attibutes</param>
        public static void RenderDateInput(this HtmlHelper htmlHelper, string fieldName)
        {
            RenderDateInput(htmlHelper, fieldName, null, null);
        }

        /// <summary>
        /// Renders a form input element and associated javascript for the date picker widget
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="fieldName">the value to use for the &quot;name&quot; and &quot;id&quot; html attibutes</param>
        /// <param name="value">the value that will be pre-polulated into the &quot;value&quot; attibute of the result</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        public static void RenderDateInput(this HtmlHelper htmlHelper, string fieldName, object value, object htmlAttributes)
        {
            htmlHelper.ViewContext.Writer.Write("<script type=\"text/javascript\">");
            htmlHelper.ViewContext.Writer.Write("$(document).ready(function() { ");
            htmlHelper.ViewContext.Writer.Write("ApplyDatePicker($('#");
            htmlHelper.ViewContext.Writer.Write(fieldName.Replace(" ", "_"));
            htmlHelper.ViewContext.Writer.Write("'), '");
            htmlHelper.ViewContext.Writer.Write(htmlHelper.GetCookie("culture"));
            htmlHelper.ViewContext.Writer.Write("', '");
            htmlHelper.ViewContext.Writer.Write(SiteClient.SiteCulture);
            htmlHelper.ViewContext.Writer.Write("'); });");
            htmlHelper.ViewContext.Writer.Write("</script>");
            htmlHelper.ViewContext.Writer.Write(htmlHelper.TextBox(fieldName, value, htmlAttributes));
        }

        /// <summary>
        /// Returns the number of accepted listing actions for the specified listing
        /// </summary>
        /// <param name="listing">the specified listing</param>
        public static int AcceptedListingActionCount(this Listing listing)
        {
            return (listing.AcceptedActionCount);
        }

        /// <summary>
        /// Returns the HTML of the specified CMS Content name for the current user's culture
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="name">the name of the specified CMS content</param>
        public static MvcHtmlString GetSiteContent(this HtmlHelper htmlHelper, string name)
        {
            return MvcHtmlString.Create(SiteClient.GetContent(name, htmlHelper.GetCookie(Strings.MVC.CultureCookie) ?? SiteClient.Settings[Strings.SiteProperties.SiteCulture]));
        }

        /// <summary>
        /// Returns the HTML of the specified tag builder using the specified render mode
        /// </summary>
        /// <param name="builder">an instance fo the TagBuilder class</param>
        /// <param name="renderMode">the specified TagRenderMode enum value</param>
        public static MvcHtmlString ToMvcHtmlString(this TagBuilder builder, TagRenderMode renderMode)
        {
            return MvcHtmlString.Create(builder.ToString(renderMode));
        }

        /// <summary>
        /// Returns the HTML version of the ToString() output method of the specified object
        /// </summary>
        /// <param name="anyObjectWithToStringMethod">the specified object</param>
        public static MvcHtmlString ToMvcHtmlString(this object anyObjectWithToStringMethod)
        {
            if (anyObjectWithToStringMethod == null) return null;

            return MvcHtmlString.Create(anyObjectWithToStringMethod.ToString());
        }

        #region ActionLink Overloads

        /// <summary>
        /// Returns an anchor element (a element) that contains the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        public static MvcHtmlString ActionLink(this HtmlHelper htmlHelper, MvcHtmlString linkText, string actionName)
        {
            return htmlHelper.ActionLink(MvcHtmlString.IsNullOrEmpty(linkText) ? " " : linkText.ToString(), actionName);
        }

        /// <summary>
        /// Returns an anchor element (a element) that contains the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        public static MvcHtmlString ActionLink(this HtmlHelper htmlHelper, MvcHtmlString linkText, string actionName, object routeValues)
        {
            return htmlHelper.ActionLink(MvcHtmlString.IsNullOrEmpty(linkText) ? " " : linkText.ToString(), actionName, routeValues);
        }

        /// <summary>
        /// Returns an anchor element (a element) that contains the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="routeValues">A RouteValueDictionary object that contains the parameters for a route.</param>
        public static MvcHtmlString ActionLink(this HtmlHelper htmlHelper, MvcHtmlString linkText, string actionName, RouteValueDictionary routeValues)
        {
            return htmlHelper.ActionLink(MvcHtmlString.IsNullOrEmpty(linkText) ? " " : linkText.ToString(), actionName, routeValues);
        }

        /// <summary>
        /// Returns an anchor element (a element) that contains the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="controllerName">The name of the controller.</param>
        public static MvcHtmlString ActionLink(this HtmlHelper htmlHelper, MvcHtmlString linkText, string actionName, string controllerName)
        {
            return htmlHelper.ActionLink(MvcHtmlString.IsNullOrEmpty(linkText) ? " " : linkText.ToString(), actionName, controllerName);
        }

        /// <summary>
        /// Returns an anchor element (a element) that contains the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        public static MvcHtmlString ActionLink(this HtmlHelper htmlHelper, MvcHtmlString linkText, string actionName, object routeValues, object htmlAttributes)
        {
            return htmlHelper.ActionLink(MvcHtmlString.IsNullOrEmpty(linkText) ? " " : linkText.ToString(), actionName, routeValues, htmlAttributes);
        }

        /// <summary>
        /// Returns an anchor element (a element) that contains the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="routeValues">A RouteValueDictionary object that contains the parameters for a route.</param>
        /// <param name="htmlAttributes">An IDictionary&lt;string, object&gt; object that contains the HTML attributes to set for this element.</param>
        public static MvcHtmlString ActionLink(this HtmlHelper htmlHelper, MvcHtmlString linkText, string actionName, RouteValueDictionary routeValues, IDictionary<string, object> htmlAttributes)
        {
            return htmlHelper.ActionLink(MvcHtmlString.IsNullOrEmpty(linkText) ? " " : linkText.ToString(), actionName, routeValues, htmlAttributes);
        }

        /// <summary>
        /// Returns an anchor element (a element) that contains the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="controllerName">The name of the controller.</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        public static MvcHtmlString ActionLink(this HtmlHelper htmlHelper, MvcHtmlString linkText, string actionName, string controllerName, object routeValues, object htmlAttributes)
        {
            return htmlHelper.ActionLink(MvcHtmlString.IsNullOrEmpty(linkText) ? " " : linkText.ToString(), actionName, controllerName, routeValues, htmlAttributes);
        }

        /// <summary>
        /// Returns an anchor element (a element) that contains the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="controllerName">The name of the controller.</param>
        /// <param name="routeValues">A RouteValueDictionary object that contains the parameters for a route.</param>
        /// <param name="htmlAttributes">An IDictionary&lt;string, object&gt; object that contains the HTML attributes to set for this element.</param>
        public static MvcHtmlString ActionLink(this HtmlHelper htmlHelper, MvcHtmlString linkText, string actionName, string controllerName, RouteValueDictionary routeValues, IDictionary<string, object> htmlAttributes)
        {
            return htmlHelper.ActionLink(MvcHtmlString.IsNullOrEmpty(linkText) ? " " : linkText.ToString(), actionName, controllerName, routeValues, htmlAttributes);
        }

        /// <summary>
        /// Returns an anchor element (a element) that contains the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="controllerName">The name of the controller.</param>
        /// <param name="protocol">The protocol for the URL, such as &quot;http&quot; or &quot;https&quot;.</param>
        /// <param name="hostName">The host name for the URL.</param>
        /// <param name="fragment">The URL fragment name (the anchor name).</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        public static MvcHtmlString ActionLink(this HtmlHelper htmlHelper, MvcHtmlString linkText, string actionName, string controllerName, string protocol, string hostName, string fragment, object routeValues, object htmlAttributes)
        {
            return htmlHelper.ActionLink(MvcHtmlString.IsNullOrEmpty(linkText) ? " " : linkText.ToString(), actionName, controllerName, protocol, hostName, fragment, routeValues, htmlAttributes);
        }

        /// <summary>
        /// Returns an anchor element (a element) that contains the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="controllerName">The name of the controller.</param>
        /// <param name="protocol">The protocol for the URL, such as &quot;http&quot; or &quot;https&quot;.</param>
        /// <param name="hostName">The host name for the URL.</param>
        /// <param name="fragment">The URL fragment name (the anchor name).</param>
        /// <param name="routeValues">A RouteValueDictionary object that contains the parameters for a route.</param>
        /// <param name="htmlAttributes">An IDictionary&lt;string, object&gt; object that contains the HTML attributes to set for this element.</param>
        public static MvcHtmlString ActionLink(this HtmlHelper htmlHelper, MvcHtmlString linkText, string actionName, string controllerName, string protocol, string hostName, string fragment, RouteValueDictionary routeValues, IDictionary<string, object> htmlAttributes)
        {
            return htmlHelper.ActionLink(MvcHtmlString.IsNullOrEmpty(linkText) ? " " : linkText.ToString(), actionName, controllerName, protocol, hostName, fragment, routeValues, htmlAttributes);
        }

        #endregion

        /// <summary>
        /// Returns an anchor element (a element) that contains the virtual path of the specified action, with an associated javascript confirmation onclick dialog.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="controllerName">The name of the controller.</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        /// <param name="jsConfirmMsg">The javascript dialog confirmation text to display before the click action is completed.</param>
        public static MvcHtmlString ActionLinkWithConfirmation(this HtmlHelper htmlHelper, MvcHtmlString linkText, string actionName, string controllerName, object routeValues, object htmlAttributes, string jsConfirmMsg)
        {
            return ActionLinkWithConfirmation(htmlHelper, linkText.ToHtmlString(), actionName, controllerName, routeValues, htmlAttributes, jsConfirmMsg);
        }

        /// <summary>
        /// Returns an anchor element (a element) that contains the virtual path of the specified action, with an associated javascript confirmation onclick dialog.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="controllerName">The name of the controller.</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        /// <param name="jsConfirmMsg">The javascript dialog confirmation text to display before the click action is completed.</param>
        public static MvcHtmlString ActionLinkWithConfirmation(this HtmlHelper htmlHelper, string linkText, string actionName, string controllerName, object routeValues, object htmlAttributes, string jsConfirmMsg)
        {
            string action = htmlHelper.ViewContext.RequestContext.HttpContext.Request.Url.GetLeftPart(UriPartial.Authority)
                + GetActionUrl(htmlHelper, actionName, controllerName, routeValues);
            string onClickValue = null;
            if (!string.IsNullOrEmpty(jsConfirmMsg))
            {
                onClickValue = "return (confirm('" + jsConfirmMsg + "'));";
            }

            var builder = new TagBuilder("a");
            builder.MergeAttribute("href", action);
            if (!string.IsNullOrEmpty(onClickValue))
            {
                builder.MergeAttribute("onclick", "PLACEHOLDER");
            }

            if (htmlAttributes != null)
            {
                if (htmlAttributes.GetType().Name == "IDictionary<string, object>")
                    builder.MergeAttributes((IDictionary<string, object>)htmlAttributes);
                else
                    builder.MergeAttributes(((IDictionary<string, object>)new RouteValueDictionary(htmlAttributes)));
            }

            builder.InnerHtml = linkText;
            return builder.ToString(TagRenderMode.Normal).Replace("PLACEHOLDER", onClickValue).ToMvcHtmlString();
        }

        /// <summary>
        /// Returns an anchor element that redirects to the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="controllerName">The name of the controller.</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        public static MvcHtmlString ActionLinkNoEncoding(this HtmlHelper htmlHelper, MvcHtmlString linkText, string actionName, string controllerName, object routeValues, object htmlAttributes)
        {
            return ActionLinkNoEncoding(htmlHelper, linkText.ToString(), actionName, controllerName, routeValues, htmlAttributes);
        }

        /// <summary>
        /// Returns an anchor element that redirects to the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="controllerName">The name of the controller.</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        public static MvcHtmlString ActionLinkNoEncoding(this HtmlHelper htmlHelper, string linkText, string actionName, string controllerName, object routeValues, object htmlAttributes)
        {
            string action = GetActionUrl(htmlHelper, actionName, controllerName, routeValues).Replace("&", "&amp;");
            string hrefValue = htmlHelper.ViewContext.RequestContext.HttpContext.Request.Url.GetLeftPart(UriPartial.Authority) + action;

            var builder = new TagBuilder("a");
            builder.MergeAttribute("href", hrefValue);

            if (htmlAttributes != null)
            {
                if (htmlAttributes.GetType().Name == "IDictionary<string, object>")
                {
                    builder.MergeAttributes(FixAttributeKeys((IDictionary<string, object>)htmlAttributes));
                }
                else
                    builder.MergeAttributes(FixAttributeKeys((IDictionary<string, object>)new RouteValueDictionary(htmlAttributes)));
            }

            builder.InnerHtml = linkText;
            return builder.ToString(TagRenderMode.Normal).ToMvcHtmlString();
        }


        #region ButtonLink Overloads

        /// <summary>
        /// Returns a button element that redirects to the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        public static MvcHtmlString ButtonLink(this HtmlHelper htmlHelper, string linkText, string actionName)
        {
            return ButtonLink(htmlHelper, linkText, actionName, null, null, null);
        }

        /// <summary>
        /// Returns a button element that redirects to the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        public static MvcHtmlString ButtonLink(this HtmlHelper htmlHelper, string linkText, string actionName, object routeValues)
        {
            return ButtonLink(htmlHelper, linkText, actionName, null, routeValues, null);
        }

        /// <summary>
        /// Returns a button element that redirects to the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="controllerName">The name of the controller.</param>
        public static MvcHtmlString ButtonLink(this HtmlHelper htmlHelper, string linkText, string actionName, string controllerName)
        {
            return ButtonLink(htmlHelper, linkText, actionName, controllerName, null, null);
        }

        /// <summary>
        /// Returns a button element that redirects to the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="controllerName">The name of the controller.</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        public static MvcHtmlString ButtonLink(this HtmlHelper htmlHelper, string linkText, string actionName, string controllerName, object routeValues)
        {
            return ButtonLink(htmlHelper, linkText, actionName, controllerName, routeValues, null);
        }

        /// <summary>
        /// Returns a button element that redirects to the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        public static MvcHtmlString ButtonLink(this HtmlHelper htmlHelper, string linkText, string actionName, object routeValues, object htmlAttributes)
        {
            return ButtonLink(htmlHelper, linkText, actionName, null, routeValues, htmlAttributes);
        }

        /// <summary>
        /// Returns a button element that redirects to the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        public static MvcHtmlString ButtonLink(this HtmlHelper htmlHelper, MvcHtmlString linkText, string actionName)
        {
            return ButtonLink(htmlHelper, linkText.ToString(), actionName, null, null, null);
        }

        /// <summary>
        /// Returns a button element that redirects to the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        public static MvcHtmlString ButtonLink(this HtmlHelper htmlHelper, MvcHtmlString linkText, string actionName, object routeValues)
        {
            return ButtonLink(htmlHelper, linkText.ToString(), actionName, null, routeValues, null);
        }

        /// <summary>
        /// Returns a button element that redirects to the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="controllerName">The name of the controller.</param>
        public static MvcHtmlString ButtonLink(this HtmlHelper htmlHelper, MvcHtmlString linkText, string actionName, string controllerName)
        {
            return ButtonLink(htmlHelper, linkText.ToString(), actionName, controllerName, null, null);
        }
        /// <summary>
        /// Returns a button element that redirects to the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="controllerName">The name of the controller.</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        public static MvcHtmlString ButtonLink(this HtmlHelper htmlHelper, MvcHtmlString linkText, string actionName, string controllerName, object routeValues)
        {
            return ButtonLink(htmlHelper, linkText.ToString(), actionName, controllerName, routeValues, null);
        }

        /// <summary>
        /// Returns a button element that redirects to the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        public static MvcHtmlString ButtonLink(this HtmlHelper htmlHelper, MvcHtmlString linkText, string actionName, object routeValues, object htmlAttributes)
        {
            return ButtonLink(htmlHelper, linkText.ToString(), actionName, null, routeValues, htmlAttributes);
        }

        /// <summary>
        /// Returns a button element that redirects to the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="controllerName">The name of the controller.</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        public static MvcHtmlString ButtonLink(this HtmlHelper htmlHelper, MvcHtmlString linkText, string actionName, string controllerName, object routeValues, object htmlAttributes)
        {
            return ButtonLink(htmlHelper, linkText.ToString(), actionName, controllerName, routeValues, htmlAttributes);
        }

        /// <summary>
        /// Returns a button element that redirects to the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="controllerName">The name of the controller.</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        public static MvcHtmlString ButtonLink(this HtmlHelper htmlHelper, string linkText, string actionName, string controllerName, object routeValues, object htmlAttributes)
        {
            return ButtonLink(htmlHelper, linkText, actionName, controllerName, routeValues, htmlAttributes, null);
        }

        /// <summary>
        /// Returns a button element that redirects to the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the anchor element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="controllerName">The name of the controller.</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        /// <param name="jsConfirmMsg">The javascript dialog confirmation text to display before the click action is completed.</param>
        public static MvcHtmlString ButtonLink(this HtmlHelper htmlHelper, MvcHtmlString linkText, string actionName, string controllerName, object routeValues, object htmlAttributes, string jsConfirmMsg)
        {
            return ButtonLink(htmlHelper, linkText.ToString(), actionName, controllerName, routeValues, htmlAttributes, jsConfirmMsg);
        }

        #endregion

        /// <summary>
        /// Returns a button element that redirects to the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="linkText">The inner text of the button element.</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="controllerName">The name of the controller.</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        /// <param name="htmlAttributes">An object that contains the HTML attributes to set for this element.</param>
        /// <param name="jsConfirmMsg">The javascript dialog confirmation text to display before the click action is completed.</param>
        public static MvcHtmlString ButtonLink(this HtmlHelper htmlHelper, string linkText, string actionName, string controllerName, object routeValues, object htmlAttributes, string jsConfirmMsg)
        {
            string action = GetActionUrl(htmlHelper, actionName, controllerName, routeValues).Replace("&", "&amp;");
            string onClickValue = "window.location.href='"
                + htmlHelper.ViewContext.RequestContext.HttpContext.Request.Url.GetLeftPart(UriPartial.Authority) + action + "';"
                + " $(this).prop('disabled', true);"
                + " return false;";

            if (!string.IsNullOrEmpty(jsConfirmMsg)) onClickValue =
                "if (confirm('" + jsConfirmMsg + "')) " + onClickValue;

            var builder = new TagBuilder("button");
            builder.MergeAttribute("onclick", "PLACEHOLDER");

            if (htmlAttributes != null)
            {
                if (htmlAttributes.GetType().Name == "IDictionary<string, object>")
                {
                    builder.MergeAttributes(FixAttributeKeys((IDictionary<string, object>)htmlAttributes));
                }
                else
                    builder.MergeAttributes(FixAttributeKeys((IDictionary<string, object>)new RouteValueDictionary(htmlAttributes)));
            }

            //builder.AddCssClass("DefaultButton");

            builder.InnerHtml = linkText;
            return builder.ToString(TagRenderMode.Normal).Replace("PLACEHOLDER", onClickValue).ToMvcHtmlString();

            //return MvcHtmlString.Create("<button class=\"DefaultButton\" onclick=\"document.location='" + action + "'; return false;\">" + linkText + "</button>");
        }

        private static IDictionary<string, object> FixAttributeKeys(IDictionary<string, object> attributes)
        {
            Dictionary<string, object> newDict = new Dictionary<string, object>(attributes.Count);
            foreach (string key in attributes.Keys)
            {
                newDict.Add(key.Replace('_', '-'), attributes[key]);
            }
            return newDict;
        }

        #region GetActionUrl Overloads

        /// <summary>
        /// Returns the URL for the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="actionName">The name of the action.</param>
        public static string GetActionUrl(this HtmlHelper htmlHelper, string actionName)
        {
            return GetActionUrl(htmlHelper, actionName, null, null);
        }

        /// <summary>
        /// Returns the URL for the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="controllerName">The name of the controller.</param>
        public static string GetActionUrl(this HtmlHelper htmlHelper, string actionName, string controllerName)
        {
            return GetActionUrl(htmlHelper, actionName, controllerName, null);
        }

        /// <summary>
        /// Returns the URL for the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        public static string GetActionUrl(this HtmlHelper htmlHelper, string actionName, object routeValues)
        {
            return GetActionUrl(htmlHelper, actionName, null, routeValues);
        }

        /// <summary>
        /// Returns the URL for the virtual path of the specified action.
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="actionName">The name of the action.</param>
        public static string GetActionUrl(this Controller controller, string actionName)
        {
            return GetActionUrl(controller, actionName, null, null);
        }

        /// <summary>
        /// Returns the URL for the virtual path of the specified action.
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="controllerName">The name of the controller.</param>
        public static string GetActionUrl(this Controller controller, string actionName, string controllerName)
        {
            return GetActionUrl(controller, actionName, controllerName, null);
        }

        /// <summary>
        /// Returns the URL for the virtual path of the specified action.
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        public static string GetActionUrl(this Controller controller, string actionName, object routeValues)
        {
            return GetActionUrl(controller, actionName, null, routeValues);
        }

        #endregion

        /// <summary>
        /// Returns the URL for the virtual path of the specified action.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="controllerName">The name of the controller.</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        public static string GetActionUrl(this HtmlHelper htmlHelper, string actionName, string controllerName, object routeValues)
        {
            string retVal = string.Empty;
            var urlhelper = new UrlHelper(htmlHelper.ViewContext.RequestContext);
            if (routeValues == null)
                retVal = urlhelper.Action(actionName, controllerName);
            else if (routeValues.GetType().Name == "RouteValueDictionary")
                retVal = urlhelper.Action(actionName, controllerName, (RouteValueDictionary)routeValues);
            else
                retVal = urlhelper.Action(actionName, controllerName, routeValues);
            return retVal;
        }

        /// <summary>
        /// Returns the URL for the virtual path of the specified action.
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="controllerName">The name of the controller.</param>
        /// <param name="routeValues">An object that contains the parameters for a route.
        /// The parameters are retrieved through reflection by examining the properties of the object.
        /// The object is typically created using object initializer syntax.</param>
        public static string GetActionUrl(this Controller controller, string actionName, string controllerName, object routeValues)
        {
            string retVal = string.Empty;
            var urlhelper = new UrlHelper(controller.ControllerContext.RequestContext);
            if (routeValues == null)
                retVal = urlhelper.Action(actionName, controllerName);
            else if (routeValues.GetType().Name == "RouteValueDictionary")
                retVal = urlhelper.Action(actionName, controllerName, (RouteValueDictionary)routeValues);
            else
                retVal = urlhelper.Action(actionName, controllerName, routeValues);
            return retVal;
        }

        /// <summary>
        /// Returns the absolute path of the application root.
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        public static string ShortBase(this HtmlHelper htmlHelper)
        {
            return VirtualPathUtility.ToAbsolute("~/");
        }

        /// <summary>
        /// Returns the full url of the application root
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        public static string Base(this HtmlHelper htmlHelper)
        {
            return htmlHelper.ViewContext.RequestContext.HttpContext.Request.Url.GetLeftPart(UriPartial.Authority)
                   + htmlHelper.ShortBase();
        }

        #region CategoryLinks Overloads

        /// <summary>
        /// Generates anchor tags to each category in the specified hierarchy
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="catHierarchy">the specified category hierarchy</param>
        /// <returns>HTML string</returns>
        public static MvcHtmlString CategoryLinks(this HtmlHelper htmlHelper, Hierarchy<int, Category> catHierarchy)
        {
            return htmlHelper.CategoryLinks(catHierarchy, null, null);
        }

        /// <summary>
        /// Generates anchor tags to each category in the specified hierarchy with the specified route values
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="catHierarchy">the specified category hierarchy</param>
        /// <param name="routeValues">the specified route values in each link</param>
        /// <returns>HTML string</returns>
        public static MvcHtmlString CategoryLinks(this HtmlHelper htmlHelper, Hierarchy<int, Category> catHierarchy, object routeValues)
        {
            return htmlHelper.CategoryLinks(catHierarchy, routeValues, null);
        }

        /// <summary>
        /// Generates anchor tags to each category in the specified hierarchy with the specified route values and html attributes
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="catHierarchy">the specified category hierarchy</param>
        /// <param name="routeValues">the specified route values in each link</param>
        /// <param name="htmlAttributes">the specified html attributes in each link</param>
        /// <returns>HTML string</returns>
        public static MvcHtmlString CategoryLinks(this HtmlHelper htmlHelper, Hierarchy<int, Category> catHierarchy, object routeValues, object htmlAttributes)
        {
            return htmlHelper.CategoryLinks(catHierarchy, routeValues, htmlAttributes, null);
        }

        #endregion

        /// <summary>
        /// Generates anchor tags to each category in the specified hierarchy with the specified route values, html attributes and separator
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="catHierarchy">the specified category hierarchy</param>
        /// <param name="routeValues">the specified route values in each link</param>
        /// <param name="htmlAttributes">the specified html attributes in each link</param>
        /// <param name="seperator">the string to insert between each linkseparator</param>
        /// <returns>HTML string</returns>
        public static MvcHtmlString CategoryLinks(this HtmlHelper htmlHelper, Hierarchy<int, Category> catHierarchy, object routeValues, object htmlAttributes, string seperator)
        {
            string actionName = Strings.MVC.BrowseAction;
            string controllerName = Strings.MVC.ListingController;
            if (string.IsNullOrEmpty(seperator))
            {
                seperator = HttpUtility.HtmlEncode(Strings.MVC.LineageSeperator);
            }
            string[] toRemove = new string[] { "Root", "Items" };
            string breadcrumbs = string.Empty;
            string extra = string.Empty;
            var sb = new StringBuilder();
            foreach (Category cat in catHierarchy.Lineage)
            {
                if (!toRemove.Contains(cat.Name))
                {
                    RouteValueDictionary newRouteValues = new RouteValueDictionary(routeValues);
                    breadcrumbs = breadcrumbs == string.Empty ? "C" + cat.ID : breadcrumbs + "-C" + cat.ID;
                    extra = extra == string.Empty ? htmlHelper.LocalizedCategoryName(cat.Name).SimplifyForURL("-") : extra + "-" + htmlHelper.LocalizedCategoryName(cat.Name).SimplifyForURL("-");
                    newRouteValues["breadcrumbs"] = breadcrumbs;
                    newRouteValues["extra"] = extra;

                    var builder = new TagBuilder("a");
                    string action = GetActionUrl(htmlHelper, actionName, controllerName, newRouteValues);
                    builder.MergeAttribute("href", action);

                    if (htmlAttributes != null)
                    {
                        if (htmlAttributes.GetType().Name == "IDictionary<string, object>")
                            builder.MergeAttributes((IDictionary<string, object>)htmlAttributes);
                        else
                            builder.MergeAttributes(((IDictionary<string, object>)new RouteValueDictionary(htmlAttributes)));
                    }

                    builder.InnerHtml = HttpUtility.HtmlEncode(htmlHelper.LocalizedCategoryName(cat.Name));
                    string htmlLink = builder.ToString(TagRenderMode.Normal);
                    if (sb.Length > 0) sb.Append(seperator);
                    sb.Append(htmlLink);
                }
            }
            return sb.ToMvcHtmlString();
        }

        #region EventCategoryLinks Overloads

        /// <summary>
        /// Generates anchor tags to each category, relative to the specified event, in the specified hierarchy
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="auctionEvent">the specified event</param>
        /// <param name="catHierarchy">the specified category hierarchy</param>
        /// <returns>HTML string</returns>
        public static MvcHtmlString EventCategoryLinks(this HtmlHelper htmlHelper, Event auctionEvent, Hierarchy<int, Category> catHierarchy)
        {
            return htmlHelper.EventCategoryLinks(auctionEvent, catHierarchy, null, null);
        }

        /// <summary>
        /// Generates anchor tags to each category, relative to the specified event, in the specified hierarchy with the specified route values
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="auctionEvent">the specified event</param>
        /// <param name="catHierarchy">the specified category hierarchy</param>
        /// <param name="routeValues">the specified route values in each link</param>
        /// <returns>HTML string</returns>
        public static MvcHtmlString EventCategoryLinks(this HtmlHelper htmlHelper, Event auctionEvent, Hierarchy<int, Category> catHierarchy, object routeValues)
        {
            return htmlHelper.EventCategoryLinks(auctionEvent, catHierarchy, routeValues, null);
        }

        /// <summary>
        /// Generates anchor tags to each category, relative to the specified event, in the specified hierarchy with the specified route values and html attributes
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="auctionEvent">the specified event</param>
        /// <param name="catHierarchy">the specified category hierarchy</param>
        /// <param name="routeValues">the specified route values in each link</param>
        /// <param name="htmlAttributes">the specified html attributes in each link</param>
        /// <returns>HTML string</returns>
        public static MvcHtmlString EventCategoryLinks(this HtmlHelper htmlHelper, Event auctionEvent, Hierarchy<int, Category> catHierarchy, object routeValues, object htmlAttributes)
        {
            return htmlHelper.EventCategoryLinks(auctionEvent, catHierarchy, routeValues, htmlAttributes, null);
        }

        #endregion

        /// <summary>
        /// Generates anchor tags to each category, relative to the specified event, in the specified hierarchy with the specified route values, html attributes and separator
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="auctionEvent">the specified event</param>
        /// <param name="catHierarchy">the specified category hierarchy</param>
        /// <param name="routeValues">the specified route values in each link</param>
        /// <param name="htmlAttributes">the specified html attributes in each link</param>
        /// <param name="seperator">the string to insert between each linkseparator</param>
        /// <returns>HTML string</returns>
        public static MvcHtmlString EventCategoryLinks(this HtmlHelper htmlHelper, Event auctionEvent, Hierarchy<int, Category> catHierarchy, object routeValues, object htmlAttributes, string seperator)
        {
            string actionName = Strings.MVC.DetailsAction;
            string controllerName = Strings.MVC.EventController;
            if (string.IsNullOrEmpty(seperator))
            {
                seperator = HttpUtility.HtmlEncode(Strings.MVC.LineageSeperator);
            }
            string[] toRemove = new string[] { "Root", "Items" };
            string breadcrumbs = string.Empty;
            string extra = string.Empty;
            var sb = new StringBuilder();
            foreach (Category cat in catHierarchy.Lineage)
            {
                if (!toRemove.Contains(cat.Name))
                {
                    RouteValueDictionary newRouteValues = new RouteValueDictionary(routeValues);
                    newRouteValues["id"] = auctionEvent.ID.ToString();
                    newRouteValues["extra2"] = auctionEvent.Title.SimplifyForURL("-");

                    breadcrumbs = breadcrumbs == string.Empty ? "C" + cat.ID : breadcrumbs + "-C" + cat.ID;
                    extra = extra == string.Empty ? cat.Name.SimplifyForURL("-") : extra + "-" + cat.Name.SimplifyForURL("-");
                    newRouteValues["breadcrumbs"] = breadcrumbs;
                    newRouteValues["extra"] = extra;

                    var builder = new TagBuilder("a");
                    string action = GetActionUrl(htmlHelper, actionName, controllerName, newRouteValues);
                    builder.MergeAttribute("href", action);

                    if (htmlAttributes != null)
                    {
                        if (htmlAttributes.GetType().Name == "IDictionary<string, object>")
                            builder.MergeAttributes((IDictionary<string, object>)htmlAttributes);
                        else
                            builder.MergeAttributes(((IDictionary<string, object>)new RouteValueDictionary(htmlAttributes)));
                    }

                    builder.InnerHtml = HttpUtility.HtmlEncode(cat.Name);
                    string htmlLink = builder.ToString(TagRenderMode.Normal);
                    if (sb.Length > 0) sb.Append(seperator);
                    sb.Append(htmlLink);
                }
            }
            return sb.ToMvcHtmlString();
        }

        /// <summary>
        /// Returns a string representing the category hierarchy of the primary category of the specified listing
        /// </summary>
        /// <param name="helper">an instance of the HtmlHelper class</param>
        /// <param name="listing">the specified listing</param>
        /// <returns>string</returns>
        public static string CategoryLineageString(this HtmlHelper helper, Listing listing)
        {
            //int catID = listing.PrimaryCategory.ID;
            //Hierarchy<int, Category> cats = CommonClient.GetCategoryPath(catID).Trees[catID];
            //return cats.ToLineageString(Strings.Fields.Name, Strings.MVC.LineageSeperator, new string[] { "Root", "Items" });
            return CategoryLineageString(helper, listing, Strings.MVC.LineageSeperator);
        }

        /// <summary>
        /// Returns a string representing the category hierarchy of the primary category of the specified listing
        /// </summary>
        /// <param name="helper">an instance of the HtmlHelper class</param>
        /// <param name="listing">the specified listing</param>
        /// <param name="separator">the specified separator to insert between each category name</param>
        /// <returns>string</returns>
        public static string CategoryLineageString(this HtmlHelper helper, Listing listing, string separator)
        {
            int catID = listing.PrimaryCategory.ID;
            Hierarchy<int, Category> cats = CommonClient.GetCategoryPath(catID).Trees[catID];
            return LocalizedCategoryLineageString(helper, cats, separator, new string[] { "Root", "Items" });
        }

        /// <summary>
        /// Returns a string representing the region hierarchy of the specified region
        /// </summary>
        /// <param name="helper">an instance of the HtmlHelper class</param>
        /// <param name="leafRegionId">ID of the specified region category</param>
        public static string RegionLineageString(this HtmlHelper helper, int leafRegionId)
        {
            return helper.RegionLineageString(leafRegionId, Strings.MVC.LineageSeperator);
        }

        /// <summary>
        /// Returns a string representing the region hierarchy of the specified region
        /// </summary>
        /// <param name="helper">an instance of the HtmlHelper class</param>
        /// <param name="leafRegionId">ID of the specified region category</param>
        /// <param name="separator">the specified separator to insert between each category name</param>
        public static string RegionLineageString(this HtmlHelper helper, int leafRegionId, string separator)
        {
            Hierarchy<int, Category> cats = CommonClient.GetCategoryPath(leafRegionId).Trees[leafRegionId];
            return LocalizedCategoryLineageString(helper, cats, separator, new string[] { "Root", "Regions" });
        }

        /// <summary>
        /// Returns a string displaying the translated category names for the specified category hierarchy
        /// </summary>
        /// <param name="helper">an instance of the HtmlHelper class</param>
        /// <param name="catHierarchy">the category (or region) heirarchy to be processed</param>
        /// <param name="separator">the separator string to insert between each category name</param>
        /// <param name="toRemove">enumerable list of category names to exclude frmo the result</param>
        private static string LocalizedCategoryLineageString(HtmlHelper helper, Hierarchy<int, Category> catHierarchy, string separator, IEnumerable<string> toRemove)
        {
            var sb = new StringBuilder();
            var currentCategory = catHierarchy.Current;
            sb.Append(helper.LocalizedCategoryName(currentCategory.Name));
            var currentParent = catHierarchy.Parent;
            while (currentParent != null)
            {
                string parentCatName = currentParent.Current.Name;
                if (!toRemove.Contains(parentCatName))
                {
                    sb.Insert(0, separator);
                    sb.Insert(0, helper.LocalizedCategoryName(parentCatName));
                }
                currentParent = currentParent.Parent;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Returns a single-selection select element using the specified HTML helper and the name of the form field
        /// </summary>
        /// <param name="helper">an instance of the HtmlHelper class</param>
        /// <param name="name">The name of the form field to return</param>
        /// <param name="options">an enumerable list of strings to convert to option elements</param>
        /// <returns>An HTML select element</returns>
        public static MvcHtmlString DropDownList(this HtmlHelper helper, string name, IEnumerable<string> options)
        {
            return helper.DropDownList(name, options, string.Empty);
        }

        /// <summary>
        /// Returns a single-selection select element using the specified HTML helper and the name of the form field
        /// </summary>
        /// <param name="helper">an instance of the HtmlHelper class</param>
        /// <param name="name">The name of the form field to return</param>
        /// <param name="options">an enumerable list of strings to convert to option elements</param>
        /// <param name="selectedValue">The selected value</param>
        /// <returns>An HTML select element</returns>
        public static MvcHtmlString DropDownList(this HtmlHelper helper, string name, IEnumerable<string> options, string selectedValue)
        {
            List<SelectListItem> selectItems = new List<SelectListItem>(options.Count());
            foreach (string opt in options)
            {
                bool isSelected = (opt == selectedValue);
                selectItems.Add(new SelectListItem() { Text = helper.GlobalResourceString(opt), Value = opt, Selected = isSelected });
            }
            return helper.DropDownList(name, selectItems);
        }

        /// <summary>
        /// Returns a single-selection select element using the specified HTML helper and the name of the form field
        /// </summary>
        /// <param name="helper">an instance of the HtmlHelper class</param>
        /// <param name="options">an enumerable list of strings to convert to option elements</param>
        /// <returns>An HTML select element</returns>
        public static List<SelectListItem> GetSelectList(this HtmlHelper helper, IEnumerable<string> options)
        {
            return helper.GetSelectList(options, null);
        }

        /// <summary>
        /// Generates a List&lt;SelectListItem&gt; with options created from the specified list of strings
        /// </summary>
        /// <param name="helper">an instance of the HtmlHelper class</param>
        /// <param name="options">An object that contains the HTML attributes to set for this element.</param>
        /// <param name="selectedValue">The selected value</param>
        public static List<SelectListItem> GetSelectList(this HtmlHelper helper, IEnumerable<string> options, string selectedValue)
        {
            List<SelectListItem> selectItems = new List<SelectListItem>(options.Count());
            foreach (string opt in options)
            {
                bool isSelected = (opt == selectedValue);
                selectItems.Add(new SelectListItem() { Text = helper.GlobalResourceString(opt), Value = opt, Selected = isSelected });
            }
            return selectItems;
        }

        /// <summary>
        /// Generates one or more HTML select boxes with parent and siblings categories selected
        /// </summary>
        /// <remarks>This helper is designed to be used in conjunction with the AJAXCategoryChooser.ascx partial</remarks>
        /// <param name="helper">MVC HTML helper object</param>
        /// <param name="selectedCategoryID">The leaf category currently selected</param>
        /// <returns>The resulting &lt;select&gt; HTML fragment needed to render the dropdown(s)</returns>
        public static MvcHtmlString CategorySelectsWithParents(this HtmlHelper helper, int selectedCategoryID)
        {
            const int rootParentID = 9;
            string result = string.Empty;
            Category selectedCat = CommonClient.GetCategoryByID(selectedCategoryID);
            if (selectedCat != null)
            {
                result = "<div class=\"awe-category-group\" id=\"spanFor" + selectedCat.ParentCategoryID.ToString() + "\"></div>";
            }
            while (selectedCat != null)
            {
                if (selectedCat.ParentCategoryID.HasValue)
                {
                    int parentID = selectedCat.ParentCategoryID.Value;
                    string temp = "<div class=\"form-group\"><select class=\"form-control col-md-4\" name=\"selectFor"
                        + parentID.ToString()
                        + "\" id=\"selectFor"
                        + parentID.ToString()
                        + "\" size=\"6\">";
                    List<Category> siblingCats = CommonClient.GetChildCategories(parentID, includeRelatedCustomFields: false);
                    foreach (Category cat in siblingCats)
                    {
                        temp += "<option value=\"" + cat.ID.ToString() + "\"";
                        if (cat.ID == selectedCat.ID)
                        {
                            temp += " selected=\"selected\" ";
                        }
                        temp += ">" + HttpUtility.HtmlEncode(helper.LocalizedCategoryName(cat.Name)) + "</option>";
                    }
                    temp += "</select></div>";
                    if (parentID == rootParentID)
                    {
                        result = temp + result;
                        selectedCat = null;
                    }
                    else
                    {
                        selectedCat = CommonClient.GetCategoryByID(parentID);
                        result = "<div class=\"awe-category-group\" id=\"spanFor" + selectedCat.ParentCategoryID.ToString() + "\">"
                            + temp + result + "</div>";
                    }
                }
                else
                {
                    selectedCat = null;
                }
            }
            return result.ToMvcHtmlString();
        }

        /// <summary>
        /// Generates one or more HTML select boxes with parent and siblings categories selected
        /// </summary>
        /// <remarks>This helper is designed to be used in conjunction with the AJAXRegionChooser.ascx partial</remarks>
        /// <param name="helper">MVC HTML helper object</param>
        /// <param name="selectedCategoryID">The leaf region currently selected</param>
        /// <returns>The resulting &lt;select&gt; HTML fragment needed to render the dropdown(s)</returns>
        public static MvcHtmlString RegionSelectsWithParents(this HtmlHelper helper, int selectedCategoryID)
        {
            const int rootParentID = 27;
            string result = string.Empty;
            Category selectedCat = CommonClient.GetCategoryByID(selectedCategoryID);
            if (selectedCat != null)
            {
                result = "<div class=\"awe-category-group\" id=\"spanForRegion" + selectedCat.ParentCategoryID.ToString() + "\"></div>";
            }
            while (selectedCat != null)
            {
                if (selectedCat.ParentCategoryID.HasValue)
                {
                    int parentID = selectedCat.ParentCategoryID.Value;
                    string temp = "<div class=\"form-group\"><select class=\"form-control col-md-4\" name=\"selectForRegion"
                        + parentID.ToString()
                        + "\" id=\"selectForRegion"
                        + parentID.ToString()
                        + "\" size=\"6\">";
                    List<Category> siblingCats = CommonClient.GetChildCategories(parentID, includeRelatedCustomFields: false);
                    foreach (Category cat in siblingCats)
                    {
                        temp += "<option value=\"" + cat.ID.ToString() + "\"";
                        if (cat.ID == selectedCat.ID)
                        {
                            temp += " selected=\"selected\" ";
                        }
                        temp += ">" + HttpUtility.HtmlEncode(helper.LocalizedCategoryName(cat.Name)) + "</option>";
                    }
                    temp += "</select></div>";
                    if (parentID == rootParentID)
                    {
                        result = temp + result;
                        selectedCat = null;
                    }
                    else
                    {
                        selectedCat = CommonClient.GetCategoryByID(parentID);
                        result = "<div class=\"awe-category-group\" id=\"spanForRegion" + selectedCat.ParentCategoryID.ToString() + "\">"
                            + temp + result + "</div>";
                    }
                }
                else
                {
                    selectedCat = null;
                }
            }
            return result.ToMvcHtmlString();
        }

        /// <summary>
        /// Generates HTML to show a tooltip with the specified message
        /// </summary>
        /// <param name="helper">MVC HTML helper object</param>
        /// <param name="message">the specified message</param>
        /// <returns>HTML string</returns>
        public static MvcHtmlString ToolTip(this HtmlHelper helper, MvcHtmlString message)
        {
            //TODO Deprecate these...
            return ToolTip(helper, message.ToString());
        }

        /// <summary>
        /// Generates HTML to show a tooltip with the specified message
        /// </summary>
        /// <param name="helper">MVC HTML helper object</param>
        /// <param name="message">the specified message</param>
        /// <returns>HTML string</returns>
        public static MvcHtmlString ToolTip(this HtmlHelper helper, string message)
        {
            TagBuilder divTB = new TagBuilder("span");
            divTB.MergeAttribute("class", "ToolTip");
            divTB.MergeAttribute("onMouseOver", "javascript:this.className='ToolTip2_Hover'");
            divTB.MergeAttribute("onMouseOut", "javascript:this.className='ToolTip2'");

            TagBuilder imgTB = new TagBuilder("img");
            imgTB.MergeAttribute("src", "Content/images/General/HelpTip.png");

            TagBuilder spanTB = new TagBuilder("span");
            spanTB.InnerHtml = message;

            imgTB.InnerHtml = spanTB.ToString(TagRenderMode.Normal);
            divTB.InnerHtml = imgTB.ToString(TagRenderMode.Normal);

            return divTB.ToMvcHtmlString(TagRenderMode.Normal);
        }

        /// <summary>
        /// Generates HTML to render a tooltip button with the specified text, linked to the tooltip area with the specified name
        /// </summary>
        /// <param name="helper">MVC HTML helper object</param>
        /// <param name="resourceName">the specified name</param>
        /// <param name="buttonText">optional button text, default &quot;?&quot;</param>
        /// <returns>HTML string</returns>
        /// <remarks>to be used with ToolTipContent helper</remarks>
        public static MvcHtmlString ToolTipButton(this HtmlHelper helper, string resourceName, string buttonText = "?")
        {
            TagBuilder buttonTag = new TagBuilder("button");
            buttonTag.MergeAttribute("type", "button");
            buttonTag.MergeAttribute("class", "btn btn-default btn-xs");
            buttonTag.MergeAttribute("onclick", "$('." + resourceName + "').toggle('show')");
            buttonTag.InnerHtml = "<strong>" + buttonText + "</strong>";
            return buttonTag.ToMvcHtmlString(TagRenderMode.Normal);
        }

        /// <summary>
        /// Generates HTML to render a hidden tooltip area with the specified text, linked to the tooltip button with the specified name
        /// </summary>
        /// <param name="helper">MVC HTML helper object</param>
        /// <param name="resourceName">the specified name</param>
        /// <param name="translatedText">the text to be displayed when the corresponding tooltip button is clicked by the user</param>
        /// <returns>HTML string</returns>
        /// <remarks>to be used with ToolTipButton helper</remarks>
        public static MvcHtmlString ToolTipContent(this HtmlHelper helper, string resourceName, MvcHtmlString translatedText)
        {
            TagBuilder containerDiv = new TagBuilder("div");
            containerDiv.MergeAttribute("class", "help-tip alert alert-info " + resourceName);
            containerDiv.MergeAttribute("style", "display:none");

            TagBuilder innerAnchor = new TagBuilder("a");
            innerAnchor.MergeAttribute("class", "close");
            innerAnchor.MergeAttribute("onclick", "$('." + resourceName + "').toggle('hide')");
            innerAnchor.InnerHtml = "&times;";

            containerDiv.InnerHtml = innerAnchor.ToString(TagRenderMode.Normal) + translatedText;

            return containerDiv.ToMvcHtmlString(TagRenderMode.Normal);
        }

        #region Admin Property Helpers

        /// <summary>
        /// Retrieves admin setting value
        /// </summary>
        /// <param name="helper">MVC HTML helper object</param>
        /// <param name="propertyName">the name of the desired setting</param>
        /// <returns>the value of the requested setting</returns>
        public static bool SitePropertyBoolValue(this HtmlHelper helper, string propertyName)
        {
            bool retVal = false;
            CustomProperty prop = SiteClient.Properties.Where(p => p.Field.Name == propertyName).FirstOrDefault();
            if (prop != null)
            {
                bool.TryParse(prop.Value, out retVal);
            }
            return retVal;
        }

        /// <summary>
        /// Retrieves admin setting value
        /// </summary>
        /// <param name="helper">MVC HTML helper object</param>
        /// <param name="propertyName">the name of the desired setting</param>
        /// <returns>the value of the requested setting</returns>
        public static decimal SitePropertyDecimalValue(this HtmlHelper helper, string propertyName)
        {
            decimal retVal = 0M;
            CustomProperty prop = SiteClient.Properties.Where(p => p.Field.Name == propertyName).FirstOrDefault();
            if (prop != null)
            {
                decimal.TryParse(prop.Value, out retVal);
            }
            return retVal;
        }

        /// <summary>
        /// Retrieves admin setting value
        /// </summary>
        /// <param name="helper">MVC HTML helper object</param>
        /// <param name="propertyName">the name of the desired setting</param>
        /// <returns>the value of the requested setting</returns>
        public static int SitePropertyIntValue(this HtmlHelper helper, string propertyName)
        {
            int retVal = 0;
            CustomProperty prop = SiteClient.Properties.Where(p => p.Field.Name == propertyName).FirstOrDefault();
            if (prop != null)
            {
                int.TryParse(prop.Value, out retVal);
            }
            return retVal;
        }

        /// <summary>
        /// Retrieves admin setting value
        /// </summary>
        /// <param name="helper">MVC HTML helper object</param>
        /// <param name="propertyName">the name of the desired setting</param>
        /// <returns>the value of the requested setting</returns>
        public static DateTime SitePropertyDateValue(this HtmlHelper helper, string propertyName)
        {
            DateTime retVal = DateTime.MinValue;
            CustomProperty prop = SiteClient.Properties.Where(p => p.Field.Name == propertyName).FirstOrDefault();
            if (prop != null)
            {
                DateTime.TryParse(prop.Value, out retVal);
            }
            return retVal;
        }

        /// <summary>
        /// Retrieves admin setting value
        /// </summary>
        /// <param name="helper">MVC HTML helper object</param>
        /// <param name="propertyName">the name of the desired setting</param>
        /// <returns>the value of the requested setting</returns>
        public static string SitePropertyValue(this HtmlHelper helper, string propertyName)
        {
            string retVal = string.Empty;
            CustomProperty prop = SiteClient.Properties.Where(p => p.Field.Name == propertyName).FirstOrDefault();
            if (prop != null)
            {
                retVal = prop.Value;
            }
            return retVal;
        }
        #endregion Admin Property Helpers

        /// <summary>
        /// Formats a decimal value as a string in the currently selected culture
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="decValue">a decimal value</param>
        /// <returns></returns>
        public static string LocalDecimal(this HtmlHelper htmlHelper, decimal decValue)
        {
            CultureInfo numberCulture = SiteClient.SupportedCultures[htmlHelper.GetCookie(Strings.MVC.CultureCookie) ?? Strings.FieldDefaults.Culture];
            numberCulture.NumberFormat.CurrencySymbol = string.Empty; // prevent $ or other currency symbol from being rendered
            return decValue.ToString(numberCulture);
        }

        /// <summary>
        /// Localizes a collection of validation issues with content defined in Validation.resx
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="issues">an enumerable list of validation issues</param>
        /// <returns>a list of validation issues with the messages locaized</returns>
        public static List<ValidationIssue> LocalizeCustomFieldValidationMessages(this Controller controller, IEnumerable<ValidationIssue> issues)
        {
            List<ValidationIssue> localizedIssues = new List<ValidationIssue>();
            foreach (ValidationIssue issue in issues)
            {
                if (issue.Message.StartsWith(Strings.ValidationMessages.CustomFieldValidationPrefix))
                {
                    string[] messageParts = issue.Message.Split('_');
                    string fieldName = string.Empty;
                    string errorKey = Strings.ValidationMessages.CustomFieldValidationPrefix;
                    for (int i = 1; i < messageParts.Length; i++)
                    {
                        if (i < messageParts.Length - 1)
                        {
                            if (!string.IsNullOrEmpty(fieldName))
                            {
                                fieldName += "_";
                            }
                            fieldName += messageParts[i];
                        }
                        else
                        {
                            errorKey += messageParts[i];
                        }
                    }

                    string localizedMessage = Strings.ValidationMessages.CustomFieldValidationPrefix + controller.ResourceString("Validation, " + errorKey,
                                                                  controller.CustomFieldResourceString(fieldName));
                    ValidationIssue localizedIssue = new ValidationIssue(localizedMessage, issue.Key, issue.Tag,
                                                                         issue.TargetName, issue.ValidatorName);
                    localizedIssues.Add(localizedIssue);
                }
                else
                {
                    localizedIssues.Add(issue);
                }
            }
            return localizedIssues;
        }

        /// <summary>
        /// Adds all form input values sent with a GET request
        /// </summary>
        /// <param name="userInput">the user input container object</param>
        /// <param name="controller">a referernce to the controller handling the request</param>
        public static void AddAllFormValues(this UserInput userInput, Controller controller)
        {
            userInput.AddAllFormValues(controller, null);
        }

        /// <summary>
        /// Adds all form input values sent with a POST request
        /// </summary>
        /// <param name="userInput">the user input container object</param>
        /// <param name="controller">a referernce to the controller handling the request</param>
        /// <param name="exceptionKeys">collection of input keys that should be ignored</param>
        public static void AddAllFormValues(this UserInput userInput, Controller controller, IEnumerable<string> exceptionKeys)
        {
            foreach (string key in controller.Request.Form.AllKeys.Where(k => k != null))
            {
                if (exceptionKeys != null && exceptionKeys.Contains(key))
                    continue;
                if (userInput.Items.ContainsKey(key))
                {
                    string additionalValue = controller.Request.Form[key] == Strings.MVC.TrueFormValue
                                                 ? Strings.MVC.TrueValue
                                                 : controller.Request.Form[key].Trim();
                    userInput.Items[key] += "," + additionalValue;
                }
                else
                {
                    userInput.Items.Add(key,
                                        controller.Request.Form[key] == Strings.MVC.TrueFormValue
                                            ? Strings.MVC.TrueValue
                                            : controller.Request.Form[key].Trim());
                }
            }
            foreach (string key in userInput.Items.Keys)
            {
                if (!controller.ModelState.ContainsKey(key))
                {
                    //...add it to the model
                    ModelState ms = new ModelState();
                    ms.Value = new ValueProviderResult(userInput.Items[key], userInput.Items[key], null);
                    controller.ModelState.Add(key, ms);
                }
            }
        }

        /// <summary>
        /// Populates the MVC model state for each property
        /// </summary>
        /// <param name="dictionary">the model state dictionary to populate</param>
        /// <param name="properties">an enumerable collection of CustomProperties</param>
        public static void FillProperties(this ModelStateDictionary dictionary, IEnumerable<CustomProperty> properties)
        {
            FillProperties(dictionary, properties, null);
        }

        /// <summary>
        /// Populates the MVC model state for each property, formatted for the specified culture
        /// </summary>
        /// <param name="dictionary">the model state dictionary to populate</param>
        /// <param name="properties">an enumerable collection of CustomProperties</param>
        /// <param name="cultureInfo">the applicable culture to format the values as</param>
        public static void FillProperties(this ModelStateDictionary dictionary, IEnumerable<CustomProperty> properties, CultureInfo cultureInfo)
        {
            foreach (CustomProperty property in properties)
            {
                if (dictionary.ContainsKey(property.Field.Name))
                    continue;

                //Add Model control
                ModelState ms = new ModelState();
                if (!string.IsNullOrEmpty(property.Value) && cultureInfo != null)
                {
                    switch (property.Field.Type)
                    {
                        case CustomFieldType.DateTime:
                            DateTime tempDateTime;
                            if (DateTime.TryParse(property.Value, out tempDateTime))
                            {
                                if (property.Field.Name == Strings.SiteProperties.DataCleanup_DeleteListings_DeleteTime)
                                {
                                    //render time value
                                    property.Value = tempDateTime.ToString("t", cultureInfo);
                                }
                                else
                                {
                                    //render date value
                                    property.Value = tempDateTime.ToString("d", cultureInfo);
                                }
                            }
                            break;
                        case CustomFieldType.Int:
                            int tempInt;
                            if (int.TryParse(property.Value, out tempInt))
                            {
                                property.Value = tempInt.ToString(cultureInfo);
                            }
                            break;
                        case CustomFieldType.Decimal:
                            decimal tempDecimal;
                            if (decimal.TryParse(property.Value, out tempDecimal))
                            {
                                property.Value = tempDecimal.ToString(Strings.Formats.Decimal, cultureInfo);
                            }
                            break;
                    }
                }
                ms.Value = new ValueProviderResult(property.Value, property.Value, null);
                dictionary.Add(property.Field.Name, ms);
            }
        }

        /// <summary>
        /// Adds all form input values sent with a GET request
        /// </summary>
        /// <param name="userInput">the user input container object</param>
        /// <param name="controller">a referernce to the controller handling the request</param>
        public static void AddAllQueryStringValues(this UserInput userInput, Controller controller)
        {
            userInput.AddAllQueryStringValues(controller, null);
        }

        /// <summary>
        /// Adds all form input values sent with a GET request
        /// </summary>
        /// <param name="userInput">the user input container object</param>
        /// <param name="controller">a referernce to the controller handling the request</param>
        /// <param name="exceptionKeys">collection of input keys that should be ignored</param>
        public static void AddAllQueryStringValues(this UserInput userInput, Controller controller, IEnumerable<string> exceptionKeys)
        {
            foreach (string key in controller.Request.QueryString.AllKeys.Where(k => k != null))
            {
                if (exceptionKeys != null && exceptionKeys.Contains(key))
                    continue;
                if (userInput.Items.ContainsKey(key))
                {
                    string additionalValue = controller.Request.QueryString[key] == Strings.MVC.TrueFormValue
                                                 ? Strings.MVC.TrueValue
                                                 : controller.Request.QueryString[key].Trim();
                    userInput.Items[key] += "," + additionalValue;
                }
                else
                {
                    userInput.Items.Add(key,
                                        controller.Request.QueryString[key] == Strings.MVC.TrueFormValue
                                            ? Strings.MVC.TrueValue
                                            : controller.Request.QueryString[key].Trim());
                }
            }
            foreach (string key in userInput.Items.Keys)
            {
                if (!controller.ModelState.ContainsKey(key))
                {
                    //...add it to the model
                    ModelState ms = new ModelState();
                    ms.Value = new ValueProviderResult(userInput.Items[key], userInput.Items[key], null);
                    controller.ModelState.Add(key, ms);
                }
            }
        }

        /// <summary>
        /// Prepend the provided path with the scheme, host, and port of the request.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string FormatAbsoluteUrl(this Uri url, string path)
        {
            return string.Format(
               "{0}/{1}", url.FormatUrlStart(), path.TrimStart('/'));
        }

        /// <summary>
        /// Generate a string with the scheme, host, and port if not 80.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static string FormatUrlStart(this Uri url)
        {
            return string.Format("{0}://{1}{2}", url.Scheme,
               url.Host, url.Port == 80 ? string.Empty : ":" + url.Port);
        }

        /// <summary>
        /// Returns the converted
        /// </summary>
        /// <param name="amountToConvert">the specified amount</param>
        /// <param name="fromCurrency">the currency code of the specified amount, e.g. "USD"</param>
        /// <param name="toCurrency">the currency code of the target currency, e.g. "EUR"</param>
        /// <returns>the converted amount</returns>
        public static decimal ConvertAmount(this decimal amountToConvert, string fromCurrency, string toCurrency)
        {
            if (fromCurrency.ToUpper() == toCurrency.ToUpper())
                return amountToConvert;
            List<Currency> allEnabledCurrencies = SiteClient.GetCurrencies();
            Currency from_Currency = allEnabledCurrencies.Where(c => c.Code == fromCurrency.ToUpper()).FirstOrDefault();
            Currency to_Currency = allEnabledCurrencies.Where(c => c.Code == toCurrency.ToUpper()).FirstOrDefault();
            decimal result;
            if (from_Currency != null && to_Currency != null)
            {
                result = (amountToConvert / from_Currency.ConversionToUSD * to_Currency.ConversionToUSD);
            }
            else if (from_Currency == null)
            {
                throw new Exception("Currency \"" + fromCurrency + "\" not Enabled");
            }
            else // if (to_Currency == null)
            {
                throw new Exception("Currency \"" + toCurrency + "\" not Enabled");
            }
            return result;
        }

        /// <summary>
        /// In a specified input string, replaces all strings that match a regular expression pattern with a specified replacement string.
        /// </summary>
        /// <param name="input">input string</param>
        /// <param name="pattern">regular expression pattern</param>
        /// <param name="replacement">the replacement string</param>
        /// <returns>the resulting string with all matches replaced</returns>
        public static string ReplaceRegEx(this string input, string pattern, string replacement)
        {
            Regex rgx = new Regex(pattern);
            return rgx.Replace(input, replacement);
        }

        /// <summary>
        /// Converts the specified string with HTML tags to the equivalent plain-text string
        /// </summary>
        /// <param name="source">string with HTML tags</param>
        /// <returns> plain-text string</returns>
        public static string StripHtml(this string source)
        {
            string result;
            string cr = ((char)13).ToString();
            string lf = ((char)10).ToString();
            string tb = ((char)11).ToString();

            // Remove HTML Development formatting
            // Replace line breaks with space
            // because browsers insert spaces
            result = source.Replace(cr, " ");

            result = result.Replace("&lt;", "<");
            result = result.Replace("&gt;", ">");

            // Replace line breaks with space
            // because browsers insert spaces
            result = result.Replace(lf, " ");

            // Remove step-formatting
            result = result.Replace(tb, "");

            // Remove repeating spaces because browsers ignore them
            result = result.ReplaceRegEx("( )+", " ");

            // Remove the header (prepare first by clearing attributes)
            result = result.ReplaceRegEx("<( )*head([^>])*>", "<head>");
            result = result.ReplaceRegEx("(<( )*(/)( )*head( )*>)", "<head>");
            result = result.ReplaceRegEx("(<head>).*(</head>)", "");

            // remove all scripts (prepare first by clearing attributes)
            result = result.ReplaceRegEx("<( )*script([^>])*>", "<script>");
            result = result.ReplaceRegEx("(<( )*(/)( )*script( )*>)", "</script>");
            result = result.ReplaceRegEx("(<script>).*(</script>)", "");

            // remove all styles (prepare first by clearing attributes)
            result = result.ReplaceRegEx("<( )*style([^>])*>", "<style>");
            result = result.ReplaceRegEx("(<( )*(/)( )*style( )*>)", "</style>");
            result = result.ReplaceRegEx("(<style>).*(</style>)", "");

            // insert tabs in place of <td> tags
            result = result.ReplaceRegEx("<( )*td([^>])*>", tb);

            // insert line breaks in place of <BR> and <LI> tags
            result = result.ReplaceRegEx("<( )*br( )*>", cr);
            result = result.ReplaceRegEx("<( )*li( )*>", cr);

            // insert line paragraphs (double line breaks) in place
            // if <P>, <DIV> and <TR> tags
            result = result.ReplaceRegEx("<( )*div([^>])*>", cr + cr);
            result = result.ReplaceRegEx("<( )*tr([^>])*>", cr + cr);
            result = result.ReplaceRegEx("<( )*p([^>])*>", cr + cr);

            // Remove remaining tags like <a>, links, images,
            // comments etc - anything that's enclosed inside < >
            result = result.ReplaceRegEx("<[^>]*>", "");

            // replace special characters:
            result = result.ReplaceRegEx(" ", " ");

            result = result.ReplaceRegEx("&bull;", " * ");
            result = result.ReplaceRegEx("&lsaquo;", "<");
            result = result.ReplaceRegEx("&rsaquo;", ">");
            result = result.ReplaceRegEx("&trade;", "(tm)");
            result = result.ReplaceRegEx("&frasl;", "/");
            result = result.ReplaceRegEx("&lt;", "<");
            result = result.ReplaceRegEx("&gt;", ">");
            result = result.ReplaceRegEx("&copy;", "(c)");
            result = result.ReplaceRegEx("&reg;", "(r)");

            // Remove all others. More can be added, see
            // http://hotwired.lycos.com/webmonkey/reference/special_characters/
            result = result.ReplaceRegEx("&(.{2,6});", "");

            // make line breaking consistent
            result = result.Replace(lf, cr);

            // Remove extra line breaks and tabs:
            // replace over 2 breaks with 2 and over 4 tabs with 4.
            // Prepare first to remove any whitespaces in between
            // the escaped characters and remove redundant tabs in between line breaks
            result = result.ReplaceRegEx("(" + cr + ")( )+(" + cr + ")", cr + cr);
            result = result.ReplaceRegEx("(" + tb + ")( )+(" + tb + ")", tb + tb);
            result = result.ReplaceRegEx("(" + tb + ")( )+(" + cr + ")", tb + cr);
            result = result.ReplaceRegEx("(" + cr + ")( )+(" + tb + ")", cr + tb);

            // Remove redundant tabs
            result = result.ReplaceRegEx("(" + cr + ")(" + tb + ")+(" + cr + ")", cr + cr);

            // Remove multiple tabs following a line break with just one tab
            result = result.ReplaceRegEx("(" + cr + ")(" + tb + ")+", cr + tb);

            // Initial replacement target string for line breaks
            string breaks = cr + cr + cr;

            // Initial replacement target string for tabs
            string tabs = tb + tb + tb + tb + tb;

            int index = 0;
            while (index < result.Length)
            {
                result = result.Replace(breaks, cr + cr);
                result = result.Replace(tabs, tb + tb + tb + tb);
                breaks += cr;
                tabs += tb;
                index++;
            }

            // That's it.

            return result.Trim();
        }

        #region ReadJsonFileContents overloads

        /// <summary>
        /// Reads the contents of a JSON file of the most specific available culture and returns the minified contents, with results cached up to 10 minutes
        /// </summary>
        /// <param name="helper">an instance of the HtmlHelper class</param>
        /// <param name="cultureCode">the culture code, e.g. &quot;en&quot; or &quot;en-US&quot;</param>
        /// <param name="resourcePath">the relative path, where {0} is replace by the culture code and ~ is replaced by the application root</param>
        /// <returns>An HtmlString representation of the minified JSON data retrieved</returns>
        public static HtmlString ReadJsonFileContents(this HtmlHelper helper, string cultureCode, string resourcePath)
        {
            string effectiveCulture;
            return ReadJsonFileContents(helper, cultureCode, resourcePath, out effectiveCulture, false);
        }

        /// <summary>
        /// Reads the contents of a JSON file of the most specific available culture and returns the minified contents, with results cached up to 10 minutes
        /// </summary>
        /// <param name="helper">an instance of the HtmlHelper class</param>
        /// <param name="cultureCode">the culture code, e.g. &quot;en&quot; or &quot;en-US&quot;</param>
        /// <param name="resourcePath">the relative path, where {0} is replace by the culture code and ~ is replaced by the application root</param>
        /// <param name="forceRefresh">true to ignore cached results</param>
        /// <returns>An HtmlString representation of the minified JSON data retrieved</returns>
        public static HtmlString ReadJsonFileContents(this HtmlHelper helper, string cultureCode, string resourcePath, bool forceRefresh)
        {
            string effectiveCulture;
            return ReadJsonFileContents(helper, cultureCode, resourcePath, out effectiveCulture, forceRefresh);
        }

        /// <summary>
        /// Reads the contents of a JSON file of the most specific available culture and returns the minified contents, with results cached up to 10 minutes
        /// </summary>
        /// <param name="helper">an instance of the HtmlHelper class</param>
        /// <param name="cultureCode">the culture code, e.g. &quot;en&quot; or &quot;en-US&quot;</param>
        /// <param name="resourcePath">the relative path, where {0} is replace by the culture code and ~ is replaced by the application root</param>
        /// <param name="effectiveCulture">returns the culture code of the closest target culture where the file exists</param>
        /// <returns>An HtmlString representation of the minified JSON data retrieved</returns>
        public static HtmlString ReadJsonFileContents(this HtmlHelper helper, string cultureCode, string resourcePath, out string effectiveCulture)
        {
            return ReadJsonFileContents(helper, cultureCode, resourcePath, out effectiveCulture, false);
        }

        #endregion ReadJsonFileContents overloads

        /// <summary>
        /// Reads the contents of a JSON file of the most specific available culture and returns the minified contents, with results cached up to 10 minutes
        /// </summary>
        /// <param name="helper">an instance of the HtmlHelper class</param>
        /// <param name="cultureCode">the culture code, e.g. &quot;en&quot; or &quot;en-US&quot;</param>
        /// <param name="resourcePath">the relative path, where {0} is replace by the culture code and ~ is replaced by the application root</param>
        /// <param name="effectiveCulture">returns the culture code of the closest target culture where the file exists</param>
        /// <param name="forceRefresh">true to ignore cached results</param>
        /// <returns>An HtmlString representation of the minified JSON data retrieved</returns>
        public static HtmlString ReadJsonFileContents(this HtmlHelper helper, string cultureCode, string resourcePath, out string effectiveCulture, bool forceRefresh)
        {
            string cacheDataKey = "jsonFormatData_" + cultureCode + "_" + resourcePath;
            string cacheCodeKey = "jsonFormatCode_" + cultureCode + "_" + resourcePath + "_code";
            string retVal = null;
            string attemptedCultures = cultureCode;
            effectiveCulture = null;
            if (!forceRefresh)
            {
                retVal = (string)SiteClient.GetCacheData(cacheDataKey);
                effectiveCulture = (string)SiteClient.GetCacheData(cacheCodeKey);
            }
            if (retVal == null)
            {
                const int cacheExpiryMinutes = 10;
                if (resourcePath.StartsWith("~"))
                {
                    resourcePath = resourcePath.Replace("~", HttpContext.Current.Server.MapPath("~")).Replace("/", "\\").Replace("\\\\", "\\");
                }
                effectiveCulture = cultureCode;
                if (File.Exists(string.Format(resourcePath, effectiveCulture)))
                {
                    retVal = File.ReadAllText(string.Format(resourcePath, effectiveCulture));
                }
                if (string.IsNullOrEmpty(retVal) && effectiveCulture.Contains("-"))
                {
                    effectiveCulture = effectiveCulture.Left(effectiveCulture.IndexOf("-"));
                    if (File.Exists(string.Format(resourcePath, effectiveCulture)))
                    {
                        retVal = File.ReadAllText(string.Format(resourcePath, effectiveCulture));
                    }
                }
                if (string.IsNullOrEmpty(retVal))
                {
                    bool abort = false;
                    //check for known alias (this is due to CLDR using a different list of country codes rather than ISO-3166 codes)
                    try
                    {
                        string metaDataPath = "~/Scripts/globalize/json/supplemental/metadata.json";
                        metaDataPath = metaDataPath.Replace("~", HttpContext.Current.Server.MapPath("~")).Replace("/", "\\").Replace("\\\\", "\\");
                        string cldrMetaDataText = File.ReadAllText(metaDataPath);
                        var cldrMetaDataParsed = JsonConvert.DeserializeObject<dynamic>(cldrMetaDataText);
                        effectiveCulture = cldrMetaDataParsed.supplemental.metadata.alias.languageAlias[effectiveCulture]._replacement;
                    }
                    catch (Exception e)
                    {
                        abort = true;
                        LogManager.WriteLog(null, "HtmlHelpers.ReadJsonFileContents(1)", "MVC",
                            System.Diagnostics.TraceEventType.Warning, "Unknown", e,
                            new Dictionary<string, object>()
                            {
                                { "effectiveCulture", effectiveCulture },
                                { "attemptedCultures", attemptedCultures },
                                { "resourcePath", resourcePath }
                            });
                    }
                    if (!abort && File.Exists(string.Format(resourcePath, effectiveCulture)))
                    {
                        retVal = File.ReadAllText(string.Format(resourcePath, effectiveCulture));
                    }
                }
                if (string.IsNullOrEmpty(retVal))
                {
                    attemptedCultures += "," + effectiveCulture;
                    effectiveCulture = SiteClient.SiteCulture;
                    if (File.Exists(string.Format(resourcePath, effectiveCulture)))
                    {
                        retVal = File.ReadAllText(string.Format(resourcePath, effectiveCulture));
                    }
                }
                if (string.IsNullOrEmpty(retVal) && effectiveCulture.Contains("-"))
                {
                    attemptedCultures += "," + effectiveCulture;
                    effectiveCulture = effectiveCulture.Left(effectiveCulture.IndexOf("-"));
                    if (File.Exists(string.Format(resourcePath, effectiveCulture)))
                    {
                        retVal = File.ReadAllText(string.Format(resourcePath, effectiveCulture));
                    }
                }
                if (string.IsNullOrEmpty(retVal))
                {
                    effectiveCulture = "en"; //Strings.FieldDefaults.Culture; // "en"
                    if (File.Exists(string.Format(resourcePath, effectiveCulture)))
                    {
                        retVal = File.ReadAllText(string.Format(resourcePath, effectiveCulture));
                    }
                }

                if (attemptedCultures.Contains(","))
                {
                    LogManager.WriteLog("Missing CLDR/JSON number/date format data", "HtmlHelpers.ReadJsonFileContents(2)", "MVC", 
                        System.Diagnostics.TraceEventType.Warning, "Unknown", null, 
                        new Dictionary<string, object>()
                        {
                            { "effectiveCulture", effectiveCulture },
                            { "attemptedCultures", attemptedCultures },
                            { "resourcePath", resourcePath }
                        });
                }

                //minify JSON data
                retVal = Regex.Replace(retVal, "(\"(?:[^\"\\\\]|\\\\.)*\")|\\s+", "$1"); // http://stackoverflow.com/questions/8913138/minify-indented-json-string-in-net

                SiteClient.SetCacheData(cacheDataKey, retVal, cacheExpiryMinutes);
                SiteClient.SetCacheData(cacheCodeKey, effectiveCulture, cacheExpiryMinutes);
            }
            return new HtmlString(retVal);
        }

        /// <summary>
        /// Generates a list of all system time zones, for use in the dropdown 
        /// </summary>
        /// <param name="helper">an instance of the HtmlHelper class</param>
        /// <param name="selectedValue">the currently selected tiume zone id</param>
        public static SelectList TimeZoneSelectList(this HtmlHelper helper, string selectedValue)
        {
            List<TimeZoneOption> timeZonesDictionary = null; // = (List<TimeZoneOption>)SiteClient.GetCacheData("timeZonesDictionary");
            if (timeZonesDictionary == null)
            {
                timeZonesDictionary = new List<TimeZoneOption>();
                foreach (var tzi in TimeZoneInfo.GetSystemTimeZones())
                {
                    DateTime nowInThisTimeZone = TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.Utc, tzi);
                    var currentUtcOffset = tzi.GetUtcOffset(nowInThisTimeZone);
                    string newUtcOffsetString =
                        "(UTC" +
                        ((currentUtcOffset.Hours >= 0 && currentUtcOffset.Minutes >= 0 ? "+" : "") +
                        currentUtcOffset.Hours.ToString("0#") +
                        ":" +
                        Math.Abs(currentUtcOffset.Minutes).ToString("00")).Replace("+00:00", "") + ")";
                    //Regex rgx = new Regex(@"\(.*?\)");
                    //string displayName = rgx.Replace(tzi.DisplayName, newUtcOffsetString, 1);
                    string displayName = string.Format("{0} {1} ({2})", newUtcOffsetString,
                        helper.TimeZoneNameString(tzi.Id), helper.TimeZoneAbbreviationString(tzi.Id));
                    timeZonesDictionary.Add(new TimeZoneOption()
                    {
                        SecondsFromUtc = currentUtcOffset.Hours * 60 + currentUtcOffset.Minutes,
                        DisplayName = displayName,
                        TimeZoneId = tzi.Id
                    });
                }
                //SiteClient.SetCacheData("timeZonesDictionary", timeZonesDictionary.OrderBy(tzo => tzo.SecondsFromUtc).ToList(), 60);
            }
            return new SelectList(timeZonesDictionary, "TimeZoneId", "DisplayName", selectedValue);
        }

        private class TimeZoneOption
        {
            public int SecondsFromUtc { get; set; }
            public string DisplayName { get; set; }
            public string TimeZoneId { get; set; }
        }

        /// <summary>
        /// Parses breadcrumb values and sets ViewData as needed
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="breadCrumbs">breadcrumb data</param>
        public static void DecodeBreadCrumbs(this Controller controller, string breadCrumbs)
        {
            DecodeBreadCrumbs(controller, breadCrumbs, null);
        }

        /// <summary>
        /// Parses breadcrumb values and sets ViewData as needed
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="breadCrumbs">breadcrumb data</param>
        /// <param name="bannerCats">optional list of categories to limit banner selection to</param>
        public static void DecodeBreadCrumbs(this Controller controller, string breadCrumbs, List<Category> bannerCats)
        {
            int categoryID = breadCrumbs.LastIndexOf(Strings.BreadCrumbPrefixes.Category);
            int storeID = breadCrumbs.LastIndexOf(Strings.BreadCrumbPrefixes.Store);
            int eventID = breadCrumbs.LastIndexOf(Strings.BreadCrumbPrefixes.Event);
            int regionID = breadCrumbs.LastIndexOf(Strings.BreadCrumbPrefixes.Region);

            int listingCatId = 0;
            if (categoryID >= 0)
            {
                int endIndex = breadCrumbs.IndexOf('-', categoryID);
                if (endIndex >= 0)
                {
                    //categoryID = int.Parse(breadCrumbs.Substring(categoryID + 1, endIndex - categoryID - 1));
                    listingCatId = int.Parse(breadCrumbs.Substring(categoryID + 1, endIndex - categoryID - 1));
                    controller.ViewData[Strings.MVC.ViewData_CategoryNavigator] = CommonClient.GetChildCategories(listingCatId, includeRelatedCustomFields: false);
                }
                else
                {
                    //categoryID = int.Parse(breadCrumbs.Substring(categoryID + 1));
                    listingCatId = int.Parse(breadCrumbs.Substring(categoryID + 1));
                    controller.ViewData[Strings.MVC.ViewData_CategoryNavigator] = CommonClient.GetChildCategories(listingCatId, includeRelatedCustomFields: false);
                }
            }
            else
            {
                //categoryID = 9;
                listingCatId = CategoryRoots.ListingCats;
                controller.ViewData[Strings.MVC.ViewData_CategoryNavigator] = CommonClient.GetChildCategories(CategoryRoots.ListingCats, includeRelatedCustomFields: false);
            }


            if (storeID >= 0)
            {
                int endIndex = breadCrumbs.IndexOf('-', storeID);
                if (endIndex >= 0)
                {
                    //categoryID = int.Parse(breadCrumbs.Substring(categoryID + 1, endIndex - categoryID - 1));
                    controller.ViewData[Strings.MVC.ViewData_StoreNavigator] = CommonClient.GetChildCategories(int.Parse(breadCrumbs.Substring(storeID + 1, endIndex - storeID - 1)), includeRelatedCustomFields: false);
                }
                else
                {
                    //categoryID = int.Parse(breadCrumbs.Substring(categoryID + 1));
                    controller.ViewData[Strings.MVC.ViewData_StoreNavigator] = CommonClient.GetChildCategories(int.Parse(breadCrumbs.Substring(storeID + 1)), includeRelatedCustomFields: false);
                }
            }
            else
            {
                //storeID = 28;
                controller.ViewData[Strings.MVC.ViewData_StoreNavigator] = CommonClient.GetChildCategories(CategoryRoots.Stores, includeRelatedCustomFields: false);
            }

            if (eventID >= 0)
            {
                int endIndex = breadCrumbs.IndexOf('-', eventID);
                if (endIndex >= 0)
                {
                    //categoryID = int.Parse(breadCrumbs.Substring(categoryID + 1, endIndex - categoryID - 1));
                    controller.ViewData[Strings.MVC.ViewData_EventNagivator] = CommonClient.GetChildCategories(int.Parse(breadCrumbs.Substring(eventID + 1, endIndex - eventID - 1)), includeRelatedCustomFields: false);
                }
                else
                {
                    //categoryID = int.Parse(breadCrumbs.Substring(categoryID + 1));
                    controller.ViewData[Strings.MVC.ViewData_EventNagivator] = CommonClient.GetChildCategories(int.Parse(breadCrumbs.Substring(eventID + 1)), includeRelatedCustomFields: false);
                }
            }
            else
            {
                //eventID = 29;
                controller.ViewData[Strings.MVC.ViewData_EventNagivator] = CommonClient.GetChildCategories(CategoryRoots.Events, includeRelatedCustomFields: false);
            }

            int regionCatId = 0;
            if (regionID >= 0)
            {
                int endIndex = breadCrumbs.IndexOf('-', regionID);
                if (endIndex >= 0)
                {
                    //categoryID = int.Parse(breadCrumbs.Substring(categoryID + 1, endIndex - categoryID - 1));
                    regionCatId = int.Parse(breadCrumbs.Substring(regionID + 1, endIndex - regionID - 1));
                    controller.ViewData[Strings.MVC.ViewData_RegionNagivator] = CommonClient.GetChildCategories(regionCatId, includeRelatedCustomFields: false);
                }
                else
                {
                    //categoryID = int.Parse(breadCrumbs.Substring(categoryID + 1));
                    regionCatId = int.Parse(breadCrumbs.Substring(regionID + 1));
                    controller.ViewData[Strings.MVC.ViewData_RegionNagivator] = CommonClient.GetChildCategories(regionCatId, includeRelatedCustomFields: false);
                }
            }
            else
            {
                //regionID = 27;
                regionCatId = CategoryRoots.Regions;
                controller.ViewData[Strings.MVC.ViewData_RegionNagivator] = CommonClient.GetChildCategories(regionCatId, includeRelatedCustomFields: false);
            }

            if (bannerCats != null)
            {
                if (listingCatId > 0)
                {
                    Category listingCategory = CommonClient.GetCategoryByID(listingCatId);
                    if (listingCategory != null) bannerCats.Add(listingCategory);
                }
                if (regionCatId > 0)
                {
                    Category regionCategory = CommonClient.GetCategoryByID(regionCatId);
                    if (regionCategory != null) bannerCats.Add(regionCategory);
                }
            }

            //ViewData["CategoryNavigator"] = SiteClient.CategoryTree.Trees[categoryID].Children;
            //ViewData["StoreNavigator"] = SiteClient.CategoryTree.Trees[storeID].Children;
            //ViewData["EventNagivator"] = SiteClient.CategoryTree.Trees[eventID].Children;
            //ViewData["RegionNagivator"] = SiteClient.CategoryTree.Trees[regionID].Children;
        }

        /// <summary>
        /// Clones a list of Media objects
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="originalMedia">the enumerable list of media objects to clone</param>
        /// <returns>a new list of the cloned objects</returns>
        public static List<Media> CloneMediaAssets(this Controller controller, IEnumerable<Media> originalMedia)
        {
            List<Media> newMediaSet = new List<Media>();

            foreach (Media media in originalMedia)
            {
                string physicalURI = string.Empty;

                //see if media contains an "Original" size
                if (media.Variations.ContainsKey("Original"))
                {
                    physicalURI = media.Variations["Original"].Asset.MetaData["PhysicalURI"];
                }
                else if (media.Variations.ContainsKey("LargeSize"))
                {
                    //if physicalURI is still empty... LOAD from largesize                              
                    IMediaLoader mediaLoader = Unity.UnityResolver.Get<IMediaLoader>(media.Loader);
                    Dictionary<string, string> loaderProviderSettings = CommonClient.GetAttributeData(mediaLoader.TypeName, media.Context);
                    physicalURI = mediaLoader.Load(loaderProviderSettings, media, "LargeSize");
                }
                else if (media.Variations.ContainsKey("FullSize"))
                {
                    //if physicalURI is still empty... LOAD from fullsize                              
                    IMediaLoader mediaLoader = Unity.UnityResolver.Get<IMediaLoader>(media.Loader);
                    Dictionary<string, string> loaderProviderSettings = CommonClient.GetAttributeData(mediaLoader.TypeName, media.Context);
                    physicalURI = mediaLoader.Load(loaderProviderSettings, media, "FullSize");
                }
                else if (media.Loader != "YouTubeVideoEmbed" && media.Variations.ContainsKey("Main"))
                {
                    //if physicalURI is still empty... LOAD from fullsize                              
                    IMediaLoader mediaLoader = Unity.UnityResolver.Get<IMediaLoader>(media.Loader);
                    Dictionary<string, string> loaderProviderSettings = CommonClient.GetAttributeData(mediaLoader.TypeName, media.Context);
                    physicalURI = mediaLoader.Load(loaderProviderSettings, media, "Main");
                }

                MemoryStream ms;
                if (physicalURI == string.Empty)
                {
                    if (!media.Type.Equals("RainWorx.FrameWorx.Providers.MediaAsset.YouTube", StringComparison.OrdinalIgnoreCase)) continue;
                    //had no Original variation, or LargeSize, this is a youtube media...           
                    ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("http://www.youtube.com/watch?v=" + media.Default.Reference));
                    ms.Seek(0, SeekOrigin.Begin);
                }
                else
                {
                    //physicalURI should have a URI now, load image      
                    if (!physicalURI.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !physicalURI.StartsWith("file", StringComparison.OrdinalIgnoreCase))
                    {
                        physicalURI = controller.Server.MapPath((!physicalURI.StartsWith("~") ? "~/" : string.Empty) + physicalURI);
                    }

                    //get image bytes into a memory stream
                    WebRequest request = WebRequest.Create(new Uri(physicalURI));
                    try
                    {
                        WebResponse response = request.GetResponse();
                        ms = new MemoryStream();
                        response.GetResponseStream().CopyTo(ms);
                        response.Close();
                        ms.Seek(0, SeekOrigin.Begin);
                    }
                    catch
                    {
                        continue;
                    }
                }

                //save new image
                Dictionary<string, string> workflowParams =
                            CommonClient.GetAttributeData("MediaAsset.Workflow", media.Context);
                if (workflowParams.Count == 0)
                {
                    throw new ArgumentException("No such context exists");
                }
                string saverString = workflowParams["Saver"];
                IMediaSaver mediaSaver =
                    RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaSaver>(saverString);
                Dictionary<string, string> saverProviderSettings =
                    CommonClient.GetAttributeData(mediaSaver.TypeName, media.Context);

                Media newMedia;
                if (media.Context == Strings.MediaUploadContexts.UploadFile)
                {
                    string fileExtension = Path.GetExtension(physicalURI).ToLower();
                    IMediaGenerator mediaGenerator = RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaGenerator>(fileExtension);
                    Dictionary<string, string> generatorProviderSettings = new Dictionary<string, string>(3);
                    generatorProviderSettings["ContentLength"] = media.Default.MetaData["Size"];
                    generatorProviderSettings["ContentType"] = media.Default.MetaData["MIMEType"];
                    generatorProviderSettings["FileName"] = media.Default.MetaData["OriginalFileName"];
                    newMedia = mediaGenerator.Generate(generatorProviderSettings, ms);
                    //copy over Title if applicable
                    if (newMedia.Default.MetaData.ContainsKey("Title") && media.Default.MetaData.ContainsKey("Title"))
                    {
                        newMedia.Default.MetaData["Title"] = media.Default.MetaData["Title"];
                    }
                }
                else
                {
                    IMediaGenerator mediaGenerator =
                        RainWorx.FrameWorx.Unity.UnityResolver.Get<IMediaGenerator>(
                            workflowParams["Generator"]);
                    Dictionary<string, string> generatorProviderSettings =
                        CommonClient.GetAttributeData(mediaGenerator.TypeName, media.Context);
                    newMedia = mediaGenerator.Generate(generatorProviderSettings, ms);
                }

                newMedia.Context = media.Context;
                newMedia.Saver = saverString;
                newMedia.Loader = workflowParams["Loader"];
                if (!saverProviderSettings.ContainsKey("VirtualFolder"))
                {
                    saverProviderSettings.Add("VirtualFolder", controller.Server.MapPath("~"));
                }
                mediaSaver.Save(saverProviderSettings, newMedia);

                //Save the media object to the db
                CommonClient.AddMedia("CloneListSimilar", newMedia);
                newMediaSet.Add(newMedia);
            }

            return newMediaSet;
        }

        /// <summary>
        /// Returns true if the specified currency code is accpted by PayPal
        /// </summary>
        /// <param name="helper">an instance of the HtmlHelper class</param>
        /// <param name="currencyCode">the 3-digit code for the specified currency</param>
        public static bool IsPayPalCurrency(this HtmlHelper helper, string currencyCode)
        {
            return ("," + SiteClient.TextSetting(Strings.SiteProperties.PayPal_AcceptedCurrencies).Replace(" ", "").ToUpper() + ",").Contains("," + currencyCode.ToUpper() + ",");
        }

        /// <summary>
        /// checks the "EnabledCustomProperty" value aganist existing site property values
        /// </summary>
        /// <param name="helper">an instance of the HtmlHelper class</param>
        /// <param name="rawArgument">the raw &quot;EnabledCustomProperty&quot; value to parse and evaluate</param>
        /// <remarks>
        /// multiple site properties will be evaluated from left to right. 
        /// supported operators: &quot;!&quot; (not), &quot;&amp;&amp;&quot; (and), &quot;||&quot; (or) 
        /// Example: &quot;CreditCardsEnabled&amp;&amp;!StripeConnect_Enabled&quot; means 
        ///     &quot;Display this category only if Credit Cards are enabled and Stripe Connect is NOT enabled&quot;
        /// </remarks>
        public static bool EvaluateComplexEnabledCustomProperty(this HtmlHelper helper, string rawArgument)
        {
            bool result = true;
            string propertyNameBuffer = string.Empty;
            string operatorBuffer = string.Empty;
            string nextOperation = "AND";
            for (int i = 0; i < rawArgument.Length; i++)
            {
                string currentChar = rawArgument.Substring(i, 1);
                if (currentChar != "&" && currentChar != "|")
                {
                    propertyNameBuffer += currentChar;
                }
                else
                {
                    if (operatorBuffer == string.Empty)
                    {
                        if (nextOperation == "AND")
                        {
                            if (propertyNameBuffer.StartsWith("!"))
                            {
                                result = result && !SiteClient.BoolSetting(propertyNameBuffer.Substring(1));
                            }
                            else
                            {
                                result = result && SiteClient.BoolSetting(propertyNameBuffer);
                            }
                        }
                        else if (nextOperation == "OR")
                        {
                            if (propertyNameBuffer.StartsWith("!"))
                            {
                                result = result || !SiteClient.BoolSetting(propertyNameBuffer.Substring(1));
                            }
                            else
                            {
                                result = result || SiteClient.BoolSetting(propertyNameBuffer);
                            }
                        }
                        propertyNameBuffer = string.Empty;
                    }
                    operatorBuffer += currentChar;
                    if (operatorBuffer == "&&")
                    {
                        nextOperation = "AND";
                        operatorBuffer = string.Empty;
                    }
                    else if (operatorBuffer == "||")
                    {
                        nextOperation = "OR";
                        operatorBuffer = string.Empty;
                    }
                }
            }
            if (nextOperation == "AND")
            {
                if (propertyNameBuffer.StartsWith("!"))
                {
                    result = result && !SiteClient.BoolSetting(propertyNameBuffer.Substring(1));
                }
                else
                {
                    result = result && SiteClient.BoolSetting(propertyNameBuffer);
                }
            }
            else if (nextOperation == "OR")
            {
                if (propertyNameBuffer.StartsWith("!"))
                {
                    result = result || !SiteClient.BoolSetting(propertyNameBuffer.Substring(1));
                }
                else
                {
                    result = result || SiteClient.BoolSetting(propertyNameBuffer);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns: True if BP has been set to Taxable for this invoice,
        ///          False if BP has been set to Non-Taxable for this invoice,
        /// otherwise returns the default setting based on applicable SiteProperties.
        /// </summary>
        public static bool BuyersPremiumIsTaxable(this Invoice invoice)
        {
            bool? bpIsTaxableForThisInvoice = null;
            if (invoice.PropertyBag != null && invoice.PropertyBag.Properties.ContainsKey(InvoiceProperties.BuyersPremiumIsTaxable))
            {
                string strBpIsTaxable = invoice.PropertyBag.Properties[InvoiceProperties.BuyersPremiumIsTaxable];
                bool blnBpIsTaxable;
                if (bool.TryParse(strBpIsTaxable, out blnBpIsTaxable))
                {
                    bpIsTaxableForThisInvoice = blnBpIsTaxable;
                }
            }
            return bpIsTaxableForThisInvoice ??
                ((SiteClient.BoolSetting(SiteProperties.VATEnabled) && SiteClient.BoolSetting(SiteProperties.VatAppliesToBuyersPremium)) || 
                 (SiteClient.BoolSetting(SiteProperties.SalesTaxEnabled) && SiteClient.BoolSetting(SiteProperties.SalesTaxAppliesToBuyersPremium)));
        }

        #region User Property Logic

        private static CustomFieldAccess GetCustomFieldAccessForCurrentUser(this HtmlHelper helper, string ownerUserName, out bool isAdmin)
        {
            isAdmin = false;
            string _userName = helper.FBOUserName();

            if (string.IsNullOrEmpty(_userName))
                return CustomFieldAccess.Anonymous;

            if (_userName == SystemActors.SystemUserName)
                return CustomFieldAccess.System;

            if (HttpContext.Current.User.Identity.IsAuthenticated)
            {
                isAdmin = HttpContext.Current.User.IsInRole(Roles.Admin);
                if (isAdmin)
                    return CustomFieldAccess.Admin;
            }

            if (ownerUserName == _userName)
                return CustomFieldAccess.Owner;

            if (HttpContext.Current.User.Identity.IsAuthenticated)
                return CustomFieldAccess.Authenticated;

            return CustomFieldAccess.Anonymous;
        }

        private static void PruneUserCustomFieldsForRoleAndSitePops(List<CustomProperty> userProperties, string userName, bool isAdmin)
        {
            //hide PayPal properties if PayPal is disabled globally
            if (!SiteClient.BoolSetting(SiteProperties.PayPal_Enabled))
            {
                userProperties.RemoveAll(p => p.Field.Name == StdUserProps.PayPal_Email || p.Field.Name == StdUserProps.AcceptPayPal);
            }

            //hide the "Accept Credit Cards" field if credit cards are disabled globally
            if (!SiteClient.BoolSetting(SiteProperties.CreditCardsEnabled))
            {
                if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.AcceptCreditCard)))
                {
                    userProperties.Remove(userProperties.First(p => p.Field.Name.Equals(StdUserProps.AcceptCreditCard)));
                }
            }

            //hide "Accept Credit Cards" and auth.net credential fields if either credit cards are disabled globally or authorize.net is disabled globally
            if (!SiteClient.BoolSetting(SiteProperties.CreditCardsEnabled) || !SiteClient.BoolSetting(SiteProperties.AuthorizeNet_Enabled))
            {
                if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.AcceptCreditCard)))
                {
                    userProperties.Remove(userProperties.First(p => p.Field.Name.Equals(StdUserProps.AcceptCreditCard)));
                }
                if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.AuthorizeNet_SellerMerchantLoginID)))
                {
                    userProperties.Remove(userProperties.First(p => p.Field.Name.Equals(StdUserProps.AuthorizeNet_SellerMerchantLoginID)));
                }
                if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.AuthorizeNet_SellerTransactionKey)))
                {
                    userProperties.Remove(userProperties.First(p => p.Field.Name.Equals(StdUserProps.AuthorizeNet_SellerTransactionKey)));
                }
            }

            //hide stripe fields if stripe is disabled globally
            if (!SiteClient.BoolSetting(SiteProperties.StripeConnect_Enabled))
            {
                if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.StripeConnect_SellerAccountConnected)))
                {
                    userProperties.Remove(userProperties.First(p => p.Field.Name.Equals(StdUserProps.StripeConnect_SellerAccountConnected)));
                }
            }

            //hide "AcceptCreditCard" and auth.net credential fields for non-admin users when AuthorizeNet_EnableForSellers is false
            if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.AcceptCreditCard)) ||
                userProperties.Any(p => p.Field.Name.Equals(StdUserProps.AuthorizeNet_SellerMerchantLoginID)) ||
                userProperties.Any(p => p.Field.Name.Equals(StdUserProps.AuthorizeNet_SellerTransactionKey)) ||
                userProperties.Any(p => p.Field.Name.Equals(StdUserProps.StripeConnect_SellerAccountConnected)))
            {
                if (!isAdmin)
                {
                    if (!SiteClient.BoolSetting(SiteProperties.AuthorizeNet_EnableForSellers))
                    {
                        if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.AuthorizeNet_SellerMerchantLoginID)))
                        {
                            userProperties.Remove(userProperties.First(p => p.Field.Name.Equals(StdUserProps.AuthorizeNet_SellerMerchantLoginID)));
                        }
                        if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.AuthorizeNet_SellerTransactionKey)))
                        {
                            userProperties.Remove(userProperties.First(p => p.Field.Name.Equals(StdUserProps.AuthorizeNet_SellerTransactionKey)));
                        }
                    }
                    if (!SiteClient.BoolSetting(SiteProperties.StripeConnect_EnabledForSellers))
                    {
                        if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.StripeConnect_SellerAccountConnected)))
                        {
                            userProperties.Remove(userProperties.First(p => p.Field.Name.Equals(StdUserProps.StripeConnect_SellerAccountConnected)));
                        }
                    }
                    if (!SiteClient.BoolSetting(SiteProperties.AuthorizeNet_EnableForSellers))
                    {
                        if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.AcceptCreditCard)))
                        {
                            userProperties.Remove(userProperties.First(p => p.Field.Name.Equals(StdUserProps.AcceptCreditCard)));
                        }
                    }
                }
            }

            //hide "BuyersPremiumPercent" property when it is disabled site-wide
            if (userProperties.Any(p => p.Field.Name.Equals(StdUserProps.BuyersPremiumPercent)))
            {
                if (!SiteClient.BoolSetting(SiteProperties.EnableBuyersPremium))
                {
                    userProperties.Remove(userProperties.First(p => p.Field.Name.Equals(StdUserProps.BuyersPremiumPercent)));
                }
            }
        }

        /// <summary>
        /// returns a list of properties minus any that should not be editable due to role and/or applicable site properties
        /// </summary>
        /// <param name="helper">an instance of the HtmlHelper class</param>
        /// <param name="userProperties">list of user properties</param>
        /// <param name="ownerUserName">the username of the owner of the properties</param>
        public static List<CustomProperty> PruneUserCustomFieldsForEdit(this HtmlHelper helper, List<CustomProperty> userProperties, string ownerUserName)
        {
            bool isAdmin;
            CustomFieldAccess access = GetCustomFieldAccessForCurrentUser(helper, ownerUserName, out isAdmin);
            var retVal = userProperties.Where(
                p => /*(int)p.Field.Visibility >= (int)access &&*/ (int)p.Field.Mutability >= (int)access).ToList();

            PruneUserCustomFieldsForRoleAndSitePops(retVal, ownerUserName, isAdmin);

            return retVal;
        }

        /// <summary>
        /// returns a list of properties minus any that should not be visible due to role and/or applicable site properties
        /// </summary>
        /// <param name="helper">an instance of the HtmlHelper class</param>
        /// <param name="userProperties">list of user properties</param>
        /// <param name="ownerUserName">the username of the owner of the properties</param>
        public static List<CustomProperty> PruneUserCustomFieldsForVisbilityOnly(this HtmlHelper helper, List<CustomProperty> userProperties, string ownerUserName)
        {
            bool isAdmin;
            CustomFieldAccess access = GetCustomFieldAccessForCurrentUser(helper, ownerUserName, out isAdmin);
            var retVal = userProperties.Where(
                p => (int)p.Field.Visibility >= (int)access /*&& (int)p.Field.Mutability < (int)access*/).ToList();

            PruneUserCustomFieldsForRoleAndSitePops(retVal, ownerUserName, isAdmin);

            return retVal;
        }

        #endregion User Property Logic

        /// <summary>
        /// Retrieves a list of all currently existant Severity options, including an "All" option
        /// </summary>
        public static List<SelectListItem> LogEntrySeverityOptions(this HtmlHelper helper, string selectedValue)
        {
            var severityOptionData = CommonClient.GetLogEntrySeverityOptions();
            var severityOpts = new List<SelectListItem>();
            severityOpts.Add(new SelectListItem() { Text = "All", Value = "All" });
            foreach(string key in severityOptionData.Keys)
            {
                severityOpts.Add(new SelectListItem() { Text = string.Format("{0} ({1})", key, severityOptionData[key]), Value = key, Selected = (selectedValue == key) });
            }
            return severityOpts;
        }

        /// <summary>
        /// Retrieves a list of all currently existant Severity options, including an "All" option
        /// </summary>
        public static MultiSelectList LogEntrySeverityOptions(this HtmlHelper helper, string[] selectedValues)
        {
            var severityOptionData = CommonClient.GetLogEntrySeverityOptions();
            var severityOpts = new List<SelectListItem>();
            severityOpts.Add(new SelectListItem() { Text = "All", Value = "All" });
            foreach (string key in severityOptionData.Keys)
            {
                severityOpts.Add(new SelectListItem()
                {
                    Text = string.Format("{0} ({1})", key, severityOptionData[key]),
                    Value = key//,
                    //Selected = selectedValues != null && selectedValues.Any(opt => opt == key)
                });
            }
            return new MultiSelectList(severityOpts, "Value", "Text", selectedValues);
        }

        /// <summary>
        /// Retrieves a list of all currently existant Functional Area options, including an "All" option
        /// </summary>
        public static List<SelectListItem> LogEntryAreaOptions(this HtmlHelper helper, string selectedValue)
        {
            var areaOptionData = CommonClient.GetLogEntryAreaOptions();
            var areaOpts = new List<SelectListItem>();
            areaOpts.Add(new SelectListItem() { Text = "All", Value = "All" });
            foreach (string key in areaOptionData.Keys)
            {
                areaOpts.Add(new SelectListItem() { Text = string.Format("{0} ({1})", key, areaOptionData[key]), Value = key, Selected = (selectedValue == key) });
            }
            return areaOpts;
        }

        /// <summary>
        /// Retrieves a list of all currently existant Functional Area options, including an "All" option
        /// </summary>
        public static MultiSelectList LogEntryAreaOptions(this HtmlHelper helper, IEnumerable<string> selectedValues)
        {
            var areaOptionData = CommonClient.GetLogEntryAreaOptions();
            var areaOpts = new List<SelectListItem>();
            areaOpts.Add(new SelectListItem() { Text = "All", Value = "All" });
            foreach (string key in areaOptionData.Keys)
            {
                areaOpts.Add(new SelectListItem()
                {
                    Text = string.Format("{0} ({1})", key, areaOptionData[key]),
                    Value = key//,
                    //Selected = selectedValues != null && selectedValues.Any(opt => opt == key)
                });
            }
            return new MultiSelectList(areaOpts, "Value", "Text", selectedValues);
        }

        /// <summary>
        /// When enabled, captures and logs performance data about the current page
        /// </summary>
        public static void LogPageRenderStats(this HtmlHelper htmlHelper)
        {
            bool logFrontEndRequestStats = false;
            bool.TryParse(ConfigurationManager.AppSettings["LogFrontEndRequestStats"] ?? "false", out logFrontEndRequestStats);
            bool logAdminRequestStats = false;
            bool.TryParse(ConfigurationManager.AppSettings["LogAdminRequestStats"] ?? "false", out logAdminRequestStats);

            var controllerName = htmlHelper.ViewContext.RouteData.Values["Controller"].ToString();
            var actionName = htmlHelper.ViewContext.RouteData.Values["Action"].ToString();

            bool abort = false;
            //if controller is "admin" then check "Log admin page request stats" site prop
            //otherwise, check "Log front end page request stats" site prop
            if ("admin".Equals(controllerName, StringComparison.OrdinalIgnoreCase) && !logAdminRequestStats)
                abort = true;
            else if (!logFrontEndRequestStats)
                abort = true;
            if (abort)
                return;

            var totalMS = (DateTime.UtcNow - htmlHelper.ViewContext.HttpContext.Timestamp.ToUniversalTime()).TotalMilliseconds.ToString("F0");
            var queryString = htmlHelper.ViewContext.HttpContext.Request.QueryString;
            var headers = htmlHelper.ViewContext.HttpContext.Request.Headers;
            var actingUsername = htmlHelper.ViewContext.HttpContext.User.Identity.Name;
            var logProps = new Dictionary<string, object>();
            foreach (string key in htmlHelper.ViewContext.RouteData.Values.Keys.Where(k => !string.IsNullOrWhiteSpace(k)))
            {
                object value = htmlHelper.ViewContext.RouteData.Values[key];
                logProps[key] = value;
            }
            foreach (string key in queryString.AllKeys.Where(k => !string.IsNullOrWhiteSpace(k)))
            {
                string value = queryString[key];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    logProps[key] = value;
                }
            }
            foreach (string key in headers.AllKeys.Where(k => 
                !string.IsNullOrWhiteSpace(k) && (
                k.Equals("Referer", StringComparison.OrdinalIgnoreCase) ||
                k.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))))
            {
                string value = headers[key];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    logProps[key] = value;
                }
            }
            LogManager.WriteLog(string.Format("Rendered in {0} MS", totalMS), string.Format("/{0}/{1}", controllerName, actionName), "Page Stats", 
                System.Diagnostics.TraceEventType.Verbose, actingUsername, null, logProps);
        }

        /// <summary>
        /// When enabled, captures and logs performance data about the current page
        /// </summary>
        public static void LogPageRenderStats(this ActionExecutedContext context)
        {
            bool logFrontEndRequestStats = false;
            bool.TryParse(ConfigurationManager.AppSettings["LogFrontEndRequestStats"] ?? "false", out logFrontEndRequestStats);
            bool logAdminRequestStats = false;
            bool.TryParse(ConfigurationManager.AppSettings["LogAdminRequestStats"] ?? "false", out logAdminRequestStats);

            var controllerName = context.RouteData.Values["Controller"].ToString();
            var actionName = context.RouteData.Values["Action"].ToString();

            bool abort = false;
            //if controller is "admin" then check "Log admin page request stats" site prop
            //otherwise, check "Log front end page request stats" site prop
            if ("admin".Equals(controllerName, StringComparison.OrdinalIgnoreCase) && !logAdminRequestStats)
                abort = true;
            else if (!logFrontEndRequestStats)
                abort = true;
            if (abort)
                return;

            var totalMS = (DateTime.UtcNow - context.HttpContext.Timestamp.ToUniversalTime()).TotalMilliseconds.ToString("F0");
            var queryString = context.HttpContext.Request.QueryString;
            var headers = context.HttpContext.Request.Headers;
            var actingUsername = context.HttpContext.User.Identity.Name;
            var logProps = new Dictionary<string, object>();
            foreach (string key in context.RouteData.Values.Keys.Where(k => !string.IsNullOrWhiteSpace(k)))
            {
                object value = context.RouteData.Values[key];
                logProps[key] = value;
            }
            foreach (string key in queryString.AllKeys.Where(k => !string.IsNullOrWhiteSpace(k)))
            {
                string value = queryString[key];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    logProps[key] = value;
                }
            }
            foreach (string key in headers.AllKeys.Where(k =>
                !string.IsNullOrWhiteSpace(k) && (
                k.Equals("Referer", StringComparison.OrdinalIgnoreCase) ||
                k.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))))
            {
                string value = headers[key];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    logProps[key] = value;
                }
            }
            LogManager.WriteLog(string.Format("Rendered in {0} MS", totalMS), string.Format("/{0}/{1}", controllerName, actionName), "Page Stats",
                System.Diagnostics.TraceEventType.Verbose, actingUsername, null, logProps);
        }

        /// <summary>
        /// Returns the request property as the specified type
        /// </summary>
        /// <typeparam name="T">the specified type to convert the value to</typeparam>
        /// <param name="properties">a list of CustomProperty objects</param>
        /// <param name="key">the name of the custom field associated with this property</param>
        /// <param name="defaultValue">this value will be returned if the property is missing or invalid</param>
        public static T GetPropertyValue<T>(this List<CustomProperty> properties, string key, T defaultValue) where T: IConvertible
        {
            T result = defaultValue;
            string rawValue = null;
            rawValue = properties
              .Where(p => p.Field.Name == key && !string.IsNullOrWhiteSpace(p.Value))
              .Select(p => p.Value).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(rawValue))
            {
                try
                {
                    result = (T)Convert.ChangeType(rawValue, typeof(T));
                }
                catch
                {
                    //Could not convert.  Pass back default value...
                }
            }
            return result;
        }

        /// <summary>
        /// Returns true if the specified payer has a card saved for use in paying the specified recipient
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="recipientUserName">the username of the recipient, or null for site fees</param>
        /// <param name="payerUserName">the username of the paying user</param>
        public static bool HasUnexpiredCardOnFile(this Controller controller, string recipientUserName, string payerUserName)
        {
            string actingUserName = "Anonymous";
            try
            {
                actingUserName = controller.User.Identity.Name;
                switch (AccountingClient.GetSaleBatchPaymentProviderName())
                {
                    case "StripeConnect":
                        return controller.HasUnexpiredStripeCardOnFile(recipientUserName, payerUserName);
                    default:
                        return UserClient.HasUnexpiredLocalCardOnFile(actingUserName, payerUserName);
                }
            }
            catch (Exception e)
            {
                LogManager.WriteLog(null, "Error checking for valid card", "MVC.HtmlHelpers", System.Diagnostics.TraceEventType.Error, actingUserName, e,
                    new Dictionary<string, object>() { { "recipientUserName", recipientUserName }, { "payerUserName", payerUserName } });
            }
            return false;
        }

        /// <summary>
        /// Returns true if the payer has a card saved for use in paying this invoice
        /// </summary>
        /// <param name="invoice">the invoice to evaluate</param>
        public static bool HasUnexpiredCardOnFile(this Invoice invoice)
        {
            string actingUserName = SystemActors.SystemUserName;
            string recipientUserName = invoice.Owner.UserName;
            string payerUserName = invoice.Payer.UserName;
            try
            {
                switch (AccountingClient.GetSaleBatchPaymentProviderName())
                {
                    case "StripeConnect":
                        return invoice.HasUnexpiredStripeCardOnFile();
                    default:
                        return UserClient.HasUnexpiredLocalCardOnFile(actingUserName, payerUserName);
                }
            }
            catch (Exception e)
            {
                LogManager.WriteLog(null, "Error checking for valid card", "MVC.HtmlHelpers", System.Diagnostics.TraceEventType.Error, actingUserName, e,
                    new Dictionary<string, object>() { { "recipientUserName", recipientUserName }, { "payerUserName", payerUserName } });
            }
            return false;
        }

        /// <summary>
        /// Returns true if the specified seller has payment gateway credentials and can process credit card payments from a card on file
        /// </summary>
        /// <param name="controller">an instance of the Controller class</param>
        /// <param name="sellerUserName">the username of the specified seller</param>
        public static bool SellerTakesCreditCardPayments(this Controller controller, string sellerUserName)
        {
            bool showProcessPaymentsLink = false;
            string saleBatchPaymentProviderName = AccountingClient.GetSaleBatchPaymentProviderName();
            if (!string.IsNullOrEmpty(saleBatchPaymentProviderName))
            {
                var seller = UserClient.GetUserByUserName(SystemActors.SystemUserName, sellerUserName);
                switch (saleBatchPaymentProviderName)
                {
                    case "StripeConnect":
                        bool hasStripeCredentials = seller.Properties.GetPropertyValue(StdUserProps.StripeConnect_SellerAccountConnected, false);
                        showProcessPaymentsLink = seller.CreditCardAccepted() && hasStripeCredentials;
                        break;
                    case "AuthorizeNetAIM":
                        bool hasAuthNetCredentials = !string.IsNullOrWhiteSpace(seller.Properties.GetPropertyValue(StdUserProps.AuthorizeNet_SellerTransactionKey, string.Empty));
                        showProcessPaymentsLink = seller.CreditCardAccepted() && hasAuthNetCredentials;
                        break;
                }
            }
            return showProcessPaymentsLink;
        }

        /// <summary>
        /// Returns true if the specified seller has payment gateway credentials and can process credit card payments from a card on file
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="sellerUserName">the username of the specified seller</param>
        public static bool SellerTakesCreditCardPayments(this HtmlHelper htmlHelper, string sellerUserName)
        {
            bool showProcessPaymentsLink = false;
            string saleBatchPaymentProviderName = AccountingClient.GetSaleBatchPaymentProviderName();
            if (!string.IsNullOrEmpty(saleBatchPaymentProviderName))
            {
                var seller = UserClient.GetUserByUserName(SystemActors.SystemUserName, sellerUserName);
                switch (saleBatchPaymentProviderName)
                {
                    case "StripeConnect":
                        bool hasStripeCredentials = seller.Properties.GetPropertyValue(StdUserProps.StripeConnect_SellerAccountConnected, false);
                        showProcessPaymentsLink = seller.CreditCardAccepted() && hasStripeCredentials;
                        break;
                    case "AuthorizeNetAIM":
                        bool hasAuthNetCredentials = !string.IsNullOrWhiteSpace(seller.Properties.GetPropertyValue(StdUserProps.AuthorizeNet_SellerTransactionKey, string.Empty));
                        showProcessPaymentsLink = seller.CreditCardAccepted() && hasAuthNetCredentials;
                        break;
                }
            }
            return showProcessPaymentsLink;
        }

        /// <summary>
        /// Example of running ad hoc SQL to update a line item status
        /// </summary>
        /// <param name="htmlHelper">an instance of the HtmlHelper class</param>
        /// <param name="lineItemId">The line item ID to update</param>
        /// <param name="newStatus">The new status to set the specified line item to</param>
        public static void SetLineItemStatus(this HtmlHelper htmlHelper, int lineItemId, string newStatus)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["db_connection"].ConnectionString;
            string sql = "update RWX_LineItems set status=@newStatus where Id=@lineItemId";
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand();
                    command.CommandText = sql;
                    command.Connection = connection;
                    command.CommandType = CommandType.Text;
                    command.Parameters.AddWithValue("@newStatus", newStatus);
                    command.Parameters.AddWithValue("@lineItemId", lineItemId);
                    command.ExecuteNonQuery();
                    connection.Close();
                }
            }
            catch (Exception e)
            {
                LogManager.WriteLog(null, "SetLineItemStatus Failed", "HtmlHelpers.SetLineItemStatus", System.Diagnostics.TraceEventType.Error, null, e,
                    new Dictionary<string, object> { { "lineItemId", lineItemId }, { "newStatus", newStatus }, { "sql", sql } });
            }
        }

        /// <summary>
        /// Populates the UserInput.Items key/value Dictionary from the specified listing
        /// </summary>
        public static void FillInputFromListing(this UserInput input, Listing listing)
        {
            CultureInfo cultureInfo = SiteClient.SupportedCultures[input.CultureName];
            input.Items.Add(Strings.Fields.ListingID, listing.ID.ToString());
            input.Items.Add(Strings.Fields.Title, listing.Title);
            input.Items.Add(Strings.Fields.Subtitle, listing.Subtitle);
            input.Items.Add(Strings.Fields.Description, listing.Description);
            input.Items.Add(Strings.Fields.Quantity, listing.CurrentQuantity.ToString(cultureInfo));
            if (listing.OriginalPrice.HasValue)
            {
                input.Items.Add(Strings.Fields.Price, listing.OriginalPrice.Value.ToString("N2", cultureInfo));
            }
            else
            {
                input.Items.Add(Strings.Fields.Price, string.Empty);
            }
            if (listing.AutoRelistRemaining > SiteClient.IntSetting(SiteProperties.MaxAutoRelists))
            {
                input.Items.Add(Strings.Fields.AutoRelist, SiteClient.IntSetting(SiteProperties.MaxAutoRelists).ToString(cultureInfo));
            }
            else
            {
                input.Items.Add(Strings.Fields.AutoRelist, listing.AutoRelistRemaining.ToString(cultureInfo));
            }

            var leafRegion = listing.LeafRegion();
            if (leafRegion != null)
            {
                input.Items.Add(Fields.RegionID, leafRegion.ID.ToString());
            }

            //properties
            foreach (var property in listing.Properties)
            {
                if (input.Items.ContainsKey(property.Field.Name))
                    continue;

                if (!string.IsNullOrEmpty(property.Value))
                {
                    switch (property.Field.Type)
                    {
                        case CustomFieldType.DateTime:
                            DateTime tempDateTime;
                            if (DateTime.TryParse(property.Value, out tempDateTime))
                            {
                                property.Value = tempDateTime.ToString("d", cultureInfo);
                            }
                            break;
                        case CustomFieldType.Int:
                            int tempInt;
                            if (int.TryParse(property.Value, out tempInt))
                            {
                                property.Value = tempInt.ToString(cultureInfo);
                            }
                            break;
                        case CustomFieldType.Decimal:
                            decimal tempDecimal;
                            if (decimal.TryParse(property.Value, out tempDecimal))
                            {
                                property.Value = tempDecimal.ToString(Strings.Formats.Decimal, cultureInfo);
                            }
                            break;
                    }
                }
                input.Items.Add(property.Field.Name, property.Value);
            }

            //media
            Utilities.SetMedia(listing.Media, input);

            //locations
            Utilities.SetLocations(SiteClient.Locations, listing.Locations, input);

            //decorations
            Utilities.SetDecorations(SiteClient.Decorations, listing.Decorations, input);

            //get listing type-specific properties
            List<CustomProperty> listingTypeProperties = ListingClient.GetListingTypeProperties(listing.Type.Name, "Site");

            //shipping
            bool shippingEnabled = bool.Parse(listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.EnableShipping).First().Value);
            if (shippingEnabled)
            {
                Utilities.SetShippingOptions(listing.ShippingOptions, input);
            }

            //duration
            string durationOpts = listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.ListingDurationOptions).First().Value;
            bool gtcOptionAvailable =
                listingTypeProperties.Exists(p => p.Field.Name == Strings.SiteProperties.EnableGTC)
                    ? bool.Parse(listingTypeProperties.Where(p => p.Field.Name == Strings.SiteProperties.EnableGTC).First().Value)
                    : false;
            TimeZoneInfo siteTimeZone = TimeZoneInfo.FindSystemTimeZoneById(SiteClient.SiteTimeZone);
            if (!listing.IsGoodTilCanceled())
            {
                if (durationOpts == Strings.DurationOptions.Duration || durationOpts == Strings.DurationOptions.StartDuration)
                {
                    if (listing.Duration.HasValue)
                    {
                        input.Items.Add(Strings.Fields.Duration, ((listing.Duration ?? 0) / 1440).ToString());
                    }
                    else
                    {
                        input.Items.Add(Strings.Fields.Duration, string.Empty);
                    }
                }
                if (durationOpts == Strings.DurationOptions.StartEnd || durationOpts == Strings.DurationOptions.StartDuration)
                {
                    if (listing.StartDTTM.HasValue && listing.StartDTTM.Value > DateTime.UtcNow)
                    {
                        DateTime localStartDTTM = TimeZoneInfo.ConvertTime(listing.StartDTTM.Value, TimeZoneInfo.Utc, siteTimeZone);
                        input.Items.Add(Strings.Fields.StartDate, localStartDTTM.ToString("d", cultureInfo));
                        input.Items.Add(Strings.Fields.StartTime, localStartDTTM.ToString("t", cultureInfo));
                    }
                    else
                    {
                        input.Items.Add(Strings.Fields.StartDate, string.Empty);
                        input.Items.Add(Strings.Fields.StartTime, string.Empty);
                    }
                }
                if (durationOpts == Strings.DurationOptions.End || durationOpts == Strings.DurationOptions.StartEnd)
                {
                    if (listing.EndDTTM.HasValue && listing.EndDTTM.Value > DateTime.UtcNow)
                    {
                        DateTime localEndDTTM = TimeZoneInfo.ConvertTime(listing.EndDTTM.Value, TimeZoneInfo.Utc, siteTimeZone);
                        input.Items.Add(Strings.Fields.EndDate, localEndDTTM.ToString("d", cultureInfo));
                        input.Items.Add(Strings.Fields.EndTime, localEndDTTM.ToString("t", cultureInfo));
                    }
                    else
                    {
                        input.Items.Add(Strings.Fields.EndDate, string.Empty);
                        input.Items.Add(Strings.Fields.EndTime, string.Empty);
                    }
                }
            }

        }

    }
}
