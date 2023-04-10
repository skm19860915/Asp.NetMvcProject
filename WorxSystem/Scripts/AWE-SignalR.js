var basic_signalR_Logging = false;
var rwx_signalR_Logging = true;
var record_SignalR_Errors = true;
function jslog() {
    if (rwx_signalR_Logging) {
        if (arguments.length > 1) {
            console.log(arguments[0], arguments[1]);
        } else if (arguments.length == 1) {
            console.log(arguments[0]);
        }
    }
}

//credit to: https://stackoverflow.com/questions/610406/javascript-equivalent-to-printf-string-format
String.prototype.formatUnicorn = String.prototype.formatUnicorn ||
    function () {
        "use strict";
        var str = this.toString();
        if (arguments.length) {
            var t = typeof arguments[0];
            var key;
            var args = ("string" === t || "number" === t) ?
                Array.prototype.slice.call(arguments)
                : arguments[0];

            for (key in args) {
                str = str.replace(new RegExp("\\{" + key + "\\}", "gi"), args[key]);
            }
        }

        return str;
    };

var interestingListings = [];
var interestingEvents = [];

var abortSignalRAlert = false;

var rwx_ListingChannelConnected = false;

var rwx_IsSyncingTime = false;

//var signalRstartedFirstTime = false;

/*
    Note: If this page becomes hidden, SignalR messages will be queued instead of executing the associated animations 
          immediately. If the maximum specified limit of queued messages is reached, the browser will reload the page instead of 
          processing queued messages, if/when it becomes visible again.
 */
var rwx_MaxInactiveMessageQueueLength = 1000; //max messages to queue while the tab is hidden
var rwx_AbortHiddenTabMessageQueuing = false; // this flag will be set if the max queue length is reached
var rwx_IgnoredMessageCount = 0;

var rwx_CountdownsActive = true;
var rwx_ProcessSignalrMessages = true;
var rwx_QueuedSignalrMessages = [];

var rwx_MaxCountDownUpdateRetries = 1600; //this failsafe value will prevent further attempts to update countdowns after approximately 4 hours when the site is unreachable
var rwx_CurrentCountDownUpdateRetries = 0;
var rwx_CountDownUpdateRetriesDisabled = false;
var rwx_DelayCountDownUpdateRetries = false;
var rwx_CountDownUpdateRetryDelayMS = 5000; //this means the retry counter will be incremented every 5 seconds that retries keep failing

var rwx_MaxReConnectRetries = 1600; //this failsafe value will prevent further attempts to reconnect SignalR after approximately 4 hours when the site is unreachable
var rwx_CurrentReConnectRetries = 0;
var rwx_ReConnectRetriesDisabled = false;

var rwx_countdownIntervalId = 0;
var rwx_footerClockIntervalId = 0;
const rwx_footerClockUpdateMS = 1000; // refresh the footer clock every X seconds, by default (once per 1 second / 1000 MS)
const rwx_footerClockTimeStyle = "short"; //"short" for no seconds, "medium" to include seconds

var visibilityDetectionSupported = true;
var pageInitiallyHidden = false;
var docHiddenPropName, visibilityChangeEventName;
var lastVisibilityChangeTimestamp = new Date();
if (typeof document.hidden !== "undefined") {
    docHiddenPropName = "hidden";
    visibilityChangeEventName = "visibilitychange";
} else if (typeof document.mozHidden !== "undefined") {
    docHiddenPropName = "mozHidden";
    visibilityChangeEventName = "mozvisibilitychange";
} else if (typeof document.msHidden !== "undefined") {
    docHiddenPropName = "msHidden";
    visibilityChangeEventName = "msvisibilitychange";
} else if (typeof document.webkitHidden !== "undefined") {
    docHiddenPropName = "webkitHidden";
    visibilityChangeEventName = "webkitvisibilitychange";
} else {
    visibilityDetectionSupported = false;
    jslog("Page Visibility API not supported.");
}

const startupTimestamp = (new Date()).getTime();
function handlePageVisibilityChange() {
    var currentlyHidden = document[docHiddenPropName];
    var currentTimeStamp = new Date();
    var timeSinceLastChangeMS = currentTimeStamp - lastVisibilityChangeTimestamp;
    lastVisibilityChangeTimestamp = currentTimeStamp;
    jslog("Visibility changed to {0}. (Previously {1} for {2} MS)".formatUnicorn(currentlyHidden ? "hidden" : "visible", !currentlyHidden ? "hidden" : "visible", timeSinceLastChangeMS));

    var diffSinceStartup = (new Date()).getTime() - startupTimestamp;

    //do not respond to any visiblity state changes before the first 1000 MS of the page's existence to prevent interferance with initial setup processes
    if (diffSinceStartup < 1000) {
        jslog("Ignoring visibility changes during page setup...");
        return;
    }

    if (currentlyHidden) {
        jslog("Suspending countdowns and animations...");
        rwx_CountdownsActive = false;
        rwx_ProcessSignalrMessages = false;
    } else {
        if (rwx_ForceMobileBrowserRefreshOnVisibilityChange && $(window).width() < 750 && interestingListings.length > 0) {
            //this appears to be a mobie browser that was in the background
            window.location.reload(true);
        } else {
            jslog("Resuming countdowns and animations...");
            RestartCountdowns(ResumeSignalRMessageProcessing);
            //ResumeSignalRMessageProcessing();
        }
    }

    //if (!currentlyHidden && typeof StalePageHandler !== "undefined") {
    //    jslog("Executing stale page handler...");
    //    StalePageHandler(timeSinceLastChangeMS);
    //}
}

function RestartCountdowns(callback) {
    SyncServerTime(function (syncedServerTime) {
        jslog("Browser Time synced (resume countdowns): " + syncedServerTime);
        rwx_CountdownsActive = true;

        if (rwx_countdownIntervalId == 0) {
            jslog("re-starting countdown loop...(2)");
            rwx_countdownIntervalId = setInterval(function () {
                if (rwx_browserDateTime != null) { //this null check prevents javascript error "Uncaught TypeError: Cannot read property 'setTime' of null"
                    var msSinceLastSync = (new Date()).getTime() - rwx_lastSyncTimeStamp;
                    rwx_browserDateTime.setTime(rwx_serverDateTime.getTime() + msSinceLastSync); //milliseconds...
                    UpdateAllCountdowns();
                }
                if (!rwx_CountdownsActive) {
                    jslog("stopping countdown loop...(2)");
                    clearInterval(rwx_countdownIntervalId);
                    rwx_countdownIntervalId = 0;
                }
            }, 1000);
        } else {
            jslog("countdown loop was still enabled, skipping re-enable...(2)");
        }
        if (rwx_footerClockIntervalId == 0) {
            jslog("re-starting footer clock loop...(2)");
            rwx_footerClockIntervalId = setInterval(function () {
                UpdateFooterClock();
                if (!rwx_CountdownsActive) {
                    jslog("stopping footer clock loop...(2)");
                    clearInterval(rwx_footerClockIntervalId);
                    rwx_footerClockIntervalId = 0;
                }
            }, rwx_footerClockUpdateMS);
        } else {
            jslog("footer clock loop was still enabled, skipping re-enable...(2)");
        }
        callback();
    });
}

