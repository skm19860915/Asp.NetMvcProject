/// jQuery plugin to add support for SwfUpload
/// (c) 2008 Steven Sanderson

function fadeIn(element, opacity) {
	var reduceOpacityBy = 5;
	var rate = 30;	// 15 fps


	if (opacity < 100) {
		opacity += reduceOpacityBy;
		if (opacity > 100) {
			opacity = 100;
		}

		if (element.filters) {
			try {
				element.filters.item("DXImageTransform.Microsoft.Alpha").opacity = opacity;
			} catch (e) {
				// If it is not set initially, the browser will throw an error.  This will set it if it is not set yet.
				element.style.filter = 'progid:DXImageTransform.Microsoft.Alpha(opacity=' + opacity + ')';
			}
		} else {
			element.style.opacity = opacity / 100;
		}
	}

	if (opacity < 100) {
		setTimeout(function () {
			fadeIn(element, opacity);
		}, rate);
	}
}

(function($) {
    $.fn.makeAsyncUploader = function(options) {
        return this.each(function() {
            // Put in place a new container with a unique ID
            var id = $(this).attr("id");
            var container = $("<span class='asyncUploader'/>");
            container.append($("<div class='ProgressBar'> <div>&nbsp;</div> </div>"));
            container.append($("<span id='" + id + "_completedMessage'/>"));
            container.append($("<span id='" + id + "_uploading'>Uploading... <input type='button' value='Cancel'/></span>"));
            container.append($("<span id='" + id + "_swf'/>"));
            container.append($("<input type='hidden' name='" + id + "_filename'/>"));
            container.append($("<input type='hidden' name='" + id + "_guid'/>"));
            container.append($("<table><tr><td>"));
            container.append($("</td></tr></table><br/>"));
            //container.append($("<input type='submit' name='" + id + "_submit' value='Proceed'/>"));
            $(this).before(container).remove();
            $("div.ProgressBar", container).hide();
            $("span[id$=_uploading]", container).hide();
            //$("input[name$=_submit]", container).hide();


            // Instantiate the uploader SWF
            var swfu;
            var width = 109, height = 22;
            if (options) {
                width = options.width || width;
                height = options.height || height;
            }
            var defaults = {
                flash_url: "swfupload.swf",
                upload_url: "/Media/AsyncUploadListingImage",
                file_size_limit: "10 MB",
                file_types: "*.*",
                file_types_description: "All Files",
                debug: false,

                button_image_url: "blankButton.png",
                button_width: width,
                button_height: height,
                button_placeholder_id: id + "_swf",
                button_text: "<font face='Arial' size='13pt'>Choose file(s)</font>",
                button_text_left_padding: (width - 85) / 2,
                button_text_top_padding: 1,

                // Called when the user chooses a new file from the file browser prompt (begins the upload)
                file_queued_handler: function(file) { swfu.startUpload(); },

                // Called when a file doesn't even begin to upload, because of some error
                file_queue_error_handler: function(file, code, msg) { alert("Sorry, your file wasn't uploaded: " + msg); },

                // Called when an error occurs during upload
                upload_error_handler: function(file, code, msg) { alert("Sorry, your file wasn't uploaded: " + msg); },

                // Called when upload is beginning (switches controls to uploading state)
                upload_start_handler: function() {
                    swfu.setButtonDimensions(0, height);
                    $("input[name$=_filename]", container).val("");
                    $("input[name$=_guid]", container).val("");
                    $("div.ProgressBar div", container).css("width", "0px");
                    $("div.ProgressBar", container).show();
                    $("span[id$=_uploading]", container).show();
                    $("span[id$=_completedMessage]", container).html("").hide();

                    if (options.disableDuringUpload)
                        $(options.disableDuringUpload).attr("disabled", "disabled");
                },

                // Called when upload completed successfully (puts success details into hidden fields)
                upload_success_handler: function(file, response) {                    
                    //$("input[name$=_filename]", container).val(file.name);
                    //$("input[name$=_guid]", container).val(response);
                    //addImage("Content/listingImages/" + response, file.name, Math.round(file.size / 1024));                                        
                    //var guid = response.toString().substring(0, 36);                    
                    //var uri = response.toString().substring(36);
                    var result = $.parseJSON(response);
                    addImage(file.name, file.size, file.type, result.guid, result.uri);

                    //$("input[name$=_submit]", container).show();
                    $("span[id$=_completedMessage]", container).html("Uploaded <b>{0}</b> ({1} KB)"
                                .replace("{0}", file.name)
                                .replace("{1}", Math.round(file.size / 1024))
                            );
                },

                // Called when upload is finished (either success or failure - reverts controls to non-uploading state)
                upload_complete_handler: function() {
                    if (this.getStats().files_queued > 0) {
                        this.startUpload();
                    } else {
                        var clearup = function() {
                            $("div.ProgressBar", container).hide();
                            $("span[id$=_completedMessage]", container).show();
                            $("span[id$=_uploading]", container).hide();
                            swfu.setButtonDimensions(width, height);
                        };
                        if ($("input[name$=_filename]", container).val() != "") // Success
                            $("div.ProgressBar div", container).animate({ width: "100%" }, { duration: "fast", queue: false, complete: clearup });
                        else // Fail
                            clearup();

                        if (options.disableDuringUpload)
                            $(options.disableDuringUpload).removeAttr("disabled");
                    }
                },

                // Called periodically during upload (moves the progess bar along)
                upload_progress_handler: function(file, bytes, total) {
                    var percent = 100 * bytes / total;
                    $("div.ProgressBar div", container).animate({ width: percent + "%" }, { duration: 500, queue: false });
                }
            };
            swfu = new SWFUpload($.extend(defaults, options || {}));

            // Called when user clicks "cancel" (forces the upload to end, and eliminates progress bar immediately)
            $("span[id$=_uploading] input[type='button']", container).click(function() {
                swfu.cancelUpload(null, false);
            });

            // Give the effect of preserving state, if requested
            if (options.existingFilename || "" != "") {
                $("span[id$=_completedMessage]", container).html("Uploaded <b>{0}</b> ({1} KB)"
                                .replace("{0}", options.existingFilename)
                                .replace("{1}", options.existingFileSize ? Math.round(options.existingFileSize / 1024) : "?")
                            ).show();
                $("input[name$=_filename]", container).val(options.existingFilename);
            }
            if (options.existingGuid || "" != "")
                $("input[name$=_guid]", container).val(options.existingGuid);
        });
    }
})(jQuery);

