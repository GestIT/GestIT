open GestIT.Leap

let sensor = new LeapSensor()

let lk = new obj()
let p s =  lock lk (fun () -> printfn "%s" s)

sensor.NewFinger.Add(fun e ->
  let frame = e.Frame
  (sprintf "Frame id: %d, timestamp: %d, hands: %d, fingers: %d, tools: %d" frame.Id frame.Timestamp frame.Hands.Count frame.Fingers.Count frame.Tools.Count) |> p

  e.ActivityFingers |> List.iter (fun f ->(sprintf "Finger id: %d" f.Id) |> p)
)

sensor.Connect() |> ignore

printfn "Press enter to quit"
System.Console.ReadLine() |> ignore
