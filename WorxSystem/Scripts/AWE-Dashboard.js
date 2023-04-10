$(document).ready(function () {
    $(this).everyTime(60000, function () {
        GetCurrentTime("#Time");
    });
});
$(document).ready(function () {
    $('[data-toggle=offcanvas]').click(function () {
        $('.row-offcanvas').toggleClass('active');
        $('.action-panel').toggleClass('hide');
        $('.navbar-toggle').toggleClass('open');
    });
});
$(window).on("load", function () {
    var selectedTab = $("li.active");
    if (selectedTab.length > 0) {
        $('.sidebar-offcanvas').animate({
            scrollTop: selectedTab.offset().top - 75
        }, 0);
    } else {
        $(".nav-sidebar li:first").addClass("active");
    }
});
$(function () {
    $("input[type=radio]").checkbox();
    $("input[type=checkbox]:not(.no-toggle):not(.plain-cb)").checkbox({
        toggle: true
    });
    $("input[type=checkbox]:not(.plain-cb)").checkbox();
});

$(".welcome-button").on("click", function () {
    $(".admin-welcome").toggleClass("awe-hidden");
    $(".show-quickstart-list").toggleClass("awe-hidden");
    if ($(".show-quickstart-list").hasClass("awe-hidden")) {
        $.cookie("hide-quickstart-checklist", "no", { path: "/", expires: 7 });
    } else {
        $.cookie("hide-quickstart-checklist", "yes", { path: "/", expires: 7 });
    }
});

$(function () {
    var loc = window.location.href; // returns the full URL

    if ((/UserSummary/.test(loc)) || (/EditUser/.test(loc)) || (/UserAddresses/.test(loc)) || (/UserCreditCards/.test(loc)) || (/UserFeedback/.test(loc)) ||
        (/EditAddress/.test(loc)) || (/AddAddress/.test(loc)) || (/AddCreditCard/.test(loc)) ) {
        $('a[href="/Admin/UserManagement"]').parent().addClass('active');
    }
    if (/RegionDetail/.test(loc)) {
        $('a[href="/Admin/RegionEditor"]').parent().addClass('active');
    }
    if (/CategoryDetail/.test(loc)) {
        $('a[href="/Admin/CategoryEditor"]').parent().addClass('active');
    }
    if (/ContentEditor/.test(loc)) {
        $('a[href="/Admin/ContentManagement"]').parent().addClass('active');
    }
    if (/EmailTemplateEditor/.test(loc)) {
        $('a[href="/Admin/EmailTemplates"]').parent().addClass('active');
    }
    if (/ExportUserCSV/.test(loc)) {
        $('a[href="/Admin/UserManagement"]').parent().addClass('active');
    }

    if (/GroupName=User/.test(loc)) {
        $('a[href="/Admin/Fields/User"]').parent().addClass('active');
    }
    if (/GroupName=Item/.test(loc)) {
        $('a[href="/Admin/Fields/Item"]').parent().addClass('active');
    }
    if (/returnUrl=%2FAdmin%2FFields%2FUser/.test(loc)) {
        $('a[href="/Admin/Fields/User"]').parent().addClass('active');
    }
    if (/returnUrl=%2FAdmin%2FFields%2FItem/.test(loc)) {
        $('a[href="/Admin/Fields/Item"]').parent().addClass('active');
    }
    if (/returnUrl=%2FAdmin%2FFields%2FEvent/.test(loc)) {
        $('a[href="/Admin/Fields/Event"]').parent().addClass('active');
    }
    if (/returnUrl=%2FAdmin%2FNewSiteFeesReport/.test(loc)) {
        $('a[href="/Admin/NewSiteFeesReport"]').parent().addClass('active');
    }
    if ((/InvoiceDetail/.test(loc)) && !(/returnUrl=%2FAdmin%2FNewSiteFeesReport/.test(loc))) {
        $('a[href="/Admin/SiteFeesReport"]').parent().addClass('active');
    }
    if (/SetEmailTemplateEnabled/.test(loc)) {
        $('a[href="/Admin/EmailTemplates"]').parent().addClass('active');
    }
});