function SyncServerTime(onSuccess) {
    if (rwx_IsSyncingTime) {
        jslog("Time Sync Aborted (a previous sync request is currently being executed)");
        return;
    }

    rwx_IsSyncingTime = true;
    var clientTimestamp = (new Date()).getTime();
    Proxy.invokeAsync("ServerTimeSync", { clientTime: clientTimestamp },
        function (data) {

            var nowTimeStamp = (new Date()).valueOf();
            var serverClientRequestDiffTime = data.diff;
            var serverTimestamp = data.serverTimestamp;
            var serverClientResponseDiffTime = nowTimeStamp - serverTimestamp;
            var responseTime = (serverClientRequestDiffTime - nowTimeStamp + clientTimestamp - serverClientResponseDiffTime) / 2

            var syncedServerTime = new Date((new Date()).valueOf() + (serverClientResponseDiffTime - responseTime));
            //console.log("syncedServerTime: ", syncedServerTime);
            var tzOffSetMS = //Date.parse(data.siteNowTimeString) - syncedServerTime.getTime();
                ParseDate(data.siteNowTimeString) - syncedServerTime.getTime();

            rwx_lastSyncTimeStamp = (new Date()).getTime();
            rwx_serverDateTime = new Date(syncedServerTime.getTime() + tzOffSetMS);
            rwx_browserDateTime = new Date(rwx_serverDateTime.getTime());
            rwx_IsSyncingTime = false;

            onSuccess(rwx_serverDateTime);
        },
        function (error) {
            jslog("Error Synchronizing With Server Time: " + error);
            rwx_IsSyncingTime = false;
        }
    );
}

function ParseDate(dateString) {
    var dateTimeParts = [];
    dateString.match(/(\d+)/g).forEach((element) => {
        dateTimeParts[dateTimeParts.length] = parseInt(element);
    });
    if (dateTimeParts.length == 7) {
        //parses local time consistently across all browsers, even Safari!
        var retVal = new Date(
            dateTimeParts[0], (dateTimeParts[1] - 1), dateTimeParts[2],
            dateTimeParts[3], dateTimeParts[4], dateTimeParts[5],
            dateTimeParts[6]);
        //console.log("Parsed Date(1): ", dateTimeParts, retVal);
        return retVal;
    } else {
        var retVal = Date.parse(dateString);
        console.log("Parsed Date(2): ", dateTimeParts, retVal);
        return retVal;
    }
}

function ResumeSignalRMessageProcessing() {
    rwx_ProcessSignalrMessages = true;
    if (rwx_QueuedSignalrMessages.length > 0) {
        rwx_QueuedSignalrMessages.forEach(function (signalRmsg) {
            jslog("Firing queued message '" + signalRmsg.EventName + "'");//, signalRmsg.MessageData);
            $.event.trigger(signalRmsg.EventName, signalRmsg.MessageData);
        });
        rwx_QueuedSignalrMessages = [];
    }
    if (rwx_AbortHiddenTabMessageQueuing) {
        write_log({
            title: "Page hidden too long",
            message: "page reloaded"
            , "URL": document.location.href
            , "Max Queue Length": rwx_MaxInactiveMessageQueueLength
            , "UserAgent": navigator.userAgent
            , "Messages Ignored": rwx_IgnoredMessageCount
        });
        window.location.reload(true);
    }
}

if (visibilityDetectionSupported) {
    document.addEventListener(visibilityChangeEventName, handlePageVisibilityChange, false);
    pageInitiallyHidden = document[docHiddenPropName];
    jslog("Page/Tab Initially Hidden: " + pageInitiallyHidden);
    if (pageInitiallyHidden) {
        jslog("Suspending countdowns and animations...");
        rwx_CountdownsActive = false;
        rwx_ProcessSignalrMessages = false;
    }
}

//Register interest in listing ID's at most one time per page, for Listing specific SignalR broadcasts
function RegisterInterestingListing(listingID) {    
    if (interestingListings.indexOf(listingID) < 0) {
        if (!rwx_SingleSigRListingCh) {
            $.connection.listingHub.server.registerListingInterest(listingID);
        }
        interestingListings[interestingListings.length] = listingID;
        jslog("RegisterInterestingListing: " + listingID);
    } else {
        //alert("Registering Listing ID: " + listingID + " skipped");
        jslog("RegisterInterestingListing: (skipped) " + listingID);
    }
}

//Register interest in event ID's at most one time per page, for Event specific SignalR broadcasts
function RegisterInterestingEvent(eventID) {
    if (interestingEvents.indexOf(eventID) < 0) {
        if (!rwx_SingleSigRListingCh) {
            $.connection.listingHub.server.registerEventInterest(eventID);
        }
        interestingEvents[interestingEvents.length] = eventID;
        jslog("RegisterInterestingEvent: " + eventID);
    } else {
        //alert("Registering Event ID: " + listingID + " skipped");
        jslog("RegisterInterestingEvent: (skipped) " + eventID);
    }
}

//Register current user name for directed SignalR broadcasts
//function RegisterUserName(userName) {    
//    $.connection.listingHub.server.registerUserName(userName);
//    jslog("RegisterUserName: " + userName);
//}

function RegisterAllInterestingObjects() {
    var objectsToRegister = [];
    interestingListings = [];
    $("[data-listingid]").each(function () {
        var listingID = $(this).data("listingid");
        if (interestingListings.indexOf(listingID) < 0) {
            objectsToRegister[objectsToRegister.length] = listingID;
            interestingListings[interestingListings.length] = listingID;
            jslog("queued Interesting Listing: " + listingID);
        } else {
            jslog("queued Interesting Listing: (skipped) " + listingID);
        }
    });
    interestingEvents = [];
    $("[data-eventid]").each(function () {
        var eventID = $(this).data("eventid");
        if (interestingEvents.indexOf(eventID) < 0) {
            interestingEvents[interestingEvents.length] = eventID;
            objectsToRegister[objectsToRegister.length] = eventID;
            jslog("queued Interesting Event: " + eventID);
        } else {
            jslog("queued Interesting Event: (skipped) " + eventID);
        }
    });
    if (objectsToRegister.length > 0) {
        if (rwx_SingleSigRListingCh) {
            if (!rwx_ListingChannelConnected) {
                $.connection.listingHub.server.registerMultipleInterest([], aweUserName);
                rwx_ListingChannelConnected = true;
                jslog("connected to all listings channel");
            }
        }
        else {
            $.connection.listingHub.server.registerMultipleInterest(objectsToRegister, aweUserName);
            jslog("registered queued Interesting Objects: " + objectsToRegister.length);
        }
    }
}

