/// File:     ApplicationTemplate.fs
/// Author:   Marta Martino

module ApplicationTemplate
    open System.Windows.Forms
    open System.Drawing
    open System.Diagnostics
    open System.IO
    open System.IO.Compression
    open GestIT
    open ClonableLeapFrame
    open LeapDriver
   
    type TrayApplication (s:ISensor<LeapFeatureTypes,LeapEventArgs>) =
        inherit Form()

        override x.OnLoad(e:System.EventArgs) =
            (* In here, you'll receive LeapSensor updates. *)
            s.SensorEvents.Add(fun e -> ())
#if PLAYBACK
            (* It starts the reading from zip file, in which LEAP macro has been stored. *)
            (s :?> PlaybackSensor<LeapFeatureTypes,LeapEventArgs>).start()
#endif
            base.OnLoad(e)

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
                        Application.Run(a)
#if RECORD
                        let f = (ss.Value :?> LeapSensor).OutputStream
                        (ss.Value :?> LeapSensor).OutputStream <- null
                        f.Close()
                        zipFile()
#endif
                        0