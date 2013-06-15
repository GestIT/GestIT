/// <summary>
/// File:   Driver.fs
/// Author: Marta Martino
/// LeapDriver implements a model for the elaboration of LEAP's raw datas.
/// </summary>
namespace LeapDriver
    open System.Windows.Forms
    open System.Drawing
    open System.Collections.Generic
    open System.IO
    open ClonableLeapFrame
    open GestIT
    open Leap

    /// </member>
    /// <member name="N:LeapDriver">
    /// <summary>
    /// <para>File:   Driver.fs</para>
    /// <para>Author: Marta Martino</para>
    /// <para>LeapDriver implements a model for the elaboration of LEAP's raw datas.</para>
    /// </summary>
    type private IgnoreMe () =
      class
      end

(*
    // KEYBOARD features that have to be notified from the sensor //
    type KeyFeatureTypes =
        | KeyDown = 0
        | KeyUp = 1
    
    type KeySensor () =
        inherit UserControl()
        let sensorEvent = new Event<SensorEventArgs<KeyFeatureTypes,KeyEventArgs>>()
        override x.OnKeyDown(e) =
            printfn "SPACE DOWN"
            sensorEvent.Trigger(new SensorEventArgs<_,_>(KeyFeatureTypes.KeyDown, e))
        override x.OnKeyUp(e) =
            printfn "SPACE UP"
            sensorEvent.Trigger(new SensorEventArgs<_,_>(KeyFeatureTypes.KeyUp, e))
        interface ISensor<KeyFeatureTypes,KeyEventArgs> with
            [<CLIEvent>]
            member x.SensorEvents = sensorEvent.Publish
*)

    // MOUSE features that have to be notified from the sensor //
    type MouseFeatureTypes =
        | MouseDown = 0
        | MouseUp = 1
        | MouseMove = 2

    type MouseSensor () =
        inherit UserControl()
        let mutable down = false
        let sensorEvent = new Event<SensorEventArgs<MouseFeatureTypes,MouseEventArgs>>()
        let debug = false

        override x.OnMouseDown(e) =
            if debug then printfn "MOUSE DOWN"
            sensorEvent.Trigger(new SensorEventArgs<_,_>(MouseFeatureTypes.MouseDown, e))
            down <- true

        override x.OnMouseUp(e) =
            if debug then printfn "MOUSE UP"
            sensorEvent.Trigger(new SensorEventArgs<_,_>(MouseFeatureTypes.MouseUp, e))
            down <- false

        override x.OnMouseMove(e) =
            if down then
                if debug then printfn "MOUSE MOVE"
                sensorEvent.Trigger(new SensorEventArgs<_,_>(MouseFeatureTypes.MouseMove, e))

        interface GestIT.ISensor<MouseFeatureTypes,MouseEventArgs> with
            [<CLIEvent>]
            member x.SensorEvents = sensorEvent.Publish

