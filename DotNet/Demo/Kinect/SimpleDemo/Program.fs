// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.

open System
open GestIT
open GestIT.FSharp
open KinectDriver
open Microsoft.Kinect
open Microsoft.Kinect.Toolkit.Interaction
open System.Windows.Media.Media3D

[<EntryPoint>]
let main argv = 
    let driver =  new KinectDriver()
    driver.DefaultStart()

    let oldPos = ref (new Vector3D(0.,0.,0.))

    let handAboveElbow = fun (e:KinectEventArgs) -> (e.SkeletonData.Joints.Item JointType.HandRight).Position.Y >= (e.SkeletonData.Joints.Item JointType.ElbowRight).Position.Y
    let printPosition = fun ((x,y,z):GestureExpr<KinectFeatureType, KinectEventArgs> * KinectFeatureType * KinectEventArgs) -> 
                                let pos = z.SkeletonData.Joints.[JointType.HandRight].Position
                                printfn "MOVED (%A, %A, %A)" pos.X pos.Y pos.Z

    let griphandler = fun (x,y,z:KinectEventArgs) -> //((x,y,z):GestureExpr<KinectFeatureType, KinectEventArgs> * KinectFeatureType * KinectEventArgs) -> 
                                let pos = z.SkeletonData.Joints.[JointType.HandRight].Position
                                oldPos := new Vector3D(float pos.X,float pos.Y,float pos.Z)
                                printfn "GRIP Right Hand (%A, %A, %A)" (!oldPos).X (!oldPos).Y (!oldPos).Z 

    let handMovementCheck  = fun (e:KinectEventArgs) ->  
                                     let p1 = e.SkeletonData.Joints.[JointType.HandRight].Position
                                     let v1 = new Vector3D(float p1.X,float p1.Y,float p1.Z)
                                     let v2 = new Vector3D(float (!oldPos).X,float (!oldPos).Y,float (!oldPos).Z)
                                     let diff = (v2 - v1)                                    
                                     //printf "LEN: %A -> %A\r" v2 diff.Length
                                     diff.Length >= 0.1
                                                                      
       
    let gripr = new GroundTerm<_,_>(KinectFeatureType.HandInteraction(InteractionHandType.Right, InteractionHandEventType.Grip), handAboveElbow) |-> griphandler  
    let ropen = new GroundTerm<_,_>(KinectFeatureType.HandInteraction(InteractionHandType.Right, InteractionHandEventType.GripRelease))// |-> fun _ -> printfn "RELEASE Right Hand"
    let rmovefar = new GroundTerm<_,_>(KinectFeatureType.Joint(JointType.HandRight, JointTrackingState.Tracked), handMovementCheck) // |-> printPosition
    let rmove = new GroundTerm<_,_>(KinectFeatureType.Joint(JointType.HandRight, JointTrackingState.Tracked))

    let click = gripr |>> ((!* rmove) |^| (rmovefar |>> ropen) |-> fun _ -> printfn "Dragged then closed!") |^| ropen |-> fun _ -> printfn "Open then closed!"
    
    let net = click.ToGestureNet(driver)

    Console.ReadLine() |> ignore
    0 // return an integer exit code
