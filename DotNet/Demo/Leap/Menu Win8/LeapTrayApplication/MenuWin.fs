// Ulteriori informazioni su F# all'indirizzo http://fsharp.net
// Per ulteriori informazioni, vedere il progetto 'Esercitazione su F#'.
module MenuWin8
    open System.Windows.Forms
    open System.IO
    open System.Drawing
    open System.Collections.Generic
    open System.Diagnostics
    open GestIT
    open GestIT.FSharp
    open ClonableLeapFrame
    open LeapDriver
    
    type Delegate = delegate of unit -> unit

    type TrayApplication () as this =
        inherit Form()
        
        let pen:Pen = new Pen(Color.FromArgb(255, 0, 0, 0))
        let mutable point = new Point(0,0)
        let mutable pointScreen = new Point(0, 0)
        let lbl = new Label()

        let mutable trayMenu = null
        let mutable trayIcon = null

//        let outf = @"C:\Users\Pc\Documents\Visual Studio 2012\Projects\LeapTrayApplication - menu Win8\output.ser"
//        let mutable (f:FileStream) = null//File.Open(outf, FileMode.Create, FileAccess.Write)
//        let mutable (f2:FileStream) = File.OpenRead(outf)
//        let formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter()
//        let pbs = new GestIT.PlaybackSensor<_,_>(f2) // useless until you wanna debug info on file
        (* Structures *)
        let s = new LeapDriver.LeapSensor()
        let frameQueue = new Queue<ClonableFrame>()
        let mutable lastFrameInQueue = new ClonableFrame() // it represents the last enqueued frame
        let mutable queueIDs = new List<FakeId>()
        let vectorX = new Leap.Vector((float32)1, (float32)0, (float32)0)
        let vectorY = new Leap.Vector((float32)0, (float32)(-1),(float32) 0)
        let vectorZ = new Leap.Vector((float32)0, (float32)0, (float32)(-1))
        (* Timestamps *)
        let ts_openedhand:TimeStamp ref = ref -1L
        let ts_closedhand:TimeStamp ref = ref -1L
        let mutable lastEnter:TimeStamp = -1L
        let mutable lastFingerLeft:TimeStamp = -1L
        let mutable lastFingerRight:TimeStamp = -1L
        let mutable lastFingerUp:TimeStamp = -1L
        let mutable lastFingerDown:TimeStamp = -1L
        let mutable thresh:TimeStamp = 300000L
        let mutable threshpointfingerleft:TimeStamp = thresh
        let mutable threshpointfingerright:TimeStamp = thresh
        let mutable threshpointfingerup:TimeStamp = thresh
        let mutable threshpointfingerdown:TimeStamp = thresh

        (* Predicates *)
        let speed (x:float32) (y:float32) = x / y
        let p = new Predicate<LeapEventArgs>(fun x -> true)

        let activehand (n:int) (x:LeapEventArgs) =
            x.Frame.HandList.Count = n

        let movehand (dir:int) (x:LeapEventArgs) = // dir = 0 per LEFT, dir = 1 per RIGHT
            let f = x.Frame
            let id = x.Id
            if f.HandList.Count <> 2 then
                false
            else
                let o = x.Frame.HandList.[id].Position
                let coda =
                    frameQueue
                    |> Seq.filter (fun y -> y.HandList.ContainsKey(id) && y.Timestamp >= f.Timestamp - 200000L)
                if coda |> Seq.isEmpty then
                    false
                else
                    if dir = 0 then // LEFT
                        let minX =
                            coda
                            |> Seq.minBy (fun z -> z.HandList.[id].Position.x)
                        if o.x - minX.HandList.[id].Position.y > 50.f then
                            coda
                            |> Seq.filter (fun z -> z.Timestamp >= minX.Timestamp)
                            |> Seq.forall (fun z -> z.HandList.[id].Position.x >= minX.HandList.[id].Position.x)
                        else
                            false
                    else if dir = 1 then // RIGHT
                        let maxX =
                            coda
                            |> Seq.minBy (fun z -> z.HandList.[id].Position.x)
                        if maxX.HandList.[id].Position.x - o.x > 50.f then
                            coda
                            |> Seq.filter (fun z -> z.Timestamp <= maxX.Timestamp)
                            |> Seq.forall (fun z -> z.HandList.[id].Position.x <= maxX.HandList.[id].Position.x)
                        else
                            false
                    else false
    
        let areclose (x:LeapEventArgs) =
            let f = x.Frame
            if f.HandList.Count <> 2 then
                false
            else
                let ids = f.HandList.Keys |> Seq.toArray
                let n1 = f.HandList.[ids.[0]].Normal
                let n2 = f.HandList.[ids.[1]].Normal
                let a1 = n1.AngleTo(vectorX) * 180.f / (float32)System.Math.PI
                let a2 = n2.AngleTo(vectorX) * 180.f / (float32)System.Math.PI
                let x1 = f.HandList.[ids.[0]].Position.x
                let x2 = f.HandList.[ids.[1]].Position.x
                (a1 < 20.f && a2 > 160.f) || (a1 > 160.f && a2 < 20.f) && System.Math.Abs(x1 - x2) < 50.f
    
        let pushhanddown (x:LeapEventArgs) =
            let thresh = 50.f
            let f = x.Frame
            if (lastEnter >= f.Timestamp - 1000000L) || (x.Frame.PointableList.Count < 4) then
                false
            else
                let id = x.Id
                let o = x.Frame.HandList.[id].Position
                let coda =
                    frameQueue
                    |> Seq.filter (fun y -> y.HandList.ContainsKey(id) && y.Timestamp >= f.Timestamp - 100000L)
                if coda |> Seq.isEmpty then
                    false
                else
                    let maxY =
                        coda
                        |> Seq.maxBy (fun z -> z.HandList.[id].Position.y)
                    if maxY.HandList.[id].Position.y - o.y > 100.f then
                        coda
                        |> Seq.filter (fun z -> z.Timestamp >= maxY.Timestamp)
                        |> Seq.forall (fun z ->
                                        let v = z.HandList.[id].Position
                                        let dx = v.x - o.x
                                        let dz = v.z - o.z
                                        (dx*dx + dz*dz) < thresh * thresh
                                        )
                    else
                        false

        let movefingerleft (x:LeapEventArgs) =
            let f = x.Frame
            let id = x.Id
            if f.PointableList.Count > 2 || f.PointableList.Count = 0 || f.Timestamp - lastFingerLeft < threshpointfingerleft then
                false
            else
                let finger =
                    f.PointableList.Values
                    |> Seq.maxBy (fun y -> y.Length)
                finger.Position.x <= -60.f
                   
        let movefingerright (x:LeapEventArgs) =
            let f = x.Frame
            let id = x.Id
            if f.PointableList.Count > 2 || f.PointableList.Count = 0 || f.Timestamp - lastFingerRight < threshpointfingerright then
                false
            else
                let finger =
                    f.PointableList.Values
                    |> Seq.maxBy (fun y -> y.Length)
                finger.Position.x >= 50.f

        let movefingerup (x:LeapEventArgs) =
            let f = x.Frame
            let id = x.Id
            if f.PointableList.Count > 2 || f.PointableList.Count = 0 || f.Timestamp - lastFingerUp < threshpointfingerup then
                false
            else
                let finger =
                    f.PointableList.Values
                    |> Seq.maxBy (fun y -> y.Length)
                finger.Position.y >= 210.f

        let movefingerdown (x:LeapEventArgs) =
            let f = x.Frame
            let id = x.Id
            if f.PointableList.Count > 2 || f.PointableList.Count = 0 || f.Timestamp - lastFingerDown < threshpointfingerdown then
                false
            else
                let finger =
                    f.PointableList.Values
                    |> Seq.maxBy (fun y -> y.Length)
                finger.Position.y <= 170.f

        let openhand (x:LeapEventArgs) =
            let f = x.Frame
            Debug.WriteLine("OPENHAND: {0} {1}", (f.HandList.Count = 1), (f.PointableList.Count >= 4))
            f.HandList.Count = 1 && f.PointableList.Count >= 4

        let closehandframe (f:ClonableFrame) =
            Debug.WriteLine("CLOSEHAND: {0} {1}", (f.HandList.Count = 1), (f.PointableList.Count <= 1))
            f.HandList.Count = 1 && f.PointableList.Count <= 1

        let closehand (x:LeapEventArgs) =
            closehandframe (x.Frame)

        let keepclosed (x:LeapEventArgs) =
            let latestFrames =
                frameQueue
                |> Seq.filter (fun y -> y.Timestamp >= x.Frame.Timestamp - 100000L)
            if Seq.length latestFrames = 0 then
                false
            else
                let c =
                    latestFrames
                    |> Seq.forall (fun y -> y.HandList.ContainsKey(x.Id) && (closehandframe y) )
                c

        let timedevent p refts thresh (x:LeapEventArgs) =
            let f = x.Frame
            (p x) && x.Frame.Timestamp - !refts < thresh

        let closetimedhand = timedevent closehand ts_openedhand 150000L
        let opentimedhand = timedevent openhand ts_closedhand 150000L

        let pointableCountIs n =
            new Predicate<LeapEventArgs>(fun x -> x.Frame.PointableList.Count = n)

        (* Useless GroundTerms definitions *)
        let vedodito = new GroundTerm<_,_>(LeapFeatureTypes.ActiveFinger, p)
        let vedodito1 = new GroundTerm<_,_>(LeapFeatureTypes.ActiveFinger, pointableCountIs 1)
        let vedodito2 = new GroundTerm<_,_>(LeapFeatureTypes.ActiveFinger, pointableCountIs 2)
        let vedodito3 = new GroundTerm<_,_>(LeapFeatureTypes.ActiveFinger, pointableCountIs 3)
        let vedodito4 = new GroundTerm<_,_>(LeapFeatureTypes.ActiveFinger, pointableCountIs 4)
        let vedodito5 = new GroundTerm<_,_>(LeapFeatureTypes.ActiveFinger, pointableCountIs 5)
        let nonvedodito4 = new GroundTerm<_,_>(LeapFeatureTypes.NotActiveFinger, pointableCountIs 4)
        let nonvedodito3 = new GroundTerm<_,_>(LeapFeatureTypes.NotActiveFinger, pointableCountIs 3)
        let nonvedodito2 = new GroundTerm<_,_>(LeapFeatureTypes.NotActiveFinger, pointableCountIs 2)
        let nonvedodito1 = new GroundTerm<_,_>(LeapFeatureTypes.NotActiveFinger, pointableCountIs 1)
        let nonvedodito0 = new GroundTerm<_,_>(LeapFeatureTypes.NotActiveFinger, pointableCountIs 0)
        let movefinger1 = new GroundTerm<_,_>(LeapFeatureTypes.MoveFinger, p)
        (*  GroundTerms definitions *)
        // chiudi menu
        let openedhand1 = new GroundTerm<_,_>(LeapFeatureTypes.MoveHand, openhand)
        let closedhand1 = new GroundTerm<_,_>(LeapFeatureTypes.MoveHand, closetimedhand)
        let keepclosedhand = new GroundTerm<_,_>(LeapFeatureTypes.MoveHand, keepclosed)

        // apri menu
        let closedhand2 = new GroundTerm<_,_>(LeapFeatureTypes.MoveHand, closehand)
        let openedhand2 = new GroundTerm<_,_>(LeapFeatureTypes.MoveHand, opentimedhand)
    
        // scorri icone menu e apri
        let movedfingerright = new GroundTerm<_,LeapEventArgs>(LeapFeatureTypes.MoveFinger, movefingerright)
        let movedfingerleft = new GroundTerm<_,LeapEventArgs>(LeapFeatureTypes.MoveFinger, movefingerleft)
        let movedfingerup = new GroundTerm<_,LeapEventArgs>(LeapFeatureTypes.MoveFinger, movefingerup)
        let movedfingerdown = new GroundTerm<_,LeapEventArgs>(LeapFeatureTypes.MoveFinger, movefingerdown)
        let pushedhanddown = new GroundTerm<_,_>(LeapFeatureTypes.MoveHand, pushhanddown)

        // batti le mani
        let activehands = new GroundTerm<_,_>(LeapFeatureTypes.ActiveHand, activehand 2)
        let movedhandright = new GroundTerm<_,_>(LeapFeatureTypes.MoveHand, movehand 0) // mano a dx (non importa quale)
        let movedhandleft = new GroundTerm<_,_>(LeapFeatureTypes.MoveHand, movehand 1)  // mano a sx (idem)
        let handsareclose = new GroundTerm<_,_>(LeapFeatureTypes.MoveHand, areclose)
        let nothand = new GroundTerm<_,_>(LeapFeatureTypes.NotActiveHand, p)

