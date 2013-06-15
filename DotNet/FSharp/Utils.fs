/// <summary>
/// <para>File:     Utils.fs</para>
/// <para>Author:   Antonio Cisternino, Marta Martino</para>
/// <para>Utils defines F# shortcuts for a quicker and more compact definition of the gesture expression.</para>
/// </summary>
module GestIT.FSharp
open GestIT

type HandlersSet = System.Collections.Generic.Dictionary<obj, bool>

let private registeredHandlers = new HandlersSet()

let private isExpr(a:#GestureExpr<'T, 'U>) (filter:System.Type) = a.GetType().GetGenericTypeDefinition() = filter.GetGenericTypeDefinition()

let private toSeq(a:#GestureExpr<'T, 'U>) (filter:System.Type) = 
  if a.GetType().GetGenericTypeDefinition() = filter.GetGenericTypeDefinition() then a.Children else seq { yield a :> GestureExpr<'T, 'U> }

/// Defines the Sequence Operator |>>.
let (|>>) (a:GestureExpr<'T, 'U>) (b:GestureExpr<'T, 'U>) = 
  let seqf = typeof<Sequence<'T, 'U>>
  if not(registeredHandlers.ContainsKey(a) || registeredHandlers.ContainsKey(b)) && (isExpr a seqf || isExpr b seqf) then
    let arga = toSeq a seqf
    let argb = toSeq b seqf
    let args = ([ arga; argb] |> Seq.concat) |> Seq.toArray
    new Sequence<'T, 'U>(args)
  else
    new Sequence<'T, 'U>(a, b)

/// Defines the Parallel Operator |=|.
let (|=|) (a:GestureExpr<'T, 'U>) (b:GestureExpr<'T, 'U>) = 
  let parf = typeof<Parallel<'T, 'U>>
  if not(registeredHandlers.ContainsKey(a) || registeredHandlers.ContainsKey(b)) && (isExpr a parf || isExpr b parf) then
    let arga = toSeq a parf
    let argb = toSeq b parf
    let args = ([ arga; argb] |> Seq.concat) |> Seq.toArray
    new Parallel<'T, 'U>(args)
  else
    new Parallel<'T, 'U>(a, b)

/// Defines the Choice Operator |^|.
let (|^|) (a:GestureExpr<'T, 'U>) (b:GestureExpr<'T, 'U>) = 
  let choicef = typeof<Choice<'T, 'U>>
  if not(registeredHandlers.ContainsKey(a) || registeredHandlers.ContainsKey(b)) && (isExpr a choicef || isExpr b choicef) then
    let arga = toSeq a choicef
    let argb = toSeq b choicef
    let args = ([ arga; argb] |> Seq.concat) |> Seq.toArray
    new Choice<'T, 'U>(args)
  else
    new Choice<'T, 'U>(a, b)

/// Defines the Iter Operator !*.
let (!*) (a:GestureExpr<'T, 'U>) =
  if not(registeredHandlers.ContainsKey(a)) && (isExpr a typeof<Iter<'T, 'U>>) then
    a
  else
    new Iter<'T, 'U>(a) :> GestureExpr<'T, 'U>

/// Defines the Disabling Operator |>|. (Notice: it is NOW the equivalent of Choice Operator, so it is like using |^|)
let (|>|) (a:GestureExpr<'T, 'U>) (b:GestureExpr<'T, 'U>) = a |^| b

/// Defines the handler to assign |-> to the GestureExpr.
let (|->) (a:GestureExpr<'T, 'U>) (evt:GestureExpr<'T, 'U>*SensorEventArgs<'T, 'U> -> unit) =
  a.Gesture.Add(evt)
  if not (registeredHandlers.ContainsKey(a)) then registeredHandlers.Add(a, true)
  a