function RegisterAdditionalInterestingListings(selectorPrefix, callback) {
    var objectsToRegister = [];
    $(selectorPrefix + " [data-listingid]").each(function () {
        var listingID = $(this).data("listingid");
        if (interestingListings.indexOf(listingID) < 0) {
            objectsToRegister[objectsToRegister.length] = listingID;
            interestingListings[interestingListings.length] = listingID;
            jslog("queued Interesting Listing: " + listingID);
        } else {
            jslog("queued Interesting Listing: (skipped) " + listingID);
        }
    });
    if (objectsToRegister.length > 0) {
        if (rwx_SingleSigRListingCh) {
            if (!rwx_ListingChannelConnected) {
                $.connection.listingHub.server.registerMultipleInterest([], aweUserName);
                rwx_ListingChannelConnected = true;
                jslog("connected to all listings channel");
            }
        }
        else {
            $.connection.listingHub.server.registerMultipleInterest(objectsToRegister, aweUserName);
            jslog("registered queued Interesting Objects: " + objectsToRegister.length);
        }
    }
    callback();
}

//global variable to store client side date/time (according to the server)
var rwx_browserDateTime = null;
var rwx_serverDateTime = null;
var rwx_lastSyncTimeStamp = null;

//global variable to store localized remaining time strings
var timeDifferenceDictionary = {};
//global variable to store localized statuses
var statusDictionary = {};
//global variable to store localized lot status HTML snippets
var lotStatusHtmlDictionary = {};
//global variable to store localized event status HTML snippets
var eventStatusHtmlDictionary = {};
//global variable to store localized event homepage bidding status HTML snippets
var eventHomepageStatusHtmlDictionary = {};
//global variable to store localized event homepage time label HTML snippets
var eventHomepageTimeLabelHtmlDictionary = {};
//global variable to store event homepage time/countdown information for 'end early' function
var eventHomepageTimeHtmlDictionary = {};
//global variable to store localized context messages
var contextMessageDictionary = {};
//global variable to store localized abbreviated context messages
var shortContextMessages = {};

//global variable to store localized signalr indicator titles
var signalrIndicatorTitlesDictionary = {};

if (!String.prototype.format) {
    String.prototype.format = function () {
        //var args = arguments;
        var args = arguments[0];
        return this.replace(/{(\d+)}/g, function (match, number) {
            return typeof args[number] != 'undefined'
              ? args[number]
              : match
            ;
        });
    };
}

jQuery.fn.quickEach = (function () {
    var jq = jQuery([1]);
    return function (c) {
        var i = -1, el, len = this.length;
        try {
            while (
                 ++i < len &&
                 (el = jq[0] = this[i]) &&
                 c.call(jq, i, el) !== false
            );
        } catch (e) {
            delete jq[0];
            throw e;
        }
        delete jq[0];
        return this;
    };
}());

$.fn.pulse = function () {
    //$(this).clearQueue().queue(function (next) {
    //    $(this).addClass("signalr-updating");
    //    next();
    //}).delay(300).queue(function (next) {
    //    $(this).addClass("signalr-updatable").removeClass("signalr-updating"); next();
    //}).delay(300).queue(function (next) {
    //    $(this).removeClass("signalr-updatable"); next();
    //});

    //$(this).fadeOut(200, function() {        
    //    $(this).fadeIn(200);
    //});

    $(this).addClass("signalr-pulse", 10, function () { $(this).removeClass("signalr-pulse", 3000); });

    //var theColorIs = $(this).css("color");
    //$(this).animate({
    //    color: '#FF8C00'
    //}, 2000, "swing", function () {        
    //    $(this).animate({
    //        color: theColorIs
    //    }, 2000);
    //});
};
$.fn.pulse_block = function () {
    $(this).addClass("signalr-pulse-block", 10, function () { $(this).removeClass("signalr-pulse-block", 3000); });
};

$(document).ready(function () {

    if (rwx_SignalRDisabled) return;

    //animate changes
    //var notLocked = true;
    //$.fn.animateHighlight = function (highlightColor, duration) {
    //    var highlightBg = highlightColor || "#FFFF9C";
    //    var animateMs = duration || 1500;
    //    var originalBg = this.css("backgroundColor");
    //    if (notLocked) {
    //        notLocked = false;
    //        this.stop().css("background-color", highlightBg)
    //            .animate({ backgroundColor: originalBg }, animateMs);
    //        setTimeout(function () { notLocked = true; }, animateMs);
    //    }
    //};

    //$.fn.pulse = function () {                
    //    $(this).clearQueue().queue(function (next) {
    //        $(this).addClass("signalr-updating");
    //        next();
    //    }).delay(300).queue(function (next) {
    //        $(this).addClass("signalr-updatable").removeClass("signalr-updating"); next();
    //    }).delay(300).queue(function (next) {
    //        $(this).removeClass("signalr-updatable"); next();
    //    });
    //};            

    if (basic_signalR_Logging) {
        $.connection.hub.logging = true;
    }

    //signalr lifetime events
    $.connection.hub.disconnected(function () {
        //alert("AWE_SignalR.js:Disconnected");
        $.event.trigger({
            type: "SignalR_Disconnected"
        });

        rwx_CurrentReConnectRetries++;
        console.log("rwx_CurrentReConnectRetries: ", rwx_CurrentReConnectRetries);

        if ($.connection.hub.lastError) {
            var errMsg = $.connection.hub.lastError.message;
            var connId = $.connection.hub.id;
            console.log("Disconnected. Reason: " + errMsg);
            var entryType = "Warning";
            if (true /*errMsg.includes("timeout")*/) entryType = "Information";
            write_log({
                title: "Disconnected With Error",
                message: errMsg,
                type: entryType
                , "URL": document.location.href
                , "UserAgent": navigator.userAgent
                , "ConnectionId": connId
            });
        }

        if (rwx_CurrentReConnectRetries > rwx_MaxReConnectRetries) {
            rwx_ReConnectRetriesDisabled = true;
            console.log('Disabled further Reconnect Retries after reaching the maximum number of attempts:', rwx_MaxReConnectRetries);
        }
        if (!rwx_ReConnectRetriesDisabled) {
            setTimeout(function () {
                //alert("AWE Reconnecting");
                $.connection.hub.start().done(function () {
                    //alert("Started");
                    $.event.trigger({
                        type: "SignalR_Started"
                    });
                });
            }, 5000); // Restart connection after 5 seconds.
        }
    });

    $.connection.hub.connectionSlow(function () {
        //alert("AWE_SignalR.js:ConnectionSlow");
        $.event.trigger({
            type: "SignalR_ConnectionSlow"
        });
    });

    $.connection.hub.reconnecting(function () {
        //alert("AWE_SignalR.js:Reconnecting");
        $.event.trigger({
            type: "SignalR_Reconnecting"
        });
    });

    $.connection.hub.reconnected(function () {
        //alert("AWE_SignalR.js:Reconnected");
        $.event.trigger({
            type: "SignalR_Reconnected"
        });
    });

    // Get the hub
    var hub = $.connection.listingHub;

    //Dispatch events

    //UpdateCurrentTime
    hub.client.updateCurrentTime = function (data) {
        if (rwx_ProcessSignalrMessages) {
            jslog("Current Time Received (signalR): " + data);
            $.event.trigger(
                "SignalR_UpdateCurrentTime"
            );
        } else {
            //there is no need to queue time update messages because the time will be refreshed after the tab becomes visible again, just prior to processing any queued messages
            //QueueSignalrMessage("SignalR_UpdateCurrentTime", data);
        }
    }

    //UpdateListingAction
    hub.client.updateListingAction = function (data) {
        if (rwx_ProcessSignalrMessages) {
            $.event.trigger(
                "SignalR_UpdateListingAction",
                data
            );
        } else {
            QueueSignalrMessage("SignalR_UpdateListingAction", data);
        }
    }

    //UpdateListingDTTM
    hub.client.updateListingDTTM = function (data) {
        if (rwx_ProcessSignalrMessages) {
            $.event.trigger(
                "SignalR_UpdateListingDTTM",
                data
            );
        } else {
            QueueSignalrMessage("SignalR_UpdateListingDTTM", data);
        }
    }

    //UpdateListingStatus
    hub.client.updateListingStatus = function (data) {
        if (rwx_ProcessSignalrMessages) {
            $.event.trigger(
                "SignalR_UpdateListingStatus",
                data
            );
        } else {
            QueueSignalrMessage("SignalR_UpdateListingStatus", data);
        }
    }

    //ListingActionResponse
    hub.client.listingActionResponse = function (data) {
        if (rwx_ProcessSignalrMessages) {
            $.event.trigger(
                "SignalR_ListingActionResponse",
                data
            );
        } else {
            QueueSignalrMessage("SignalR_ListingActionResponse", data);
        }
    }

    //UpdateEventStatus
    hub.client.updateEventStatus = function (data) {
        if (rwx_ProcessSignalrMessages) {
            $.event.trigger(
                "SignalR_UpdateEventStatus",
                data
            );
        } else {
            QueueSignalrMessage("SignalR_UpdateEventStatus", data);
        }
    }

    //UpdateInvoiceStatus
    hub.client.updateInvoiceStatus = function (data) {
        if (rwx_ProcessSignalrMessages) {
            $.event.trigger(
                "SignalR_UpdateInvoiceStatus",
                data
            );
        } else {
            QueueSignalrMessage("SignalR_UpdateInvoiceStatus", data);
        }
    }

    //per https://stackoverflow.com/questions/16248091/fail-to-initiate-connection-in-signal-r-in-asp-net, define client functions PRIOR TO starting hub
    // Start the connection
    $.connection.hub.start().done(function () {
        //alert("AWE_SignalR.js:Started");
        $.event.trigger({
            type: "SignalR_Started"
        });
    });

});

