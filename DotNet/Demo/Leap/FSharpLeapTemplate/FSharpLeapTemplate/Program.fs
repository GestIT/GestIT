open Leap

type HWListener() =
  inherit Listener()

  let lk = new obj()
  let p s =  lock lk (fun () -> printfn "%s" s)

  override this.OnInit c =
    p "Init..."

  override this.OnConnect c =
    p "Connected..."

  override this.OnExit c =
    p "Exited..."

  override this.OnFrame c =
    let frame = c.Frame()
    (sprintf "Frame id: %d, timestamp: %d, hands: %d, fingers: %d, tools: %d" frame.Id frame.Timestamp frame.Hands.Count frame.Fingers.Count frame.Tools.Count) |> p
    if frame.Fingers.Count > 0 then
      for f in frame.Fingers do
        (sprintf "Finger id: %d" f.Id) |> p

let listener = new HWListener()
let controller = new Controller()

controller.AddListener(listener) |> ignore

printfn "Press enter to quit"
System.Console.ReadLine() |> ignore

controller.RemoveListener(listener) |> ignore
controller.Dispose()

