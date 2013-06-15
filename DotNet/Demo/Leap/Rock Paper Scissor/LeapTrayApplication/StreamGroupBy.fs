module Seq =
    let sortedGroupBy (projection : 'T -> 'Key) (source : seq<'T>) =
        let rec impl (e : System.Collections.Generic.IEnumerator<_>) groupKey members = seq { 
            if not (e.MoveNext()) then
                yield (groupKey, List.rev members :> seq<_>)
            else
                let element = e.Current
                let key = projection  element
                if (groupKey = key) then 
                    yield! impl e groupKey (element :: members)
                else
                    yield (groupKey, List.rev members :> seq<_>)
                    yield! impl e key (element :: [])
        }
        seq { 
            use e = source.GetEnumerator()
            if e.MoveNext() then
                yield! impl e (projection  (e.Current)) ((e.Current) :: [])
        }

let words = [ 10;12;14;13;12; 15;18;20;7;9;5;3 ]
    //|> Seq.sortBy (fun w -> w.Length)

words |> Seq.pairwise
      |> Seq.sortedGroupBy (fun (a,b) -> a <= b)
      |> Seq.filter(fun (increasing,pairs) -> increasing)
      |> Seq.map (fun (increasing,pairs) -> pairs |> Seq.map (fun (a,b) -> a))
      |> Seq.toList
      |> ignore

words |> Seq.scan (fun (increasing,oldElement) (newElement) -> (oldElement <= newElement, newElement)) (true, 100000)
      |> Seq.sortedGroupBy (fun (a,b) -> a)
      |> Seq.toList
      |> Seq.map (fun (increasing,pairs) -> pairs |> Seq.map (fun (a,b) -> b))
      |> Seq.toList
      |> ignore