namespace KinectDriver

    open System
    open System.Collections.Generic
    open GestIT
    open Microsoft.Kinect
    open Microsoft.Kinect.Toolkit
    open Microsoft.Kinect.Toolkit.Controls
    open Microsoft.Kinect.Toolkit.Interaction

  
    type KinectFeatureType = 
        | Joint of JointType * JointTrackingState                              //moving joint
        | HandInteraction of InteractionHandType * InteractionHandEventType    //gripping hands
        | HandPress of InteractionHandType * bool                              //pushing hands
        | Skeleton of SkeletonTrackingState                                    //moving skeleton tracking
        
       
        
    type KinectEventArgs(s:Skeleton) =
        inherit System.EventArgs()
        member this.SkeletonData = s
    
    type MyInteractionClient() =
            interface IInteractionClient with 
                member this.GetInteractionInfoAtLocation(skeletonTrackingId:int, handType:InteractionHandType, x:float, y:float) =
                    let info = new InteractionInfo() 
                    info 

    type DummyInteractionClient() =
        interface IInteractionClient with 
            member this.GetInteractionInfoAtLocation(skeletonTrackingId, handtype, x, y) =
                 let i = new InteractionInfo(
                            IsGripTarget = true,
                            IsPressTarget = true,
                            PressAttractionPointX = 0.5,
                            PressAttractionPointY = 0.5,
                            PressTargetControlId = 1)
                 i
                

    type KinectDriver() =
        let sensorEvents = new System.Collections.Generic.Dictionary<_,_>()
        let DEPTH_MASK = 0x1FFF
        let DEFAULT_DEPTH_FORMAT = DepthImageFormat.Resolution640x480Fps30
        
        let mutable sensor:KinectSensor = null
        let mutable interactionStream:InteractionStream = null
        let mutable lastSkeleton:Skeleton = null
        let mutable hp:HandPointer = null

        let sensorConnected (x:KinectSensor) =
          match x.Status with
            KinectStatus.Connected -> true
            | _ -> false
       
        do 
           //Joint Events
           for i in Seq.cast (Enum.GetValues(typeof<JointType>)) do
               for j in Seq.cast (Enum.GetValues(typeof<JointTrackingState>)) do
                    sensorEvents.Add(Joint(i, j), new Event<KinectEventArgs>())

           //Hand Interaction
           for i in Seq.cast (Enum.GetValues(typeof<InteractionHandType>)) do
               for j in Seq.cast (Enum.GetValues(typeof<InteractionHandEventType>)) do
                    sensorEvents.Add(HandInteraction(i, j), new Event<KinectEventArgs>())

           //Hand press events
           for i in Seq.cast (Enum.GetValues(typeof<InteractionHandType>)) do
                for j in [ true; false ] do
                    sensorEvents.Add(HandPress(i, j), new Event<KinectEventArgs>())

           //Skeleton Events
           for i in Seq.cast (Enum.GetValues(typeof<SkeletonTrackingState>)) do 
                sensorEvents.Add(Skeleton(i), new Event<KinectEventArgs>())


        interface ISensor<KinectFeatureType,KinectEventArgs> with
           member this.Item with get f = sensorEvents.[f].Publish
          
     
        member this.Attach () = 
            try
                sensor <-  (KinectSensor.KinectSensors |>  Seq.find sensorConnected )
                true
            with
            | :? KeyNotFoundException -> printfn "KinectManager.KeyNotFoundException"; sensor <- null;false
        

        member this.GetReferenceSkel (sf:SkeletonFrame) =
            let mutable skelArray = Array.empty<Skeleton>
            if not (null = sf) then                 
                skelArray <- Array.create sf.SkeletonArrayLength null
                sf.CopySkeletonDataTo skelArray
            
            skelArray |> Seq.filter (fun x -> x.TrackingState <> SkeletonTrackingState.NotTracked) //todo forse questo andra tolto perche' potremmo volerlo controllare dalle gestures
                      |> Seq.fold (fun (x:Skeleton) y -> if (x = null || x.Position.Z > y.Position.Z) then y else x) null

        member this.ComputeDepthData (e:AllFramesReadyEventArgs) =
            use depthfram = e.OpenDepthImageFrame()
            try
                interactionStream.ProcessDepth(depthfram.GetRawPixelData(), depthfram.Timestamp)
            with
            | :? InvalidOperationException -> ()
        member this.ComputeSkeletonStream (e:AllFramesReadyEventArgs) =
            use skelframe = e.OpenSkeletonFrame()
           
            let mutable skelArray = Array.empty<Skeleton>
            if  null <> skelframe then    
               try             
                skelArray <- Array.create skelframe.SkeletonArrayLength null
                skelframe.CopySkeletonDataTo skelArray
                interactionStream.ProcessSkeleton(skelArray, (sensor.AccelerometerGetCurrentReading()), skelframe.Timestamp)
            
                let referenceSkeleton = this.GetReferenceSkel(skelframe)
           
                if referenceSkeleton <> null then
                    if lastSkeleton = null then
                        lastSkeleton <- referenceSkeleton

                    for i in Seq.cast (Enum.GetValues(typeof<JointType>)) do
                        let joint = referenceSkeleton.Joints.Item i
                        let feature =  Joint(i, joint.TrackingState)
                        let evtArgs =  new KinectEventArgs(referenceSkeleton)                  
                        sensorEvents.[feature].Trigger(evtArgs)

                
                    lastSkeleton <- referenceSkeleton
                with
                | :? InvalidOperationException -> ()
            ()

        member this.InteractionReady (e:InteractionFrameReadyEventArgs) =
                let frame = e.OpenInteractionFrame()
                if frame <> null then
                    let mutable userInfo:UserInfo array = Array.zeroCreate 6 
                    frame.CopyInteractionDataTo(userInfo)
                    for i in userInfo do
                        for hp in i.HandPointers do
                            if hp <> null then
                                sensorEvents.[HandInteraction(hp.HandType, hp.HandEventType)].Trigger(new KinectEventArgs(lastSkeleton))

                //(Array.toSeq userInfo) |> Seq.filter 


        member this.AllframeReady (e:AllFramesReadyEventArgs) = 
                this.ComputeDepthData(e)
                this.ComputeSkeletonStream(e)    
                ()
              
        member this.InitializeStreams (depthFormat, mode) =                  
              if (not (sensor = null)) && (sensor.Status = KinectStatus.Connected) then
                sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30)
                sensor.DepthStream.Enable(depthFormat)
               
               
                (*let smoothingParams = new TransformSmoothParameters( Smoothing = 0.5f,
                                                                     Correction = 0.5f,
                                                                     Prediction = 0.5f,
                                                                     JitterRadius = 0.05f,
                                                                     MaxDeviationRadius = 0.04f *)
                                                               
                sensor.SkeletonStream.Enable()
                sensor.SkeletonStream.TrackingMode <- mode

                sensor.AllFramesReady.Add this.AllframeReady

                interactionStream <- new InteractionStream(sensor, new DummyInteractionClient()) //todo controllare il null
                interactionStream.InteractionFrameReady.Add this.InteractionReady

                sensor.Start()


        member this.InitializeStreams () = this.InitializeStreams(DEFAULT_DEPTH_FORMAT, SkeletonTrackingMode.Default)

        member this.DefaultStart() = 
            this.Attach() |> ignore
            this.InitializeStreams()


       
  