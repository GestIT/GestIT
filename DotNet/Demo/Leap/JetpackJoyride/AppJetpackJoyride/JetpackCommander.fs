open System.Windows.Forms
open GestIT
open GestIT.FSharp
open GestIT.Leap

let mutable lastUp = -1L

let movefingerup (n:int) (x:LeapSensorEventArgs) =
  let f = x.Frame
  if (n = 5 && f.Timestamp - 25000L <= lastUp) || f.Fingers.Count > n || f.Fingers.Count = 0 then
    false
  else
    let finger = f.Fingers |> Seq.maxBy (fun y -> y.Length)
    finger.StabilizedTipPosition.y >= 230.f

let movefingerGoUp_h (s, f, e) =
  SendKeys.SendWait("{UP}")

let movefingerStay_h (_, _, e:LeapSensorEventArgs) =
  lastUp <- e.Frame.Timestamp
  SendKeys.SendWait("{UP}")

let s = new LeapSensor()
s.Controller.SetPolicyFlags(Leap.Controller.PolicyFlag.POLICYBACKGROUNDFRAMES)

type LeapFeatures =
| NewHand
| ActiveHand
| NewFinger
| ActiveFinger
| NewTool
| ActiveTool

s.Connect() |> ignore

let fs = new FusionSensor<_, _>()
fs.Listen(LeapFeatures.ActiveFinger, s.ActiveFinger)

let movefinger_goup = (new GroundTerm<_,_>(LeapFeatures.ActiveFinger, movefingerup 2)) |-> movefingerGoUp_h
let movefinger_stay = (new GroundTerm<_,_>(LeapFeatures.ActiveFinger, movefingerup 5)) |-> movefingerStay_h
let expr1 = (!* movefinger_goup) |^| movefinger_stay
expr1.ToGestureNet(fs) |> ignore
let expr2 = (!* movefinger_stay) |^| movefinger_goup
expr2.ToGestureNet(fs) |> ignore
System.Console.ReadLine() |> ignore

s.Dispose()
