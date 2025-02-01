namespace XeSoft.EventStore.Core.Utils.FunctionalProcess

open System.Threading.Tasks
open FSharp.Control

type SimpleProcess<'initArg, 'model, 'msg, 'effect> = {
    Init: 'initArg -> 'model * 'msg
    Update: 'model -> 'msg -> 'model * List<'effect>
    //Services: 'model -> List<'service>
    //Stopped: 'model -> bool
}

type SimpleSideEffects<'effect, 'msg> = {
    Perform: 'effect -> Task<'msg>
    //StartService: 'service -> TaskSeq<'msg> * IDisposable
    //Stop: unit -> unit
}


module SimpleProcess =

    let test (proc: SimpleProcess<'initArg, 'model, 'msg, 'effect>) (model: 'model) (msgs: List<'msg>) =
        let init = model, []
        let update (model, effects) msg =
            let (model, newEffects) = proc.Update model msg
            (model, List.append effects newEffects)
        List.fold update init msgs

    let run (sideEffects: SimpleSideEffects<'effect, 'msg>) (proc: SimpleProcess<'initArg, 'model, 'msg, 'effect>) (initArg: 'initArg) : Task<Result<'model, exn>> =
        task {
            try
                let initModel, initMsg = proc.Init initArg
                // while, b/c recursive Task doesn't do tail call optimization, and can result in OOM exception
                let mutable model = initModel
                let mutable msgs = [initMsg]
                while not (List.isEmpty msgs) do
                    // process messages
                    let nModel, nEffects =
                        match msgs with
                        | [] -> (model, [])
                        | _ -> test proc model msgs
                    model <- nModel
                    // run all effects in parallel
                    // When sequencing matters Update will return
                    //   one Effect at a time for control flow anyway
                    let! nMsgs = nEffects |> List.map sideEffects.Perform |> Task.WhenAll
                    msgs <- List.ofArray nMsgs
                return Ok model
            with ex ->
                return Error ex
        }
