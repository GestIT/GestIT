function TouchEvent() {
  this.altKey = false;
  this.ctrlKey = false;
  this.metaKey = false;
  this.shiftKey = false;
  this.type = '';
  this.identifier = 0;
  this.screenX = 0;
  this.screenY = 0;
  this.clientX = 0;
  this.clientY = 0;
  this.pageX = 0;
  this.pageY = 0;
  this.target = null;
  this.radiusX = 0;
  this.radiusY = 0;
  this.rotationAngle = 0;
  this.force = 0;
  
  this.copyMods = function (event) {
    this.altKey = event.altKey;
    this.ctrlKey = event.ctrltKey;
    this.metaKey = event.metaKey;
    this.shiftKey = event.shiftKey;
    this.type = event.type;
  }
  
  this.copyCommon = function (event) {
    this.screenX = event.screenX;
    this.screenY = event.screenY;
    this.clientX = event.clientX;
    this.clientY = event.clientY;
    this.pageX = event.pageX;
    this.pageY = event.pageY;
    this.target = event.target;
  }
  
  this.copyFromMSPointer = function (event) {
    this.srcEvent = event;
    this.copyMods(event);
    this.copyCommon(event);
    this.identifier = event.pointerId;
    this.radiusX = event.width;
    this.radiusY = event.height;
    this.rotationAngle = event.rotation;
    this.force = event.pressure;
  };
  
  this.copyFromTouch = function (parentevent, touchevent) {
    this.srcEvent = parentevent;
    this.copyMods(parentevent);
    this.copyCommon(touchevent);
    this.identifier = touchevent.identifier;
    this.radiusX = touchevent.radiusX;
    this.radiusY = touchevent.radiusY;
    this.rotationAngle = touchevent.rotationAngle;
    this.force = touchevent.force;
  };
}

var TouchFeature = {
  TouchDown: 0,
  TouchMove: 1,
  TouchUp: 2
}

function listenTouch(element, capturing) {
  var maxid = 0;
  var idmap = {};
  var d = typeof(element) == 'string' ? document.getElementById(element) : element;
  var ret = new FusionSensor();
  if (navigator.msMaxTouchPoints) {
    d.style.msTouchAction = 'none';
    var MShandler = function (f) { return function (e) { 
      var ret = e.pointerType == 'touch' || e.pointerType == 2 ? new TouchEvent() : null; // == 2 needed by winphone and (I guess IE < 11)
      if (ret) {
        e.preventDefault();
        ret.copyFromMSPointer(e);
        if (f == TouchFeature.TouchDown) idmap[ret.identifier] = maxid++;
        ret.identifier = idmap[ret.identifier];
        if (f == TouchFeature.TouchUp) delete idmap[ret.identifier];
        return [ ret ];
      }
      return [];
    }; };
    ret.listen(d, TouchFeature.TouchDown, 'MSPointerDown', capturing ? true : false, MShandler(TouchFeature.TouchDown));
    ret.listen(d, TouchFeature.TouchMove, 'MSPointerMove', capturing ? true : false, MShandler(TouchFeature.TouchMove));
    ret.listen(d, TouchFeature.TouchUp, 'MSPointerUp', capturing ? true : false, MShandler(TouchFeature.TouchUp));
    d.addEventListener('MSHoldVisual', function (event) {
        event.preventDefault();
    });
  } else {
    var THandler = function (f) { return function (e) { 
      var ret = []; 
      e.preventDefault();
      for (var i = 0; i < e.changedTouches.length; i++) {
        var t = new TouchEvent();
        t.copyFromTouch(e, e.changedTouches[i]);
        if (f == TouchFeature.TouchDown) idmap[t.identifier] = maxid++;
        t.identifier = idmap[t.identifier];
        if (f == TouchFeature.TouchUp) delete idmap[t.identifier];
        ret.push(t);
      }
      return ret;
    }; };
    ret.listen(d, TouchFeature.TouchDown, 'touchstart', capturing ? true : false, THandler(TouchFeature.TouchDown));
    ret.listen(d, TouchFeature.TouchMove, 'touchmove', capturing ? true : false, THandler(TouchFeature.TouchMove));
    ret.listen(d, TouchFeature.TouchUp, 'touchend', capturing ? true : false, THandler(TouchFeature.TouchUp));
  }
  d.addEventListener('contextmenu', function (event) {
        event.preventDefault();
  });
  d.addEventListener('selectstart', function (event) {
        event.preventDefault();
  });
  return ret;
}