function QueueSignalrMessage(eventname, data) {
    if (!rwx_AbortHiddenTabMessageQueuing && rwx_QueuedSignalrMessages.length > rwx_MaxInactiveMessageQueueLength) {
        jslog("Message queue limit reached, aborting further message queuing.");
        rwx_AbortHiddenTabMessageQueuing = true;
        rwx_IgnoredMessageCount = rwx_QueuedSignalrMessages.length;
        rwx_QueuedSignalrMessages = [];
    }
    if (rwx_AbortHiddenTabMessageQueuing) {
        jslog("Ignoring message '" + eventname + "'");
        rwx_IgnoredMessageCount++;
        return;
    }
    jslog("Queuing message '" + eventname + "'");
    rwx_QueuedSignalrMessages[rwx_QueuedSignalrMessages.length] = { EventName: eventname, MessageData: data };
}

function UpdateFooterClock() {
    if (rwx_browserDateTime == null) return;
    var localizedDTTM = Globalize.formatDate(rwx_browserDateTime, { date: "full" }) + ' ' + Globalize.formatDate(rwx_browserDateTime, { time: rwx_footerClockTimeStyle });
    if (timeZoneLabel) localizedDTTM += (' ' + timeZoneLabel);
    $('#Time').text(localizedDTTM);
    //jslog("New Footer Clock Time: ", Globalize.formatDate(rwx_browserDateTime, { time: "medium" }));
}

