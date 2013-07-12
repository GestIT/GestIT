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


function multiseqnet(startterm, extractid, iterfeature, itercomplete, stopfeature, stopcomplete, sensor) {
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

  startterm.gesture.add(function (e) { dragid(extractid(e.evt)); });
  return startterm.toGestureNet(sensor);
}