function multiseqnet(startterm, extractid, iterfeature, itercomplete, stopfeature, stopcomplete, sensor, stoplist) {
  var dragid = function (id) {
    var tm = new GroundTerm(iterfeature, function (e) { 
      return extractid(e) == id && (!stoplist || stoplist.indexOf(id) == -1); });
    var tu = new GroundTerm(stopfeature, function (e) { return extractid(e) == id });
    var notm = new GroundTerm(iterfeature, function (e) { 
      return extractid(e) == id && (!stoplist || stoplist.indexOf(id) != -1); });
    var drag = new Choice([new Iter(tm), tu, notm]);
    tm.gesture.add(itercomplete);
    tu.gesture.add(stopcomplete);
    var net = drag.toGestureNet(sensor);
    drag.gesture.add(function (e) { net.dispose(); });
  };

  startterm.gesture.add(function (e) { dragid(extractid(e.evt)); });
  return startterm.toGestureNet(sensor);
}

var clearPinchAndZoom = null;

function pinchAndZoom(pzevt, pzendevt, sensor, timeout) {
  var id1 = null, id2 = null;
  var td1 = new GroundTerm(TouchFeature.TouchDown, function () { return id1 == null; });
  var td2 = new GroundTerm(TouchFeature.TouchDown, function () { return id1 != null && id2 == null; });
  var tout = new GroundTerm('Timer', function () { return true; });
  tout.gesture.add(function () { id1 = null; id2 = null; });

  var pts = {};
  var ttok = null;
  td1.gesture.add(function (e) { 
                    pts = {}; 
                    id1 = e.evt.identifier;
                    pts[id1] = { x: e.evt.clientX, y: e.evt.clientY }; 
                    ttok = sensor.startTimeout('Timer', e, timeout ? timeout : 0.5); 
                  });
  td2.gesture.add(function (e) { if (ttok) sensor.clearTimeout(ttok); id2 = e.evt.identifier; pts[id2] = { x: e.evt.clientX, y: e.evt.clientY }; });
  var triggered = false;
  var trigger = function () {
    triggered = true;
    var e = {
      p1: pts[id1],
      p2: pts[id2],
      angle: function () {  
               var dx = this.p2.x - this.p1.x;
               var dy = this.p2.y - this.p1.y;
               return (dy ? Math.atan(dx / dy) : 0) + Math.PI / 2 + (dy > 0 ? -Math.PI : 0);
             },
      magnitude: function () {
                   var dx = this.p2.x - this.p1.x;
                   var dy = this.p2.y - this.p1.y;
                   return Math.sqrt(dx*dx + dy*dy);
                 }
    };
    pzevt(e);
  };

  var sameid1 = function (e) { return e.identifier == id1; };
  var tm1 = new GroundTerm(TouchFeature.TouchMove, sameid1);
  var tu1 = new GroundTerm(TouchFeature.TouchUp, sameid1);
  var sameid2 = function (e) { return e.identifier == id2; };
  var tm2 = new GroundTerm(TouchFeature.TouchMove, sameid2);
  var tu2 = new GroundTerm(TouchFeature.TouchUp, sameid2);
  
  var start = new Sequence([ td1, new Choice([ new Sequence([td2, new Choice([ new Parallel([ new Iter(tm1), new Iter(tm2) ]), tu2])]), tu1, tout ]) ]);
  tm1.gesture.add(function (e) {
    pts[id1] = { x: e.evt.clientX, y: e.evt.clientY, id: e.evt.identifier }; 
    trigger(); 
  });
  tm2.gesture.add(function (e) { 
    pts[id2] = { x: e.evt.clientX, y: e.evt.clientY, id: e.evt.identifier };
    trigger(); 
  });

  start.gesture.add(function (e) { id1 = null; id2 = null; if (triggered) pzendevt(); triggered = false; });
  return start.toGestureNet(sensor);
}