//This is called from SignalRHandler.cshtml, and can't be in a document.ready, because it must happen after the document.ready code in SignalRHandler.cshtml
function CompleteSignalRHandling() {
    //Prepare handlers for SignalR Status
    $(document).on("SignalR_Started", function () {
        $("#SignalRStatus").html('<div title="' + signalrIndicatorTitlesDictionary["Started"] + '"><span class="glyphicon glyphicon-stats SignalRStatus-connected"></span></div>');
        abortSignalRAlert = true;
        HideSignalRAlert();
    });

    $(document).on("SignalR_ConnectionSlow", function () {
        $("#SignalRStatus").html('<div title="' + signalrIndicatorTitlesDictionary["ConnectionSlow"] + '"><span class="glyphicon glyphicon-stats SignalRStatus-reconnect"></span></div>');
    });

    $(document).on("SignalR_Reconnecting", function () {
        $("#SignalRStatus").html('<div title="' + signalrIndicatorTitlesDictionary["Reconnecting"] + '"><span class="glyphicon glyphicon-stats SignalRStatus-reconnect"></span></div>');
        abortSignalRAlert = false;
        jslog("SignalR Connection Slow, delay til aert: " + rwx_DisconnectAlertDelayMS);
        // Do not display the disconnect alert until after the configured # of MS (10 seconds by default),
        // to prevent normal navigational clicks from triggering it unless the server takes more than 10 seconds (by default) to respond.
        setTimeout(function () {
            jslog("Alert Delay Reached -- Abort? " + abortSignalRAlert);
            if (!abortSignalRAlert) ShowSignalRAlert();
        }, rwx_DisconnectAlertDelayMS);
    });

    $(document).on("SignalR_Reconnected", function () {
        $("#SignalRStatus").html('<div title="' + signalrIndicatorTitlesDictionary["Reconnected"] + '"><span class="glyphicon glyphicon-stats SignalRStatus-connected"></span></div>');
        abortSignalRAlert = true;
        HideSignalRAlert();

        SyncServerTime(function (syncedServerTime) {
            jslog("Browser Time synced (SignalR_Reconnected): " + syncedServerTime);
        });

    });

    $(document).on("SignalR_Disconnected", function () {
        $("#SignalRStatus").html('<div title="' + signalrIndicatorTitlesDictionary["Disconnected"] + '"><span class="glyphicon glyphicon-stats SignalRStatus-stopped"></span></div>');
        abortSignalRAlert = false;

        //moved to this call to the "Reconnecting" event handler because otherwise an extra 30 second delay is added before alert is displayed
        //setTimeout(function () { if (!abortSignalRAlert) ShowSignalRAlert(); }, rwx_DisconnectAlertDelayMS);
    });

    //prepare current time update handler, this should fire every 59 seconds
    $(document).on("SignalR_UpdateCurrentTime", function () {
        //UpdateFooterClock();

        abortSignalRAlert = true;
        HideSignalRAlert();
    });

    //moved the first countdown tick to the callback function of the initial server time sync done in Shared/SignalRHandler.cshtml
    //UpdateAllCountdowns();

    if (!rwx_SignalRDisabled && rwx_CountdownsActive) {
        //update all countdowns every second
        if (rwx_countdownIntervalId == 0) {
            jslog("starting countdown loop...(1)");
            rwx_countdownIntervalId = setInterval(function () {
                if (rwx_browserDateTime != null) { //this null check prevents javascript error "Uncaught TypeError: Cannot read property 'setTime' of null"
                    var msSinceLastSync = (new Date()).getTime() - rwx_lastSyncTimeStamp;
                    rwx_browserDateTime.setTime(rwx_serverDateTime.getTime() + msSinceLastSync); //milliseconds...
                    UpdateAllCountdowns();
                }
                if (!rwx_CountdownsActive) {
                    jslog("stopping countdown loop...(1)");
                    clearInterval(rwx_countdownIntervalId);
                    rwx_countdownIntervalId = 0;
                }
            }, 1000);
        } else {
            jslog("countdown loop was still enabled, skipping re-enable...(1)");
        }
        if (rwx_footerClockIntervalId == 0) {
            jslog("starting footer clock loop...(1)");
            rwx_footerClockIntervalId = setInterval(function () {
                UpdateFooterClock();
                if (!rwx_CountdownsActive) {
                    jslog("stopping footer clock loop...(1)");
                    clearInterval(rwx_footerClockIntervalId);
                    rwx_footerClockIntervalId = 0;
                }
            }, rwx_footerClockUpdateMS);
        } else {
            jslog("footer clock loop was still enabled, skipping re-enable...(1)");
        }
    }

    //[re]register interesting listings
    $(document).on("SignalR_Started", function () {
        //interestingListings = [];
        //$("[data-listingid]").each(function () {
        //    RegisterInterestingListing($(this).data("listingid"));
        //});
        //interestingEvents = [];
        //$("[data-eventid]").each(function () {
        //    RegisterInterestingEvent($(this).data("eventid"));
        //});

        RegisterAllInterestingObjects();

        //disabled this logic because there is now a better mechanism to handle this in place
        ////due to the behavior of some smart phone browsers when locked, it is necessary to force a page refresh after they are unlocked to ensure that stale data is not displayed
        //if (signalRstartedFirstTime && $(window).width() < 750 && interestingListings.length > 0) {
        //    window.location.reload(true);
        //} else {
        //    signalRstartedFirstTime = true;
        //}
    });
    $(document).on("SignalR_Reconnected", function () {
        //interestingListings = [];
        //$("[data-listingid]").each(function () {
        //    RegisterInterestingListing($(this).data("listingid"));
        //});
        //interestingEvents = [];
        //$("[data-eventid]").each(function () {
        //    RegisterInterestingEvent($(this).data("eventid"));
        //});

        RegisterAllInterestingObjects();

        //disabled this logic because there is now a better mechanism to handle this in place
        ////due to the behavior of some smart phone browsers when locked, it is necessary to force a page refresh after they are unlocked to ensure that stale data is not displayed
        //if ($(window).width() < 750 && interestingListings.length > 0) {
        //    window.location.reload(true);
        //}
    });
    //$(window).on("resize", function () {
    //    console.log("CURRENT WINDOW SIZE: ", $(window).width());
    //});

    //Update Action Processing For All Listings
    $(document).on("SignalR_UpdateListingAction", function (event, data) {
        //Update Quantity                      
        $('[data-listingid="' + data.ListingID + '"] .awe-rt-Quantity').each(function () {
            $(this).html(Globalize.formatNumber(data.Quantity, { minimumFractionDigits: 0, maximumFractionDigits: 0 }));
            $(this).pulse();
        });

        //Set Accepted Listing Action Count
        $('[data-listingid="' + data.ListingID + '"] .awe-rt-AcceptedListingActionCount').each(function () {
            if ($(this).data("previousValue") != data.AcceptedActionCount) {
                $(this).data("previousValue", data.AcceptedActionCount);
                $(this).html(data.AcceptedActionCount);
                $(this).pulse();
            }
        });

        //Update Current Price
        $('[data-listingid="' + data.ListingID + '"] .awe-rt-CurrentPrice span.NumberPart').each(function () {
            $(this).html(Globalize.formatNumber(data.Price, { minimumFractionDigits: 2, maximumFractionDigits: 2 }));
            $(this).pulse();
        });

        //if the listing's currency is different from the user's currency...
        var listingCurrency = $('[data-currency]').data("currency");
        var currentUserCurrency = $.cookie("currency");
        if (listingCurrency != currentUserCurrency) {
            //show an informational currency conversion
            var convertedAmount = ConvertPrice(data.Price, listingCurrency, currentUserCurrency);
            $(".Bidding_Local_Price span.NumberPart").html(Globalize.formatNumber(convertedAmount, { minimumFractionDigits: 2, maximumFractionDigits: 2 }));
            $(".Bidding_Local_Price").pulse();
        }
    });

    //Update Listing DTTM Processing for All Listing Types
    $(document).on("SignalR_UpdateListingDTTM", function (event, data) {
        //Remaining Time Updates
        $('[data-listingid="' + data.ListingID + '"] [data-epoch="' + data.Epoch + '"]').each(function () {
            $(this).data("actionTime", data.DTTMString);
            var tempDate = new Date($(this).data("actionTime"));
            $(this).data("actionMilliseconds", tempDate.getTime());
            var diff = tempDate.getTime() - rwx_browserDateTime.getTime();
            $(this).html(TimeDifference(diff));
            //$(this).pulse();
        });

        //Literal DTTM Updates
        var localizedDTTM = Globalize.formatDate(new Date(data.DTTMString), { date: "full" }) + ' ' + Globalize.formatDate(new Date(data.DTTMString), { time: "short" });
        var localizedShortDateOnly = Globalize.formatDate(new Date(data.DTTMString), { date: "short" });
        var localizedShortDTTM = localizedShortDateOnly + ' ' + Globalize.formatDate(new Date(data.DTTMString), { time: "short" });
        if (timeZoneLabel) localizedDTTM += (' ' + timeZoneLabel);
        $('[data-listingid="' + data.ListingID + '"] .awe-rt-' + data.Epoch + 'DTTM').each(function () {
            if ($(this).hasClass("awe-short-date")) {
                $(this).html(localizedShortDTTM);
            } else if ($(this).hasClass("awe-date-only")) {
                $(this).html(localizedShortDateOnly);
            } else {
                $(this).html(localizedDTTM);
            }
            $(this).pulse();
        });
    });

    //Update Listing Status Processing
    $(document).on("SignalR_UpdateListingStatus", function (event, data) {
        jslog("SignalR_UpdateListingStatus: " + data.ListingID + ", " + data.Source + ", " + data.Status);
        //show refresh alert on "updated"
        if (data.Source == "UPDATELISTING_ORIGIN" || data.Source == "ADDFEES_ORIGIN") {
            $('[data-listingid="' + data.ListingID + '"] .awe-rt-RefreshAlert').each(function () {
                $(this).show();
            });
        } else {
            //force countdown updating
            if (data.Status == "Active") {
                $('[data-listingid="' + data.ListingID + '"] [data-epoch="starting"]').each(function () {
                    $(this).removeAttr("data-action-time");
                    $(this).removeAttr("data-action-milliseconds");
                    $(this).removeAttr("data-epoch");
                    var endVal = $(this).attr("data-end-value");
                    if (typeof endVal != typeof undefined && endVal != false) {
                        $(this).html($(this).data("endValue"));
                        $(this).pulse();
                    }
                    var hideSelector = $(this).attr("data-end-hide-selector");
                    if (typeof hideSelector != typeof undefined && hideSelector != false) {
                        $($(this).data("endHideSelector")).hide();
                    }
                    var showSelector = $(this).attr("data-end-show-selector");
                    if (typeof showSelector != typeof undefined && showSelector != false) {
                        $($(this).data("endShowSelector")).show();
                    }
                });
            } else {
                $('[data-listingid="' + data.ListingID + '"] [data-epoch="ending"]').each(function () {
                    $(this).removeAttr("data-action-time");
                    $(this).removeAttr("data-action-milliseconds");
                    $(this).removeAttr("data-epoch");
                    var endVal = $(this).attr("data-end-value");
                    if (typeof endVal != typeof undefined && endVal != false) {
                        $(this).html($(this).data("endValue"));
                        $(this).pulse();
                    }
                    var hideSelector = $(this).attr("data-end-hide-selector");
                    if (typeof hideSelector != typeof undefined && hideSelector != false) {
                        $($(this).data("endHideSelector")).hide();
                    }
                    var showSelector = $(this).attr("data-end-show-selector");
                    if (typeof showSelector != typeof undefined && showSelector != false) {
                        $($(this).data("endShowSelector")).show();
                    }
                });
            }

            //show/hide ActionBox
            $('[data-listingid="' + data.ListingID + '"] .awe-rt-BuyBox').each(function () {
                if (data.Status == "Active") {
                    if (!$(this).is(":visible")) {
                        //$(this).fadeTo(1000, 1, function () {
                        $(this).slideDown(500);
                        //});
                    }
                } else {
                    if ($(this).is(":visible")) {
                        //$(this).fadeTo(1000, 0, function () {
                        $(this).slideUp(500);
                        //});
                    }
                }
            });

            //update Listing "Options" dropdown, first hide everything hideable
            $('[data-listingid="' + data.ListingID + '"] .awe-rt-hideable').each(function () {
                $(this).hide();
            });
            //then show explicitly
            $('[data-listingid="' + data.ListingID + '"] .awe-rt-ShowStatus' + data.Status).each(function () {
                $(this).show();
            });

            //update Status string
            $('[data-listingid="' + data.ListingID + '"] .awe-rt-Status').each(function () {
                $(this).html(statusDictionary[data.Status]);
                $(this).pulse();
            });

            //update colored Status tag
            $('[data-listingid="' + data.ListingID + '"] .awe-rt-ColoredStatus').each(function () {
                $(this).html(lotStatusHtmlDictionary[data.Status]);
                $(this).pulse();
            });

        }

        ////clear the status which is potentially invalidated
        //$(".ContextualStatus").each(function () {
        //    if ($(this).is(":visible")) {
        //        $(this).slideUp(500);
        //    }
        //});

        //update the contextual status
        RefreshListingContextualStatus(data.ListingID);

        //show the ListingClosedMessage
        if (data.Status == "Ended" || data.Status == "Unsuccessful" || data.Status == "Successful") {
            $('[data-listingid="' + data.ListingID + '"] .awe-rt-ListingClosedMessage').each(function () {
                $(this).slideDown();
            });
        }
    });

    $(document).on("SignalR_UpdateEventStatus", function (event, data) {
        jslog("onSignalR_UpdateEventStatus: " + data.EventID + ", " + data.Source + ", " + data.Status);
        if (data.Source == "EVENT_PUBLICATION_FINISHED_ORIGIN") {
            $('[data-eventid="' + data.EventID + '"] .awe-rt-PublishIndicator').hide();
            $('[data-eventid="' + data.EventID + '"] .awe-rt-PublishCompletedMessage').show();
        }
        if (data.Source == "EVENT_DRAFT_VALIDATION_FINISHED_ORIGIN") {
            $('[data-eventid="' + data.EventID + '"] .awe-rt-ValidationIndicator').hide();
            $('[data-eventid="' + data.EventID + '"] .awe-rt-ValidationCompletedMessage').show();
            $("#ValidateAllDraftsLink").prop("disabled", false);
        }

        //update Event "Options" dropdown, first hide everything hideable
        $('[data-eventid="' + data.EventID + '"] .awe-rt-hideable').each(function () {
            $(this).hide();
        });
        //then show explicitly
        $('[data-eventid="' + data.EventID + '"] .awe-rt-ShowStatus' + data.Status).each(function () {
            $(this).show();
        });

        //update Status string
        $('[data-eventid="' + data.EventID + '"] .awe-rt-Status').each(function () {
            $(this).html(statusDictionary[data.Status]).pulse();
        });

        //update colored Status tag
        $('[data-eventid="' + data.EventID + '"] .awe-rt-ColoredStatus').each(function () {
            $(this).html(eventStatusHtmlDictionary[data.Status]).pulse();
        });

        //update homepage time label 
        $('[data-eventid="' + data.EventID + '"] .awe-rt-eventtimelabel').each(function () {
            $(this).html(eventHomepageTimeLabelHtmlDictionary[data.Status]).pulse_block();
        });
        //update homepage bid status label 
        $('[data-eventid="' + data.EventID + '"] .awe-rt-eventbidstatuslabel').each(function () {
            $(this).html(eventHomepageStatusHtmlDictionary[data.Status]).pulse_block();
        });
        //update homepage bid status label 
        $('[data-eventid="' + data.EventID + '"] .awe-rt-eventtimecountdown').each(function () {
            if (data.Status == "Active") {
                $(this).find(".awe-rt-Pending").hide();
                $(this).find(".awe-rt-Active").show();
            }
            if (data.Status == "Closing") {
                $(this).find(".awe-rt-Active").hide();
            }
            if (data.Status == "Closed") {
                $(this).find(".awe-rt-Active").hide();
                $(this).find(".awe-rt-Ended").show();
            }
        });
    });
}

