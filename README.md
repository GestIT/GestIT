GestIT
======

Look at demos available at:

* [Javascript](http://gestit.github.io/GestIT)
* [F#](http://www.tryfsharp.org/create/cisterni/GestIT/tutorial/DragAndDrop.fsx) on TryFsharp.org (Silverlight required)

GestIT is an elegant library for reactive programming for .NET and Javascript. The origin of the library has its roots in defining a programming model for defining gestures by composition and moving forward from the current situation where framewoks give precooked gestures such as pinch and zoom. The library, however, can be used to orchestrate an arbitrary set of events in a very natural way.

Just as an example let us consider how drag and drop in traditional programming can be expressed: 

    let md = Place<_,_>(MouseEvents.MouseDown, fun (evt:MouseEventArgs) -> rectContains rect (evt.X, evt.Y))
    let mm = Place<_,_>(MouseEvents.MouseMove)
    let mu = Place<_,_>(MouseEvents.MouseUp)

    let gest = (md |-> fun (_, _, evt) -> markPosition evt)
                |>> (!* (mm |-> fun (_, _, evt) -> moveRect evt))
                |>| mu

You define places where events are handled, each place is active only if a token is present in it. Places are composed using operators, in this example we use sequence (`|>>`), iteration (`!*`) and disable (`|>|`). You can register event handlers to places that get fired whenever a token leaves a place. Tokens flow from a place into another only if a predicate is satisfied.

In our example the place `md` is interested in receiving mouse down events that will be allowed to pass only if the location is contained within a given `rect`. In this case the `markPosition` function gets called. Then an the token moves in  `mm` place which loops onto itself and records mouse move events. Eventually a mouse up event will be received by the `mu` place and this will disable the previous gesture stopping the `mm` iteration.

We used GestIT to implement demos using the LeapMotion sensor as you can see in this [video](http://www.youtube.com/watch?v=QRB0-IiopKA).
