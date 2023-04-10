//Visit http://javascriptkit.com for the original version of this script

//specify message to alert

///No editing required beyond here/////

//Alert only once per browser session (0=no, 1=yes)
var alertMessage = "";
var alertMessage_Pre = "You are running an older version of Internet Explorer: version ";
var alertMessage_Post = " which is NOT supported.  Please upgrade your web browser to view this website.";

var browserName = "";
var browserVersion = 0;

var once_per_session = 1;

window.onload = function () {

    navigator.sayswho = (function () {
        var ua = navigator.userAgent, tem,
        M = ua.match(/(opera|chrome|safari|firefox|msie|trident(?=\/))\/?\s*([\d\.]+)/i) || [];
        if (/trident/i.test(M[1])) {
            tem = /\brv[ :]+(\d+(\.\d+)?)/g.exec(ua) || [];
            return 'IE ' + (tem[1] || '');
        }
        M = M[2] ? [M[1], M[2]] : [navigator.appName, navigator.appVersion, '-?'];
        if ((tem = ua.match(/version\/([\.\d]+)/i)) != null) M[2] = tem[1];

        browserName = M[0];
        browserVersion = M[1];

        return M.join(' ');
    })();

    if (browserName == "MSIE" && browserVersion <= 8.0) {

        if (once_per_session == 0) {
            loadalert(browserVersion);
        }
        else {
            alertornot(browserVersion);
        }
    }
};


function get_cookie(name)
{
    var search = name + "=";
    var returnvalue = "";

    if (document.cookie.length > 0)
    {
        offset = document.cookie.indexOf(search)
        if (offset != -1)
        {
            // if cookie exists
            offset += search.length
            // set index of beginning of value
            end = document.cookie.indexOf(";", offset);
            // set index of end of cookie value
            if (end == -1)
            {
                end = document.cookie.length;
            }

            returnvalue = unescape(document.cookie.substring(offset, end));
        }
    }
    return returnvalue;
}

function alertornot(v){
    if (get_cookie('old-browser-alerted') == '')
    {
        loadalert(v);
        document.cookie = "old-browser-alerted=yes";
    }
}

function loadalert(v)
{
    alert(alertMessage_Pre + v + alertMessage_Post);
}