//        let s1 = new Sequence<_,_>(closedhand2, keepclosedhand) // chiudi (tenendo chiusa mano)
//        let s2 = new Sequence<_,_>(closedhand2, openedhand2) // apri
//        let iterr = new Iter<_,_>(movedfingerright)
//        let iterl = new Iter<_,_>(movedfingerleft)
//        let iteru = new Iter<_,_>(movedfingerup)
//        let iterd = new Iter<_,_>(movedfingerdown)
//        let ch1 = new Choice<_,_>(iterr, iterl, iteru, iterd)
//        let ch2 = new Choice<_,_>(pushedhanddown, s1)
//    
//        let ch3 = new Choice<_,_>(ch1, ch2)
//        let s22 = new Sequence<_,_>(s2, ch3)
//        let net222 = s22.ToGestureNet(s)

        let expr = (closedhand2 |>> openedhand2) |>> ( ((!* movedfingerleft) |?| (!* movedfingerright) |?| (!* movedfingerup) |?| (!* movedfingerdown)) |?| (pushedhanddown |?| (closedhand2 |>> keepclosedhand)) )
        let exprnet = expr.ToGestureNet(s)

        let par = new Parallel<_,_>(movedhandleft, movedhandright)
        let iterpar = new Iter<_,_>(par)
        let sequ = new Sequence<_,_>(activehands, iterpar)
        let choi1 = new Choice<_,_>(handsareclose, nothand)
        let choi2 = new Choice<_,_>(sequ, choi1)
