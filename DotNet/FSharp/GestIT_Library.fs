namespace GestIT

/// </member>
/// <member name="N:GestIT">
/// <summary>
/// <para>File:   GestIT_Library.fs</para>
/// <para>Author: Andrea Canciani</para>
/// <para>GestIT provide for gesture recognition.</para>
/// </summary>
type private IgnoreMeTwice () =
  class
  end

/// <summary>
/// Generic sensor interface, to be implemented for publishing events.
/// </summary>
/// <typeparam name="T">The generic 'T type is relative to the feature.</typeparam>
/// <typeparam name="U">The generic 'U type is the information about the event itself.</typeparam>
type ISensor<'T,'U> when 'U :> System.EventArgs =
  abstract Item : 'T -> IEvent<'U> with get

/// <summary>
/// Generic type representing a Petri's Net token.
/// </summary>
type Token = obj

/// <summary>
/// It represents the model of a net created from a gesture definition.
/// When one or more tokens come to the end of a net, it is able to notify its completion through sensor events.
/// </summary>
/// <typeparam name="T">The generic 'T type is relative to the feature.</typeparam>
/// <typeparam name="U">The generic 'U type is the information about the event itself.</typeparam>
[<AbstractClass>]
type GestureNet<'T,'U> when 'U :> System.EventArgs () =
  let completionEvent = new Event<'T * 'U * seq<Token>>()
  member this.Completed(f,e,t) = completionEvent.Trigger(f,e,t)
  member this.Completion = completionEvent.Publish
  abstract member Front: GestureNet<'T,'U> list
  abstract member AddTokens: Token seq -> unit
  abstract member RemoveTokens: Token seq -> unit
  abstract member ClearTokens: unit -> unit
  interface System.IDisposable with
    member this.Dispose() = this.ClearTokens()

/// <summary>
/// It represents the gesture expression, which has to be transformed into a GestureNet for allowing the token transition.
/// </summary>
/// <typeparam name="T">The generic 'T type is relative to the feature.</typeparam>
/// <typeparam name="U">The generic 'U type is the information about the event itself.</typeparam>
[<AbstractClass>]
type GestureExpr<'T,'U> when 'U :> System.EventArgs () =
  let gestureEvent = new Event<_>()
  member this.Gestured(f,e) = gestureEvent.Trigger(this, f, e)
  [<CLIEvent>]
  member this.Gesture = gestureEvent.Publish

  abstract member Children : seq<GestureExpr<'T, 'U>>
  abstract member ToNet: ISensor<'T,'U> -> GestureNet<'T,'U>
  member this.ToInternalGestureNet(s) =
    let net = this.ToNet(s)
    net.Completion.Add(fun (f,e,ts) -> this.Gestured(f,e))
    net
  /// <summary>
  /// It permits to relate Petri Net with the sensor. It is necessary in order to recognize the gesture.
  /// </summary>
  /// <param name="s">Leap sensor.</param>
  /// <returns>The net.</returns>
  member this.ToGestureNet(s) =
    let net = this.ToInternalGestureNet(s)
    for subn in net.Front do
      subn.Completion.Add(fun _ -> net.AddTokens([new Token()]))
    net.AddTokens([new Token()])
    net

/// <summary>
/// It represents a predicate, which is a condition associated to a single Ground Term.
/// If validated, it permits the transition of the token to the next Ground Term.
/// </summary>
type Predicate<'U> = delegate of 'U -> bool

/// <summary>
/// It represents a generic Ground Term, which is a combination of a feature (for example a touch start) and a predicate.
/// As subclass of GestureExpr, even a single GroundTerm can notify with an event the validation of its predicate.
/// </summary>
/// <typeparam name="T">The generic 'T type is relative to the feature.</typeparam>
/// <typeparam name="U">The generic 'U type is the information about the event itself.</typeparam>
type GroundTerm<'T,'U> when 'U :> System.EventArgs (f:'T, ?p:Predicate<'U>) =
  inherit GestureExpr<'T,'U>()
  /// <summary>
  /// Assigns a feature and a function that will be the Predicate.
  /// </summary>
  /// <param name="f">Feature.</param>
  /// <param name="p">Function that will be the Predicate.</param>
  new(f,p) = GroundTerm<_,_>(f, new Predicate<_>(p))

  /// </member>
  /// <member name="M:GestIT.SensorEventArgs`2.#ctor(`0,`1)">
  /// <summary>
  /// Assigns feature and event.
  /// </summary>
  /// <param name="f">Feature.</param>
  /// <param name="p">Predicate.</param>
  member private x.IgnoreMe() = ()

  member this.Feature = f
  member this.Predicate = p
  override this.ToNet(s) = new GroundTermNet<_,_>(this, s) :> GestureNet<_,_>
  override this.Children = Seq.empty