function UpdateAllCountdowns() {
    if (rwx_browserDateTime == null) {
        return;
    }

    $("[data-action-time]").each(function () {
        var thisElement = $(this);
        var attr = $(this).attr("data-action-milliseconds");
        if (typeof attr == typeof undefined || attr == false) {
            var tempDate = new Date($(this).data("actionTime"));
            $(this).attr("data-action-milliseconds", tempDate.getTime());
        }
        var diff = thisElement.data("actionMilliseconds") - rwx_browserDateTime.getTime();
        if (diff <= 0) {
            var listingId = thisElement.closest("[data-listingid]").attr("data-listingid");

            if (listingId) {
                if (thisElement.data("epoch") == "ending") {

                    if (!rwx_CountDownUpdateRetriesDisabled && !rwx_DelayCountDownUpdateRetries) {

                        //this is a listing EndDTTM countdown, confirm actual EndDTTM before further processing...
                        var oldActionTime = new Date($(this).data("actionTime"));
                        var promise = Proxy.invokeAsync("GetTimeRemaining", {
                            listingId: listingId
                        }, function (data) {
                            //got result
                            if (data.error == "") {
                                var newDiff = 0;
                                newDiff = data.secondsRemaining * 1000; //reported milliseconds remaining
                                if (newDiff <= 1000) { // if it's within one second of closing, consider this valid
                                    //end dttm confirmed, render "Ended"
                                    ProcessCountdownFinished(thisElement);

                                    if (newDiff > 500) {
                                        //we were off by at least 500 MS, perform a time sync

                                        SyncServerTime(function (syncedServerTime) {
                                            jslog("Browser Time synced (countdown was off by 500-1000 MS): " + rwx_browserDateTime);
                                        });

                                    }

                                } else {
                                    //the end dttm was wrong for some reason, fix it
                                    thisElement.data("actionMilliseconds", rwx_browserDateTime.getTime() + newDiff);
                                    thisElement.html(TimeDifference(newDiff));
                                    thisElement.pulse();
                                    RefreshListingVitals(listingId, oldActionTime, newDiff);
                                    //ShowSignalRAlert();
                                    jslog("corrected EndDTTM for listing #" + listingId);

                                    if (rwx_AutoRefreshOnCountdownError) {
                                        //because a countdown was off by more than 1000 MS, refresh the page now
                                        setTimeout(function () {
                                            window.location.reload(true);
                                        }, 1000); //wait a second before refreshing the page, so the log entry in RefreshListingVitals can succeed
                                    }

                                }
                            } else {
                                //invalid request parameters?  this may happen if the EndDTTM is more than 100 years from "Now"
                                //window.alert(data.error);
                                //ShowSignalRAlert();
                                jslog("error (1) retrieving EndDTTM for #" + listingId);
                            }
                        }, function (error) {
                            //website down?  request timed out?
                            //window.alert(error);
                            //ShowSignalRAlert();
                            jslog("error (2) retrieving EndDTTM for #" + listingId);
                            });
                        promise.fail(function (jqXHR, textStatus) {
                            //ShowSignalRAlert();
                            jslog("error (3) retrieving EndDTTM for #" + listingId);

                            if (!rwx_DelayCountDownUpdateRetries) {
                                rwx_DelayCountDownUpdateRetries = true;
                                rwx_CurrentCountDownUpdateRetries++;
                                setTimeout(function () {
                                    rwx_DelayCountDownUpdateRetries = false;
                                    console.log("rwx_CurrentCountDownUpdateRetries: ", rwx_CurrentCountDownUpdateRetries);
                                }, rwx_CountDownUpdateRetryDelayMS);
                            }

                            if (rwx_CurrentCountDownUpdateRetries > rwx_MaxCountDownUpdateRetries) {
                                rwx_CountDownUpdateRetriesDisabled = true;
                                console.log('Disabled further Countdown Update Retries after reaching the maximum number of attempts:', rwx_MaxCountDownUpdateRetries);
                            }

                        });

                    } //retry test

                } else {
                    //this is a listing StartDTTM -- don't bother confirming, just process any applicable "Listing Started" things as usual
                    ProcessCountdownFinished(thisElement);
                }
            } else {
                //this is an event countdown
                ProcessCountdownFinished(thisElement);
            }
        } else {
            thisElement.html(TimeDifference(diff));
        }
    });
}

