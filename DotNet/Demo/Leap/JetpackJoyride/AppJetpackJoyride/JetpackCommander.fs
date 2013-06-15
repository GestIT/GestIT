/// File:   JetpackCommander.fs
/// Author: Marta Martino
module Program
    open System.Windows.Forms
    open LeapDriver
    open ClonableLeapFrame
    open GestIT
    open GestIT.FSharp

    let mutable lastUp:TimeStamp = -1L

    let movefingerup (n:int) (x:LeapEventArgs) =
        let f = x.Frame
        let id = x.Id
        if (n = 5 && f.Timestamp - 25000L <= lastUp) || f.PointableList.Count > n || f.PointableList.Count = 0 then
            false
        else
            let finger =
                f.PointableList.Values
                |> Seq.maxBy (fun y -> y.Length)
            finger.Position.y >= 230.f

    let movefingerGoUp_h (sender, e:SensorEventArgs<LeapFeatureTypes, LeapEventArgs>) =
        SendKeys.SendWait("{UP}")

    let movefingerStay_h (sender, e:SensorEventArgs<LeapFeatureTypes, LeapEventArgs>) =
        lastUp <- e.Event.Frame.Timestamp
        SendKeys.SendWait("{UP}")

    let s = new LeapDriver.LeapSensor()
#if CLASSICWAY
    let i1 = new Iter<_,_>(movefinger_goup)
    let c1 = new Choice<_,_>(i1, movefinger_stay)
    i1.Gesture.Add(fun (sender,e) -> SendKeys.SendWait("{UP}"))
    let netgioco1 = c1.ToGestureNet(s)
    let i2 = new Iter<_,_>(movefinger_stay)
    let c2 = new Choice<_,_>(i2, movefinger_goup)
    i2.Gesture.Add(fun (sender,e) -> lastUp <- e.Event.Frame.Timestamp
                                     SendKeys.SendWait("{UP}"))
    let netgioco2 = c2.ToGestureNet(s)
#endif
    let movefinger_goup = (new GroundTerm<_,_>(LeapFeatureTypes.MoveFinger, movefingerup 2)) |-> movefingerGoUp_h
    let movefinger_stay = (new GroundTerm<_,_>(LeapFeatureTypes.MoveFinger, movefingerup 5)) |-> movefingerStay_h
    let expr1 = (!* movefinger_goup) |^| movefinger_stay
    expr1.ToGestureNet(s) |> ignore
    let expr2 = (!* movefinger_stay) |^| movefinger_goup
    expr2.ToGestureNet(s) |> ignore
    System.Console.ReadLine() |> ignore