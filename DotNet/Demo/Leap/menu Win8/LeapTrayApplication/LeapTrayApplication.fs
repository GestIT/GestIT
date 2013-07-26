/// File:    LeapTrayApplication.fs
/// Author:  Marta Martino

// Ulteriori informazioni su F# all'indirizzo http://fsharp.net
// Per ulteriori informazioni, vedere il progetto 'Esercitazione su F#'.
module LeapTrayApplication
    open System.Windows.Forms
    open System.Drawing
    open System.Collections.Generic
    open System.Diagnostics
    open System.IO
    open System.IO.Compression
    open GestIT
    open GestIT.FSharp
    open ClonableLeapFrame
    open LeapDriver
    
    type Direction =
        | Up = 0
        | Down = 1
        | Left = 2
        | Right = 3

    type TrayApplication (s:ISensor<LeapFeatureTypes,LeapEventArgs>) =
        inherit Form()
        let mutable trayMenu = null
        let mutable trayIcon = null
        (* Structures *)
        let frameQueue = new Queue<ClonableFrame>()
        let mutable lastFrameInQueue = new ClonableFrame() // it represents the last enqueued frame
        let vectorX = new Leap.Vector(1.f, 0.f, 0.f)
        let vectorY = new Leap.Vector(0.f, -1.f, 0.f)
        let vectorZ = new Leap.Vector(0.f, 0.f, -1.f)
        (* Timestamps *)
        let ts_openedhand = ref(-1L : TimeStamp)
        let ts_closedhand = ref(-1L : TimeStamp)
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

        let initializeTrashes =
            threshpointfingerup <- thresh                
            threshpointfingerdown <- thresh
            threshpointfingerleft <- thresh
            threshpointfingerright <- thresh

        (* Predicates *)
        let p = new Predicate<LeapEventArgs>(fun x -> true)    
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

        let movefinger (d:Direction) (x:LeapEventArgs) =
            let f = x.Frame
            let id = x.Id
            let mutable lastfingerdir = -1L
            let mutable threshpointfingerdir = -1L
            match d with
            | Direction.Up -> lastfingerdir <- lastFingerUp; threshpointfingerdir <- threshpointfingerup
            | Direction.Down -> lastfingerdir <- lastFingerDown; threshpointfingerdir <- threshpointfingerdown
            | Direction.Left -> lastfingerdir <- lastFingerLeft; threshpointfingerdir <- threshpointfingerleft
            | Direction.Right -> lastfingerdir <- lastFingerRight; threshpointfingerdir <- threshpointfingerright
            | _ -> ()
            if f.PointableList.Count > 2 || f.PointableList.Count = 0 || f.Timestamp - lastfingerdir < threshpointfingerdir then
                false
            else
                match d with
                | Direction.Left ->
                        let finger =
                            f.PointableList.Values
                            |> Seq.maxBy (fun y -> y.Length)
                        finger.Position.x <= -60.f
                | Direction.Right ->
                        let finger =
                            f.PointableList.Values
                                |> Seq.maxBy (fun y -> y.Length)
                        (finger.Position.x >= 50.f) 
                | Direction.Up ->
                        let finger =
                            f.PointableList.Values
                                |> Seq.maxBy (fun y -> y.Length)
                        finger.Position.y >= 210.f        
                | Direction.Down -> 
                        let finger =
                            f.PointableList.Values
                                |> Seq.maxBy (fun y -> y.Length)
                        finger.Position.y <= 170.f
                | _ -> false

        let openhand (x:LeapEventArgs) =
            let f = x.Frame
            f.HandList.Count = 1 && f.PointableList.Count >= 4

        let closehandframe (f:ClonableFrame) =
            f.HandList.Count = 1 && f.PointableList.Count < 1

        let closehand (x:LeapEventArgs) =
            closehandframe (x.Frame)

        let keepclosed (x:LeapEventArgs) =
                let latestFrames =
                    frameQueue
                    |> Seq.filter (fun y -> y.Timestamp >= x.Frame.Timestamp - 100000L)
                if Seq.length latestFrames = 0 then
                    false
                else
                    latestFrames
                    |> Seq.forall (fun y -> y.HandList.ContainsKey(x.Id) && (closehandframe y) )

        let timedevent p refts thresh (x:LeapEventArgs) =
            let f = x.Frame
            (p x) && x.Frame.Timestamp - !refts < thresh

        let closetimedhand = timedevent closehand ts_openedhand 150000L
        let opentimedhand = timedevent openhand ts_closedhand 150000L

        let pointableCountIs n =
            new Predicate<LeapEventArgs>(fun x -> x.Frame.PointableList.Count = n)

        (* Handlers *)
        let openmenu (sender,f,e) =            
            SendKeys.SendWait("^{ESC}")
        let closemenu (sender,f,e) =
            SendKeys.SendWait("{ESC}")
        let savelastclosehand (sender,f:LeapFeatureTypes, e:LeapEventArgs) =
            ts_closedhand := e.Frame.Timestamp
        let movefingerleft (sender,f:LeapFeatureTypes, e:LeapEventArgs) =
            threshpointfingerdown <- thresh
            threshpointfingerright <- thresh
            threshpointfingerup <- thresh
            let t = threshpointfingerleft - 30000L
            if t > 0L then threshpointfingerleft <- t;
            lastFingerLeft <- e.Frame.Timestamp
            SendKeys.SendWait("{LEFT 1}")
        let movefingerright (sender,f:LeapFeatureTypes, e:LeapEventArgs) =
            threshpointfingerdown <- thresh
            threshpointfingerleft <- thresh
            threshpointfingerup <- thresh
            let t = threshpointfingerright - 30000L
            if t > 0L then
                threshpointfingerright <- t;
            lastFingerRight <- e.Frame.Timestamp
            SendKeys.SendWait("{RIGHT 1}")
        let movefingerup (sender,f:LeapFeatureTypes, e:LeapEventArgs) =
            threshpointfingerdown <- thresh
            threshpointfingerright <- thresh
            threshpointfingerleft <- thresh
            let t = threshpointfingerup - 30000L
            if t > 0L then threshpointfingerup <- t;
            lastFingerUp <- e.Frame.Timestamp
            SendKeys.SendWait("{UP 1}")
        let movefingerdown (sender,f:LeapFeatureTypes, e:LeapEventArgs) =
            threshpointfingerleft <- thresh
            threshpointfingerright <- thresh
            threshpointfingerup <- thresh
            let t = threshpointfingerdown - 30000L
            if t > 0L then threshpointfingerdown <- t;
            lastFingerDown <- e.Frame.Timestamp
            SendKeys.SendWait("{DOWN 1}")
        let openapplication (sender,f:LeapFeatureTypes, e:LeapEventArgs) =
            initializeTrashes
            lastEnter <- e.Frame.Timestamp
            SendKeys.SendWait("{ENTER}")

        (*  GroundTerms definitions *)
        let keepclosedhand = new GroundTerm<_,_>(LeapFeatureTypes.MoveHand, keepclosed)
        let closedhand2 = (new GroundTerm<_,_>(LeapFeatureTypes.MoveHand, closehand)) |-> savelastclosehand
        let openedhand2 = new GroundTerm<_,_>(LeapFeatureTypes.MoveHand, opentimedhand)
        let movedfingerup = (new GroundTerm<_,LeapEventArgs>(LeapFeatureTypes.MoveFinger, movefinger Direction.Up)) |-> movefingerup
        let movedfingerdown = (new GroundTerm<_,LeapEventArgs>(LeapFeatureTypes.MoveFinger, movefinger Direction.Down)) |-> movefingerdown
        let movedfingerleft = (new GroundTerm<_,LeapEventArgs>(LeapFeatureTypes.MoveFinger, movefinger Direction.Left)) |-> movefingerleft
        let movedfingerright = (new GroundTerm<_,LeapEventArgs>(LeapFeatureTypes.MoveFinger, movefinger Direction.Right))  |-> movefingerright
        let pushedhanddown = (new GroundTerm<_,_>(LeapFeatureTypes.MoveHand, pushhanddown)) |-> openapplication

        do
            (* Net definition *)
            let expr = 
              ((closedhand2 |>> openedhand2) |-> openmenu) 
              |>> ( ((!* movedfingerleft) |^| (!* movedfingerright) |^| (!* movedfingerup) |^| (!* movedfingerdown))
                    |^| (pushedhanddown |^| ((closedhand2 |>> keepclosedhand) |-> closemenu))
                  )
            expr.ToGestureNet(s) |> ignore

            trayMenu <- new ContextMenu()
            trayIcon <- new NotifyIcon()
            trayIcon.Text <- "MyTrayApp";
            trayIcon.Icon <- new Icon(SystemIcons.Application, 40, 40);
            trayIcon.ContextMenu <- trayMenu;
            trayIcon.Visible <- true;

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
            (s :?> LeapSensor).LeapEventReceived.Add(fun (ft, e) ->
                (* Removing too old frames *)
                let t = e.Frame.Timestamp
                while (frameQueue.Count > 0 && (t - frameQueue.Peek().Timestamp > (int64)250000)) do
                    frameQueue.Dequeue() |> ignore
                (* Receiving updates from sensor *)
                let f = e.Frame
                let id = e.Id
                if lastFrameInQueue.Timestamp <> f.Timestamp then
                    (* in this case, surely lastFrame.TS < f.TS, so it has to be added to the queue *)
                    let newFrame = f.Clone()
                    frameQueue.Enqueue(newFrame)
                    lastFrameInQueue <- newFrame
                else
                    (* update frame informations *)
                    UpdateInformations(f, ft, id)
            )

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
#if PLAYBACK
            (* It starts the reading from zip file, in which LEAP macro has been stored. *)
            (s :?> PlaybackSensor<LeapFeatureTypes,LeapEventArgs>).start()
