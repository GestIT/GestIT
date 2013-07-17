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
    this.copyMods(event);
    this.copyCommon(event);
    this.identifier = event.pointerId;
    this.radiusX = event.width;
    this.radiusY = event.height;
    this.rotationAngle = event.rotation;
    this.force = event.pressure;
  };
  
  this.copyFromTouch = function (parentevent, touchevent) {
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

function listenTouch(element) {
  var d = typeof(element) == 'string' ? document.getElementById(element) : element;
  var ret = new FusionSensor();
  if (navigator.msMaxTouchPoints) {
    d.style.msTouchAction = 'none';
    var MShandler = function (e) { 
      var ret = e.pointerType == 'touch' ? new TouchEvent() : null; 
      if (ret) {
        e.preventDefault();
        ret.copyFromMSPointer(e);
        return [ ret ];
      }
      return [];
    };
    ret.listen(d, TouchFeature.TouchDown, 'MSPointerDown', MShandler);
    ret.listen(d, TouchFeature.TouchMove, 'MSPointerMove', MShandler);
    ret.listen(d, TouchFeature.TouchUp, 'MSPointerUp', MShandler);
    d.addEventListener('MSHoldVisual', function (event) {
        event.preventDefault();
    });
  } else {
    var THandler = function (e) { 
      var ret = []; 
      e.preventDefault();
      for (var i = 0; i < e.changedTouches.length; i++) {
        var t = new TouchEvent();
        t.copyFromTouch(e, e.changedTouches[i]);
        ret.push(t);
      }
      return ret;
    };
    ret.listen(d, TouchFeature.TouchDown, 'touchstart', THandler);
    ret.listen(d, TouchFeature.TouchMove, 'touchmove', THandler);
    ret.listen(d, TouchFeature.TouchUp, 'touchend', THandler);
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
    var sameid = function (e) { return extractid(e) == id; };
    var tm = new GroundTerm(iterfeature, sameid);
    var tu = new GroundTerm(stopfeature, sameid);
    var drag = new Choice([new Iter(tm), tu]);
    tm.gesture.add(itercomplete);
    tu.gesture.add(stopcomplete);
    var net = drag.toGestureNet(sensor);
    drag.gesture.add(function (e) { net.dispose(); });
  };

  startterm.gesture.add(function (e) { if (!stoplist || stoplist.indexOf(e.evt.identifier) == -1) dragid(extractid(e.evt)); });
  return startterm.toGestureNet(sensor);
}

function pinchAndZoom(pzevt, pzendevt) {
  var id1 = null, id2 = null;
  var td1 = new GroundTerm(TouchFeature.TouchDown, function () { return id1 == null; });
  var td2 = new GroundTerm(TouchFeature.TouchDown, function () { return id2 == null; });

  var pts = {};
  td1.gesture.add(function (e) { pts = {}; id1 = e.evt.identifier; pts[id1] = { x: e.evt.clientX, y: e.evt.clientY }; });
  td2.gesture.add(function (e) { id2 = e.evt.identifier; pts[id2] = { x: e.evt.clientX, y: e.evt.clientY }; });
    
  var trigger = function () {
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
  
  var start = new Sequence([ td1, td2, new Choice([ new Parallel([ new Iter(tm1), new Iter(tm2) ]), tu1, tu2]) ]);
  tm1.gesture.add(function (e) { pts[id1] = { x: e.evt.clientX, y: e.evt.clientY, id: e.evt.identifier }; trigger(); });
  tm2.gesture.add(function (e) { pts[id2] = { x: e.evt.clientX, y: e.evt.clientY, id: e.evt.identifier }; trigger(); });

  start.gesture.add(function (e) { id1 = null; id2 = null; pzendevt() });
  return start;
}

