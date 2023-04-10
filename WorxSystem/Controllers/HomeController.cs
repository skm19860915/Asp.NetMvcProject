using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Web;
using System.Web.Mvc;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.Validation;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.DTO.FaultContracts;
using RainWorx.FrameWorx.MVC.Helpers;
using MvcSiteMap.Core;
using RainWorx.FrameWorx.MVC.Models;
using RainWorx.FrameWorx.SiteMap;
using RainWorx.FrameWorx.Utility;
using RainWorx.FrameWorx.Strings;

namespace RainWorx.FrameWorx.MVC.Controllers
{
    /// <summary>
    /// Provides methods that respond to all MVC requests not specific to admin, account or listing areas
    /// </summary>
    [GoUnsecure]
    public class HomeController : AuctionWorxController
    {
        #region Homepage

        /// <summary>
        /// Displays site homepage
        /// </summary>
        /// <param name="viewFilter">the filter code for the specified search criteria (e.g. &quot;All&quot;, &quot;Current&quot;, &quot;Preview&quot;, &quot;Closed&quot;)</param>
        /// <param name="page">0-based page index</param>
        /// <returns>View()</returns>        
        [Authenticate]
        public ActionResult Index(string viewFilter, int? page)
        {
            ViewData["ViewFilter"] = viewFilter;

            ViewData["ValidCategoryCounts"] = true;

            ViewData["ShowDecorations"] = SiteClient.BoolSetting(Strings.SiteProperties.ShowHomepageDecorations);

            string statuses = Strings.ListingStatuses.Active + "," + Strings.ListingStatuses.Preview;
            //if (SiteClient.BoolSetting(Strings.SiteProperties.ShowPendingListings)) statuses += "," + Strings.ListingStatuses.Pending;
            ViewData[Strings.MVC.ViewData_CategoryCounts] = CommonClient.GetCategoryCounts(new List<string>(0), statuses, string.Empty);

            ViewData[Strings.MVC.ViewData_CategoryNavigator] = CommonClient.GetChildCategories(CategoryRoots.ListingCats, includeRelatedCustomFields: false);
            //ViewData[Strings.MVC.ViewData_StoreNavigator] = CommonClient.GetChildCategories(CategoryRoots.Stores, includeRelatedCustomFields: false);
            //ViewData[Strings.MVC.ViewData_EventNagivator] = CommonClient.GetChildCategories(CategoryRoots.Events, includeRelatedCustomFields: false);
            ViewData[Strings.MVC.ViewData_RegionNagivator] = CommonClient.GetChildCategories(CategoryRoots.Regions, includeRelatedCustomFields: false);

            ViewData[Strings.MVC.PageIndex] = page ?? 0;

            return View();
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Raises an error to test error handling configuration
        /// </summary>
        /// <returns>always raises an error</returns>
        public ActionResult Error()
        {
            if (false)
            {
                //Test sending a "no template" email notification
                const int testListingID = 228011;

                NotifierClient.QueueSystemNotification(
                    SystemActors.SystemUserName, null,

                    //specify template as "NO_TEMPLATE" to indicate a template is not intended
                    Templates.NoTemplate, 
                    
                    //optionally specify an email detail type and detail ID (use string.Empty and 0, if not needed)
                    DetailTypes.Listing, testListingID,
                    //string.Empty, 0,

                    //all text before the first | char will be used as the message subject
                    "A no-template subject about listing #[Detail.ID]!" + 

                    "|" +

                    //all text after the first | char will be used as the message body
                    "A no-template message about <strong>[Detail.Title] ([Detail.ID])</strong> owned by <strong>[Detail.Owner.UserName]</strong>.",
                    null, null, null, null);
            }

            throw new Exception("Benign");
        }

        #endregion

        #region CMS Content Pages

        /// <summary>
        /// Displays "About" CMS content
        /// </summary>
        /// <returns>View()</returns>
        public ActionResult About()
        {
            return View();
        }

        /// <summary>
        /// Displays "Terms" CMS content
        /// </summary>
        /// <returns>View()</returns>
        public ActionResult Terms()
        {
            return View();
        }

        /// <summary>
        /// Displays "Privacy Policy" CMS content
        /// </summary>
        /// <returns>View()</returns>
        public ActionResult PrivacyPolicy()
        {
            return View();
        }

        /// <summary>
        /// Displays "Help" CMS content
        /// </summary>
        /// <returns>View()</returns>
        public ActionResult Help()
        {
            return View();
        }

        /// <summary>
        /// Displays "Contact Us" CMS content
        /// </summary>
        /// <returns>View()</returns>
        public ActionResult Contact()
        {
            //if user is logged in, pre-populate fields (first, last and email)
            if (User.Identity.IsAuthenticated)
            {
                var loggedOnUser = UserClient.GetUserByUserName(User.Identity.Name, User.Identity.Name);
                ViewData[Strings.Fields.FirstName] = loggedOnUser.FirstName();
                ViewData[Strings.Fields.LastName] = loggedOnUser.LastName();
                ViewData[Strings.Fields.Email] = loggedOnUser.Email;
            }

            return View();
        }

        /// <summary>
        /// Displays "Contact Us" CMS content
        /// </summary>
        /// <param name="formCollection">a collection of all submitted form values</param>
        /// <returns>View()</returns>
        [HttpPost]
        public async Task<ActionResult> Contact(FormCollection formCollection)
        {
            var allFields = new Dictionary<string, string>(formCollection.Count);
            foreach (string key in formCollection.AllKeys.Where(k => k != null))
            {
                allFields.Add(key, formCollection[key]);
                ViewData[key] = formCollection[key];
            }
            try
            {
                //validate required fields
                var validation = new ValidationResults();

                string demoReCaptchaPublicKey = ConfigurationManager.AppSettings["DemoReCaptchaPublicKey"];
                if (SiteClient.BoolSetting(Strings.SiteProperties.EnableRecaptchaForContactUs) || !string.IsNullOrWhiteSpace(demoReCaptchaPublicKey))
                {
                    await this.ValidateCaptcha(validation, this);
                }

                if (!allFields.ContainsKey(Strings.Fields.FirstName) || string.IsNullOrEmpty(allFields[Strings.Fields.FirstName]))
                    validation.AddResult(new ValidationResult("FirstName_Required", this,
                                                              Strings.Fields.FirstName, Strings.Fields.FirstName, null));
                if (!allFields.ContainsKey(Strings.Fields.LastName) || string.IsNullOrEmpty(allFields[Strings.Fields.LastName]))
                    validation.AddResult(new ValidationResult("LastName_Required", this,
                                                              Strings.Fields.LastName, Strings.Fields.LastName, null));
                if (!allFields.ContainsKey(Strings.Fields.Email) || string.IsNullOrEmpty(allFields[Strings.Fields.Email]))
                    validation.AddResult(new ValidationResult("Email_Required", this,
                                                              Strings.Fields.Email, Strings.Fields.Email, null));
                if (!allFields.ContainsKey(Strings.Fields.Message) || string.IsNullOrEmpty(allFields[Strings.Fields.Message]))
                    validation.AddResult(new ValidationResult("Message_Required", this,
                                                              Strings.Fields.Message, Strings.Fields.Message, null));
                if (!validation.IsValid)
                {
                    Statix.ThrowValidationFaultContract(validation);
                }

                //send message
                var propertyBag = CommonClient.CreatePropertyBag(allFields);
                NotifierClient.QueueSystemNotification(Strings.SystemActors.SystemUserName, null,
                                                       Strings.Templates.ContactUsMessage,
                                                       Strings.DetailTypes.PropertyBag, propertyBag.ID, null, allFields[Strings.Fields.Email], null, null, null);
                return RedirectToAction(Strings.MVC.ContactUsSubmittedAction);
            }
            catch (FaultException<ValidationFaultContract> vfc)
            {
                //display validation errors                
                foreach (ValidationIssue issue in vfc.Detail.ValidationIssues)
                {
                    ModelState.AddModelError(issue.Key, issue.Message);
                }
            }
            catch (Exception e)
            {
                ModelState.AddModelError(Strings.MVC.FormModelErrorKey, e.Message);
                PrepareErrorMessage("Contact", MessageType.Method);
            }
            return View();
        }

        /// <summary>
        /// Displays "ContactUsSubmitted" CMS content
        /// </summary>
        /// <returns>View()</returns>
        public ActionResult ContactUsSubmitted()
        {
            return View();
        }

        /// <summary>
        /// Displays "Not Found" CMS content
        /// </summary>
        /// <returns>View()</returns>
        public ActionResult NotFound()
        {
            return View();
        }

        #endregion

        #region Language / Currency

        //TODO: verify this function is deprecated given ajax 'SetUserCulture' function
        /// <summary>
        /// Processes request to change current culture
        /// </summary>
        /// <param name="culture">requested language code (e.g. "en-US")</param>
        /// <returns>Redirect to site homepage</returns>
        public ActionResult ChangeCulture(string culture)
        {
            this.SetCookie(Strings.MVC.CultureCookie, culture);
            return RedirectToAction(Strings.MVC.IndexAction, Strings.MVC.HomeController);
        }

        #endregion

        #region Sitemap

        /// <summary>
        /// Displays human-readable sitemap
        /// </summary>
        /// <returns>View(List&lt;SiteMapNode&gt;)</returns>
        public ActionResult Sitemap()
        {
            //SiteMapNode retVal = GetDynamicSiteMap();
            List<SiteMapNode> retVal = GetDynamicSiteMapColumns("sitemapDisplayColumn");
            return View(retVal);
        }

        /// <summary>
        /// Displays sitemap as an xml document
        /// </summary>
        /// <returns>XmlSitemapResult</returns>
        public ActionResult SitemapXml()
        {
            XmlSitemapResult retVal = new XmlSitemapResult(GetDynamicSiteMap());
            return retVal;
        }

        #region Sitemap Helper functions

        #region AddSiteMapTree via CommonClient.GetCategoryHierarchy() calls

        private void AddSiteMapTree(SiteMapNode currentNode, Hierarchy<int, Category> catTree, string priority, string changeFrequency, string crumbFormat)
        {
            AddSiteMapTree(currentNode, catTree, priority, changeFrequency, crumbFormat, string.Empty, string.Empty, false);
        }

        private void AddSiteMapTree(SiteMapNode currentNode, Hierarchy<int, Category> catTree, string priority, string changeFrequency, string crumbFormat, string parentsCrumbs, string parentExtra, bool includeCurrent)
        {
            SiteMapNodeCollection childNodes = new SiteMapNodeCollection(currentNode.ChildNodes);

            if (includeCurrent)
            {
                Category category = catTree.Current;

                string currentBreadcrumbs = string.Format(crumbFormat, category.ID);
                if (!string.IsNullOrEmpty(parentsCrumbs)) currentBreadcrumbs = parentsCrumbs + "-" + currentBreadcrumbs;

                string currentExtra = category.Name.SimplifyForURL("-");
                if (!string.IsNullOrEmpty(parentExtra)) currentExtra = parentExtra + "-" + currentExtra;

                MvcSiteMapNode newNode = new MvcSiteMapNode(System.Web.SiteMap.Provider, category.ID.ToString(), null)
                {
                    Controller = "Listing",
                    Action = "Browse",
                    RouteValues = new Dictionary<string, object> { { "breadcrumbs", currentBreadcrumbs }, { "extra", currentExtra } },
                    Title = this.LocalizedCategoryName(category.Name),
                    Description = this.LocalizedCategoryName(category.Name)
                };

                newNode["changefreq"] = changeFrequency;
                newNode["priority"] = priority;
                foreach (Hierarchy<int, Category> subCatTree in catTree.ChildHierarchies.OrderBy(c => c.Current.DisplayOrder))
                {
                    AddSiteMapTree(newNode, subCatTree, priority, changeFrequency, crumbFormat, currentBreadcrumbs, currentExtra, true);
                }
                childNodes.Add(newNode);
                newNode.ParentNode = currentNode;
                currentNode.ChildNodes = childNodes;
            }
            else
            {
                foreach (Hierarchy<int, Category> subCatTree in catTree.ChildHierarchies.OrderBy(c => c.Current.DisplayOrder))
                {
                    AddSiteMapTree(currentNode, subCatTree, priority, changeFrequency, crumbFormat, parentsCrumbs, parentExtra, true);
                }
            }
        }

        #endregion

        #region AddSiteMapTree via CommonClient.GetChildCategories() calls

        private void AddSiteMapTree(SiteMapNode node, Category category, string priority, string changeFrequency, string crumbFormat, int maxTiers)
        {
            AddSiteMapTree(node, category, priority, changeFrequency, crumbFormat, maxTiers, string.Empty, string.Empty, 1);
        }

        private void AddSiteMapTree(SiteMapNode node, Category category, string priority, string changeFrequency, string crumbFormat, int maxTiers, string parentsCrumbs, string parentExtra, int currentTier)
        {
            SiteMapNodeCollection childNodes = new SiteMapNodeCollection(node.ChildNodes);

            string breadcrumbs = string.Format(crumbFormat, category.ID);
            if (!string.IsNullOrEmpty(parentsCrumbs)) breadcrumbs = parentsCrumbs + "-" + breadcrumbs;

            //string extra = CommonClient.GetCategoryByID(category.ID).Name.SimplifyForURL("-");
            string extra = category.Name.SimplifyForURL("-");
            if (!string.IsNullOrEmpty(parentExtra)) extra = parentExtra + "-" + extra;

            MvcSiteMapNode newNode = new MvcSiteMapNode(System.Web.SiteMap.Provider, category.ID.ToString(), null)
            {
                Controller = "Listing",
                Action = "Browse",
                RouteValues = new Dictionary<string, object> { { "breadcrumbs", breadcrumbs }, { "extra", extra } },
                Title = this.LocalizedCategoryName(category.Name),
                Description = this.LocalizedCategoryName(category.Name)
            };
            newNode["changefreq"] = changeFrequency;
            newNode["priority"] = priority;
            if (currentTier < maxTiers)
            {
                foreach (Category childCategory in CommonClient.GetChildCategories(category.ID))
                {
                    AddSiteMapTree(newNode, childCategory, priority, changeFrequency, crumbFormat, maxTiers, breadcrumbs, extra, (currentTier + 1));
                }
            }
            childNodes.Add(newNode);
            newNode.ParentNode = node;
            node.ChildNodes = childNodes;
        }

        #endregion

        private SiteMapNode CloneSiteMap(SiteMapNode originalNode, bool isBuyer, bool isSeller)
        {
            bool activeDirectoryEnabled = SiteClient.BoolSetting(SiteProperties.ActiveDirectoryEnabled);

            SiteMapNode clone = originalNode.Clone();
            SiteMapNodeCollection children = new SiteMapNodeCollection();
            foreach (SiteMapNode childNode in originalNode.ChildNodes)
            {
                bool addNode;
                //skip certain nodes if the corresponding site property is disabled
                switch (childNode.Key)
                {
                    case "ListingRegions":
                        addNode = SiteClient.BoolSetting(Strings.SiteProperties.EnableRegions);
                        break;
                    case "Messaging":
                        addNode = SiteClient.BoolSetting(Strings.SiteProperties.UserMessagingEnabled);
                        break;
                    case "Feedback":
                        addNode = SiteClient.BoolSetting(Strings.SiteProperties.FeedbackEnabled);
                        break;
                    case "_Account_UserVerification_UserVerification":
                        addNode = SiteClient.BoolSetting(Strings.SiteProperties.VerifyUserEmail);
                        break;
                    case "_Account_SalesTaxManagement_Taxes":
                        addNode = !SiteClient.BoolSetting(Strings.SiteProperties.HideTaxFields);
                        break;
                    case "_Account_CreditCards_CreditCards":
                        addNode = SiteClient.BoolSetting(Strings.SiteProperties.CreditCardsEnabled);
                        break;
                    case "MyAccount":
                        addNode = (isBuyer || isSeller);
                        break;
                    case "CreateListing":
                        addNode = (isSeller && (!SiteClient.EnableEvents || SiteClient.BoolSetting(Strings.SiteProperties.EnableNonAuctionListingsForEvents)));
                        break;
                    case "CreateEvent":
                        addNode = (isSeller && SiteClient.EnableEvents);
                        break;
                    case "CreateLot":
                        addNode = (isSeller && SiteClient.EnableEvents);
                        break;
                    case "MyAccount_Events":
                        addNode = (isSeller && SiteClient.EnableEvents);
                        break;
                    case "MyAccount_Listing":
                        addNode = (isSeller && (!SiteClient.EnableEvents || SiteClient.BoolSetting(Strings.SiteProperties.EnableNonAuctionListingsForEvents)));
                        break;
                    case "MyAccount_Bidding":
                        addNode = isBuyer;
                        break;
                    case "Invoices_Purchases":
                        addNode = (isBuyer && !SiteClient.EnableEvents);
                        break;
                    case "Invoices_Sales":
                        addNode = (isSeller && (!SiteClient.EnableEvents || SiteClient.BoolSetting(Strings.SiteProperties.EnableNonAuctionListingsForEvents)));
                        break;
                    case "EventSaleInvoices":
                        addNode = (isSeller && SiteClient.EnableEvents);
                        break;
                    case "EventPurchaseInvoices":
                        addNode = (isBuyer && SiteClient.EnableEvents);
                        break;
                    case "Feedback_Buyer":
                        addNode = isBuyer;
                        break;
                    case "Feedback_Seller":
                        addNode = isSeller;
                        break;
                    case "ListingPreferences":
                        addNode = isSeller;
                        break;
                    case "Password":
                        addNode = !activeDirectoryEnabled;
                        break;
                    case "Register":
                        addNode = !activeDirectoryEnabled;
                        break;
                    case "UserVerification":
                        addNode = !activeDirectoryEnabled;
                        break;
                    case "ForgotPassword":
                        addNode = !activeDirectoryEnabled;
                        break;
                    default:
                        addNode = true;
                        break;
                }
                if (addNode)
                {
                    SiteMapNode childClone = CloneSiteMap(childNode, isBuyer, isSeller);
                    children.Add(childClone);
                    childClone.ParentNode = clone;
                }
            }
            clone.ChildNodes = children;
            return clone;
        }

        private SiteMapNode GetDynamicSiteMap()
        {
            bool isBuyer = false;
            bool isSeller = false;
            if (User.Identity.IsAuthenticated)
            {
                isBuyer = User.IsInRole(Strings.Roles.Buyer);
                isSeller = User.IsInRole(Strings.Roles.Seller);
            }
            SiteMapNode clone = CloneSiteMap(System.Web.SiteMap.RootNode, isBuyer, isSeller);
            IList<SiteMapNode> nodes = new List<SiteMapNode>();
            foreach (SiteMapNode n in clone.GetAllNodes())
            {
                nodes.Add(n);
            }

            int maxCatTiers = 0;
            if (!int.TryParse(ConfigurationManager.AppSettings["SiteMapCategoryDepth"], out maxCatTiers))
                maxCatTiers = 1; // if value is missing, assume only top level categories
            int maxRegionTiers = 0;
            if (!int.TryParse(ConfigurationManager.AppSettings["SiteMapRegionDepth"], out maxRegionTiers))
                maxRegionTiers = 1; // if value is missing, assume only top level regions

            /*  
             **************************************************************************
             * This method uses a series of CommonClient.GetChildCategories() calls
             **************************************************************************
             *
             
            //get dynamic listing category nodes
            SiteMapNode listingCatsNode = (from n in nodes
                                        where n.Key == "ListingCategories"
                                        select n).FirstOrDefault();
            if (listingCatsNode != null) {
                string priority = listingCatsNode["priority"];
                string changeFrequency = listingCatsNode["changefreq"];
                foreach (Category listingCat in CommonClient.GetChildCategories(9)) {
                    AddSiteMapTree(listingCatsNode, listingCat, priority, changeFrequency, "C{0}", maxCatTiers);
                }
            }

            //get dynamic listing region nodes
            SiteMapNode listingRegionsNode = (from n in nodes
                                        where n.Key == "ListingRegions"
                                        select n).FirstOrDefault();
            if (listingRegionsNode != null)
            {
                string priority = listingRegionsNode["priority"];
                string changeFrequency = listingRegionsNode["changefreq"];
                foreach (Category listingRegion in CommonClient.GetChildCategories(27))
                {
                    AddSiteMapTree(listingRegionsNode, listingRegion, priority, changeFrequency, "R{0}", maxRegionTiers);
                }
            }
             * 
             */

            /*
             **************************************************************************
             * This method uses a CommonClient.GetCategoryHierarchy() call
             **************************************************************************
             */

            //get dynamic listing category nodes
            SiteMapNode listingCatsNode = (from n in nodes
                                           where n.Key == "ListingCategories"
                                           select n).FirstOrDefault();
            if (listingCatsNode != null)
            {
                string priority = listingCatsNode["priority"];
                string changeFrequency = listingCatsNode["changefreq"];
                //Hierarchy<int, Category> topCatTree = CommonClient.GetCategoryHierarchy(9);
                Hierarchy<int, Category> topCatTree = CommonClient.GetLimitedCategoryHierarchy(9, maxCatTiers);
                AddSiteMapTree(listingCatsNode, topCatTree, priority, changeFrequency, "C{0}");
            }

            //get dynamic listing region nodes
            SiteMapNode listingRegionsNode = (from n in nodes
                                              where n.Key == "ListingRegions"
                                              select n).FirstOrDefault();
            if (listingRegionsNode != null)
            {
                string priority = listingRegionsNode["priority"];
                string changeFrequency = listingRegionsNode["changefreq"];
                //Hierarchy<int, Category> topRegionTree = CommonClient.GetCategoryHierarchy(27);
                Hierarchy<int, Category> topRegionTree = CommonClient.GetLimitedCategoryHierarchy(27, maxRegionTiers);
                AddSiteMapTree(listingRegionsNode, topRegionTree, priority, changeFrequency, "R{0}");
            }

            /* 
             */

            return clone;
        }

        //"sitemapDisplayColumn"
        private List<SiteMapNode> GetDynamicSiteMapColumns(string columnAttribName)
        {
            List<SiteMapNode> columnNodes = new List<SiteMapNode>();
            Dictionary<string, SiteMapNodeCollection> columnNodeChildren = new Dictionary<string, SiteMapNodeCollection>();

            bool isBuyer = false;
            bool isSeller = false;
            if (User.Identity.IsAuthenticated)
            {
                isBuyer = User.IsInRole(Strings.Roles.Buyer);
                isSeller = User.IsInRole(Strings.Roles.Seller);
            }
            SiteMapNode clone = CloneSiteMap(System.Web.SiteMap.RootNode, isBuyer, isSeller);
            IList<SiteMapNode> nodes = new List<SiteMapNode>();
            foreach (SiteMapNode n in clone.GetAllNodes())
            {
                //n.Title = this.GlobalResourceString(n.Title);
                nodes.Add(n);

                string columnKey = n[columnAttribName];
                if (!string.IsNullOrEmpty(columnKey))
                {
                    string columnNodeKey = "column_" + columnKey;
                    SiteMapNode matchingColumnNode =
                        (from cn in columnNodes
                         where cn.Key == columnNodeKey
                         select cn).FirstOrDefault();
                    if (matchingColumnNode == null)
                    {
                        matchingColumnNode = new MvcSiteMapNode(System.Web.SiteMap.Provider, columnNodeKey, null)
                                                 {
                                                     Url = "#" + columnNodeKey,
                                                     ParentNode = clone
                                                 };
                        //matchingColumnNode.Title = this.GlobalResourceString(matchingColumnNode.Title);
                        columnNodes.Add(matchingColumnNode);
                        columnNodeChildren.Add(columnNodeKey, new SiteMapNodeCollection());
                    }
                    columnNodeChildren[columnNodeKey].Add(n);
                }

            }

            int maxCatTiers = 0;
            if (!int.TryParse(ConfigurationManager.AppSettings["SiteMapCategoryDepth"], out maxCatTiers))
                maxCatTiers = 1; // if value is missing, assume only top level categories
            int maxRegionTiers = 0;
            if (!int.TryParse(ConfigurationManager.AppSettings["SiteMapRegionDepth"], out maxRegionTiers))
                maxRegionTiers = 1; // if value is missing, assume only top level regions

            /*  
             **************************************************************************
             * This method uses a series of CommonClient.GetChildCategories() calls
             **************************************************************************
             *
             
            //get dynamic listing category nodes
            SiteMapNode listingCatsNode = (from n in nodes
                                        where n.Key == "ListingCategories"
                                        select n).FirstOrDefault();
            if (listingCatsNode != null) {
                string priority = listingCatsNode["priority"];
                string changeFrequency = listingCatsNode["changefreq"];
                foreach (Category listingCat in CommonClient.GetChildCategories(9)) {
                    AddSiteMapTree(listingCatsNode, listingCat, priority, changeFrequency, "C{0}", maxCatTiers);
                }
            }

            //get dynamic listing region nodes
            SiteMapNode listingRegionsNode = (from n in nodes
                                        where n.Key == "ListingRegions"
                                        select n).FirstOrDefault();
            if (listingRegionsNode != null)
            {
                string priority = listingRegionsNode["priority"];
                string changeFrequency = listingRegionsNode["changefreq"];
                foreach (Category listingRegion in CommonClient.GetChildCategories(27))
                {
                    AddSiteMapTree(listingRegionsNode, listingRegion, priority, changeFrequency, "R{0}", maxRegionTiers);
                }
            }

             * 
             */

            /*
             **************************************************************************
             * This method uses a CommonClient.GetCategoryHierarchy() call
             **************************************************************************
             */

            //get dynamic listing category nodes
            SiteMapNode listingCatsNode = (from n in nodes
                                           where n.Key == "ListingCategories"
                                           select n).FirstOrDefault();
            if (listingCatsNode != null)
            {
                string priority = listingCatsNode["priority"];
                string changeFrequency = listingCatsNode["changefreq"];
                //Hierarchy<int, Category> topCatTree = CommonClient.GetCategoryHierarchy(9);
                Hierarchy<int, Category> topCatTree = CommonClient.GetLimitedCategoryHierarchy(9, maxCatTiers);
                AddSiteMapTree(listingCatsNode, topCatTree, priority, changeFrequency, "C{0}");
            }

            //get dynamic listing region nodes
            SiteMapNode listingRegionsNode = (from n in nodes
                                              where n.Key == "ListingRegions"
                                              select n).FirstOrDefault();
            if (listingRegionsNode != null)
            {
                string priority = listingRegionsNode["priority"];
                string changeFrequency = listingRegionsNode["changefreq"];
                //Hierarchy<int, Category> topRegionTree = CommonClient.GetCategoryHierarchy(27);
                Hierarchy<int, Category> topRegionTree = CommonClient.GetLimitedCategoryHierarchy(27, maxRegionTiers);
                AddSiteMapTree(listingRegionsNode, topRegionTree, priority, changeFrequency, "R{0}");
            }

            /* 
             */

            foreach (SiteMapNode cn in columnNodes)
            {
                cn.ChildNodes = columnNodeChildren[cn.Key];
            }

            LocalizeNodeTitles(columnNodes);

            //return clone;
            return columnNodes;
        }

        private void LocalizeNodeTitles(IEnumerable<SiteMapNode> nodes)
        {
            foreach (SiteMapNode n in nodes)
            {
                if (!string.IsNullOrEmpty(n.Title))
                {
                    n.Title = this.GlobalResourceString(n.Title);
                }
                List<SiteMapNode> childNodes = new List<SiteMapNode>();
                foreach (SiteMapNode cn in n.ChildNodes)
                {
                    childNodes.Add(cn);
                }
                LocalizeNodeTitles(childNodes);

            }
        }

        #endregion

        #endregion

        #region Version Info

        /// <summary>
        /// Displays Version Information for Debug Purposes
        /// </summary>
        /// <returns></returns>
        public ActionResult Version()
        {
            return View();
        }

        #endregion Version Info

        #region Site Map Placeholder Redirects

        /// <summary>
        /// Redirects to the applicable view
        /// </summary>
        public ActionResult Information() { return RedirectToAction(Strings.MVC.IndexAction); }

        #endregion Site Map Placeholder Redirects

        #region Maintenance

        /// <summary>
        /// Displays maintenance mode view
        /// </summary>
        /// <returns>View()</returns>
        public ActionResult Maintenance()
        {
            return View();
        }

        #endregion

    }
}
