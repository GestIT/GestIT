/// <summary>
/// File:     LeapRPS.fs
/// Author:   Marta Martino
/// </summary>
module RockPaperScissor
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
   
    type Delegate = delegate of string -> unit

    type Morra =
        | Rock
        | Paper
        | Scissor

    type Direction =
        | Left
        | Right

    type TrayApplication (s:ISensor<LeapFeatureTypes,LeapEventArgs>) as this =
        inherit Form()

        let lbl = new Label()
        let btn = new Button()

        let printLabel (s:string) =
            lbl.Text <- s
        let printLabel1 = printLabel
        let deleg = new Delegate(printLabel1)

        (* Structures *)
        let frameQueue = new Queue<ClonableFrame>()
        let mutable lastFrameInQueue = new ClonableFrame() // it represents the last enqueued frame
        let vectorX = new Leap.Vector(1.f, 0.f, 0.f)
        let vectorY = new Leap.Vector(0.f, -1.f, 0.f)
        let vectorZ = new Leap.Vector(0.f, 0.f, -1.f)
        let mutable playerplay = Morra.Rock
        let mutable pcplay = 0
        let r = new System.Random()
        (* Timestamps *)
        let ts_openedhand = ref(-1L : TimeStamp)
        let ts_closedhand = ref(-1L : TimeStamp)
        let mutable lastEnter:TimeStamp = -1L
        let mutable lastHandRight:TimeStamp = -1L
        let mutable lastHandLeft:TimeStamp = -1L
        let mutable lastHandDown:TimeStamp = -1L
        let threshpointfinger:TimeStamp = 300000L
        let mutable minX_glob = new ClonableFrame()
        let mutable maxY_glob = new ClonableFrame()

        (* Predicates *)
        let speed (x:float32) (y:float32) = x / y
        let p = new Predicate<LeapEventArgs>(fun x -> true)
//        let checkfingers (ptblist:ClonablePointable seq) (handlist:ClonableHand seq) =
//            let h = [| for x in handlist -> x |].[0]
//            ptblist |> Seq.forall(fun y -> y.IdHand = h.Id)
        let movehand (d:Direction) (x:LeapEventArgs) =
            let f = x.Frame
            let id = x.Id
            let lasthanddir =
                match d with
                | Direction.Right as z -> lastHandRight
                | Direction.Left as z -> lastHandLeft
            if frameQueue |> Seq.exists (fun f -> not (f.HandList.ContainsKey(id))) || (f.HandList.Count <> 1) || (f.PointableList.Count > 2)
               || (lasthanddir >= f.Timestamp - 500000L) then
                    false
            else
                let coda =
                    frameQueue
                    |> Seq.filter (fun y -> y.Timestamp >= f.Timestamp - 150000L)
                if Seq.isEmpty coda then
                    false
                else
                    match d with
                    | Direction.Right ->
                        let minX =
                            coda
                            |> Seq.minBy (fun z -> z.HandList.[id].Position.x)
                        // if hand moved less than 5cm to the right, false; else keep checking
                        if f.HandList.[id].Position.x - minX.HandList.[id].Position.x < 50.F then
                            false
                        else
                            // if in 2/10 sec i'm going always to the right, then true
                            let exists =
                                coda
                                |> Seq.forall (fun q -> q.HandList.[id].Position.x >= minX.HandList.[id].Position.x)
                            exists
                    | Direction.Left ->
                        let maxX =
                            coda
                            |> Seq.maxBy (fun z -> z.HandList.[id].Position.x)
                        if maxX.HandList.[id].Position.x - f.HandList.[id].Position.x < 50.F then
                            false
                        else
                            let exists =
                                coda
                                |> Seq.forall (fun q -> q.HandList.[id].Position.x <= maxX.HandList.[id].Position.x)
                            exists
    
        let movehanddown (x:LeapEventArgs) =
            let f = x.Frame
            let id = x.Id
            if f.HandList.Count <> 1 || (lastHandDown >= f.Timestamp - 750000L) then
                false
            else
                let id = x.Id
                let o = x.Frame.HandList.[id].Position
                let coda =
                    frameQueue
                    |> Seq.filter (fun y -> y.HandList.ContainsKey(id) && y.Timestamp >= f.Timestamp - 150000L)
                if coda |> Seq.isEmpty then
                    false
                else
                    let maxY =
                        coda
                        |> Seq.maxBy (fun z -> z.HandList.[id].Position.y)
                    if maxY.HandList.[id].Position.y - o.y > 70.f then
                        let c1 =
                            coda
                            |> Seq.filter (fun z -> z.Timestamp >= maxY.Timestamp)
                            |> Seq.forall (fun z -> z.HandList.[id].Position.y <= maxY.HandList.[id].Position.y)
                        let lastframes =
                            coda
                            |> Seq.filter (fun z -> z.Timestamp >= f.Timestamp - 50000L)
                            |> Seq.forall (fun z -> Debug.WriteLine("{0}", z.HandList.[id].Velocity.MagnitudeSquared); z.HandList.[id].Velocity.MagnitudeSquared < 1500.f * 1000.f)
                        c1 && lastframes
                    else
                        false

        (* Handler *)
        let movedhandright_h (sender, e:SensorEventArgs<LeapFeatureTypes, LeapEventArgs>) =
            lastHandRight <- e.Event.Frame.Timestamp
            lbl.Invoke(deleg, "... ~~> THINKING ~~> ...") |> ignore
            lbl.BackColor <- Color.PeachPuff
            lbl.Invalidate()

        let movedhandleft_h (sender, e:SensorEventArgs<LeapFeatureTypes, LeapEventArgs>) =
            lastHandLeft <- e.Event.Frame.Timestamp
            lbl.Invoke(deleg, "... <~~ THINKING <~~ ...") |> ignore
            lbl.BackColor <- Color.PeachPuff
            lbl.Invalidate()

        let movedhanddown_h (sender, e:SensorEventArgs<LeapFeatureTypes, LeapEventArgs>) =
            let f = e.Event.Frame
            let h = [| for x in f.HandList.Values -> x |].[0]
            lastHandDown <- f.Timestamp
            let fingers =
                f.PointableList.Values
                |> Seq.filter (fun x -> x.IdHand = h.Id)
                |> Seq.length
            let mutable s = "Player: "
            if fingers <= 1 then
                s <- s + "* ROCK! *\n"
                playerplay <- Morra.Rock
            else if fingers <= 3 then
                s <- s +  "* SCISSOR! *\n"
                playerplay <- Morra.Scissor
            else if fingers >= 4 then
                s <- s + "* PAPER! *\n"
                playerplay <- Morra.Paper
            pcplay <- r.Next(0, 2)
            s <- s + "PC: "
            match playerplay with
            | Morra.Rock -> match pcplay with
                                | 0 -> lbl.Invoke(deleg, s + "* ROCK! *\nYou're even!") |> ignore
                                | 1 -> lbl.Invoke(deleg, s + "* PAPER! *\nPC wins!") |> ignore; lbl.BackColor <- Color.Red
                                | 2 -> lbl.Invoke(deleg, s + "* SCISSOR! *\nPlayer wins!") |> ignore; lbl.BackColor <- Color.Green
                                | _ -> ()
            | Morra.Paper -> match pcplay with
                                | 0 -> lbl.Invoke(deleg, s + "* ROCK! *\nPlayer wins!") |> ignore; lbl.BackColor <- Color.Green
                                | 1 -> lbl.Invoke(deleg, s + "* PAPER! *\nYou're even!") |> ignore
                                | 2 -> lbl.Invoke(deleg, s + "* SCISSOR! *\nPC wins!") |> ignore; lbl.BackColor <- Color.Red
                                | _ -> ()
            | Morra.Scissor -> match pcplay with
                                | 0 -> lbl.Invoke(deleg, s + "* ROCK! *\nPC wins!") |> ignore; lbl.BackColor <- Color.Red
                                | 1 -> lbl.Invoke(deleg, s + "* PAPER! *\nPlayer wins!") |> ignore; lbl.BackColor <- Color.Green
                                | 2 -> lbl.Invoke(deleg, s + "* SCISSOR! *\nYou're even!") |> ignore
                                | _ -> ()
            lbl.Invalidate()

        let movedhandleft = new GroundTerm<LeapFeatureTypes,LeapEventArgs>(LeapFeatureTypes.MoveHand, movehand Direction.Left)
        let movedhandright = new GroundTerm<LeapFeatureTypes,LeapEventArgs>(LeapFeatureTypes.MoveHand, movehand Direction.Right)
        let movedhanddown = new GroundTerm<_,_>(LeapFeatureTypes.MoveHand, movehanddown)

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

        do
            (* NET for Rock Paper Scissor (this is one of the two available methods for the implementation of the net, or...) *)
