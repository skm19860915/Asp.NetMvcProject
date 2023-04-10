// *** Controller Calling Proxy Class
function controllerProxy(controllerURL) {
    var _I = this;
    this.controllerURL = controllerURL;    

    // *** Call a wrapped object
    this.invoke = function (method, data, callback, error, bare) {
        // *** Convert input data into JSON - REQUIRES Json2.js        
        var json = JSON.stringify(data);

        // *** The service endpoint URL        
        var url = _I.controllerURL + method;

        console.log("Synchronous AJAX call made to: " + url);

        $.ajax({
            url: url,
            data: json,
            type: "POST",
            processData: false,
            contentType: "application/json",
            timeout: 10000,
            dataType: "text",  // not "json" we'll parse
            async: false,
            success:
                    function (res) {
                        if (!callback) return;

                        // *** Use json library so we can fix up MS AJAX dates
                        var result = $.parseJSON(res); // JSON.parse(res);

                        // *** Bare message IS result                        
                        //if (bare)
                        if (true)
                        { callback(result); return; }

                        // *** Wrapped message contains top level object node
                        // *** strip it off                        
                        for (var property in result) {                            
                            callback(result[property]);
                            break;
                        }
                    },
            error: function (xhr) {
                if (!error) return;
                if (xhr.responseText) {
                    var err = $.parseJSON(xhr.responseText); // JSON.parse(xhr.responseText);
                    if (err)
                        error(err);
                    else
                        error({ Message: "Unknown server error." });
                }
                return;
            }
        });
    };

    this.invokeAsync = function (method, data, callback, error, bare) {
        // *** Convert input data into JSON - REQUIRES Json2.js        
        var json = JSON.stringify(data);

        // *** The service endpoint URL        
        var url = _I.controllerURL + method;

        //console.log("Asynchronous AJAX call made to: " + url);

        var promise = $.ajax({
            url: url,
            data: json,
            type: "POST",
            processData: false,
            contentType: "application/json",
            timeout: 10000,
            dataType: "text",  // not "json" we'll parse
            async: true,
            success:
                    function (res) {

                        //console.log("Asynchronous AJAX response rec'd");

                        if (!callback) return;

                        // *** Use json library so we can fix up MS AJAX dates
                        var result = $.parseJSON(res); // JSON.parse(res);

                        // *** Bare message IS result                        
                        //if (bare)
                        if (true)
                        { callback(result); return; }

                        // *** Wrapped message contains top level object node
                        // *** strip it off                        
                        for (var property in result) {
                            callback(result[property]);
                            break;
                        }
                    },
            error: function (xhr) {
                if (!error) return;
                if (xhr.responseText) {
                    console.log("xhr.responseText: ", xhr.responseText);
                    var err = xhr.responseText;
                    try {
                        err = JSON.parse(xhr.responseText);
                    } catch (e) {
                        console.log("error was not JSON, ignoring");
                    }
                    if (err)
                        error(err);
                    else
                        error({ Message: "Unknown server error." });
                }
                return;
            }
        });
        return promise;
    };
}

//// *** Service Calling Proxy Class
//function serviceProxy(serviceUrl) {    
//    var _I = this;
//    this.serviceUrl = serviceUrl;

//    // *** Call a wrapped object
//    this.invoke = function(method, data, callback, error, bare) {
//        // *** Convert input data into JSON - REQUIRES Json2.js
//        var json = JSON.stringify(data);

//        // *** The service endpoint URL        
//        var url = _I.serviceUrl + method;

//        $.ajax({
//            url: url,
//            data: json,
//            type: "POST",
//            processData: false,
//            contentType: "application/json",
//            timeout: 10000,
//            dataType: "text",  // not "json" we'll parse
//            success:
//                    function(res) {                        
//                        if (!callback) return;
//                        
//                        // *** Use json library so we can fix up MS AJAX dates
//                        var result = JSON.parse(res);

//                        // *** Bare message IS result
//                        if (bare)
//                        { callback(result); return; }

