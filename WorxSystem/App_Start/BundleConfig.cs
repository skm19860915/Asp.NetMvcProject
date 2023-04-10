using System.Web;
using System.Web.Optimization;

namespace RainWorx.FrameWorx.MVC
{
    public class BundleConfig
    {
        // For more information on bundling, visit http://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery-{version}.js"));

            //jquery UI css should not be bundled, because this breaks relative image links within this css definition
            //bundles.Add(new StyleBundle("~/bundles/jqueryuicss").Include(
            //            "~/Content/themes/base/jquery-ui.css"));

            bundles.Add(new ScriptBundle("~/bundles/generalAJAX").Include(
                        "~/Scripts/jquery.timers.js",
                        "~/Scripts/jquery.cookie.js",
                        "~/Scripts/jshashtable-{version}.js",
                        "~/Scripts/jquery.numberformatter-{version}.js",
                        "~/Scripts/jquery-ui-{version}.js",
                        "~/Scripts/json2.js",
                        "~/Scripts/FrameWorx.js"));

            //<!--[if IE 8]>
            //    <link type="text/css" href="Content/css/Template/IE8001.css" rel="stylesheet" />
            //<![endif]-->


            bundles.Add(new StyleBundle("~/bundles/lightbox/css").Include(
                        "~/Content/css/lightbox.css"));

            bundles.Add(new ScriptBundle("~/bundles/lightbox_js").Include(
                        "~/Scripts/lightbox-2.6.min.js"));

            bundles.Add(new ScriptBundle("~/bundles/imageupload").Include(
                        "~/Scripts/swfupload.js",
                        "~/Scripts/jquery-asyncUpload-{version}.js"));

            //note: bundle for ckeditor disabled because of dynamic relative reference issues
            //bundles.Add(new ScriptBundle("~/bundles/ckeditor").Include(
            //            "~/Scripts/ckeditor/ckeditor.js"));

            bundles.Add(new StyleBundle("~/bundles/timepicker_css").Include(
                        "~/Content/css/jquery.ptTimeSelect.css"));

            bundles.Add(new ScriptBundle("~/bundles/datetimepicker_js").Include(
                        "~/Scripts/jquery.ptTimeSelect.js",
                        "~/Scripts/jquery-ui-i18n.rainworx.js",
                        "~/Scripts/jquery-ui-i18n.min.js"));

            bundles.Add(new StyleBundle("~/bundles/css/uistars").Include(
                        "~/Content/css/jquery.ui.stars.css"));

            bundles.Add(new ScriptBundle("~/bundles/uistars_js").Include(
                        "~/Scripts/jquery.ui.stars.js"));

            // Use the development version of Modernizr to develop with and learn from. Then, when you're
            // ready for production, use the build tool at http://modernizr.com to pick only the tests you need.
            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));
           

            bundles.Add(new ScriptBundle("~/bundles/bootstrap_js").Include(
                        "~/Scripts/bootstrap.min.js",
                        "~/Scripts/bootstrap-dialog.js"));

            bundles.Add(new StyleBundle("~/bundles/bootstrap_css").Include(
                      "~/Content/bootstrap.css",
                      "~/Content/AWE-Print.css"/*,  removed because the theme can now selected via a site property
                      "~/Content/AWE_Default_01.css"*/));

            bundles.Add(new ScriptBundle("~/bundles/jquery_everslider").Include(
                        "~/Scripts/jquery.everslider.js",
                        "~/Scripts/jquery.mousewheel.js",
                        "~/Scripts/jquery.respond.min.js"));

            bundles.Add(new StyleBundle("~/bundles/everslider/css").Include(
                        "~/Content/css/everslider.css"));

            bundles.Add(new ScriptBundle("~/bundles/jquery_scrollUp").Include(
                        "~/Scripts/jquery.easing.{version}.js",
                        "~/Scripts/jquery.scrollUp.js"));

            bundles.Add(new StyleBundle("~/bundles/scrollup_css").Include(
                        "~/Content/css/scrollup.css"));

            bundles.Add(new StyleBundle("~/bundles/bootstrap_dialog_css").Include(
                        "~/Content/bootstrap-dialog.css"));

            bundles.Add(new ScriptBundle("~/bundles/oldbrowser_js").Include(
                        "~/Scripts/old-browser-alert.js"));

            bundles.Add(new ScriptBundle("~/bundles/signalr").Include(
                        "~/Scripts/jquery.signalR-{version}.js"));
            bundles.Add(new ScriptBundle("~/bundles/AWE-signalr").Include(
                        "~/Scripts/AWE-SignalR.js"));

            bundles.Add(new ScriptBundle("~/bundles/jquery-globalize").Include(
                        "~/Scripts/globalize/cldr.js",
                        "~/Scripts/globalize/cldr/event.js",
                        "~/Scripts/globalize/cldr/supplemental.js",
                        "~/Scripts/globalize/globalize.js",
                        "~/Scripts/globalize/globalize/date.js",
                        "~/Scripts/globalize/globalize/number.js"));
            //Removed from v3.1
            //bundles.Add(new StyleBundle("~/bundles/social_buttons_css").Include(
            //            "~/Content/bootstrap-social.css",
             //           "~/Content/font-awesome.css"));

            //uncomment the line below to confirm that bundling optimizations work correctly while developing locally
            //BundleTable.EnableOptimizations = true;

        }
    }
}