#if EXPLICIT
            let oscillations = new Parallel<_,_>(movedhandright, movedhandleft)
            let iteroscillations = new Iter<_,_>(oscillations)
            let s1 = new Choice<_,_>(iteroscillations, movedhanddown)
            (* NET for debugging (pbs reads leap input from a file) *)
            s1.ToGestureNet(s) |> ignore
            movedhandright.Gesture.Add(movedhandright_h)
            movedhandleft.Gesture.Add(movedhandleft_h)
            movedhanddown.Gesture.Add(movedhanddown_h)
#else
            (* ...You can implement the net in a faster way! *)
            let expr = !* ((movedhandleft |-> movedhandleft_h) |=| (movedhandright |-> movedhandright_h)) |>| (movedhanddown |-> movedhanddown_h)
            expr.ToGestureNet(s) |> ignore
#endif

            this.BackColor <- Color.PeachPuff
            this.MaximizeBox <- false
            this.FormBorderStyle <- FormBorderStyle.FixedSingle
            lbl.Visible <- true
            lbl.Width <- 200
            lbl.Height <- 50
            lbl.Location <- new Point(this.Location.X + this.Width / 2 - lbl.Width / 2, this.Location.Y + this.Height / 2 - lbl.Height / 2)
            lbl.Font <- new Font("Verdana", 10.F)
            lbl.Text <- "* ROCK PAPER SCISSOR *"
            this.Controls.Add(lbl)

        override x.OnLoad(e:System.EventArgs) =
            x.Visible <- true
            x.ShowInTaskbar <- true
            s.SensorEvents.Add(fun e ->
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
            base.OnLoad(e)

(* ** Main part ** *)
#if RECORD
    let mutable outf = ""
    let filename = "AppName "
    let path = "FileRecording"
    let timeformat = "HHmmss"

    let openFileForZip () =
        Directory.CreateDirectory(path) |> ignore
        outf <- (path + "\\" + filename + System.DateTime.Now.ToString(timeformat) + ".file")
        File.Open(outf, FileMode.Create, FileAccess.Write) 

    let zipFile () =
        let zipdestination = filename + System.DateTime.Now.ToString(timeformat) + ".zip"
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
#if PLAYBACK
                        a.Load.Add(fun _ -> (s :?> PlaybackSensor<LeapFeatureTypes,LeapEventArgs>).start())
#endif
                        Application.Run(a)
#if RECORD
                        let f = (ss.Value :?> LeapSensor).OutputStream
                        (ss.Value :?> LeapSensor).OutputStream <- null
                        f.Close()
                        zipFile()
#endif
                        0