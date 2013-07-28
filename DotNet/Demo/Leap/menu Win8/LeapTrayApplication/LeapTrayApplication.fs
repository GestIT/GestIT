/// File:    LeapTrayApplication.fs
/// Author:  Marta Martino

open System.Windows.Forms
open System.Drawing
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.IO.Compression
open GestIT
open GestIT.FSharp
open GestIT.Leap
    
type Direction =
    | Up = 0
    | Down = 1
    | Left = 2
    | Right = 3

type LeapFeatures =
| NewHand
| ActiveHand
| InactiveHand
| NewFinger
| ActiveFinger
| InactiveFinger
| NewTool
| ActiveTool
| InactiveTool

type TrayApplication (s:LeapSensor) =
    inherit Form()
    let mutable trayMenu = null
    let mutable trayIcon = null
    (* Structures *)
    let frameQueue = new Queue<Leap.Frame>()
    let mutable lastFrameInQueue : Leap.Frame = null // it represents the last enqueued frame
    let mutable lastHandFrameInQueue : Leap.Frame = null // it represents the last enqueued frame
    let mutable lastPointableFrameInQueue : Leap.Frame = null // it represents the last enqueued frame
    let vectorX = new Leap.Vector(1.f, 0.f, 0.f)
    let vectorY = new Leap.Vector(0.f, -1.f, 0.f)
    let vectorZ = new Leap.Vector(0.f, 0.f, -1.f)
    (* Timestamps *)
    let ts_openedhand = ref -1L
    let ts_closedhand = ref -1L
    let mutable lastEnter = -1L
    let mutable lastFingerLeft = -1L
    let mutable lastFingerRight = -1L
    let mutable lastFingerUp = -1L
    let mutable lastFingerDown = -1L
    let mutable thresh = 300000L
    let mutable threshpointfingerleft = thresh
    let mutable threshpointfingerright = thresh
    let mutable threshpointfingerup = thresh
    let mutable threshpointfingerdown = thresh

    let initializeTrashes =
        threshpointfingerup <- thresh                
        threshpointfingerdown <- thresh
        threshpointfingerleft <- thresh
        threshpointfingerright <- thresh

    (* Predicates *)
    let p = new Predicate<LeapSensorEventArgs>(fun x -> true)    
    let pushhanddown (x:LeapSensorEventArgs) =
        let thresh = 50.f
        let f = x.Frame
        if (lastEnter >= f.Timestamp - 1000000L) || (x.Frame.Fingers.Count < 4) then
            false
        else
            let o = x.Frame.Hands.Frontmost
            let coda =
                frameQueue
                |> Seq.filter (fun y -> (y.Hands |> Seq.exists (fun h -> h.Id = o.Id)) && y.Timestamp >= f.Timestamp - 100000L)
            if coda |> Seq.isEmpty then
                false
            else
                let maxY =
                    coda
                    |> Seq.maxBy (fun z -> z.Hand(o.Id).PalmPosition.y)
                if maxY.Hand(o.Id).PalmPosition.y - o.PalmPosition.y > 100.f then
                    coda
                    |> Seq.filter (fun z -> z.Timestamp >= maxY.Timestamp)
                    |> Seq.forall (fun z ->
                                    let v = z.Hand(o.Id).PalmPosition
                                    let dx = v.x - o.PalmPosition.x
                                    let dz = v.z - o.PalmPosition.z
                                    (dx*dx + dz*dz) < thresh * thresh
                                    )
                else
                    false

    let movefinger (d:Direction) (x:LeapSensorEventArgs) =
        let f = x.Frame
        let mutable lastfingerdir = -1L
        let mutable threshpointfingerdir = -1L
        match d with
        | Direction.Up -> lastfingerdir <- lastFingerUp; threshpointfingerdir <- threshpointfingerup
        | Direction.Down -> lastfingerdir <- lastFingerDown; threshpointfingerdir <- threshpointfingerdown
        | Direction.Left -> lastfingerdir <- lastFingerLeft; threshpointfingerdir <- threshpointfingerleft
        | Direction.Right -> lastfingerdir <- lastFingerRight; threshpointfingerdir <- threshpointfingerright
        | _ -> ()
        if f.Fingers.Count > 2 || f.Fingers.Count = 0 || f.Timestamp - lastfingerdir < threshpointfingerdir then
            false
        else
            match d with
            | Direction.Left ->
                    let finger =
                        f.Fingers
                        |> Seq.maxBy (fun y -> y.Length)
                    finger.TipPosition.x <= -60.f
            | Direction.Right ->
                    let finger =
                        f.Fingers
                            |> Seq.maxBy (fun y -> y.Length)
                    (finger.TipPosition.x >= 50.f) 
            | Direction.Up ->
                    let finger =
                        f.Fingers
                            |> Seq.maxBy (fun y -> y.Length)
                    finger.TipPosition.y >= 210.f        
            | Direction.Down -> 
                    let finger =
                        f.Fingers
                            |> Seq.maxBy (fun y -> y.Length)
                    finger.TipPosition.y <= 170.f
            | _ -> false

    let openhand (x:LeapSensorEventArgs) =
        let f = x.Frame
        f.Hands.Count = 1 && f.Fingers.Count >= 4

    let closehandframe (f:Leap.Frame) =
        f.Hands.Count = 1 && f.Fingers.Count < 1

    let closehand (x:LeapSensorEventArgs) =
        closehandframe (x.Frame)

    let keepclosed (x:LeapSensorEventArgs) =
            let latestFrames =
                frameQueue
                |> Seq.filter (fun y -> y.Timestamp >= x.Frame.Timestamp - 100000L)
            if Seq.length latestFrames = 0 then
                false
            else
                latestFrames
                |> Seq.forall (fun y -> (y.Hands |> Seq.exists (fun h -> h.Id = x.Frame.Hands.Frontmost.Id)) && (closehandframe y) )

    let timedevent p refts thresh (x:LeapSensorEventArgs) =
        let f = x.Frame
        (p x) && x.Frame.Timestamp - !refts < thresh

    let closetimedhand = timedevent closehand ts_openedhand 150000L
    let opentimedhand = timedevent openhand ts_closedhand 150000L

    let pointableCountIs n =
        new Predicate<LeapSensorEventArgs>(fun x -> x.Frame.Fingers.Count = n)

    (* Handlers *)
    let openmenu (sender,f,e) =            
        SendKeys.SendWait("^{ESC}")
    let closemenu (sender,f,e) =
        SendKeys.SendWait("{ESC}")
    let savelastclosehand (sender,f:LeapFeatures, e:LeapSensorEventArgs) =
        ts_closedhand := e.Frame.Timestamp
    let movefingerleft (sender,f:LeapFeatures, e:LeapSensorEventArgs) =
        threshpointfingerdown <- thresh
        threshpointfingerright <- thresh
        threshpointfingerup <- thresh
        let t = threshpointfingerleft - 30000L
        if t > 0L then threshpointfingerleft <- t;
        lastFingerLeft <- e.Frame.Timestamp
        SendKeys.SendWait("{LEFT 1}")
    let movefingerright (sender,f:LeapFeatures, e:LeapSensorEventArgs) =
        threshpointfingerdown <- thresh
        threshpointfingerleft <- thresh
        threshpointfingerup <- thresh
        let t = threshpointfingerright - 30000L
        if t > 0L then
            threshpointfingerright <- t;
        lastFingerRight <- e.Frame.Timestamp
        SendKeys.SendWait("{RIGHT 1}")
    let movefingerup (sender,f:LeapFeatures, e:LeapSensorEventArgs) =
        threshpointfingerdown <- thresh
        threshpointfingerright <- thresh
        threshpointfingerleft <- thresh
        let t = threshpointfingerup - 30000L
        if t > 0L then threshpointfingerup <- t;
        lastFingerUp <- e.Frame.Timestamp
        SendKeys.SendWait("{UP 1}")
    let movefingerdown (sender,f:LeapFeatures, e:LeapSensorEventArgs) =
        threshpointfingerleft <- thresh
        threshpointfingerright <- thresh
        threshpointfingerup <- thresh
        let t = threshpointfingerdown - 30000L
        if t > 0L then threshpointfingerdown <- t;
        lastFingerDown <- e.Frame.Timestamp
        SendKeys.SendWait("{DOWN 1}")
    let openapplication (sender,f:LeapFeatures, e:LeapSensorEventArgs) =
        initializeTrashes
        lastEnter <- e.Frame.Timestamp
        SendKeys.SendWait("{ENTER}")

    (*  GroundTerms definitions *)
    let keepclosedhand = new GroundTerm<_,_>(LeapFeatures.ActiveHand, keepclosed)
    let closedhand2 = (new GroundTerm<_,_>(LeapFeatures.ActiveHand, closehand)) |-> savelastclosehand
    let openedhand2 = new GroundTerm<_,_>(LeapFeatures.ActiveHand, opentimedhand)
    let movedfingerup = (new GroundTerm<_,LeapSensorEventArgs>(LeapFeatures.ActiveFinger, movefinger Direction.Up)) |-> movefingerup
    let movedfingerdown = (new GroundTerm<_,LeapSensorEventArgs>(LeapFeatures.ActiveFinger, movefinger Direction.Down)) |-> movefingerdown
    let movedfingerleft = (new GroundTerm<_,LeapSensorEventArgs>(LeapFeatures.ActiveFinger, movefinger Direction.Left)) |-> movefingerleft
    let movedfingerright = (new GroundTerm<_,LeapSensorEventArgs>(LeapFeatures.ActiveFinger, movefinger Direction.Right))  |-> movefingerright
    let pushedhanddown = (new GroundTerm<_,_>(LeapFeatures.ActiveHand, pushhanddown)) |-> openapplication

    (* Sensor *)
    let UpdateInformations (f:Leap.Frame, e:LeapFeatures) =
        if lastFrameInQueue.Timestamp = f.Timestamp then
          (* Update informations in the last enqueued frame *)
          match e with
              | LeapFeatures.NewHand -> lastHandFrameInQueue <- f
              | LeapFeatures.NewFinger | LeapFeatures.NewTool -> lastPointableFrameInQueue <- f
              | LeapFeatures.ActiveHand -> lastHandFrameInQueue <- f
              | LeapFeatures.ActiveFinger | LeapFeatures.ActiveTool -> lastPointableFrameInQueue <- f
              | LeapFeatures.InactiveHand -> lastHandFrameInQueue <- null
              | LeapFeatures.InactiveFinger | LeapFeatures.InactiveTool -> lastPointableFrameInQueue <- null

    do
        let fs = new FusionSensor<_, _>()
        s.CurrentFrame.Add(fun e ->
            (* Removing too old frames *)
            let t = e.Frame.Timestamp
            while (frameQueue.Count > 0 && (t - frameQueue.Peek().Timestamp > (int64)250000)) do
                frameQueue.Dequeue() |> ignore
            (* Receiving updates from sensor *)
            let f = e.Frame
            let id = e.Frame.Id
            if lastFrameInQueue = null || lastFrameInQueue.Timestamp <> f.Timestamp then
                (* in this case, surely lastFrame.TS < f.TS, so it has to be added to the queue *)
                frameQueue.Enqueue(f)
                lastFrameInQueue <- f
        )
        fs.Listen(LeapFeatures.NewHand, s.NewHand |> Event.map (fun e -> UpdateInformations(e.Frame, LeapFeatures.NewHand); e))
        fs.Listen(LeapFeatures.ActiveHand, s.ActiveHand |> Event.map (fun e -> UpdateInformations(e.Frame, LeapFeatures.ActiveHand); e))
        fs.Listen(LeapFeatures.InactiveHand, s.InactiveHand |> Event.map (fun e -> UpdateInformations(e.Frame, LeapFeatures.InactiveHand); e))
        fs.Listen(LeapFeatures.NewFinger, s.NewFinger |> Event.map (fun e -> UpdateInformations(e.Frame, LeapFeatures.NewFinger); e))
        fs.Listen(LeapFeatures.ActiveFinger, s.ActiveFinger |> Event.map (fun e -> UpdateInformations(e.Frame, LeapFeatures.ActiveFinger); e))
        fs.Listen(LeapFeatures.InactiveFinger, s.InactiveFinger |> Event.map (fun e -> UpdateInformations(e.Frame, LeapFeatures.InactiveFinger); e))
        fs.Listen(LeapFeatures.NewTool, s.NewTool |> Event.map (fun e -> UpdateInformations(e.Frame, LeapFeatures.NewTool); e))
        fs.Listen(LeapFeatures.ActiveTool, s.ActiveTool |> Event.map (fun e -> UpdateInformations(e.Frame, LeapFeatures.ActiveTool); e))
        fs.Listen(LeapFeatures.InactiveTool, s.InactiveTool |> Event.map (fun e -> UpdateInformations(e.Frame, LeapFeatures.InactiveTool); e))

        (* Net definition *)
        let expr = 
            ((closedhand2 |>> openedhand2) |-> openmenu) 
            |>> ( ((!* movedfingerleft) |^| (!* movedfingerright) |^| (!* movedfingerup) |^| (!* movedfingerdown))
                |^| (pushedhanddown |^| ((closedhand2 |>> keepclosedhand) |-> closemenu))
                )
        expr.ToGestureNet(fs) |> ignore

        trayMenu <- new ContextMenu()
        trayIcon <- new NotifyIcon()
        trayIcon.Text <- "MyTrayApp";
        trayIcon.Icon <- new Icon(SystemIcons.Application, 40, 40);
        trayIcon.ContextMenu <- trayMenu;
        trayIcon.Visible <- true;


    override x.OnLoad(e:System.EventArgs) =
        x.Visible <- false
        trayIcon.Visible <- true
        x.ShowInTaskbar <- false; // Remove from taskbar.

        trayIcon.MouseDoubleClick.Add(fun _ ->
                                        if x.Visible = true then
                                            x.Visible <- false
                                        else
                                            x.Visible <- true
                                        x.Invalidate()
                                )
        trayIcon.MouseClick.Add(fun e ->
                                    if e.Button = MouseButtons.Right then
                                        trayIcon.Dispose()
                                        Application.Exit()
                                )
    override x.OnClosing(e:System.ComponentModel.CancelEventArgs) =
        trayIcon.Dispose()
        Application.Exit()


(* ** Main part ** *)
[<EntryPoint; System.STAThread>]
let main argv = 
    let mutable ss : ISensor<_,_> option = None
    let s = new LeapSensor()
    s.Controller.SetPolicyFlags(Leap.Controller.PolicyFlag.POLICYBACKGROUNDFRAMES)
    s.Connect() |> ignore
    let a = new TrayApplication(s)
    Application.Run(a) |> ignore
    0