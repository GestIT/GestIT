namespace GestIT.Leap

open Leap

type LeapActivity =
| NewHand of int list
| ActiveHand of int list
| InactiveHand of int list
| NewFinger of int list
| ActiveFinger of int list
| InactiveFinger of int list
| NewTool of int list
| ActiveTool of int list
| InactiveTool of int list

type LeapSensorEventArgs(f:Frame, ?activity) =
  inherit System.EventArgs()

  member this.Frame = f
  member this.Activity = activity

  member this.ActivityHands 
    with get() =
      match activity with
      | Some(NewHand l) | Some(ActiveHand l)-> f.Hands |> Seq.filter (fun h -> l |> List.exists(fun el -> h.Id = el)) |> Seq.toList
      | _ -> []

  member this.ActivityFingers
    with get() =
      match activity with
      | Some(NewFinger l) | Some(ActiveFinger l)-> f.Fingers |> Seq.filter (fun h -> l |> List.exists(fun el -> h.Id = el)) |> Seq.toList
      | _ -> []

  member this.ActivityTools
    with get() =
      match activity with
      | Some(NewTool l) | Some(ActiveTool l)-> f.Tools |> Seq.filter (fun h -> l |> List.exists(fun el -> h.Id = el)) |> Seq.toList
      | _ -> []

type LeapSensor() =
  inherit Listener()

  let controller = new Controller()

  let mutable activeHands : int Set = Set.empty
  let mutable activeFingers : int Set = Set.empty
  let mutable activeTools : int Set = Set.empty

  let initEvt = new Event<System.EventArgs>()
  let connectEvt = new Event<System.EventArgs>()
  let exitEvt = new Event<System.EventArgs>()
  let frameEvt = new Event<LeapSensorEventArgs>()
  let newHandEvt = new Event<LeapSensorEventArgs>()
  let activeHandEvt = new Event<LeapSensorEventArgs>()
  let inactiveHandEvt = new Event<LeapSensorEventArgs>()
  let newFingerEvt = new Event<LeapSensorEventArgs>()
  let activeFingerEvt = new Event<LeapSensorEventArgs>()
  let inactiveFingerEvt = new Event<LeapSensorEventArgs>()
  let newToolEvt = new Event<LeapSensorEventArgs>()
  let activeToolEvt = new Event<LeapSensorEventArgs>()
  let inactiveToolEvt = new Event<LeapSensorEventArgs>()
  
  member this.Controller = controller

  member this.Connect    () = controller.AddListener(this)
  member this.Disconnect () = controller.RemoveListener(this)

  member this.Init = initEvt.Publish
  member this.Connected = connectEvt.Publish
  member this.Exit = exitEvt.Publish
  member this.CurrentFrame = frameEvt.Publish

  member this.NewHand = newHandEvt.Publish
  member this.NewFinger = newFingerEvt.Publish
  member this.NewTool = newToolEvt.Publish
  member this.ActiveHand = activeHandEvt.Publish
  member this.ActiveFinger = activeFingerEvt.Publish
  member this.ActiveTool = activeToolEvt.Publish
  member this.InactiveHand = inactiveHandEvt.Publish
  member this.InactiveFinger = inactiveFingerEvt.Publish
  member this.InactiveTool = inactiveToolEvt.Publish

  override this.Dispose () =
    this.Disconnect() |> ignore
    base.Dispose()
    controller.Dispose()

  override this.OnInit c = initEvt.Trigger(new System.EventArgs())
  override this.OnConnect c = connectEvt.Trigger(new System.EventArgs())
  override this.OnExit c = exitEvt.Trigger(new System.EventArgs())

  override this.OnFrame c =
    let frame = c.Frame()
    frameEvt.Trigger(new LeapSensorEventArgs(frame))
    let processHands () =
      let hs = frame.Hands |> Seq.map (fun h -> h.Id) |> Set.ofSeq
      let nh = hs - activeHands
      let ah = hs |> Set.intersect activeHands
      let dh = activeHands - hs
      if not nh.IsEmpty then newHandEvt.Trigger(new LeapSensorEventArgs(frame, NewHand (nh |> Set.toList)))
      if not ah.IsEmpty then activeHandEvt.Trigger(new LeapSensorEventArgs(frame, ActiveHand (ah |> Set.toList)))
      if not dh.IsEmpty then inactiveHandEvt.Trigger(new LeapSensorEventArgs(frame, InactiveHand (dh |> Set.toList)))
      activeHands <- hs

    let processFingers () =
      let fs = frame.Fingers |> Seq.map (fun h -> h.Id) |> Set.ofSeq
      let nf = fs - activeFingers
      let af = fs |> Set.intersect activeFingers
      let df = activeFingers - fs
      if not nf.IsEmpty then newFingerEvt.Trigger(new LeapSensorEventArgs(frame, NewFinger (nf |> Set.toList)))
      if not af.IsEmpty then activeFingerEvt.Trigger(new LeapSensorEventArgs(frame, ActiveFinger (af |> Set.toList)))
      if not df.IsEmpty then inactiveFingerEvt.Trigger(new LeapSensorEventArgs(frame, InactiveFinger (df |> Set.toList)))
      activeFingers <- fs

    let processTools () =
      let ts = frame.Tools |> Seq.map (fun h -> h.Id) |> Set.ofSeq
      let nt = ts - activeTools
      let at = ts |> Set.intersect activeTools
      let dt = activeTools - ts
      if not nt.IsEmpty then newToolEvt.Trigger(new LeapSensorEventArgs(frame, NewTool (nt |> Set.toList)))
      if not at.IsEmpty then activeToolEvt.Trigger(new LeapSensorEventArgs(frame, ActiveTool (at |> Set.toList)))
      if not dt.IsEmpty then inactiveToolEvt.Trigger(new LeapSensorEventArgs(frame, InactiveTool (dt |> Set.toList)))
      activeTools <- ts

    processHands()
    processFingers()
    processTools()
