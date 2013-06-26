// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.

open System
open GestIT
open GestIT.FSharp
open KinectDriver
open Microsoft.Kinect
open Microsoft.Kinect.Toolkit.Interaction

[<EntryPoint>]
let main argv = 
    let driver =  new KinectDriver()
    driver.DefaultStart()

    let manosopragomito = fun (e:KinectEventArgs) -> (e.SkeletonData.Joints.Item JointType.HandRight).Position.Y >= (e.SkeletonData.Joints.Item JointType.ElbowRight).Position.Y

    let gripr = new GroundTerm<_, _>(KinectFeatureType.HandInteraction(InteractionHandType.Right, InteractionHandEventType.Grip), manosopragomito) |-> fun _ -> printfn "GRIP Right Hand"
    let ropen = new GroundTerm<_, _>(KinectFeatureType.HandInteraction(InteractionHandType.Right, InteractionHandEventType.GripRelease)) |-> fun _ -> printfn "RELEASE Right Hand"

    let click = gripr |>> ropen |-> fun _ -> printfn "Open then closed!"
    
    let net = click.ToGestureNet(driver)

    Console.ReadLine() |> ignore
    0 // return an integer exit code
