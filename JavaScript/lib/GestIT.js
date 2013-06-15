function SensorEventArgs(feature, evt) {
  this.featureType = feature;
  this.event = e;
}

function Event() {
  var listeners = new Array();
  this.add = function(listener) { listeners.add(listener); };
  this.remove = function(listener) { listeners.remove(listener); }; // FIXME
  this.trigger = function(sender, evt) {
    for (var i = 0; i < listeners.length; i++)
      listeners[i](sender, evt);
  }
  this.publish = this;
}

var ISensorPrototype = { 'sensorEvents': null };

var __tokenId = 0;
function Token() {
  this.id = __tokenId++;
}

function GestureNet() {
  var completionEvent = new Event();
  this.completed = function (e, t) { completionEvent.trigger(this, { 'evt': e, 'tokens': t}); };
  this.completion = completionEvent.publish;
  this.front = function () { return new Array(); };
  this.addTokens = function (tokens) { };
  this.removeTokens = function (tokens) { };
  this.clearTokens = function () {};
  this.dispose = function () { this.clearTokens(); };
}

function GestureExpr() {
  var gestureEvent = new Event();
  this.gestured = function (e) { gestureEvent.trigger(this, e); }
  this.gesture = gestureEvent.publish;
  this.children = new Array();
  this.toNet = function (s) { return null; };
  this.toInternalGestureNet = function (s) {
    var net = this.toNet(s);
    net.completion.add(function (e) { this.gestured(e.evt); });
    return net;
  };
  this.toGestureNet = function (s) {
    var net = this.toInternalGestureNet(s)
    var f = net.front();
    for (var i = 0; i < f.length; i++) 
      f[i].completion.add(function (e) { net.addTokens([ new Token() ]); });
    net.addTokens([new Token()]);
    return net;
  }
}

function GroundTerm(f, p) {
  this.prototype = new GestureExpr();
  this.feature = f;
  this.predicate = p;
  
  this.toNet = function (s) { return new GroundTermNet(this, s); };
  this.children = [];
}

function GroundTermNet(exp, sensor) {
  this.prototype = new GestureNet();
  var tokens = new Array();
  var handler = null;
  var handle = function(event) {
    if (exp.feature == event.featureType) {
      if (!exp.predicate || predicate(event.event)) {
        var oldtokens = tokens;
        this.clearTokens();
        this.completed(event, oldtokens);
      }
    }
  };
  this.front = function () { return [ this ]; };
  this.addTokens = function (ts) {
    for (var i = 0; i < ts; i++)
      tokens.push(ts[i]);
    sensor.sensorEvents.add(handle);
    handler = handle;
  };
  this.removeTokens = function(ts) {
    for (var i = 0; i < ts; i++)
      tokens.remove(ts[i]);  // FIXME
    if (tokens.length == 0) this.clearTokens();
  };
  this.clearTokens = function() {
    tokens = new Array();
    if (handler != null) {
      sensor.sensorEvents.remove(handler);
      handler = null;
    }
  };
}

function OperatorNet(subnets) {
  this.prototype = new GestureNet();
  this.addTokens = function (ts) {
    var f = this.front();
    for (var i = 0; i < f.length; i++)
      f[i].addTokens(ts);
  };
  this.removeTokens = funtion (ts) {
    for (var i = 0; i < subnets; i++)
      subnets[i].removeTokens(ts);
  };
  this.clearTokens = funtion () {
    for (var i = 0; i < subnets; i++)
      subnets[i].clearTokens(ts);
  };
}

function Sequence(subexprs) {
  this.prototype = new GestureExpr();
  this.children = subexprs;
  
  this.toNet = function(s) {
    var subnets = subexprs.map(function (x) { return x.toInternalGestureNet(s); });
    var net = new OperatorNet(subnets);
    net.front = subnets[0].front;
    for (var i = 0; i < subnets.length - 1; i++)
      subnets[i].completion.add((function (subnet) { return function (evt) { subnet.addTokens(evt.tokens); }; })(subnets[i + 1]));
    subnets[subnets.length - 1].completion.add(net.completed);
    return net;
  };
}

function Parallel(subexprs) {
  this.prototype = new GestureExpr();
  this.children = subexprs;

  this.toNet = function (s) {
    var subnets = subexprs.map(function (x) { return x.toInternalGestureNet(s); });
    var net = new OperatorNet(subnets);
    net.front = subnets.map(function (x) { return x.front; });
    var completed = {};
    var mycb = function (e) {
      var comp = new Array();
      var ts = e.tokens;
      for (var i = 0; i < ts.length; i++) {
        var t = ts[i];
        var count = 1 + (completed[t.id] ? completed[t.id] : 0);
        if (count == subnets.length) {
          completed[t.id] = null;
          comp.push(t);
        } else {
          completed[t.id] = count;
        }
      }
      if (comp.length)
        net.completed({'evt': e.evt, 'tokens': comp });
    };
    for (var i = 0; i < subnets.length; i++)
      subnets[i].completion.add(mycb);
    return net;
  };
}

function Choice(subexprs) {
  this.prototype = new GestureExpr();
  this.children = subexprs;
  this.toNet = function (s) {
    var subnets = subexprs.map(function (x) { return x.toInternalGestureNet(s); });
    var net = new OperatorNet(subnets);
    net.front = subnets.map(function (x) { return x.front; });
    for (var i = 0; i < subnets.length; i++) {
      subnets[i].completion((function (n) { function (e) {
        for (var i = 0; i < subnets.length; i++)
          if (i != n) subnets[i].removeTokens(e.tokens);
        net.completed(e);
      }})(i));
    };
    return net;
  };  
}

function Iter(x) {
  this.prototype = new GestureExpr();
  this.children = [ x ];
  this.toNet = function (s) {
    var subnet = x.toInternalGestureNet(s);
    var net = new OperatorNet(subnets);
    this.front = subnet.front;
    subnet.completion.add(function (e) {
      subnet.addTokens(e.tokens);
      this.gestured(e.evt);
    });
    return net;
  };
}
