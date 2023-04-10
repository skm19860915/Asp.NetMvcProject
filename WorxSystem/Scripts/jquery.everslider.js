/**
 * Everslider - Responsive jQuery Carousel Plugin
 * http://plugins.gravitysign.com/everslider
 * Copyright (c) 2013 Roman Yurchuk
 * Version 1.6.1
*/

(function($){

	"use strict";
		
	// Function to get vendor-specific CSS3 style property (e.g. WebkitTransition)
	// ---------------------------------------------------------------------------
	
	function getVendorProperty(name){
		var property = false, prefix = ['Webkit','Moz','O','ms'];		
		var test = document.createElement('div');
		// check unprefixed property
		if(typeof test.style[name] === 'string') {
			property = name;
		} else {
			var name_u = name.charAt(0).toUpperCase() + name.substr(1);
			for(var p in prefix) {
				if(typeof test.style[prefix[p]+name_u] === 'string') {
					property = prefix[p]+name_u;
					break;
				}
			}
		}
		// prevent memory leaks in IE
		test = null;
		// return property name or 'false'
		return property;
	}
	
	
	// Function to get vendor specific CSS3 prefix (-webkit-,-moz-,-ms-,-o- ...)
	// ---------------------------------------------------------------------------
	
	function getVendorPrefix() {
		var prefix = {
			WebkitTransition: '-webkit-',
			MozTransition: '-moz-',
			msTransition: '-ms-',
			OTransition: '-o-',
			transition: ''
		};
		
		// return "-webkit-" prefix for Safari and Chrome
		if(/(Safari|Chrome)/.test(navigator.userAgent)) {
			return prefix['WebkitTransition'];
		}
		
		return prefix[getVendorProperty('transition')];
	}
	
	
	// Function to detect 3D support in browser
	// ---------------------------------------------------------------------------
	
	function checkTransform3D(){
		var support = false, test = document.createElement('div');
		var transform = getVendorProperty('transform');
		// apply 3d property and get its value back
		test.style[transform] = 'rotateY(45deg)';
		if(test.style[transform] !== '') {
			support = true;
		}
		// prevent memory leaks in IE
		test = null;
		return support;
	}
	
	
	// Function to get pixel offset for element (CSS3 "translate" or box model)
	// ---------------------------------------------------------------------------
	
	function getPixelOffset(element, cssok) {
		var transform = getVendorProperty('transform'); 
		var position = {left:0, top:0};
		if(transform && cssok) {
			var matrix = element.css(transform);
			if(matrix.indexOf('matrix') === 0) {
				matrix = matrix.split('(')[1].split(')')[0].split(/,\s*/);
				position.left = parseInt(matrix[4],10);
				position.top = parseInt(matrix[5],10);
			}
		} else {
			position = element.position();
		}
		return position;
	}
	
	
	// CSS3 variables
	// ---------------------------------------------------------------------------	
	
	var transition = getVendorProperty('transition');
	var transform = getVendorProperty('transform');
	var cssprefix = getVendorPrefix();
	var transform3d = checkTransform3D();	
	
	
	// Function to translate list position with "translate3d()"
	// ---------------------------------------------------------------------------		
	
	function translate(element, pixels, animate){
		if(typeof animate === 'object') {
			// setup transition for "transform" property
			var property = cssprefix + 'transform';
			setTransition(element, property, animate.duration, animate.easing, animate.delay, animate.complete);
			// run "animate.complete()" if there is no value change in CSS3 "transform" 
			// property (in this case "transitionend" event won't fire)
			if(pixels === getPixelOffset(element, true).left) {
				animate.complete.call(element, property);
			}
		}
		// apply style
		if( transform3d ) {
			element.css(transform, 'translate3d(' + parseInt(pixels, 10) + 'px, 0px, 0px)');
		} else {
			element.css(transform, 'translate(' + parseInt(pixels, 10) + 'px, 0px)');
		}
	}
	
	
	// Function to apply CSS3 transition to element. Additional "complete" function
	// can be provided to run as soon as transition ends
	// ---------------------------------------------------------------------------
	
	function setTransition(element, properties, duration, easing, delay, complete){
		// map jquery easing to cubic-bezier for CSS3 transition
		var easing_map = {
			'linear':         'linear',
			'swing':          'cubic-bezier(.02,.01,.47,1)',
			'easeOutCubic':   'cubic-bezier(.215,.61,.355,1)',
			'easeInOutCubic': 'cubic-bezier(.645,.045,.355,1)',
			'easeInCirc':     'cubic-bezier(.6,.04,.98,.335)',
			'easeOutCirc':    'cubic-bezier(.075,.82,.165,1)',
			'easeInOutCirc':  'cubic-bezier(.785,.135,.15,.86)',
			'easeInExpo':     'cubic-bezier(.95,.05,.795,.035)',
			'easeOutExpo':    'cubic-bezier(.19,1,.22,1)',
			'easeInOutExpo':  'cubic-bezier(1,0,0,1)',
			'easeInQuad':     'cubic-bezier(.55,.085,.68,.53)',
			'easeOutQuad':    'cubic-bezier(.25,.46,.45,.94)',
			'easeInOutQuad':  'cubic-bezier(.455,.03,.515,.955)',
			'easeInQuart':    'cubic-bezier(.895,.03,.685,.22)',
			'easeOutQuart':   'cubic-bezier(.165,.84,.44,1)',
			'easeInOutQuart': 'cubic-bezier(.77,0,.175,1)',
			'easeInQuint':    'cubic-bezier(.755,.05,.855,.06)',
			'easeOutQuint':   'cubic-bezier(.23,1,.32,1)',
			'easeInOutQuint': 'cubic-bezier(.86,0,.07,1)',
			'easeInSine':     'cubic-bezier(.47,0,.745,.715)',
			'easeOutSine':    'cubic-bezier(.39,.575,.565,1)',
			'easeInOutSine':  'cubic-bezier(.445,.05,.55,.95)',
			'easeInBack':     'cubic-bezier(.6,-.28,.735,.045)',
			'easeOutBack':    'cubic-bezier(.175, .885,.32,1.275)',
			'easeInOutBack':  'cubic-bezier(.68,-.55,.265,1.55)'
		};
		// map css3 transition property to vendor specific event name
		var event_map = {				
			'transition': 'transitionend',
			'OTransition': 'oTransitionEnd otransitionend',
			'WebkitTransition': 'webkitTransitionEnd',
			'MozTransition': 'transitionend'
		};
					
		// argument defaults
		properties = properties.split(/\s+/);
		duration = (parseInt(duration,10) / 1000 || 0) + 's';
		easing = easing_map[easing] || easing_map['swing'];
		// if delay has been ommited in favour of complete function
		if(typeof delay === 'function') {
			complete = delay;
			delay = 0;
		}
		delay = (parseInt(delay,10) / 1000 || 0) + 's';
		complete = complete || $.noop;
		
		// save old transition
		var transition_tmp = element.css(transition);
											
		// register "transitionend" event handler
		element.bind(event_map[transition], function(e){
			var event = e.originalEvent;
			// make sure we deal with event target but not its descendants
			if(event.target === this) {
				complete.call(element, event.propertyName);
				$(this).css(transition, transition_tmp).unbind(e);
			}
			e.stopPropagation();
		});
								
		// set transition for element
		var string = '';
		for(var n = 0; n < properties.length; n++) {
			string += properties[n] + ' ' + duration + ' ' + easing + ' ' + delay + ', ';
		}
		element.css(transition, string.slice(0,-2));
	}
		
			
	// Constructor
	// ---------------------------------------------------------------------------
	
	function Slider(container, settings){
		this.offset = 0;
		this.visible = 0;
		this.lock = false;
		this.timer = 0;
		this.api = {};
		this.settings = settings;
		this.container = $(container);		
		this.list = this.container.find('ul.es-slides');
		this.total = this.list.children('li').length;
		this.slide = this.list.children('li:first-child');
		this.cssok = getVendorProperty('transition') && this.settings.useCSS;
		var self = this;
		
		// return if .es-slides list doesn't exist or has no slides
		if(this.total === 0) {
			return null;
		}
				
		// set move slides number
		if(this.settings.moveSlides === 'auto') {
			this.settings.moveSlides = 9999; // will be forced to visible
		} else {
			this.settings.moveSlides = parseInt(this.settings.moveSlides,10) || 1;
		}
		
		// set slide width (only >0)
		this.settings.itemWidth = parseInt(this.settings.itemWidth,10) || 0;			
		if(this.settings.itemWidth > 0) {
			this.list.children().css('width', this.settings.itemWidth);
		}
		
		// set slide height (only >0)
		this.settings.itemHeight = parseInt(this.settings.itemHeight,10) || 0;
		if(this.settings.itemHeight > 0) {
			this.list.children().css('height', '100');
		}
		
		// set slide margin
		if(this.settings.itemMargin !== false) {
			this.list.children().css('margin-right', parseInt(this.settings.itemMargin,10) || 0);
		}
		
		// if 'itemKeepRatio' is false set slide height to 'auto'
		if( !this.settings.itemKeepRatio ) {
			this.list.children().css({height:'auto'});
		}		
		
		// save size of first slide for further calculations
		this.slide_width = this.slide.width();
		this.slide_margin = parseInt(this.slide.css('margin-right'), 10) || 0;
		this.slide_ratio = this.slide.height()/this.slide.width();
				
		// set viewport width
		if(this.settings.maxVisible > 0) {
			var max_width = this.settings.maxVisible * (this.slide_width + this.slide_margin) - this.slide_margin;
			this.container.css('max-width', max_width);
		} else {
			this.container.css('max-width', this.settings.maxWidth);
		}

		// carousel mode
		if(this.settings.mode === 'carousel') {		
			var cloned_before = this.list.children().clone(true), 
				cloned_after = this.list.children().clone(true);
			
			// add cloned slides before and after original slides
			this.list.prepend(document.createComment(' END CLONED ')).prepend(cloned_before).
			prepend(document.createComment(' BEGIN CLONED '));
			this.list.append(document.createComment(' BEGIN CLONED ')).append(cloned_after).
			append(document.createComment(' END CLONED '));
			
			// change offset and total
			this.offset = this.total;
			this.total = this.total * 3;
			
			// set initial list offset
			var p = this.offset * (this.slide_width + this.slide_margin);
			if(this.cssok) {
				translate(this.list, -p);
			} else {
				this.list.css('left', -p);
			}			
		}
		
		// ticker
		if(this.settings.ticker && this.settings.mode !== 'normal') {
			this.enableTicker();
		}
							
		// navigation
		if(this.settings.navigation) {
			this.container.append(['<div class="es-navigation">',
			'<a href="" class="es-prev">' + this.settings.prevNav + '</a>',
			'<a href="" class="es-next">' + this.settings.nextNav + '</a>',
			'</div>'].join('\n'));
			// update navigation 
			this.updateNavigation(this.offset);
			// assing click handlers
			this.container.find('.es-prev').click(function(e){
				self.slidePrevious();
				e.preventDefault();
			}).end().find('.es-next').click(function(e){
				self.slideNext();						
				e.preventDefault();
			}).end();
		}
		
		// pagination
		if(this.settings.pagination && this.settings.mode !== 'carousel') {
			this.container.append('<div class="es-pagination"></div>');
		}
		
		// touchSwipe navigation
		if(this.settings.touchSwipe) {
			this.enableTouchSwipe();
		}
		
		// mousewheel navigation
		if(this.settings.mouseWheel) {
			this.enableMouseWheel();
		}
		
		// keyboard navigation
		if(this.settings.keyboard) {
			this.enableKeyboard();
		}
		
		// slide auto resize
		$(window).bind('resize', function(){
			window.clearTimeout(self.timer);
			self.timer = window.setTimeout(function(){
				self.resizeSlides();
			}, self.settings.fitDelay);
		}).trigger('resize');
								
		// create slider API
		$.extend(this.api, {	
			slideNext: function(){
				self.slideNext();
			},
			slidePrevious: function(){
				self.slidePrevious();
			},
			slideTo: function(p) {
				self.slideTo(p);
			},
			isSliding: function(){
				return self.isSliding();
			},
			getVisibleSlides: function(){
				return self.getVisibleSlides();
			},
			tickerPause: function(){
				if('tickerPause' in self) {
					self.tickerPause();
				}
			},
			tickerPlay: function(){
				if('tickerPlay' in self) {
					self.tickerPlay();
				}
			}
		});
				
		// make API methods available through data and event
		this.container.data('everslider', this.api);
		this.container.bind('everslider', function(e, method, param){	
			if(method in self.api) {
				self.api[method](param);
			}
			return false;
		});
		
		// when slides ready
		window.setTimeout(function(){
			// assign ready class to the container
			self.container.addClass('es-slides-ready');
			// add 'es-after-slide' class to visible slides
			self.getVisibleSlides().addClass('es-after-slide');
			// run slidesReady() callback
			if(typeof self.settings.slidesReady === 'function') {
				self.settings.slidesReady.call(self.container.get(0), self.api);
			}
		}, parseInt(this.settings.fitDelay,10) + parseInt(this.settings.fitDuration,10));
	}
	
	
	// Method to move slider next (API method)
	// ---------------------------------------------------------------------------
	
	Slider.prototype.slideNext = function(){
		if( !this.lock ) {
			this.slideOffset(this.getOffset('next'));
		}
	};
	
	
	// Method to move slider previous (API method)
	// ---------------------------------------------------------------------------
	
	Slider.prototype.slidePrevious = function(){
		if( !this.lock ) {
			this.slideOffset(this.getOffset('prev'));
		}
	};
	
	
	// Method to move slider to specific zero-based position (API method)
	// ---------------------------------------------------------------------------
	
	Slider.prototype.slideTo = function(p) {
		// normalize position for "carousel" mode
		if(this.settings.mode === 'carousel') {
			p = this.total/3 + Math.min(p, this.total/3 - this.visible);
		}
		var position_offset = p - this.offset;
		var direction = position_offset > 0 ? 'next' : 'prev';
		// save original offset
		var offset_tmp = this.offset;
		// shift carousel to requested position N times to get new offset
		for(var n = 0; n < Math.abs(position_offset); n++){
			this.offset = this.getOffset(direction);
		}
		// save received offset
		var offset = this.offset;
		this.offset = offset_tmp;
		// start main carousel transition
		this.slideOffset(offset);
	};
	
	
	// Method to find out if carousel is sliding (API method)
	// ---------------------------------------------------------------------------
	
	Slider.prototype.isSliding = function(){
		return this.lock;
	};

			
	// Method to get subset of slides that are visible at the moment (API method)
	// ---------------------------------------------------------------------------
	
	Slider.prototype.getVisibleSlides = function(){
		return this.list.children().slice(this.offset, this.offset + this.visible);
	};
	
	
	// Method to get offset number based on slide direction
	// ---------------------------------------------------------------------------

	Slider.prototype.getOffset = function(direction){
		// don't move more slides than visible
		var slide_limit = Math.min(this.settings.moveSlides, this.visible);
		
		// get offset for "prev" direction
		if (direction === 'prev') {
			if(this.settings.mode === 'carousel' && this.offset === 0) {
				var p = this.total/3 * (this.slide.width() + this.slide_margin);
				if(this.cssok) {
					if(this.settings.effect !== 'fade') translate(this.list, -p);
				} else {
					if(this.settings.effect !== 'fade') this.list.css('left', -p); 
				}
				return this.total/3 - slide_limit;
			} else if(this.settings.mode === 'circular' && this.offset === 0) {
				return this.total - this.visible;
			} else {
				return this.offset - (this.offset > slide_limit ? slide_limit : this.offset);
			}
		}

		// get offset for "next" direction
		if(direction === 'next') {
			var left = this.total - (this.offset + this.visible);
			if(this.settings.mode === 'carousel' && left === 0) {
				var p = (this.offset - this.total/3) * (this.slide.width() + this.slide_margin);
				if(this.cssok) {
					if(this.settings.effect !== 'fade') translate(this.list, -p);
				} else {
					if(this.settings.effect !== 'fade') this.list.css('left', -p);
				}
				return this.offset - this.total/3 + slide_limit;
			} else if(this.settings.mode === 'circular' && left === 0) {
				return 0;
			} else {
				return this.offset + (left > slide_limit ? slide_limit : left);
			}
		} 
	};

	
	// Method to change slider position by the amount of offset
	// ---------------------------------------------------------------------------
	
	Slider.prototype.slideOffset = function(offset, force){
		// return if offset won't change (force param must be unset)
		if(!force && offset === this.offset) {
			return;
		}
		
		var self = this;
		var unlock = function(){
			// unlock slides
			self.lock = false;
			// save new offset
			self.offset = offset; 
			// callbacks and events that run after slide transition completes
			if(!force) {
				// sync container height
				self.syncContainerHeight();
				// when transition has completed add 'es-after-slide' class to visible items
				self.list.children('.es-after-slide').removeClass('es-after-slide');
				self.getVisibleSlides().removeClass('es-before-slide').addClass('es-after-slide').
				trigger('es-after-slide');
				// run "afterSlide" callback
				if(typeof self.settings.afterSlide === 'function') {
					self.settings.afterSlide.call(self.container.get(0), self.getVisibleSlides());
				}
			}
		};
		
		// set lock
		this.lock = true;
		
		// callbacks and events that run before slide transition begins
		if(!force) {
			// add 'es-before-slide' class to the new visible items as well as trigger custom event
			this.list.children().slice(offset, offset + this.visible).not('.es-after-slide').
			addClass('es-before-slide').trigger('es-before-slide');
			
			// run "beforeSlide" callback
			if(typeof this.settings.beforeSlide === 'function') {
				this.settings.beforeSlide.call(this.container.get(0), this.getVisibleSlides());
			}
		}
		
		// update pagination
		if(this.settings.pagination && this.settings.mode !== 'carousel') {
			var slide_limit = Math.min(this.settings.moveSlides, this.visible);
			var active_page = Math.ceil(offset / slide_limit);
			this.container.find('.es-pagination a:eq('+active_page+')').addClass('es-active').
			siblings().removeClass('es-active');
		}
		
		// update navigation 
		this.updateNavigation(offset);
								
		// get pixel offset by offset number
		var pixel_offset = offset * (this.slide.width() + this.slide_margin);
								
		// the code below performs main carousel transition using either slide or fade effect;
		// code is split into two blocks: if browser is CSS3 compliant and if it's not, in which case it uses jQuery $.animate;
		// at the end of transition unlock() function will be executed to run callbacks
		if( this.cssok ) {
			// 'fade' animation effect
			if(this.settings.effect === 'fade') {
				var now_visible = this.getVisibleSlides();
				var next_visible = this.list.children().slice(offset, offset + this.visible);
				// when 'fadeDirection' plugin option is set to -1 we reverse fade direction
				if(this.settings.fadeDirection * offset > this.offset * this.settings.fadeDirection) {
					next_visible = Array.prototype.reverse.call(next_visible);
					now_visible = Array.prototype.reverse.call(now_visible);
				}
				// hide currently visible slides using delayed fade-out animation
				$.each(now_visible, function(n){
					setTransition($(this), 'opacity', 
					self.settings.fadeDuration, self.settings.fadeEasing, self.settings.fadeDelay * n, function(){
						if(n < self.visible - 1) return; // wait for last slide
						// set opacity = 0 on slides that will be visible next moment; 
						// translate list by amount of offset, then run fade-in animation for next slides 
						next_visible.css('opacity',0); translate(self.list, -pixel_offset, { 
							duration: 0, easing: 'linear', delay: 15, complete: function(){
								$.each(next_visible, function(n){
									setTransition($(this), 'opacity', 
									self.settings.fadeDuration, self.settings.fadeEasing, 
									self.settings.fadeDelay * n, function(){ 
										if(n < self.visible - 1) return; // wait for last slide
										now_visible.add(next_visible).css('opacity', '');
										unlock();
									});
									$(this).css('opacity', 1); 
								});
							} 
						});
					});
					$(this).css('opacity', 0);
				});
			} else {
				// 'slide' animation effector
				translate(this.list, -pixel_offset, {
					duration: this.settings.slideDuration,
					easing: this.settings.slideEasing,
					delay: this.settings.slideDelay,
					complete: unlock
				});				
			}
		} else {
			// 'fade' animation effect (jQuery fallback)
			if(this.settings.effect === 'fade') {
				var now_visible = this.getVisibleSlides();
				var next_visible = this.list.children().slice(offset, offset + this.visible);
				// when 'fadeDirection' plugin option is set to -1 we reverse fade direction
				if(this.settings.fadeDirection * offset > this.offset * this.settings.fadeDirection) {
					next_visible = Array.prototype.reverse.call(next_visible);
					now_visible = Array.prototype.reverse.call(now_visible);
				}
				// hide currently visible slides using delayed fade-out animation
				$.each(now_visible, function(n){
					$(this).stop().delay(self.settings.fadeDelay * n).animate({opacity: 0},
					self.settings.fadeDuration, self.settings.fadeEasing, function(){
						if(n < self.visible - 1) return; // wait for last slide
						next_visible.css('opacity', 0); self.list.delay(10).queue(function(){
							$(this).css('left', -pixel_offset).dequeue();
							$.each(next_visible, function(n){
								$(this).stop().delay(self.settings.fadeDelay * n).animate({opacity: 1},
								self.settings.fadeDuration, self.settings.fadeEasing, function(){
									if(n < self.visible - 1) return; // wait for last slide
									now_visible.add(next_visible).css('opacity', '');
									unlock();
								});
							});						
						});
					});
				});
			} else {
				// 'slide' animation effect (jQuery fallback)
				this.list.stop().delay(this.settings.slideDelay).animate({left: -pixel_offset},
				this.settings.slideDuration, this.settings.slideEasing, unlock);
			}
		} // this.cssok
		
	};
	
					
	// Method to resize slides when browser width is changed
	// ---------------------------------------------------------------------------
	
	Slider.prototype.resizeSlides = function(){		
		// lock resize
		this.lock = true;
						
		// get number of slides that would fit into container viewport
		this.visible = this.container.width()/(this.slide_width + this.slide_margin);
		if (this.visible % 1 === 0 || this.visible % 1 < 0.5)
		{
		    //this.visible = Math.floor(this.visible) > 0 ? Math.floor(this.visible) : 1;
		    //} else {
			this.visible = Math.ceil(this.visible);
		}
									
		// calculate slide size using new "visible" value
		var width = (this.container.width() + this.slide_margin)/this.visible - this.slide_margin;
		var height = this.slide_ratio * width;
		var size = {width: Math.round(width)};
		if(this.settings.itemKeepRatio) {
			size.height = Math.round(height);
		}
		
		// correct slide list offset
		if(this.offset > 0) {
			// make sure offset number is correct after new "visible" value
			if(this.offset + this.visible > this.total) {
				this.offset = this.total - this.visible;
			}
			var pixel_offset = this.offset * (width + this.slide_margin);
			if(this.cssok) {
				translate(this.list, -pixel_offset);
			} else {
				this.list.css('left', -pixel_offset);
			}
		}
		
		// prepare to animation
		var self = this;
		var duration = this.settings.fitDuration;
		var	easing = this.settings.fitEasing;
		var unlock = function(){
			self.lock = false;
			self.syncContainerHeight();
		};

		// run animation to change slides size
		this.list.children().each(function(){
			if(self.cssok){
				// if width won't change run "unlock()" immediately
				if($(this).width() === Math.round(width)) {
					unlock(); 
				} else {				
					setTransition($(this), 'width height', duration, easing, unlock);
					$(this).css(size);
				}
			} else {
				$(this).stop().animate(size, duration, easing, unlock);
			}
		});
		
		// update pagination
		this.updatePagination();
	};
	
	
	// Method to sync container height with the highest visible slide
	// ---------------------------------------------------------------------------
	
	Slider.prototype.syncContainerHeight = function(){
		if(this.settings.syncHeight && !this.settings.itemKeepRatio) {
			// find highest visible slide
			var max_height = 0;
			$.each(this.getVisibleSlides(), function(){
				if($(this).height() > max_height) {
					max_height = $(this).height();
				}
			});
			// animate container height
			var duration = this.settings.syncHeightDuration, 
				easing = this.settings.syncHeightEasing;
			if(this.cssok) {
				setTransition(this.container, 'height', duration, easing);
				this.container.css('height', max_height);
			} else {
				this.container.stop().animate({height: max_height}, duration, easing);
			}
		}
	};
	
	
	// Method to create/update pagination bullets
	// ---------------------------------------------------------------------------
	
	Slider.prototype.updatePagination = function(){
		// do nothing if pagination is not enabled
		if( !this.settings.pagination || this.settings.mode === 'carousel') {
			return;
		}
		
		// calculate how many pages we need to display
		var self = this;
		var slide_limit = Math.min(this.settings.moveSlides, this.visible);
		var total_pages = Math.ceil(this.total * 2 / (slide_limit + this.visible));
					
		// re-add page bullets
		var pagination = this.container.find('.es-pagination').empty();
		for(var i = 0; i < total_pages; i++) {
			$('<a href="#">' + i + '</a>').click(
				(function(index){
					return function(e) {
						// check if not locked
						if(self.lock) { return; }
						// offset cannot be more then "total - visible" slides!
						var offset =  Math.min(index * slide_limit, self.total - self.visible);
						self.slideOffset(offset);
						e.preventDefault();
					};
				})(i)
			).appendTo(pagination);
		}
		
		// figure out active bullet
		var active_page = Math.ceil(this.offset / slide_limit);
		pagination.find('a:eq('+active_page+')').addClass('es-active').siblings().removeClass('es-active');
	};
	
	
	// Method to update navigation controls
	// ---------------------------------------------------------------------------
	
	Slider.prototype.updateNavigation = function(offset){
		// add classes to navigation to indicate reaching first/last slide
		if(this.settings.navigation && this.settings.mode === 'normal') {
			var	navigation = this.container.find('.es-navigation a');
			// add classes on first/last slide
			navigation.removeClass('es-first es-last');
            if(offset === 0) { 
                navigation.filter('.es-prev').addClass('es-first');
            }
			if(offset === this.total - this.visible){ 
                navigation.filter('.es-next').addClass('es-last'); 
            }
		}
	};
	
	
	// Method to enable touchSwipe functionality
	// ---------------------------------------------------------------------------
	
	Slider.prototype.enableTouchSwipe = function(){
		var self = this, swipe = false;
		var touch_x = 0, touch_y = 0, pixel_offset = 0;
		
		var swipeStart = function(e){
			var event = e;
			if(e.type.indexOf('touch') === 0) {
				event = e.originalEvent.changedTouches[0];
			}
			// start swipe if not locked
			if( !self.lock ) {
				swipe = true;
				// save touch point position
				touch_x = event.pageX; 
				touch_y = event.pageY;
				// save slides list position
				pixel_offset = getPixelOffset(self.list, self.cssok).left;
				// bind move handler
				self.container.bind('mousemove touchmove', swipeMove);
				// add grab class to container on swipe start
				self.container.addClass('es-swipe-grab');
			}
		};
										
		var swipeMove = function(e) {
			var event = e;
			if(e.type.indexOf('touch') === 0) {
				event = e.originalEvent.changedTouches[0];
			}
			// get distance from touch point to current pointer position
			var swipe_x = event.pageX - touch_x;
			var	swipe_y = event.pageY - touch_y;
			// check if threshold is exceeded
			if(Math.abs(swipe_x) < self.settings.swipeThreshold) {
				// apply visual offset only for "slide" effect
				if(self.settings.effect === 'slide'){
					if(self.cssok) {
						translate(self.list, pixel_offset + swipe_x);
					} else {
						self.list.css('left', pixel_offset + swipe_x);
					}
				}
			} else {
				// get swipe direction
				var swipe_direction = (swipe_x > 0) ? 'prev' : 'next';
				var offset = self.getOffset(swipe_direction);
				// main carousel transition
				self.slideOffset(offset);
				// remove move handler
				self.container.unbind('mousemove touchmove', swipeMove);
			}
			// check if vertical swipe should scroll page
			if( !self.settings.swipePage ) {
				e.preventDefault();
			}
		};
		
		var swipeEnd = function(){
			if( swipe ){
				// if carousel has moved slightly but threshold has not exceeded return it to previous position
				if(!self.lock && pixel_offset !== getPixelOffset(self.list, self.cssok).left) {
					self.slideOffset(self.offset, true);
				}
				// unbind move handler
				self.container.unbind('mousemove touchmove', swipeMove);
				swipe = false;
				// remove grab class from container on swipe end
				self.container.removeClass('es-swipe-grab');
			}
		};
		
		// register event listeners
		this.container.bind('mousedown touchstart', swipeStart);
		$('body').bind('mouseup touchend touchcancel', swipeEnd);
		
		// disable dragging
		this.container.bind('dragstart', function(e) {
			e.preventDefault();
		});
	};
	
	
	// Method to enable mousewheel
	// ---------------------------------------------------------------------------
	
	Slider.prototype.enableMouseWheel = function(){
		// check if mousewheel plugin is loaded
		if(typeof $.fn.mousewheel !== 'function') {
			return;
		}			
		// bind to mousewheel event
		var self = this;
		this.container.bind('mousewheel', function(e,delta){				
			if(delta > 0) {
				self.slidePrevious();
			} else {
				self.slideNext();
			}
			e.preventDefault();
		});
	};
	
	
	// Method to enable keyboard navigation
	// ---------------------------------------------------------------------------
	
	Slider.prototype.enableKeyboard	= function(){
		var self = this;
		$(document).bind('keydown', function(e){
			if(e.which === 39) {
				self.slideNext();
			} else if(e.which === 37) {
				self.slidePrevious();
			}
		});
	};
	
	
	// Method to enable ticker (autoplay)
	// ---------------------------------------------------------------------------
	
	Slider.prototype.enableTicker = function(){
		var self = this, first_run = true, ticker_timer, timeout;
		var delay = 0, duration = 0, ticker_timeout = parseInt(this.settings.tickerTimeout,10);
		
		// get delay and duration for animation effect
		if(this.settings.effect === 'fade') {
			delay = parseInt(this.settings.fadeDelay,10);
			duration = parseInt(this.settings.fadeDuration,10);
		} else {
			delay = parseInt(this.settings.slideDelay,10);
			duration = parseInt(this.settings.slideDuration,10);
		}
		
		// functions to start ticker
		this.tickerPlay = function(){
			this.container.find('.es-ticker a').hide().filter('.es-pause').show();
			// calculate timeout
			if( first_run ) {
				timeout = ticker_timeout;
			} else {
				if(self.settings.effect === 'fade') {
					timeout = ((self.visible - 1) * delay + self.visible * duration) + ticker_timeout;
				} else {
					timeout = (delay + duration) + ticker_timeout;
				}
			}
			// start timer
			window.clearInterval(ticker_timer);
			ticker_timer = window.setInterval(function(){
				self.slideNext();
				// after first run restart timer with new timeout
				if( first_run ) {
					first_run = false;
					self.tickerPlay();
				}
			}, timeout);
		};
		
		// function to pause ticker
		this.tickerPause = function(){
			this.container.find('.es-ticker a').hide().filter('.es-play').show();
			// stop timer
			window.clearInterval(ticker_timer);
			first_run = true;
		};
		
		// create play/pause controls
		this.container.append('<div class="es-ticker"></div>');
		$('<a href="#" class="es-play">' + this.settings.tickerPlay + '</a>').click(function(e){
			self.tickerPlay();
			e.preventDefault();
		}).appendTo(this.container.find('.es-ticker'));
		$('<a href="#" class="es-pause">' + this.settings.tickerPause + '</a>').click(function(e){
			self.tickerPause();
			e.preventDefault();
		}).appendTo(this.container.find('.es-ticker'));
		
		// pause ticker on hover (with delay)
		if(this.settings.tickerHover) {
			var hover_timer = 0;
			this.container.hover(function(){
				window.clearTimeout(hover_timer);
				hover_timer = window.setTimeout(function(){
					self.tickerPause();
				}, self.settings.tickerHoverDelay);
			}, function(){
				window.clearTimeout(hover_timer);
				hover_timer = window.setTimeout(function(){
					self.tickerPlay();
				}, self.settings.tickerHoverDelay);
			});
		}
		
		// stop ticker
		this.tickerPause();
		
		// start ticker if needed
		if(this.settings.tickerAutoStart) {
			this.tickerPlay();
		}
	};
	
	
	// ---------------------------------------------------------------------------
	// Register jQuery plugin
	// ---------------------------------------------------------------------------
	
	$.fn.everslider = function(c) {
		// default settings
		var s = $.extend({
			mode: 'normal',						// carousel mode - 'normal', 'circular' or 'carousel'
			effect: 'slide',					// animation effect - 'slide' or 'fade'
			useCSS: true,						// set false to disable CSS3 transitions
			itemWidth: false,					// slide width, px (or false for css value)
			itemHeight: false,					// slide height, px (or false for css value)
			itemMargin: false,					// slide margin, px (or false for css value)
			itemKeepRatio: true,				// during resize always retain side ratio of a slide
			maxWidth: '100%',					// max container width, px or %
			maxVisible: 0,						// show only N slides (overrides maxWidth, 0 to ignore)
			moveSlides: 1,						// number of slides to move or 'auto' to move all visible
			slideDelay: 0,						// slide effect initial delay, ms
			slideDuration: 500,					// slide effect duration, ms
			slideEasing: 'swing',				// slide effect easing
			fadeDelay: 200,						// fade effect delay, ms
			fadeDuration: 500,					// fade effect duration, ms
			fadeEasing: 'swing',				// fade effect easing
			fadeDirection: 1,					// 1 is default, set -1 to start fade from opposite side
			fitDelay: 300,						// slides fit (carousel resize) delay, ms
			fitDuration: 200,					// slides fit (carousel resize) duration, ms
			fitEasing: 'swing',					// slides fit (carousel resize) easing
			syncHeight: false,					// sync carousel height with largest visible slide
			syncHeightDuration: 200,			// carousel height sync duration, ms
			syncHeightEasing: 'swing',			// carousel height sync easing
			navigation: true,					// enable prev/next navigation
			nextNav: '<span>Next</span>',		// next navigation control, text/html
			prevNav: '<span>Previous</span>',	// previous navigation control, text/html
			pagination: true,					// enable pagination (only 'normal' or 'circular' mode)
			touchSwipe: true,					// enable touchSwipe
			swipeThreshold: 50,					// pixels to exceed to start slide transition
			swipePage: false,					// allow touchswipe to scroll page also
			mouseWheel: false,					// enable mousewheel (requires jquery-mousewheel plugin)
			keyboard: false,					// enable keyboard left/right key navigation
			ticker: false,						// enable ticker ('circular' or 'carousel' mode)
			tickerTimeout: 2000,				// ticker timeout, ms
			tickerAutoStart: true,				// start ticker when plugin loads
			tickerPlay: '<span>Play</span>',	// ticker play control, text/html
			tickerPause: '<span>Pause</span>',	// ticker pause control, text/html
			tickerHover: false,					// pause ticker on mousehover
			tickerHoverDelay: 300,				// delay before ticker will pause on mousehover
			slidesReady: function(){},			// slider ready callback
			beforeSlide: function(){},			// before slide callback
			afterSlide: function(){}			// after slide callback
		}, c);
			
		return this.each(function(){
			// create slider instance
			new Slider(this, s);
		});
	};

})(jQuery);