//        let netclaphands = choi2.ToGestureNet(s)
//        let netclaphands = choi2.ToGestureNet(pbs)


        (* PROVA PAINT *)
////        let mf = new GroundTerm<_,_>(LeapFeatureTypes.MoveFinger, p)
////        let naf = new GroundTerm<_,_>(LeapFeatureTypes.NotActiveFinger, p)
////        let i = new Iter<_,_>(mf)
////        let c = new Choice<_,_>(i, naf)
////        let netpaint = c.ToGestureNet(s)

        do
            trayMenu <- new ContextMenu()
            trayIcon <- new NotifyIcon()
            trayIcon.Text <- "MyTrayApp";
            trayIcon.Icon <- new Icon(SystemIcons.Application, 40, 40);
            trayIcon.ContextMenu <- trayMenu;
            trayIcon.Visible <- true;
            lbl.Visible <- true
            lbl.Width <- 100
            lbl.Height <- 50
            lbl.Location <- new Point(100, 100)
            lbl.BackColor <- Color.Red
            this.Controls.Add(lbl)
        
        let printLabel () =
            lbl.Text <- point.ToString()
        let printLabel1 = printLabel
        let deleg = new Delegate(printLabel1)

        (* Sensor *)
        let UpdateInformations (f:ClonableFrame, e:LeapFeatureTypes, id:FakeId) =
            (* Update informations in the last enqueued frame *)
            match e with
                | LeapFeatureTypes.ActiveHand -> lastFrameInQueue.HandList.Add(id, f.HandList.[id].Clone())
                | LeapFeatureTypes.ActiveFinger | LeapFeatureTypes.ActiveTool -> lastFrameInQueue.PointableList.Add(id, f.PointableList.[id].Clone())
                | LeapFeatureTypes.MoveHand -> lastFrameInQueue.HandList.[id] <- f.HandList.[id].Clone()
                | LeapFeatureTypes.MoveFinger | LeapFeatureTypes.MoveTool -> lastFrameInQueue.PointableList.[id] <- f.PointableList.[id].Clone()
                | LeapFeatureTypes.NotActiveHand -> lastFrameInQueue.HandList.Remove(id) |> ignore
                | LeapFeatureTypes.NotActiveFinger | LeapFeatureTypes.NotActiveTool -> lastFrameInQueue.PointableList.Remove(id) |> ignore
                | _ -> ()

        override x.OnLoad(e:System.EventArgs) =
            x.Visible <- false
            trayIcon.Visible <- true
            x.ShowInTaskbar <- false; // Remove from taskbar.
            (s :> ISensor<_,_>).SensorEvents.Add(fun e ->
//                if f <> null then formatter.Serialize(f, (System.DateTime.Now, e.FeatureType, e.Event))
                (* Removing too old frames *)
                let t = e.Event.Frame.Timestamp
                while (frameQueue.Count > 0 && (t - frameQueue.Peek().Timestamp > (int64)250000)) do
                    frameQueue.Dequeue() |> ignore
                (* Receiving updates from sensor *)
                let f = e.Event.Frame
                let id = e.Event.Id
                if lastFrameInQueue.Timestamp <> f.Timestamp then
                    (* in this case, surely lastFrame.TS < f.TS, so it has to be added to the queue *)
                    let newFrame = f.Clone()
                    frameQueue.Enqueue(newFrame)
                    lastFrameInQueue <- newFrame
                else
                    (* update frame informations *)
                    UpdateInformations(f, e.FeatureType, id)
            )
            let initializeTrashes =
                threshpointfingerup <- thresh                
                threshpointfingerdown <- thresh
                threshpointfingerleft <- thresh
                threshpointfingerright <- thresh

            (closedhand2 |>> keepclosedhand).Gesture.Add(fun _ -> SendKeys.SendWait("{ESC}")) // close menu
            openedhand1.Gesture.Add(fun (sender,e) -> ts_openedhand := e.Event.Frame.Timestamp)
            closedhand2.Gesture.Add(fun (sender,e) -> ts_closedhand := e.Event.Frame.Timestamp)
            (closedhand2 |>> openedhand2).Gesture.Add(fun (sender,e) -> Debug.WriteLine("TS menu: {0}", e.Event.Frame.Timestamp); SendKeys.SendWait("^{ESC}")) // open menu
            (!* movedfingerright).Gesture.Add(fun (sender,e) -> threshpointfingerdown <- thresh
                                                                threshpointfingerleft <- thresh
                                                                threshpointfingerup <- thresh
                                                                let t = threshpointfingerright - 30000L
                                                                if t > 0L then
                                                                    threshpointfingerright <- t;
                                                                lastFingerRight <- e.Event.Frame.Timestamp
                                                                SendKeys.SendWait("{RIGHT 1}"))
            (!* movedfingerleft).Gesture.Add(fun (sender,e) ->  threshpointfingerdown <- thresh
                                                                threshpointfingerright <- thresh
                                                                threshpointfingerup <- thresh
                                                                let t = threshpointfingerleft - 30000L
                                                                if t > 0L then threshpointfingerleft <- t;
                                                                lastFingerLeft <- e.Event.Frame.Timestamp
                                                                SendKeys.SendWait("{LEFT 1}"))
            (!* movedfingerup).Gesture.Add(fun (sender,e) -> threshpointfingerdown <- thresh
                                                             threshpointfingerright <- thresh
                                                             threshpointfingerleft <- thresh
                                                             let t = threshpointfingerup - 30000L
                                                             if t > 0L then threshpointfingerup <- t;
                                                             lastFingerUp <- e.Event.Frame.Timestamp
                                                             SendKeys.SendWait("{UP 1}"))
            (!* movedfingerdown).Gesture.Add(fun (sender,e) ->  threshpointfingerleft <- thresh
                                                                threshpointfingerright <- thresh
                                                                threshpointfingerup <- thresh
                                                                let t = threshpointfingerdown - 30000L
                                                                if t > 0L then threshpointfingerdown <- t;
                                                                lastFingerDown <- e.Event.Frame.Timestamp
                                                                SendKeys.SendWait("{DOWN 1}"))
            pushedhanddown.Gesture.Add(fun (sender,e) -> initializeTrashes
                                                         Debug.WriteLine("PUSH!")
                                                         lastEnter <- e.Event.Frame.Timestamp
                                                         SendKeys.SendWait("{ENTER}"))
            trayIcon.MouseDoubleClick.Add(fun _ ->
                                            if x.Visible = true then
                                                x.Visible <- false
                                            else
                                                x.Visible <- true
                                            x.Invalidate()
                                    )
            (* PROVA PAINT *)