(*
    type TouchFeatureTypes =
        | Start = 0
        | Move = 1
        | End = 2
*)

        
// *** LEAP *** //
 
    // devo tenermi traccia di cosa ho visto nel frame (precedente): tengo dunque un dizionario in cui la Key e' l'ID
    // (NB: ID e' un campo unico per ogni frame) e il valore è il Frame stesso.
    // In tal modo, quando ricevo un nuovo frame, controllo che gli ID:
    // * non siano presenti -> mando Active
    // * siano già presenti -> mando Move
    // * non siano più presenti -> mando NotActive

    /// <summary>
    /// Information sent on event triggering.
    /// </summary>
    type LeapFeatureTypes =
        | ActiveHand = 0
        | ActiveFinger = 1
        | ActiveTool = 2
        | MoveHand = 3
        | MoveFinger = 4
        | MoveTool = 5
        | NotActiveHand = 6
        | NotActiveFinger = 7
        | NotActiveTool = 8

    /// <summary>
    /// Generic type of Cleaner, which will operate on frame's history and current application state.
    /// </summary>
    type IStateCleaner<'T,'U> =
        
        /// <summary>
        /// Clears the history preceding a timestamp, calling the given function for each of them as soon as it has been removed;
        /// returns the elements that have been dropped.
        /// </summary>
        /// <param name="removeCallback"> A functioon to be called on each element immediately after its removal. </param>
        /// <param name="timestamp"> Minimum timestamp which should be kept in the current status. </param>
        /// <returns></returns>
        abstract member TruncateHistory: ('T -> unit) -> TimeStamp -> unit

        /// <summary>
        /// Predicts the model state at the given timestamp, as an evolution of the currently known information.
        /// </summary>
        /// <param name="timestamp">Time to which the model state is predicted.</param>
        /// <returns>unit</returns>
        abstract member Predict: TimeStamp -> unit

        /// <summary>
        /// Updates the current state using the new information (assuming matching ids).
        /// returns the model representation of the new information if the update succeeded
        /// </summary>
        /// <param name="visibleObject">Hand or pointable to be updated.</param>
        /// <returns>An updated Hand or Pointable, assuming matching ids; None otherwise.</returns>
        abstract member TryUpdate: 'U -> 'T option

        /// <summary>
        /// Tries to corrects the current state using the new information.
        /// </summary>
        /// <param name="visibleObject">Hand or pointable to be corrected.</param>
        /// <returns>The model representation of the new information if the correction succeeded; None otherwise.</returns>
        abstract member Correct: 'U -> 'T option

        /// <summary>
        /// Extends the current state using the new information.
        /// </summary>
        /// <param name="visibleObject">Hand or pointable which informations will update the current state.</param>
        /// <returns>The model representation of the new information. </returns>
        abstract member Extend: 'U -> 'T

    /// <summary>
    /// Defines a generic type of Leap ID.
    /// </summary>
    type LeapId = int
    
    /// <summary>
    /// ClonableHandCleaner is the cleaner that works on ClonableHand.
    /// </summary>
    type ClonableHandCleaner(state:ClonableFrame) =
        let debugHand = true

        /// <summary>
        /// Calculates the distance between two hands.
        /// </summary>
        /// <param name="h1">First hand</param>
        /// <param name="h2">Second hand</param>
        /// <returns>The distance in float32.</returns>
        let handDistance (h1:ClonableHand) (h2:Hand) =
            (h1.Position - h2.PalmPosition).Magnitude
        
        /// <summary>
        /// Calculates if a new hand (which means a hand whose ID hasn't been seen until now) may be a zombie hand (a disappeared one).
        /// </summary>
        /// <param name="zombiets">Last real timestamp of zombie hand.</param>
        /// <param name="zombiehand">Zombie hand.</param>
        /// <param name="newhand">New hand.</param>
        /// <returns>A boolean which represents if two hands are the same.</returns>
        let isTheSameHand (zombiets:TimeStamp) (zombiehand:ClonableHand) (newhand:Hand) =
            let speedError = (1.F (* max speed in m/s *) * 1.e-6F (* 1/s -> 1/us *) * 1.e3F (* m -> mm *)) (* mm / us *)
            //let speedError = 0.001
            let a = (handDistance zombiehand newhand)
            let b = float32(newhand.Frame.Timestamp - zombiets) * speedError + 3.F
            System.Diagnostics.Debug.WriteLine("---> {0},{1},{2} - {3},{4},{5} ", zombiehand.Position.x, zombiehand.Position.y, zombiehand.Position.z, newhand.PalmPosition.x, newhand.PalmPosition.y, newhand.PalmPosition.z)
            System.Diagnostics.Debug.WriteLine("---> {0} < {1}", a, b)
            a < b

        let handTimestamps = new Dictionary<LeapId, TimeStamp>()
        let leapToFake = new Dictionary<LeapId, FakeId>()
        let mutable lastTimestamp:TimeStamp = -1L
        
        /// </member>
        /// <member name="M:LeapDriver.ClonableHandCleaner.#ctor(ClonableLeapFrame.ClonableFrame)">
        /// <summary>
        /// Assigns the current initial state to the cleaner, which will be able to internally modify it.
        /// </summary>
        /// <param name="state">Initial state.</param>
        member private x.IgnoreMe () = ()

        /// <summary>
        /// Returns the FakeId corresponding to parameter LeapId.
        /// </summary>
        /// <param name="leapId">Hand's LeapID.</param>
        /// <returns>Hand's FakeID.</returns>
        member x.GetFakeId(leapId:LeapId) =
            if leapId = Leap.Hand.Invalid.Id then
                null
            else
                leapToFake.[leapId]

        interface IStateCleaner<ClonableHand,Hand> with
            member x.TruncateHistory f t =
                let removedIds =
                    handTimestamps
                    |> Seq.filter (fun x -> x.Value < t)
                    |> Seq.map (fun x -> x.Key)
                    |> Seq.toArray
                for leapId in removedIds do
                    let fakeId = leapToFake.[leapId]
                    let h = state.HandList.[leapToFake.[leapId]]
                    for p in state.PointableList.Values do
                        if p.IdHand = fakeId then
                            p.IdHand <- null
                    handTimestamps.Remove(leapId) |> ignore
                    state.HandList.Remove(fakeId) |> ignore
                    leapToFake.Remove(leapId) |> ignore
                    f h
                    if debugHand then printfn "RIMOSSO: %A" leapId

            member x.Predict(t) =
                for h in state.HandList do
                    let hand = h.Value
                    if true then
                      printfn "%A ... %A * %A = %A" hand.Position hand.Velocity.Magnitude ((float32)(t (* us *) - lastTimestamp) * (1.e-6f))
                       (hand.Velocity.Magnitude * ((float32)(t (* us *) - lastTimestamp) * (1.e-6f)))
                    if hand.Velocity.Magnitude > 200.f || true then
                      hand.Position <- hand.Position (* mm *) + hand.Velocity (* mm/s *) * ((float32)(t (* us *) - lastTimestamp) * (1.e-6f))
                lastTimestamp <- t

            member x.TryUpdate(h) =
                let leapId = h.Id
                if leapToFake.ContainsKey(leapId) then
                    let fakeId = leapToFake.[leapId]
                    let hand = new ClonableHand(fakeId, h.Direction, h.PalmPosition, h.PalmVelocity, h.PalmNormal, h.SphereCenter, h.SphereRadius)
                    state.HandList.[fakeId] <- hand
                    handTimestamps.[leapId] <- h.Frame.Timestamp
                    Some hand
                else
                    None

            member x.Correct(h) =
                (* controllo se questa mano e' gia' contenuta nello stato *)
                let leapId = h.Id
                let maybeZombie =
                    handTimestamps
                    |> Seq.filter ( fun x -> x.Value < h.Frame.Timestamp )
                    |> Seq.map ( fun x -> x.Key,state.HandList.[leapToFake.[x.Key]] )
                    |> Seq.sortBy ( fun (oldLeapId,x) -> handDistance x h )
                    |> Seq.tryFind ( fun (oldLeapId,x) -> isTheSameHand handTimestamps.[oldLeapId] x h )
                match maybeZombie with
                | None -> None
                | Some (oldLeapId, hand) ->
                    let fakeId = hand.Id
                    let hand = new ClonableHand(fakeId, h.Direction, h.PalmPosition, h.PalmVelocity, h.PalmNormal, h.SphereCenter, h.SphereRadius)
                    handTimestamps.Remove(oldLeapId) |> ignore
                    leapToFake.Remove(oldLeapId) |> ignore
                    state.HandList.[fakeId] <- hand
                    handTimestamps.Add(leapId, h.Frame.Timestamp)
                    leapToFake.Add(leapId, fakeId)
                    if debugHand then printfn "NUOVO ID %A, TROVATO ZOMBIE: %A" leapId oldLeapId
                    Some hand

            member x.Extend(h) = 
                let leapId = h.Id
                let fakeId = new FakeId()
                let hand = new ClonableHand(fakeId, h.Direction, h.PalmPosition, h.PalmVelocity, h.PalmNormal, h.SphereCenter, h.SphereRadius)
                state.HandList.Add(fakeId, hand)
                handTimestamps.Add(leapId, h.Frame.Timestamp)
                leapToFake.Add(leapId, fakeId)
                if debugHand then System.Diagnostics.Debug.WriteLine ("NUOVO ID {0}, L'HO AGGIUNTO", leapId )
                hand

    /// <summary>
    /// ClonablePointableCleaner is the cleaner that works on ClonablePointable.
    /// </summary>
    type ClonablePointableCleaner(state:ClonableFrame,handCleaner:ClonableHandCleaner) =
        let epsilon = 1000.0f * 1.5F
        let debugPtbl = false
        let fingerDistance (p1:ClonablePointable) (p2:Pointable) =
            (p1.Position - p2.TipPosition).Magnitude
        
        /// <summary>
        /// Calculates if a new pointable (which means a hand whose ID hasn't been seen until now) may be a zombie pointable (a disappeared one).
        /// </summary>
        /// <param name="zombiets">Last real timestamp of zombie pointable.</param>
        /// <param name="zombiePointable">Zombie pointable.</param>
        /// <param name="newPointable">New pointable.</param>
        /// <returns>A boolean which represents if two pointables are the same.</returns>
        let isTheSameFinger (zombiets:TimeStamp) (zombiePointable:ClonablePointable) (newPointable:Pointable) =
            let speedError = (1.F (* max speed in m/s *) * 1.e-6F (* us -> s *) * 1.e3F (* m -> mm *)) (* mm / us *)
            let diffLength = System.Math.Abs(zombiePointable.Length - newPointable.Length) // finger length
            let diffWidth = System.Math.Abs(zombiePointable.Width - newPointable.Width) // finger width
            if debugPtbl then printfn "--->> %A %A %A" diffLength diffWidth ((fingerDistance zombiePointable newPointable) < (float32(newPointable.Frame.Timestamp - zombiets) * speedError + 3.F))
            (diffLength < epsilon) && (diffWidth < epsilon) &&
                ((fingerDistance zombiePointable newPointable) < (float32(newPointable.Frame.Timestamp - zombiets) * speedError + 3.F))

        let pointableTimestamps = new Dictionary<LeapId, TimeStamp>()
        let leapToFake = new Dictionary<LeapId, FakeId>()
        let mutable lastTimestamp:TimeStamp = -1L

        /// </member>
        /// <member name="M:LeapDriver.ClonablePointableCleaner.#ctor(ClonableLeapFrame.ClonableFrame,LeapDriver.ClonableHandCleaner)">
        /// <summary>
        /// Assigns the current initial state to the cleaner, which will be able to internally modify it.
        /// </summary>
        /// <param name="state">Initial state.</param>
        /// <param name="handCleaner">Hand cleaner.</param>
        member private x.IgnoreMe () = ()

        interface IStateCleaner<ClonablePointable,Pointable> with
            member x.TruncateHistory f t =
                let removedIds =
                    pointableTimestamps
                    |> Seq.filter (fun x -> x.Value < t)
                    |> Seq.map (fun x -> x.Key)
                    |> Seq.toArray
                for leapId in removedIds do
                    let p = state.PointableList.[leapToFake.[leapId]]
                    pointableTimestamps.Remove(leapId) |> ignore
                    state.PointableList.Remove(leapToFake.[leapId]) |> ignore
                    leapToFake.Remove(leapId) |> ignore
                    f p
                    if debugPtbl then printfn "%A RIMUOVO: %A => %A" state.Timestamp leapId (leapToFake.Keys |> Seq.toArray)
            
            member x.TryUpdate(p) =
                let leapId = p.Id
                if leapToFake.ContainsKey(leapId) then
                    let fakeId = leapToFake.[leapId]
                    let pointable = new ClonablePointable(fakeId, handCleaner.GetFakeId(p.Hand.Id), p.Direction, p.TipPosition, p.TipVelocity, p.IsFinger, p.IsTool, p.Length, p.Width)
                    state.PointableList.[fakeId] <- pointable
                    pointableTimestamps.[leapId] <- p.Frame.Timestamp
                    Some pointable
                else
                    None

            member x.Predict(t) =
#if false
                for p in state.PointableList do
                    let ptb = p.Value
                    ptb.Position <- ptb.Position + ptb.Velocity * ((float32)(t - lastTimestamp) * (1.e-6f))
#endif
                lastTimestamp <- t

            member x.Correct(p) =
                (* controllo se questo pointable e' gia' contenuto nello stato *)
                let leapId = p.Id
                let maybeZombie =
                    pointableTimestamps
                    |> Seq.filter ( fun x -> x.Value < p.Frame.Timestamp )
                    |> Seq.map ( fun x -> x.Key,state.PointableList.[leapToFake.[x.Key]] )
                    |> Seq.sortBy ( fun (oldLeapId,x) -> fingerDistance x p )
                    |> Seq.tryFind ( fun (oldLeapId,x) -> isTheSameFinger pointableTimestamps.[oldLeapId] x p )
                match maybeZombie with
                | None -> None
                | Some (oldLeapId,pointable) ->
                    let fakeId = pointable.Id
                    let pointable = new ClonablePointable(fakeId, handCleaner.GetFakeId(p.Hand.Id), p.Direction, p.TipPosition, p.TipVelocity, p.IsFinger, p.IsTool, p.Length, p.Width)
                    pointableTimestamps.Remove(oldLeapId) |> ignore
                    leapToFake.Remove(oldLeapId) |> ignore
                    state.PointableList.[fakeId] <- pointable
                    pointableTimestamps.Add(leapId, p.Frame.Timestamp)
                    leapToFake.Add(leapId, fakeId)
                    if debugPtbl then printfn "%A RINOMINO %A -> %A => %A" state.Timestamp oldLeapId leapId (leapToFake.Keys |> Seq.toArray)
                    Some pointable

            member x.Extend(p) =
                let leapId = p.Id
                let fakeId = new FakeId()
                let pointable = new ClonablePointable(fakeId, handCleaner.GetFakeId(p.Hand.Id), p.Direction, p.TipPosition, p.TipVelocity, p.IsFinger, p.IsTool, p.Length, p.Width)
                state.PointableList.Add(fakeId, pointable)
                pointableTimestamps.Add(leapId, p.Frame.Timestamp)
                leapToFake.Add(leapId, fakeId)
                if debugPtbl then printfn "%A NUOVO ID: %A => %A" state.Timestamp leapId (leapToFake.Keys |> Seq.toArray)
                pointable

    /// <summary>
    /// Represents the event to be triggered by LeapSensor.
    /// </summary>
    type LeapEventArgs(f:ClonableFrame, id:FakeId) =
        inherit System.EventArgs()

        /// </member>
        /// <member name="M:LeapDriver.LeapEventArgs.#ctor(ClonableLeapFrame.ClonableFrame,ClonableLeapFrame.FakeId)">
        /// <summary>
        /// Assigns a frame and a ID to the event to be sent.
        /// </summary>
        /// <param name="f">Frame to be sent.</param>
        /// <param name="id">Id to which the event is related to.</param>
        member private this.IgnoreMe() = ()

        // Oggetto Frame corrente.
        member this.Frame = f
        // ID dell'oggetto a cui si riferisce la feature (es. ID di un Hand / Finger / Tool).
        member this.Id = id

    /// <summary>
    /// Sensor that receives raw data from LEAP and trigger new events to its listeners. It implements LEAP's <c>Listener</c> interface to communicate with the device.
    /// </summary>
    type LeapSensor () as this =
        inherit Leap.Listener()
        let ctrl = new Controller()

        let zombieWindow = 200000L
        let state = new ClonableFrame()
        let handCleaner = new ClonableHandCleaner(state)
        let pointableCleaner = new ClonablePointableCleaner(state, handCleaner) :> IStateCleaner<_,_>
        let handCleaner = handCleaner :> IStateCleaner<_,_>

        let sensorEvent = new Event<SensorEventArgs<LeapFeatureTypes, LeapEventArgs>>()

        let formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter()
        let mutable outputStream : System.IO.Stream = null
        let debugSensor = false

        do
            ctrl.AddListener(this) |> ignore
            ctrl.SetPolicyFlags(Controller.PolicyFlag.POLICYBACKGROUNDFRAMES)

        member this.OutputStream
            with get() = outputStream
            and set(v) = outputStream <- v

        member this.Controller = ctrl
        interface ISensor<LeapFeatureTypes,LeapEventArgs> with
            [<CLIEvent>]
            member x.SensorEvents = sensorEvent.Publish

        member private this.MyTrigger t e =
            if this.OutputStream <> null then formatter.Serialize(this.OutputStream, (System.DateTime.Now, t, e)) 
            sensorEvent.Trigger(new SensorEventArgs<_,_>(t, e))

        override this.OnInit(c:Controller) =
            if debugSensor then System.Diagnostics.Debug.WriteLine("OnInit")
        override this.OnConnect(c:Controller) =
            if debugSensor then System.Diagnostics.Debug.WriteLine("OnConnect")
        override this.OnFrame(c:Controller) =
            let frame = c.Frame()
            let currenttimestamp = frame.Timestamp

            state.Timestamp <- currenttimestamp

            pointableCleaner.TruncateHistory (fun pointable ->
                                                let e = new LeapEventArgs(state, pointable.Id)
                                                let t = if pointable.IsFinger then LeapFeatureTypes.NotActiveFinger else LeapFeatureTypes.NotActiveTool
                                                this.MyTrigger t e
                ) (currenttimestamp - zombieWindow)

            handCleaner.TruncateHistory (fun hand ->
                                                let e = new LeapEventArgs(state, hand.Id)
                                                this.MyTrigger LeapFeatureTypes.NotActiveHand e
                ) (currenttimestamp - zombieWindow)

            handCleaner.Predict(currenttimestamp)
            pointableCleaner.Predict(currenttimestamp)

            let mutable unmatched = []
            for h in frame.Hands do
                match handCleaner.TryUpdate(h) with
                | Some hand ->
                    let e = new LeapEventArgs(state,hand.Id)
                    this.MyTrigger LeapFeatureTypes.MoveHand e
                | None -> unmatched <- h::unmatched

            for h in unmatched do
                let hand,t =
                    match handCleaner.Correct(h) with
                    | Some hand -> hand,LeapFeatureTypes.MoveHand
                    | None -> handCleaner.Extend(h),LeapFeatureTypes.ActiveHand
                let e = new LeapEventArgs(state,hand.Id)
                this.MyTrigger t e

            let mutable unmatched = []
            for p in frame.Pointables do
                match pointableCleaner.TryUpdate(p) with
                | Some pointable ->
                    let t = if pointable.IsFinger then LeapFeatureTypes.MoveFinger else LeapFeatureTypes.MoveTool
                    let e = new LeapEventArgs(state,pointable.Id)
                    this.MyTrigger t e
                | None -> unmatched <- p::unmatched

            for p in unmatched do
                let pointable,t =
                    match pointableCleaner.Correct(p) with
                    | Some pointable ->
                        let t = if pointable.IsFinger then LeapFeatureTypes.MoveFinger else LeapFeatureTypes.MoveTool
                        pointable,t
                    | None ->
                        let pointable = pointableCleaner.Extend(p)
                        let t = if pointable.IsFinger then LeapFeatureTypes.ActiveFinger else LeapFeatureTypes.ActiveTool
                        pointable,t
                let e = new LeapEventArgs(state,pointable.Id)
                this.MyTrigger t e

        override this.OnDisconnect(c:Controller) =
            if debugSensor then System.Console.WriteLine "OnDisconnect"
        override this.OnExit (c:Controller) =
            if debugSensor then System.Console.WriteLine "OnExit"