(function($) {
    $.fn.makeAsyncUploaderWithSuccessHandler = function(options, successHandler) {
        return this.each(function() {
            // Put in place a new container with a unique ID
            var id = $(this).attr("id");
            var container = $("<span class='asyncUploader'/>");
            container.append($("<div class='ProgressBar'> <div>&nbsp;</div> </div>"));
            container.append($("<span id='" + id + "_completedMessage'/>"));
            container.append($("<span id='" + id + "_uploading'>Uploading... <input type='button' value='Cancel'/></span>"));
            container.append($("<span id='" + id + "_swf'/>"));
            container.append($("<input type='hidden' name='" + id + "_filename'/>"));
            container.append($("<input type='hidden' name='" + id + "_guid'/>"));
            container.append($("<table><tr><td>"));
            container.append($("</td></tr></table><br/>"));
            //container.append($("<input type='submit' name='" + id + "_submit' value='Proceed'/>"));
            $(this).before(container).remove();
            $("div.ProgressBar", container).hide();
            $("span[id$=_uploading]", container).hide();
            //$("input[name$=_submit]", container).hide();


            // Instantiate the uploader SWF
            var swfu;
            var width = 109, height = 22;
            if (options) {
                width = options.width || width;
                height = options.height || height;
            }
            var defaults = {
                flash_url: "swfupload.swf",
                upload_url: "/Media/AsyncUploadListingImage",
                file_size_limit: "10 MB",
                file_types: "*.*",
                file_types_description: "All Files",
                debug: false,

                button_image_url: "blankButton.png",
                button_width: width,
                button_height: height,
                button_placeholder_id: id + "_swf",
                button_text: "<font face='Arial' size='13pt'>Choose file(s)</font>",
                button_text_left_padding: (width - 85) / 2,
                button_text_top_padding: 1,

                // Called when the user chooses a new file from the file browser prompt (begins the upload)
                file_queued_handler: function(file) { swfu.startUpload(); },

                // Called when a file doesn't even begin to upload, because of some error
                file_queue_error_handler: function(file, code, msg) { alert("Sorry, your file wasn't uploaded: " + msg); },

                // Called when an error occurs during upload
                upload_error_handler: function(file, code, msg) { alert("Sorry, your file wasn't uploaded: " + msg); },

                // Called when upload is beginning (switches controls to uploading state)
                upload_start_handler: function() {
                    swfu.setButtonDimensions(0, height);
                    $("input[name$=_filename]", container).val("");
                    $("input[name$=_guid]", container).val("");
                    $("div.ProgressBar div", container).css("width", "0px");
                    $("div.ProgressBar", container).show();
                    $("span[id$=_uploading]", container).show();
                    $("span[id$=_completedMessage]", container).html("").hide();

                    if (options.disableDuringUpload)
                        $(options.disableDuringUpload).attr("disabled", "disabled");
                },

                // Called when upload completed successfully (puts success details into hidden fields)
                upload_success_handler: function(file, response) {                    
                    //$("input[name$=_filename]", container).val(file.name);
                    //$("input[name$=_guid]", container).val(response);
                    //addImage("Content/listingImages/" + response, file.name, Math.round(file.size / 1024));                                        
                    //var guid = response.toString().substring(0, 36);                    
                    //var uri = response.toString().substring(36);                    
                    var result = $.parseJSON(response);

                    //addImage(file.name, file.size, file.type, guid, uri);
                    successHandler(file.name, file.size, file.type, result.guid, result.uri);

                    //$("input[name$=_submit]", container).show();
                    $("span[id$=_completedMessage]", container).html("Uploaded <b>{0}</b> ({1} KB)"
                                .replace("{0}", file.name)
                                .replace("{1}", Math.round(file.size / 1024))
                            );
                },

                // Called when upload is finished (either success or failure - reverts controls to non-uploading state)
                upload_complete_handler: function() {
                    if (this.getStats().files_queued > 0) {
                        this.startUpload();
                    } else {
                        var clearup = function() {
                            $("div.ProgressBar", container).hide();
                            $("span[id$=_completedMessage]", container).show();
                            $("span[id$=_uploading]", container).hide();
                            swfu.setButtonDimensions(width, height);
                        };
                        if ($("input[name$=_filename]", container).val() != "") // Success
                            $("div.ProgressBar div", container).animate({ width: "100%" }, { duration: "fast", queue: false, complete: clearup });
                        else // Fail
                            clearup();

                        if (options.disableDuringUpload)
                            $(options.disableDuringUpload).removeAttr("disabled");
                    }
                },

                // Called periodically during upload (moves the progess bar along)
                upload_progress_handler: function(file, bytes, total) {
                    var percent = 100 * bytes / total;
                    $("div.ProgressBar div", container).animate({ width: percent + "%" }, { duration: 500, queue: false });
                }
            };
            swfu = new SWFUpload($.extend(defaults, options || {}));

            // Called when user clicks "cancel" (forces the upload to end, and eliminates progress bar immediately)
            $("span[id$=_uploading] input[type='button']", container).click(function() {
                swfu.cancelUpload(null, false);
            });

            // Give the effect of preserving state, if requested
            if (options.existingFilename || "" != "") {
                $("span[id$=_completedMessage]", container).html("Uploaded <b>{0}</b> ({1} KB)"
                                .replace("{0}", options.existingFilename)
                                .replace("{1}", options.existingFileSize ? Math.round(options.existingFileSize / 1024) : "?")
                            ).show();
                $("input[name$=_filename]", container).val(options.existingFilename);
            }
            if (options.existingGuid || "" != "")
                $("input[name$=_guid]", container).val(options.existingGuid);
        });
    }
})(jQuery);

