function SensorEventArgs(feature, evt) {
  this.featureType = feature;
  this.event = e;
}

function arrayRemove(a, e) {
  var idx = a.indexOf(e);
  var ret = idx == -1 ? a : new Array();
  if (idx > -1) {
    for (var i = 0; i < a.length; i++)
      if (i != idx) ret.push(a[i]);
  }
  return ret;
}

function Event() {
  var listeners = new Array();
  this.add = function(listener) { var o = { f: listener }; listeners = listeners.slice(0, listeners.length); listeners.push(o); return o; };
  this.remove = function(listener) { listeners = arrayRemove(listeners, listener); };
  this.trigger = function(sender, evt) {
    var oldlisteners = listeners;
    for (var i = 0; i < oldlisteners.length; i++)
      oldlisteners[i].f(evt);
  }
  this.publish = this;
}

var ISensorPrototype = { 'sensorEvents': null };

var __tokenId = 0;
function Token() {
  this.id = __tokenId++;
}

function GestureNet() {
  this.completed = null;
  this.completion = null;
  this._init = function () {
    var o = this;
    var completionEvent = new Event();
    this.completion = completionEvent.publish;
    this.completed = function (e, t) { completionEvent.trigger(o, { 'evt': e, 'tokens': t}); };
    this.dispose = function () { o.clearTokens(); };
  }
  this.front = function () { return new Array(); };
  this.addTokens = function (tokens) { };
  this.removeTokens = function (tokens) { };
  this.clearTokens = function () {};
  this.dispose = null;
}

function GestureExpr() {
  this.gestured = null;
  this.gesture = null;
  this.children = new Array();
  this._init = function () {
    var o = this;
    var gestureEvent = new Event();
    this.gestured = function (e) { gestureEvent.trigger(o, e); }
    this.gesture = gestureEvent.publish;
  }
  this.toNet = function (s) { return null; };
  this.toInternalGestureNet = function (s) {
    var o = this;
    var net = this.toNet(s);
    net.completion.add(function (e) { o.gestured(e.evt); });
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
  var o = this;
  this._init();
  this.feature = f;
  this.predicate = p;
  
  this.toNet = function (s) { return new GroundTermNet(o, s); };
  this.children = [];
}
GroundTerm.prototype = new GestureExpr();

function GroundTermNet(exp, sensor) {
  var o = this;
  this._init();
  var tokens = new Array();
  var handler = null;
  var handle = function(event) {
    if (exp.feature == event.featureType) {
      if (!exp.predicate || exp.predicate(event.event)) {
        var oldtokens = tokens;
        o.clearTokens();
        o.completed(event, oldtokens);
      }
    }
  };
  this.front = function () { return [ o ]; };
  this.addTokens = function (ts) {
    for (var i = 0; i < ts.length; i++)
      tokens.push(ts[i]);
    if (handler == null)
      handler = sensor.sensorEvents.add(handle);
  };
  this.removeTokens = function(ts) {
    for (var i = 0; i < ts.length; i++)
      tokens = arrayRemove(tokens, ts[i]);
    if (tokens.length == 0) o.clearTokens();
  };
  this.clearTokens = function() {
    tokens = new Array();
    if (handler != null) {
      sensor.sensorEvents.remove(handler);
      handler = null;
    }
  };
}
GroundTermNet.prototype = new GestureNet();

function OperatorNet(subnets) {
  var o = this;
  this._init();
  var fronteer = null;
  this.addTokens = function (ts) {
    if (fronteer == null) fronteer = o.front();
    var f = fronteer;
    for (var i = 0; i < f.length; i++)
      f[i].addTokens(ts);
  };
  this.removeTokens = function (ts) {
    for (var i = 0; i < subnets.length; i++)
      subnets[i].removeTokens(ts);
  };
  this.clearTokens = function () {
    for (var i = 0; i < subnets.length; i++)
      subnets[i].clearTokens();
  };
}
OperatorNet.prototype = new GestureNet();

function Sequence(subexprs) {
  this._init();
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
Sequence.prototype = new GestureExpr();

function Parallel(subexprs) {
  this._init();
  this.children = subexprs;

  this.toNet = function (s) {
    var subnets = subexprs.map(function (x) { return x.toInternalGestureNet(s); });
    var net = new OperatorNet(subnets);
    net.front = function () { 
      var fs = subnets.map(function (x) { return x.front(); });
      var ret = new Array();
      for (var i = 0; i < fs.length; i++)
        for (var j = 0; j < fs[i].length; j++)
          ret.push(fs[i][j]);
      return ret;
    };
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
Parallel.prototype = new GestureExpr();

function Choice(subexprs) {
  this._init();
  this.children = subexprs;
  this.toNet = function (s) {
    var subnets = subexprs.map(function (x) { return x.toInternalGestureNet(s); });
    var net = new OperatorNet(subnets);
    net.front = function () { 
      var fs = subnets.map(function (x) { return x.front(); });
      var ret = new Array();
      for (var i = 0; i < fs.length; i++)
        for (var j = 0; j < fs[i].length; j++)
          ret.push(fs[i][j]);
      return ret;
    };
    for (var i = 0; i < subnets.length; i++) {
      subnets[i].completion.add((function (n) { return function (e) {
        for (var i = 0; i < subnets.length; i++)
          if (i != n) subnets[i].removeTokens(e.tokens);
        net.completed(e);
      }})(i));
    };
    return net;
  };  
}
Choice.prototype = new GestureExpr();

function Iter(x) {
  var o = this;
  this._init();
  this.children = [ x ];
  this.toNet = function (s) {
    var subnet = x.toInternalGestureNet(s);
    var net = new OperatorNet([subnet]);
    net.front = subnet.front;
    subnet.completion.add(function (e) {
      subnet.addTokens(e.tokens);
      o.gestured(e.evt);
    });
    return net;
  };
}
Iter.prototype = new GestureExpr();