and private GroundTermNet<'T,'U> when 'U :> System.EventArgs (exp:GroundTerm<'T,'U>, sensor:ISensor<'T,'U>) as this =
  inherit GestureNet<'T,'U>()

  let mutable tokens = new System.Collections.Generic.HashSet<Token>()
  let mutable handler:System.IDisposable = null

  let handle (event:'U) =
    let p =
      match exp.Predicate with
        | None -> true
        | Some d -> d.Invoke(event)
    if p then
      let oldtokens = tokens
      this.ClearTokens()
      this.Completed(exp.Feature, event, oldtokens)

  override this.Front = [this]

  override this.AddTokens(ts) =
    for t in ts do
      tokens.Add(t) |> ignore
    if handler = null then
      handler <- sensor.[exp.Feature].Subscribe(handle)

  override this.RemoveTokens(ts) =
    for t in ts do
      tokens.Remove(t) |> ignore
    if tokens.Count = 0 then
      this.ClearTokens()

  override this.ClearTokens() =
    tokens <- new System.Collections.Generic.HashSet<Token>()
    if handler <> null then
      handler.Dispose()
      handler <- null

/// <summary>
/// It represents a generic operator, which is the element that connects two or more subnets between each others.
/// Please note that every subnet could be a single Ground Term.
/// </summary>
/// <typeparam name="T">The generic 'T type is relative to the feature.</typeparam>
/// <typeparam name="U">The generic 'U type is the information about the event itself.</typeparam>
[<AbstractClass>]
type private OperatorNet<'T,'U> when 'U :> System.EventArgs ([<System.ParamArray>] subnets:GestureNet<'T,'U>[]) =
  inherit GestureNet<'T,'U>()

  override this.AddTokens(ts) =
    for n in this.Front do
      n.AddTokens(ts)

  override this.RemoveTokens(ts) =
    for n in subnets do
      n.RemoveTokens(ts)

  override this.ClearTokens() =
    for n in subnets do
      n.ClearTokens()

/// <summary>
/// It represents the Sequence operator.
/// </summary>
/// <typeparam name="T">The generic 'T type is relative to the feature.</typeparam>
/// <typeparam name="U">The generic 'U type is the information about the event itself.</typeparam>
type Sequence<'T,'U> when 'U :> System.EventArgs ([<System.ParamArray>] subexprs:GestureExpr<'T,'U>[]) =
  inherit GestureExpr<'T,'U>()

  /// </member>
  /// <member name="M:GestIT.Sequence`2.#ctor(GestIT.GestureExpr{`0,`1}[])">
  /// <summary>
  /// Assigns subexpressions to the operator.
  /// </summary>
  /// <param name="subexprs">Subexpressions of the operator.</param>
  member private this.IgnoreMe() = ()

  override this.Children = subexprs |> Array.toSeq

  /// <summary>
  /// This method is not intended to be used outside.
  /// </summary>
  /// <param name="s"></param>
  /// <returns></returns>
  override this.ToNet(s) =
    let subnets = subexprs |> Array.map (fun x -> x.ToInternalGestureNet(s))
    let net = { new OperatorNet<_,_>(subnets) with
                override this.Front = subnets.[0].Front
                } :> GestureNet<_,_>
    for i = 0 to subnets.Length - 2 do
      subnets.[i].Completion.Add(fun (f,e,ts) -> subnets.[i+1].AddTokens(ts))
    subnets.[subnets.Length-1].Completion.Add(net.Completed)
    net

/// <summary>
/// It represents the Parallel operator.
/// </summary>
/// <typeparam name="T">The generic 'T type is relative to the feature.</typeparam>
/// <typeparam name="U">The generic 'U type is the information about the event itself.</typeparam>
type Parallel<'T,'U> when 'U :> System.EventArgs ([<System.ParamArray>] subexprs:GestureExpr<'T,'U>[]) =
  inherit GestureExpr<'T,'U>()

  /// </member>
  /// <member name="M:GestIT.Parallel`2.#ctor(GestIT.GestureExpr{`0,`1}[])">
  /// <summary>
  /// Assigns subexpressions to the operator.
  /// </summary>
  /// <param name="subexprs">Subexpressions of the operator.</param>
  member private this.IgnoreMe() = ()

  override this.Children = subexprs |> Array.toSeq

  /// <summary>
  /// This method is not intended to be used outside.
  /// </summary>
  /// <param name="s">.</param>
  /// <returns></returns>
  override this.ToNet(s) =
    let completed = new System.Collections.Generic.Dictionary<_,_>()
    let subnets = subexprs |> Array.map (fun x -> x.ToInternalGestureNet(s))
    let net = { new OperatorNet<_,_>(subnets) with
                override this.Front = subnets |> Seq.map (fun x -> x.Front) |> List.concat
                override this.RemoveTokens(ts) =
                  base.RemoveTokens(ts)
                  for t in ts do
                    completed.Remove(t) |> ignore
              } :> GestureNet<_,_>
    let mycb (f,e,ts) =
      let mutable comp = []
      for t in ts do
        let found,count = completed.TryGetValue(t)
        let count = 1 + (if found then count else 0)
        if count = subnets.Length then
          completed.Remove(t) |> ignore
          comp <- t::comp
        else
          completed.[t] <- count
      if comp <> [] then
        net.Completed(f,e,comp)
    for n in subnets do
      n.Completion.Add(mycb)
    net

/// <summary>
/// It represents the Choice operator.
/// </summary>
/// <typeparam name="T">The generic 'T type is relative to the feature.</typeparam>
/// <typeparam name="U">The generic 'U type is the information about the event itself.</typeparam>
type Choice<'T,'U> when 'U :> System.EventArgs ([<System.ParamArray>] subexprs:GestureExpr<'T,'U>[]) =
  inherit GestureExpr<'T,'U>()

  /// </member>
  /// <member name="M:GestIT.Choice`2.#ctor(GestIT.GestureExpr{`0,`1}[])">
  /// <summary>
  /// Assigns subexpressions to the operator.
  /// </summary>
  /// <param name="subexprs">Subexpressions of the operator.</param>
  member private this.IgnoreMe() = ()

  override this.Children = subexprs |> Array.toSeq

  /// <summary>
  /// This method is not intended to be used outside.
  /// </summary>
  /// <param name="s"></param>
  /// <returns></returns>
  override this.ToNet(s) =
    let subnets = subexprs |> Array.map (fun x -> x.ToInternalGestureNet(s))
    let net = { new OperatorNet<_,_>(subnets) with
                override this.Front = subnets |> Seq.map (fun x -> x.Front) |> List.concat
                } :> GestureNet<_,_>
    for n in subnets do
      n.Completion.Add(fun (f,e,ts) ->
                       for othern in subnets do
                         if othern <> n then
                           othern.RemoveTokens(ts)
                       net.Completed(f,e,ts))
    net

/// <summary>
/// It represents the Iter operator.
/// </summary>
/// <typeparam name="T">The generic 'T type is relative to the feature.</typeparam>
/// <typeparam name="U">The generic 'U type is the information about the event itself.</typeparam>
type Iter<'T,'U> when 'U :> System.EventArgs (x:GestureExpr<'T,'U>) =
  inherit GestureExpr<'T,'U>()

  /// </member>
  /// <member name="M:GestIT.Iter`2.#ctor(GestIT.GestureExpr{`0,`1})">
  /// <summary>
  /// Assigns subexpressions to the operator.
  /// </summary>
  /// <param name="subexprs">Subexpressions of the operator.</param>
  member private this.IgnoreMe() = ()

  override this.Children = seq { yield x }

  /// <summary>
  /// This method is not intended to be used outside.
  /// </summary>
  /// <param name="s"></param>
  /// <returns></returns>
  override this.ToNet(s) =
    let subnet = x.ToInternalGestureNet(s)
    let net = { new OperatorNet<_,_>(subnet) with
                override this.Front = subnet.Front
                } :> GestureNet<_,_>
    subnet.Completion.Add(fun (f,e,ts) ->
                          subnet.AddTokens(ts)
                          this.Gestured(f,e))
    net