//                        // *** Wrapped message contains top level object node
//                        // *** strip it off
//                        for (var property in result) {
//                            callback(result[property]);
//                            break;
//                        }
//                    },
//            error: function(xhr) {
//                if (!error) return;
//                if (xhr.responseText) {
//                    var err = JSON.parse(xhr.responseText);
//                    if (err)
//                        error(err);
//                    else
//                        error({ Message: "Unknown server error." })
//                }
//                return;
//            }
//        });
//    }
//}

$.url = function (url) {
    return $('base').attr('href') + url;        
};
// *** Create a static instance
//var Proxy = new serviceProxy($.url('RealTime.svc/'));

// *** Create a static instance
var Proxy = new controllerProxy($.url('RealTime/'));

var PriceDeadManSwitch = true;
function GetCurrentPrice(ListingID, ListingPriceDIV, LocalPriceDIV, ListingIncrementDIV, LocalIncrementDIV, NextListingPriceDIV, NextLocalPriceDIV) {
    //window.alert("Price Alive?" + PriceDeadManSwitch);
    if (PriceDeadManSwitch) {
        PriceDeadManSwitch = false;
        Proxy.invoke("GetCurrentPrice", { listingID: ListingID },
        function (result) {
            if (result == null) return;
            $(ListingPriceDIV).text(result.DisplayListingPrice);
            $(LocalPriceDIV).text(result.DisplayLocalPrice);
            $(ListingIncrementDIV).text(result.DisplayListingIncrement);
            $(LocalIncrementDIV).text(result.DisplayLocalIncrement);
            $(NextListingPriceDIV).text(result.DisplayNextListingPrice);
            $(NextLocalPriceDIV).text(result.DisplayNextLocalPrice);
            PriceDeadManSwitch = true;
        },
        function (error) { window.alert(error); });
    }
}

function GetEndDTTM(ListingID, EndDTTMDIV) {
    Proxy.invoke("GetCurrentEndDTTM", { listingID: ListingID },
    function(result) {
        $(EndDTTMDIV).text(result);        
    },
    function(error) { window.alert(error); });
}

var TimeDeadManSwitch = true;
function GetCurrentTime(DivClass) {
    //window.alert("Time Alive?" + TimeDeadManSwitch);
    if (TimeDeadManSwitch) {
        TimeDeadManSwitch = false;
        Proxy.invokeAsync("GetCurrentTime", {},
        function (result) {
            $(DivClass).text(result);
            TimeDeadManSwitch = true;
        },
        function (error) { window.alert(error); });
    }
}

function EmailInvoice(template, invoiceID, recipient) {
    Proxy.invoke("EmailInvoice", { template: template, invoiceID: invoiceID, recipient: $.htmlDecode(recipient) },
    function (result) {
        
    },
    function (error) { window.alert(error); });
}

function AttemptBatchPayment(invoiceID) {
    var retVal;
    Proxy.invoke("AttemptBatchPayment", { invoiceID: invoiceID },
    function (result) {
        if (result.toString() == "true") {            
            retVal = true;
        } else {            
            retVal = false;
        }
    },
    function (error) { window.alert(error); });

    return retVal;
}

function DemandBatchProcessing() {
    Proxy.invoke("DemandBatchProcessing", {},
    function (result) {

    },
    function (error) { window.alert(error); });
}

function DemandSalesBatchProcessing() {
    Proxy.invoke("DemandSalesBatchProcessing", {},
        function (result) {

        },
        function (error) { window.alert(error); });
}

var disableDatePicker = false;
function ApplyDatePicker(inputObject, usersCulture, defaultCulture) {
    if (disableDatePicker) return;
    if (usersCulture.length > 0 && $.datepicker.regional[usersCulture]) {
        //console.log('DatePicker: using full culture "' + usersCulture + '".');
        inputObject.datepicker($.datepicker.regional[usersCulture]);
    } else if (usersCulture.length > 1 && $.datepicker.regional[usersCulture.substring(0, 2)]) {
        console.log('DatePicker: "' + usersCulture + '" not supported, using basic culture "' + usersCulture.substring(0, 2) + '". Missing datetimepicker_js bundle or culture definition?');
        inputObject.datepicker($.datepicker.regional[usersCulture.substring(0, 2)]);
    } else if ($.datepicker.regional[defaultCulture]) {
        console.log('DatePicker: using full default site culture "' + defaultCulture + '". Missing datetimepicker_js bundle or culture definition?');
        inputObject.datepicker($.datepicker.regional[defaultCulture]);
    } else /*if (defaultCulture.length > 1 && $.datepicker.regional[defaultCulture.substring(0, 2)])*/ {
        console.log('DatePicker: "' + defaultCulture + '" not supported, attempting basic site culture "' + defaultCulture.substring(0, 2) + '", or defaulting to English. Missing datetimepicker_js bundle or culture definition?');
        inputObject.datepicker($.datepicker.regional[defaultCulture.substring(0, 2)]);
    }
}