#endif

        override x.OnClosing(e:System.ComponentModel.CancelEventArgs) =
            trayIcon.Dispose()
            Application.Exit()


(* ** Main part ** *)
#if RECORD
    let mutable outf = ""
    let filename = "[AppName] "
    let path = "FileRecording"
    let timeformat = "HHmmss"

    let openFileForZip () =
        Directory.CreateDirectory(path) |> ignore
        outf <- (path + "\\" + filename + System.DateTime.Now.ToString(timeformat) + ".file")
        File.Open(outf, FileMode.Create, FileAccess.Write) 

    let zipFile () =
        let zipdestination = "\\" + filename + System.DateTime.Now.ToString(timeformat) + ".zip"
        System.IO.Compression.ZipFile.CreateFromDirectory(path, zipdestination)
        Directory.Delete(path, true)
#endif

    [<EntryPoint; System.STAThread>]
    let main argv = 
        let mutable ss : ISensor<_,_> option = None
#if PLAYBACK
        (* Creates a form, in which it has to be chosen the .zip from which to load the gesture's macro *)
        let ofd = new OpenFileDialog()
        ofd.InitialDirectory <- Directory.GetCurrentDirectory()
        ofd.Filter <- "Zip Files (*.zip)|*.zip"
        ofd.Multiselect <- false
        let userclicked = ofd.ShowDialog()
        if userclicked = DialogResult.OK then
            let archivezip = ZipFile.Open(ofd.FileName, ZipArchiveMode.Read)
            let filetounzip = 
                try
                    archivezip.Entries
                    |> Seq.find (fun x -> x.FullName.EndsWith(".file"))
                with _ ->
                    null
            if filetounzip <> null then
                let f = filetounzip.Open()
                ss <- Some(new GestIT.PlaybackSensor<LeapFeatureTypes,LeapEventArgs>(f) :> ISensor<LeapFeatureTypes,LeapEventArgs>)
            else
                MessageBox.Show("File input not valid or already existing.") |> ignore
        else if userclicked = DialogResult.Cancel || userclicked = DialogResult.Ignore || userclicked = DialogResult.Abort || userclicked = DialogResult.No then
            ()
#else
        (* If you wanna register macros (Record debug configuration), or simply play with LEAP (Debug configuration), you'll need a LeapSensor. *)
        let s = new LeapDriver.LeapSensor()
#if RECORD
        s.OutputStream <- openFileForZip()
#endif
        ss <- Some(s :> ISensor<LeapFeatureTypes,LeapEventArgs>)
#endif
        match ss with
            | None -> 0
            | Some s -> let a = new TrayApplication(s)
                        Application.Run(a)
#if RECORD
                        let f = (ss.Value :?> LeapSensor).OutputStream
                        (ss.Value :?> LeapSensor).OutputStream <- null
                        f.Close()
                        zipFile()
#endif
                        0