(function ($) {
    $.fn.makeAsyncUploaderWithSuccessOrErrorHandlers = function (options, successHandler, errorhandler) {
        return this.each(function () {
            // Put in place a new container with a unique ID
            var id = $(this).attr("id");
            var container = $("<span class='asyncUploader'/>");
            container.append($("<div class='ProgressBar'> <div>&nbsp;</div> </div>"));
            container.append($("<span id='" + id + "_completedMessage'/>"));
            container.append($("<span id='" + id + "_uploading'>Uploading... <input type='button' value='Cancel'/></span>"));
            container.append($("<span id='" + id + "_swf'/>"));
            container.append($("<input type='hidden' name='" + id + "_filename'/>"));
            container.append($("<input type='hidden' name='" + id + "_guid'/>"));
            container.append($("<table><tr><td>"));
            container.append($("</td></tr></table><br/>"));
            //container.append($("<input type='submit' name='" + id + "_submit' value='Proceed'/>"));
            $(this).before(container).remove();
            $("div.ProgressBar", container).hide();
            $("span[id$=_uploading]", container).hide();
            //$("input[name$=_submit]", container).hide();


            // Instantiate the uploader SWF
            var swfu;
            var width = 109, height = 22;
            if (options) {
                width = options.width || width;
                height = options.height || height;
            }
            var defaults = {
                flash_url: "swfupload.swf",
                upload_url: "/Media/AsyncUploadListingImage",
                file_size_limit: "10 MB",
                file_types: "*.*",
                file_types_description: "All Files",
                debug: false,

                button_image_url: "blankButton.png",
                button_width: width,
                button_height: height,
                button_placeholder_id: id + "_swf",
                button_text: "<font face='Arial' size='13pt'>Choose file(s)</font>",
                button_text_left_padding: (width - 85) / 2,
                button_text_top_padding: 1,

                // Called when the user chooses a new file from the file browser prompt (begins the upload)
                file_queued_handler: function (file) { swfu.startUpload(); },

                // Called when a file doesn't even begin to upload, because of some error
                file_queue_error_handler: function (file, code, msg) { alert("Sorry, your file wasn't uploaded: " + msg); },

                // Called when an error occurs during upload
                upload_error_handler: function (file, code, msg) { alert("Sorry, your file wasn't uploaded: " + msg); },

                // Called when upload is beginning (switches controls to uploading state)
                upload_start_handler: function () {
                    swfu.setButtonDimensions(0, height);
                    $("input[name$=_filename]", container).val("");
                    $("input[name$=_guid]", container).val("");
                    $("div.ProgressBar div", container).css("width", "0px");
                    $("div.ProgressBar", container).show();
                    $("span[id$=_uploading]", container).show();
                    $("span[id$=_completedMessage]", container).html("").hide();

                    if (options.disableDuringUpload)
                        $(options.disableDuringUpload).attr("disabled", "disabled");
                },

                // Called when upload completed successfully (puts success details into hidden fields)
                upload_success_handler: function (file, response) {
                    //$("input[name$=_filename]", container).val(file.name);
                    //$("input[name$=_guid]", container).val(response);
                    //addImage("Content/listingImages/" + response, file.name, Math.round(file.size / 1024));                                                                              

                    var result = $.parseJSON(response);
                    if (result.error) {
                        errorhandler(result.error);
                        return;
                    } else {
                        successHandler(result.name, result.size, result.type, result.guid, result.uri, result.title);
                    }

                    //addImage(file.name, file.size, file.type, guid, uri);                    

                    //$("input[name$=_submit]", container).show();
                    $("span[id$=_completedMessage]", container).html("Uploaded <b>{0}</b> ({1} KB)"
                                .replace("{0}", file.name)
                                .replace("{1}", Math.round(file.size / 1024))
                            );
                },

                // Called when upload is finished (either success or failure - reverts controls to non-uploading state)
                upload_complete_handler: function () {
                    if (this.getStats().files_queued > 0) {
                        this.startUpload();
                    } else {
                        var clearup = function () {
                            $("div.ProgressBar", container).hide();
                            $("span[id$=_completedMessage]", container).show();
                            $("span[id$=_uploading]", container).hide();
                            swfu.setButtonDimensions(width, height);
                        };
                        if ($("input[name$=_filename]", container).val() != "") // Success
                            $("div.ProgressBar div", container).animate({ width: "100%" }, { duration: "fast", queue: false, complete: clearup });
                        else // Fail
                            clearup();

                        if (options.disableDuringUpload)
                            $(options.disableDuringUpload).removeAttr("disabled");
                    }
                },

                // Called periodically during upload (moves the progess bar along)
                upload_progress_handler: function (file, bytes, total) {
                    var percent = 100 * bytes / total;
                    $("div.ProgressBar div", container).animate({ width: percent + "%" }, { duration: 500, queue: false });
                }
            };
            swfu = new SWFUpload($.extend(defaults, options || {}));

            // Called when user clicks "cancel" (forces the upload to end, and eliminates progress bar immediately)
            $("span[id$=_uploading] input[type='button']", container).click(function () {
                swfu.cancelUpload(null, false);
            });

            // Give the effect of preserving state, if requested
            if (options.existingFilename || "" != "") {
                $("span[id$=_completedMessage]", container).html("Uploaded <b>{0}</b> ({1} KB)"
                                .replace("{0}", options.existingFilename)
                                .replace("{1}", options.existingFileSize ? Math.round(options.existingFileSize / 1024) : "?")
                            ).show();
                $("input[name$=_filename]", container).val(options.existingFilename);
            }
            if (options.existingGuid || "" != "")
                $("input[name$=_guid]", container).val(options.existingGuid);
        });
    }
})(jQuery);