$.htmlDecode = (function () {
    // this prevents any overhead from creating the object each time
    var element = document.createElement('div');

    function decodeHTMLEntities(str) {
        if (str && typeof str === 'string') {
            // strip script/html tags
            str = str.replace(/<script[^>]*>([\S\s]*?)<\/script>/gmi, '');
            str = str.replace(/<\/?\w(?:[^"'>]|"[^"]*"|'[^']*')*>/gmi, '');
            element.innerHTML = str;
            str = element.textContent;
            element.textContent = '';
        }

        return str;
    }

    return decodeHTMLEntities;
})();

function TimeDifference(milliseconds) {        
    if (milliseconds <= 0) return "";

    var diff = milliseconds;

    var retVal = "";

    //var diff = date2.getTime() - date1.getTime();

    var days = Math.floor(diff / (1000 * 60 * 60 * 24));
    diff -= days * (1000 * 60 * 60 * 24);

    var hours = Math.floor(diff / (1000 * 60 * 60));
    diff -= hours * (1000 * 60 * 60);

    var minutes = Math.floor(diff / (1000 * 60));
    diff -= minutes * (1000 * 60);

    var seconds = Math.floor(diff / (1000));
    diff -= seconds * (1000);

    if (aweTimeRemainingStyle == 'Active') {

        retVal += days + " ";
        if (days != 1) {
            retVal += timeDifferenceDictionary["Days"];
        }
        else {
            retVal += timeDifferenceDictionary["Day"];
        }
        retVal += " " + pad(hours, 2) + ":" + pad(minutes, 2) + ":" + pad(seconds, 2);

    } else /*if (aweTimeRemainingStyle == 'Classic')*/ {

        //mimic private static MvcHtmlString TimeDifferenceToString
        //timeDifferenceDictionary defined in _layout.cshtml
        if (days > 0) {
            retVal += days + " ";
            if (days > 1) {
                retVal += timeDifferenceDictionary["Days"];
            }
            else {
                retVal += timeDifferenceDictionary["Day"];
            }
            retVal += ", ";
        }

        if (hours > 0) {
            retVal += hours + " ";
            if (hours > 1) {
                retVal += timeDifferenceDictionary["Hours"];
            }
            else {
                retVal += timeDifferenceDictionary["Hour"];
            }

            if (days > 0) {
                return retVal;
            } else {
                retVal += ", ";
            }
        }

        if (minutes > 0) {
            retVal += minutes + " ";
            if (minutes > 1) {
                retVal += timeDifferenceDictionary["Minutes"];
            }
            else {
                retVal += timeDifferenceDictionary["Minute"];
            }

            if (hours > 0 || days > 0) {
                return retVal;
            } else {
                retVal += ", ";
            }
        }

        if (seconds > 0) {
            retVal += seconds + " ";
            if (seconds > 1) {
                retVal += timeDifferenceDictionary["Seconds"];
            }
            else {
                retVal += timeDifferenceDictionary["Second"];
            }
        }

        if (retVal.substr(retVal.length - 2, 2) == ", ") retVal = retVal.substring(0, retVal.length - 2);
    }

    return retVal;
}

function pad(num, size) {
    var s = "000000000" + num;
    return s.substr(s.length-size);
}

function BracketEllipsize(value, length) {
    if (value.Length <= length) return value;

    return value.substring(0, length - 5) + "[...]";
}

function getIEVersion() {
    var agent = navigator.userAgent;
    var reg = /MSIE\s?(\d+)(?:\.(\d+))?/i;
    var matches = agent.match(reg);
    if (matches != null) {
        return { major: matches[1], minor: matches[2] };
    }
    return { major: "-1", minor: "-1" };
}

function getSafariVersion() {
    var ua = navigator.userAgent, tem, M = ua.match(/(safari)\/?\s*(\d+)/i) || [];
    if (/trident/i.test(M[1])) {
        tem = /\brv[ :]+(\d+)/g.exec(ua) || [];
        return 'IE ' + (tem[1] || '');
    }
    if (M[1] === 'Chrome') {
        tem = ua.match(/\bOPR\/(\d+)/)
        if (tem != null) { return 'Opera ' + tem[1]; }
    }
    M = M[2] ? [M[1], M[2]] : [navigator.appName, navigator.appVersion, '-?'];
    if ((tem = ua.match(/version\/(\d+)/i)) != null) { M.splice(1, 1, tem[1]); }
    else return -1;
    return M[1];
}

/*
function get_browser(){
    var ua=navigator.userAgent,tem,M=ua.match(/(opera|chrome|safari|firefox|msie|trident(?=\/))\/?\s*(\d+)/i) || []; 
    if(/trident/i.test(M[1])){
        tem=/\brv[ :]+(\d+)/g.exec(ua) || []; 
        return 'IE '+(tem[1]||'');
    }   
    if(M[1]==='Chrome'){
        tem=ua.match(/\bOPR\/(\d+)/)
        if(tem!=null)   {return 'Opera '+tem[1];}
    }   
    M=M[2]? [M[1], M[2]]: [navigator.appName, navigator.appVersion, '-?'];
    if((tem=ua.match(/version\/(\d+)/i))!=null) {M.splice(1,1,tem[1]);}
    return M[0];
}

function get_browser_version(){
    var ua=navigator.userAgent,tem,M=ua.match(/(opera|chrome|safari|firefox|msie|trident(?=\/))\/?\s*(\d+)/i) || [];                                                                                                                         
    if(/trident/i.test(M[1])){
        tem=/\brv[ :]+(\d+)/g.exec(ua) || [];
        return 'IE '+(tem[1]||'');
    }
    if(M[1]==='Chrome'){
        tem=ua.match(/\bOPR\/(\d+)/)
        if(tem!=null)   {return 'Opera '+tem[1];}
    }   
    M=M[2]? [M[1], M[2]]: [navigator.appName, navigator.appVersion, '-?'];
    if((tem=ua.match(/version\/(\d+)/i))!=null) {M.splice(1,1,tem[1]);}
    return M[1];
}
*/

function getQueryStrings() {
    //Holds key:value pairs
    var queryStringColl = new Array();

    //Get querystring from url
    var requestUrl = window.location.search.toString();
    if (requestUrl != '') {
        //window.location.search returns the part of the URL
        //that follows the ? symbol, including the ? symbol
        requestUrl = requestUrl.substring(1);

        //Get key:value pairs from querystring
        var kvPairs = requestUrl.split('&');

        for (var i = 0; i < kvPairs.length; i++) {
            var kvPair = kvPairs[i].split('=');
            queryStringColl[kvPair[0]] = decodeURIComponent(replaceAll(kvPair[1], "+", " "));
        }
    }
    return queryStringColl;
}

function escapeRegExp(string) {
    return string.replace(/([.*+?^=!:${}()|\[\]\/\\])/g, "\\$1");
}

function replaceAll(string, find, replace) {
    return string.replace(new RegExp(escapeRegExp(find), 'g'), replace);
}

function fileSizeLabel(fileSize) {
    var sizeLabel;
    if (fileSize < 1000) {
        sizeLabel = parseInt(fileSize) + ' B';
    } else if (fileSize < 1000000) {
        sizeLabel = parseInt(fileSize / 1000) + ' KB';
    } else if (fileSize < 1000000000) {
        sizeLabel = parseInt(fileSize / 1000000) + ' MB';
    } else {
        sizeLabel = parseInt(fileSize / 1000000000) + ' GB';
    }
    return sizeLabel;
}

$(document).ready(function () {
    $(".disable-on-click").on("click", function () {
        $(this).prop("disabled", true);
    });
});
