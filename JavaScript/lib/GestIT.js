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

// A Sensor is simply a map from features to events
// we assume features can be used as keys in a JS object.
var ISensorPrototype = { };

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
    this.completed = function (f, e, t) { completionEvent.trigger(o, { 'feature': f, 'evt': e, 'tokens': t}); };
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
    net.completion.add(function (e) { o.gestured( { 'feature': e.feature, 'evt': e.evt }); });
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
    if (!exp.predicate || exp.predicate(event.event)) {
      var oldtokens = tokens;
      o.clearTokens();
      o.completed(event.feature, event.event, oldtokens);
    }
  };
  this.front = function () { return [ o ]; };
  this.addTokens = function (ts) {
    for (var i = 0; i < ts.length; i++)
      tokens.push(ts[i]);
    if (handler == null)
      handler = sensor[exp.feature].add(handle);
  };
  this.removeTokens = function(ts) {
    for (var i = 0; i < ts.length; i++)
      tokens = arrayRemove(tokens, ts[i]);
    if (tokens.length == 0) o.clearTokens();
  };
  this.clearTokens = function() {
    tokens = new Array();
    if (handler != null) {
      sensor[exp.feature].remove(handler);
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
    var completed = {};
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
    var baseremove = net.removeTokens;
    net.removeTokens = function (ts) {
      baseremove(ts);
      for (var i = 0; i < ts.length; i++) {
        delete(completed[ts[i].id]);
        if (completed[ts[i].id]) completed[ts[i].id] = null; // if delete misbehave
      }
    };
    var mycb = function (e) {
      var comp = new Array();
      var ts = e.tokens;
      for (var i = 0; i < ts.length; i++) {
        var t = ts[i];
        var count = 1 + (completed[t.id] ? completed[t.id] : 0);
        if (count == subnets.length) {
          delete(completed[t.id]);
          if (completed[t.id]) completed[t.id] = null; // if delete misbehave
          comp.push(t);
        } else {
          completed[t.id] = count;
        }
      }
      if (comp.length)
        net.completed({ 'feature': e.feature, 'evt': e.evt, 'tokens': comp });
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

// Utils

function FusionSensor()  {
  var events = new Array();
  this.listen = function (obj, feature, event, filter) {
    var o = this;
    if (!o[feature])
      o[feature] = new Event();
    var r = { 'o': obj, 
              'en': event, 
              'e': 
                (!filter ? function (e) { o[feature].trigger(obj, { 'feature': feature, 'event': e }) } :
                          function (e) { 
                            var evts = filter(e);
                            for (var i = 0; i < evts.length; i++) {
                              o[feature].trigger(obj, { 'feature': feature, 'event': evts[i] });
                            }})
              };
    events.push(r);
    obj.addEventListener(r.en, r.e, false);  
  };
  this.dispose = function () {
    for (var i = 0; i < events.length; i++) {
      var r = events[i];
      r.o.removeEventListener(r.en, r.e, false);
    }
  };
}
