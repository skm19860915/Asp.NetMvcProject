using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace RainWorx.FrameWorx.MVC
{
    /// <summary>
    /// Handles MVC routing configuration
    /// </summary>
    public class RouteConfig
    {
        /// <summary>
        /// Registers MVC routes
        /// </summary>
        /// <param name="routes">a collection of ASP.NET routes</param>
        /// <param name="homepageOption">one of &quot;default&quot; or &quot;browse&quot;</param>
        public static void RegisterRoutes(RouteCollection routes, string homepageOption)
        {
            routes.IgnoreRoute("favicon.ico");
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                "MediaUpload",                                              // Route name
                "Media/AsyncUploadFile",                           // URL with parameters
                new { controller = "Media", action = "AsyncUploadFile", id = "" }  // Parameter defaults
            );

            routes.MapRoute(
                "MediaUpload2",                                              // Route name
                "Media/AsyncUploadListingImage",                           // URL with parameters
                new { controller = "Media", action = "AsyncUploadListingImage", id = "" }  // Parameter defaults
            );

            routes.MapRoute(
                "MediaUpload3",                                              // Route name
                "Media/AsyncUploadEventImage",                           // URL with parameters
                new { controller = "Media", action = "AsyncUploadEventImage", id = "" }  // Parameter defaults
            );

            routes.MapRoute(
                "MediaUpload4",                                              // Route name
                "Media/AsyncUploadEventBanner",                           // URL with parameters
                new { controller = "Media", action = "AsyncUploadEventBanner", id = "" }  // Parameter defaults
            );

            routes.MapRoute(
                "MediaUpload5",                                              // Route name
                "Media/AsyncUploadLogo",                           // URL with parameters
                new { controller = "Media", action = "AsyncUploadLogo", id = "" }  // Parameter defaults
            );

            routes.MapRoute(
                "MediaUpload6",                                              // Route name
                "Media/AsyncUploadBanner",                           // URL with parameters
                new { controller = "Media", action = "AsyncUploadBanner", id = "" }  // Parameter defaults
            );


            routes.MapRoute(
                "MediaDelete",                                              // Route name
                "Media/DeleteMedia",                           // URL with parameters
                new { controller = "Media", action = "DeleteMedia", id = "" }  // Parameter defaults
            );

            routes.MapRoute(
                "MediaRotate",                                              // Route name
                "Media/RotateMedia",                           // URL with parameters
                new { controller = "Media", action = "RotateMedia", id = "" }  // Parameter defaults
            );

            routes.MapRoute(
                "Media",                                              // Route name
                "Media/{id}",                           // URL with parameters
                new { controller = "Media", action = "Get", id = "" }  // Parameter defaults
            );

            routes.MapRoute(
              "IncomingMessages",                                              // Route name
              "Account/Inbox",                           // URL with parameters
              new { controller = "Account", action = "ViewMessages", incoming = true }  // Parameter defaults
            );

            routes.MapRoute(
              "OutgoingMessages",                                              // Route name
              "Account/Outbox",                           // URL with parameters
              new { controller = "Account", action = "ViewMessages", incoming = false }  // Parameter defaults
            );

            routes.MapRoute(
               "Fields",                                              // Route name
               "Admin/Fields/{GroupName}",                           // URL with parameters
               new { controller = "Admin", action = "Fields", GroupName = (string)null }  // Parameter defaults
            );

            routes.MapRoute(
               "ListingTypeProperties",                             // Route name
               "Admin/ListingTypeProperties/{ListingTypeName}",                           // URL with parameters
               new { controller = "Admin", action = "ListingTypeProperties", ListingTypeName = (string)null }  // Parameter defaults
            );

            // routes.MapRoute(
            //    "SignIn",                                              // Route name
            //    "Account/SignIn",                           // URL with parameters
            //    new { controller = "Account", action = "LogOn" }  // Parameter defaults
            //);

            routes.MapRoute(
                "Contact",                                              // Route name
                "Contact",                           // URL with parameters
                new { controller = "Home", action = "Contact" }  // Parameter defaults
            );

            routes.MapRoute(
                "ContactUsSubmitted",                                              // Route name
                "ContactUsSubmitted",                           // URL with parameters
                new { controller = "Home", action = "ContactUsSubmitted" }  // Parameter defaults
            );

            routes.MapRoute(
                "Help",                                              // Route name
                "Help",                           // URL with parameters
                new { controller = "Home", action = "Help" }  // Parameter defaults
            );

            routes.MapRoute(
                "Sitemap",                                              // Route name
                "SiteMap",                           // URL with parameters
                new { controller = "Home", action = "Sitemap" }  // Parameter defaults
            );

            routes.MapRoute(
                "About",                                              // Route name
                "About",                           // URL with parameters
                new { controller = "Home", action = "About" }  // Parameter defaults
            );

            routes.MapRoute(
                 "Version",                                              // Route name
                 "Version",                           // URL with parameters
                 new { controller = "Home", action = "Version" }  // Parameter defaults
             );

            routes.MapRoute(
                "ListingsActive",                                              // Route name
                "Account/Listings/Active",                           // URL with parameters
                new { controller = "Account", action = "ListingsActive" }  // Parameter defaults
            );

            routes.MapRoute(
                "ListingsPending",                                              // Route name
                "Account/Listings/Pending",                           // URL with parameters
                new { controller = "Account", action = "ListingsPending" }  // Parameter defaults
            );

            routes.MapRoute(
                "ListingsSuccessful",                                              // Route name
                "Account/Listings/Successful",                           // URL with parameters
                new { controller = "Account", action = "ListingsSuccessful" }  // Parameter defaults
            );

            routes.MapRoute(
                "ListingsUnsuccessful",                                              // Route name
                "Account/Listings/Unsuccessful",                           // URL with parameters
                new { controller = "Account", action = "ListingsUnsuccessful" }  // Parameter defaults
            );

            routes.MapRoute(
                "ListingsEnded",                                              // Route name
                "Account/Listings/Ended",                           // URL with parameters
                new { controller = "Account", action = "ListingsEnded" }  // Parameter defaults
            );

            routes.MapRoute(
                "BiddingWatching",                                              // Route name
                "Account/Bidding/Watching",                           // URL with parameters
                new { controller = "Account", action = "BiddingWatching" }  // Parameter defaults
            );

            routes.MapRoute(
                "BiddingActive",                                              // Route name
                "Account/Bidding/Active",                           // URL with parameters
                new { controller = "Account", action = "BiddingActive" }  // Parameter defaults
            );

            routes.MapRoute(
                "BiddingWon",                                              // Route name
                "Account/Bidding/Won",                           // URL with parameters
                new { controller = "Account", action = "BiddingWon" }  // Parameter defaults
            );

            routes.MapRoute(
                "BiddingNotWon",                                              // Route name
                "Account/Bidding/Lost",                           // URL with parameters
                new { controller = "Account", action = "BiddingNotWon" }  // Parameter defaults
            );

            routes.MapRoute(
                "InvoicePurchases",                                              // Route name
                "Account/Invoice/Purchases",                           // URL with parameters
                new { controller = "Account", action = "InvoicePurchases" }  // Parameter defaults
            );

            routes.MapRoute(
                "InvoiceSales",                                              // Route name
                "Account/Invoice/Sales",                           // URL with parameters
                new { controller = "Account", action = "InvoiceSales" }  // Parameter defaults
            );

            routes.MapRoute(
                "InvoiceEventSales",                                              // Route name
                "Account/Invoice/EventSales",                           // URL with parameters
                new { controller = "Account", action = "InvoiceEventSales" }  // Parameter defaults
            );

            routes.MapRoute(
                "InvoiceDetail",                                              // Route name
                "Account/Invoice/{id}",                           // URL with parameters
                new { controller = "Account", action = "InvoiceDetail", id = "" }  // Parameter defaults
            );

            routes.MapRoute(
                "Details",                                              // Route name
                "Listing/Details/{id}/{extra}",                           // URL with parameters
                new { controller = "Listing", action = "Details", id = "", extra = "" }  // Parameter defaults
            );

            routes.MapRoute(
                "LotDetails",                                              // Route name
                "Event/LotDetails/{id}/{extra}",                           // URL with parameters
                new { controller = "Event", action = "LotDetails", id = "", extra = "" }  // Parameter defaults
            );

            routes.MapRoute(
                "EventDetails",                                              // Route name
                "Event/Details/{id}/{extra2}/{breadcrumbs}/{extra}",                           // URL with parameters
                new { controller = "Event", action = "Details", id = "", extra2 = "", breadcrumbs = "", extra = "" }  // Parameter defaults
            );

            routes.MapRoute(
               "Search",                                              // Route name
               "Search",                           // URL with parameters
               new { controller = "Listing", action = "Search" }  // Parameter defaults
           );

            routes.MapRoute(
               "UserPage",                                              // Route name
               "Page/{name}",                           // URL with parameters
               new { controller = "Page", action = "Index", name = "" }  // Parameter defaults
           );

            routes.MapRoute(
                "Browse",                                              // Route name
                "Browse/{breadcrumbs}/{extra}",                           // URL with parameters
                new { controller = "Listing", action = "Browse", breadcrumbs = "", extra = "" }  // Parameter defaults
            );

            routes.MapRoute(
                "Events",                                              // Route name
                "Events",                           // URL with parameters
                new { controller = "Event", action = "Index" }  // Parameter defaults
            );

            routes.MapRoute(
                name: "SitemapXml",
                url: "sitemap.xml",
                defaults: new { controller = "Home", action = "SitemapXml" });

            routes.MapMvcAttributeRoutes();

            if (homepageOption == "browse")
            {
                routes.MapRoute(
                    name: "AlternateHomepage1",
                    url: "",
                    defaults: new { controller = "Listing", action = "Browse", id = UrlParameter.Optional }
                );
                routes.MapRoute(
                    name: "AlternateHomepage2",
                    url: "Home/",
                    defaults: new { controller = "Listing", action = "Browse", id = UrlParameter.Optional }
                );
                routes.MapRoute(
                    name: "AlternateHomepage3",
                    url: "Home/Index/{id}",
                    defaults: new { controller = "Listing", action = "Browse", id = UrlParameter.Optional }
                );
            }

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