////            mf.Gesture.Add(fun (sender,e) -> point <- new Point((int)e.Event.Frame.PointableList.[e.Event.Id].Position.x, (int)e.Event.Frame.PointableList.[e.Event.Id].Position.y)
////                                             lbl.Invoke(deleg) |> ignore
////                                             lbl.Invalidate()
////                                             x.Invalidate())
////            nothand.Gesture.Add(fun (sender,e) -> Debug.WriteLine("Mano scomparsa..."))
            handsareclose.Gesture.Add(fun (sender,e) -> Debug.WriteLine("n hands: {0}", e.Event.Frame.HandList.Count); SendKeys.SendWait("^+{ESC}"))
//            pbs.start()
       
        override x.OnPaint(e:PaintEventArgs) =
            let g = e.Graphics
            pointScreen.X <- point.X + System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width / 2
            pointScreen.Y <- (0 - point.Y) + System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height / 2 + 200
            g.DrawLine(pen, pointScreen, new Point(pointScreen.X + 20, pointScreen.Y + 20))

        override x.OnClosing(e:System.ComponentModel.CancelEventArgs) =
            trayIcon.Dispose()
            Application.Exit()

    [<EntryPoint; System.STAThread>]
    let main argv = 
        let a = new TrayApplication()
        a.Width <- System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width
        a.Height <- System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height
        Application.Run(a)
        0