function ProcessCountdownFinished(theCountdownElement) {

    jslog("processing countdown ended... " + theCountdownElement.attr("data-end-hide-selector"), theCountdownElement);

    theCountdownElement.removeAttr("data-action-time");
    theCountdownElement.removeAttr("data-action-milliseconds");
    theCountdownElement.removeAttr("data-epoch");
    var endVal = theCountdownElement.attr("data-end-value");
    if (typeof endVal != typeof undefined && endVal != false) {
        theCountdownElement.html(theCountdownElement.data("endValue"));
        theCountdownElement.pulse();
    }
    var hideSelector = theCountdownElement.attr("data-end-hide-selector");
    if (typeof hideSelector != typeof undefined && hideSelector != false) {
        $(theCountdownElement.data("endHideSelector")).hide();
    }
    var showSelector = theCountdownElement.attr("data-end-show-selector");
    if (typeof showSelector != typeof undefined && showSelector != false) {
        $(theCountdownElement.data("endShowSelector")).show();
    }
}

function ConvertPrice(amount, fromCurrency, toCurrency) {
    var result = new Number(amount);
    if (fromCurrency != toCurrency) {
        result = PriceFromUSD(PriceToUSD(result, fromCurrency), toCurrency);
    }
    return result;
}

function RefreshListingVitals(listingId, oldEndDTTM, newMsRemaining) {
    var promise = Proxy.invokeAsync("GetListingVitals", {
        listingId: listingId
    }, function (data) {
        //got result
        var oldQty = "";
        var newQty = "";
        var oldBidCount = "";
        var newBidCount = "";
        var oldPrice = "";
        var newPrice = "";

        //Update Quantity                      
        $('[data-listingid="' + listingId + '"] .awe-rt-Quantity').each(function () {
            oldQty = $(this).html();
            newQty = Globalize.formatNumber(data.Quantity, { minimumFractionDigits: 0, maximumFractionDigits: 0 });
            $(this).html(newQty);
            $(this).pulse();
        });

        //Set Accepted Listing Action Count
        $('[data-listingid="' + listingId + '"] .awe-rt-AcceptedListingActionCount').each(function () {
            oldBidCount = $(this).html();
            if ($(this).data("previousValue") != data.AcceptedActionCount) {
                $(this).data("previousValue", data.AcceptedActionCount);
                newBidCount = data.AcceptedActionCount;
                $(this).html(newBidCount);
                $(this).pulse();
            }
        });

        //Update Current Price
        $('[data-listingid="' + listingId + '"] .awe-rt-CurrentPrice span.NumberPart').each(function () {
            oldPrice = $(this).html();
            newPrice = Globalize.formatNumber(data.Price, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
            $(this).html(newPrice);
            $(this).pulse();
        });

        //if the listing's currency is different from the user's currency...
        var listingCurrency = data.Currency;
        var currentUserCurrency = $.cookie("currency");
        if (listingCurrency != currentUserCurrency) {
            //show an informational currency conversion
            var convertedAmount = ConvertPrice(data.Price, listingCurrency, currentUserCurrency);
            $(".Bidding_Local_Price span.NumberPart").html(Globalize.formatNumber(convertedAmount, { minimumFractionDigits: 2, maximumFractionDigits: 2 }));
            $(".Bidding_Local_Price").pulse();
        }

        write_log({
            title: "Update(s) missed",
            message: "A countdown reached 0 incorrectly"
            , "Listing ID": listingId
            , "Old EndDTTM": oldEndDTTM
            , "Corrected EndDTTM": data.EndDTTM
            , "Browser Time": rwx_browserDateTime
            , "Updated Seconds Remaining": newMsRemaining / 1000
            , "URL": document.location.href
            , "UserAgent": navigator.userAgent
            //, newStatus: data.Status
            //, oldQty: oldQty
            //, newQty: newQty
            //, oldBidCount: oldBidCount
            //, newBidCount: newBidCount
            //, oldPrice: oldPrice
            //, newPrice: newPrice
        });

    }, function (error) {
        ShowSignalRAlert();
        jslog("error (1) retrieving vital stats for listing #" + listingId);
    });
    promise.fail(function (jqXHR, textStatus) {
        ShowSignalRAlert();
        jslog("error (2) retrieving vital stats for listing #" + listingId);
    });
}

//function RefreshListingContextualStatus(listingId) {
//    var contextStatusAreas = $(".ContextualStatus");
//    if (contextStatusAreas.length > 0 && aweUserName) {
//        var promise = Proxy.invokeAsync("GetListingContextStatus", {
//            listingId: listingId
//        }, function (data) {
//            //got result
//            if (!data.Disregard && !data.Error) {
//                var message = "";
//                var cssClass = "";

//                if (!contextMessageDictionary[data.Status]) {
//                    console.log("Dev Error: contextMessageDictionary['" + data.Status + "'] is not defined.");
//                    message = "Unknown Status";
//                } else {
//                    message = contextMessageDictionary[data.Status].format(data.Parameters);
//                }
//                switch (data.Disposition) {
//                    case 0:
//                        cssClass = "alert alert-success";
//                        break;
//                    case 1:
//                        cssClass = "alert alert-warning";
//                        break;
//                    case 2:
//                        cssClass = "alert alert-danger";
//                        break;
//                }
//                //if Context message wasn't already visible, show it
//                contextStatusAreas.each(function () {
//                    $(this).html("<div class='" + cssClass + "'>" + message + "</div>");
//                    if (!$(this).is(":visible")) {
//                        $(this).fadeTo(1000, 1, function () {
//                            $(this).slideDown(500);
//                        });
//                    }
//                });
//            } else {
//                //disregard or error -- just hide it, if visible
//                contextStatusAreas.each(function () {
//                    if ($(this).is(":visible")) {
//                        $(this).slideUp(500);
//                    }
//                });
//            }
//            if (data.Error) {
//                console.log("Error updating contextual status for listing #" + listingId + ": ", data.Error, data);
//            }
//        }, function (error) {
//            ShowSignalRAlert();
//            jslog("error (1) retrieving contextual status for listing #" + listingId + ": " + error);
//        });
//        promise.fail(function (jqXHR, textStatus) {
//            ShowSignalRAlert();
//            jslog("error (2) retrieving contextual status for listing #" + listingId + ": " + textStatus);
//        });
//    }

//}

function RefreshListingContextualStatus(listingId) {
    if ($.inArray(listingId, interestingListings) < 0) return;

    //console.log("RefreshListingContextualStatus triggered for ", listingId);
    var contextStatusAreas = $('[data-listingid="' + listingId + '"] .ContextualStatus');
    //console.log("contextStatusAreas ", contextStatusAreas.length);
    var inlineContextAreas = $('[data-listingid="' + listingId + '"] .InlineContextualStatus');
    //console.log("inlineContextAreas ", inlineContextAreas.length);
    if ((contextStatusAreas.length > 0 || inlineContextAreas.length > 0) && aweUserName) {
        var requestStart = performance.now();
        var promise = Proxy.invokeAsync("GetListingContextStatus", {
            listingId: listingId
        }, function (data) {
            //got result
            //console.log("data: ", data);
            if (!data.Disregard && !data.Error) {
                var message = "";
                var cssClass = "";

                //this part applies to the listing/lot detail page, and shows full-length context messages
                if (contextStatusAreas.length > 0) {
                    //console.log("contextStatusAreas.length > 0 ");
                    if (!contextMessageDictionary[data.Status]) {
                        console.log("Dev Error: contextMessageDictionary['" + data.Status + "'] is not defined.");
                        message = "Unknown Status";
                    } else {
                        message = contextMessageDictionary[data.Status].format(data.Parameters);
                    }
                    switch (data.Disposition) {
                        case 0:
                            cssClass = "alert alert-success";
                            break;
                        case 1:
                            cssClass = "alert alert-warning";
                            break;
                        case 2:
                            cssClass = "alert alert-danger";
                            break;
                    }
                    //if Context message wasn't already visible, show it
                    contextStatusAreas.each(function () {
                        $(this).html("<div class='" + cssClass + "'>" + message + "</div>");
                        if (!$(this).is(":visible")) {
                            $(this).fadeTo(1000, 1, function () {
                                $(this).slideDown(500);
                            });
                        }
                    });
                }

                //this part applies to pages that show a list of listings/lots, and shows abbreviated context messages
                if (inlineContextAreas.length > 0) {
                    var labelCssClass, alertCssClass;
                    //console.log("inlineContextAreas.length > 0 ");
                    if (!shortContextMessages[data.Status]) {
                        console.log("Dev Error: shortContextMessages['" + data.Status + "'] is not defined.");
                        message = "";
                    } else {
                        message = shortContextMessages[data.Status].format(data.Parameters);
                    }
                    //console.log("short message: ", message);
                    switch (data.Disposition) {
                        case 0:
                            labelCssClass = "label-success";
                            alertCssClass = "alert-success";
                            break;
                        case 1:
                            labelCssClass = "label-danger";
                            alertCssClass = "alert-danger";
                            break;
                        case 2:
                            labelCssClass = "label-danger";
                            alertCssClass = "alert-danger";
                            break;
                    }

                    //update context message and ensure it is visible
                    if (message != "") {
                        inlineContextAreas.each(function () {
                            if ($(this).hasClass("alert")) {
                                $(this).html(message)
                                    .removeClass("alert-success").removeClass("alert-danger")
                                    .addClass(alertCssClass)
                                    .show().pulse_block();
                            } else {
                                $(this).html(message)
                                    .removeClass("label-success").removeClass("label-danger")
                                    .addClass(labelCssClass)
                                    .show().pulse();
                            }
                            //console.log("updating/unhiding context label for ", listingId);
                        });
                    } else {
                        inlineContextAreas.hide();
                    }

                }

            } else {
                //disregard or error -- just hide it, if visible
                contextStatusAreas.each(function () {
                    if ($(this).is(":visible")) {
                        $(this).slideUp(500);
                    }
                });
                //console.log("hiding context label for ", listingId);
                inlineContextAreas.hide();
            }
            if (data.Error) {
                console.log("Error updating contextual status for listing #" + listingId + ": ", data.Error, data);
            }
        }, function (error) {
            ShowSignalRAlert();
            jslog("error (1) retrieving contextual status for listing #" + listingId + ": " + error);
        });
        promise.fail(function (jqXHR, textStatus) {
            ShowSignalRAlert();
            var timeStr = "; duration: " + (performance.now() - requestStart) + " MS";
            jslog("error (2) retrieving contextual status for listing #" + listingId + ": " + textStatus + timeStr);
        });
    }

}

function write_log(data) {
    if (record_SignalR_Errors) {
        $.post("RealTime/LogError", data);
    }
}
