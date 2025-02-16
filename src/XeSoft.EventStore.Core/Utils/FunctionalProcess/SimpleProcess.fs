namespace XeSoft.EventStore.Core.Utils.FunctionalProcess

open System.Threading.Tasks
open FSharp.Control

// This is all we need to run a process
type SimpleProcessCore<'model, 'msg, 'effect> = {
    Update: 'model -> 'msg -> 'model * List<'effect>
}

/// SimpleProcess is suitable for a workflow that is waited on for its return value.
/// Once an initial run completes, its resulting model can be used to resume from its previous state.
type SimpleProcess<'model, 'msg, 'effect, 'initArg, 'resumeArg> = {
    Core: SimpleProcessCore<'model, 'msg, 'effect>
    // Init and Resume just translate external data into arguments compatible with Update
    Init: 'initArg -> 'model * 'msg
    Resume: Option<'resumeArg -> 'msg>
}

type SimpleSideEffects<'effect, 'msg> = {
    Perform: 'effect -> Task<'msg>
}


module SimpleProcess =

    let test (proc: SimpleProcessCore<'model, 'msg, 'effect>) (model: 'model) (msgs: List<'msg>) =
        let init = model, []
        let update (model, effects) msg =
            let (model, newEffects) = proc.Update model msg
            (model, List.append effects newEffects)
        List.fold update init msgs

    let run (sideEffects: SimpleSideEffects<'effect, 'msg>) (proc: SimpleProcessCore<'model, 'msg, 'effect>) (model: 'model) (msg: 'msg) : Task<Result<'model, exn>> =
        task {
            try
                // while loop, b/c recursive Task does not tail call optimize and can result in OOM exception
                let mutable model_ = model
                let mutable msgs_ = [msg]
                while not (List.isEmpty msgs_) do
                    // process messages
                    let nModel, nEffects = test proc model_ msgs_
                    model_ <- nModel
                    // run all effects in parallel
                    // When sequencing matters Update will return
                    //   one Effect at a time for control flow anyway
                    let! nMsgs = nEffects |> List.map sideEffects.Perform |> Task.WhenAll
                    msgs_ <- List.ofArray nMsgs
                return Ok model
            with ex ->
                return Error ex
        }

    let start (sideEffects: SimpleSideEffects<'effect, 'msg>) (proc: SimpleProcess<'model, 'msg, 'effect, 'initArg, 'resumeArg>) (initArg: 'initArg) : Task<Result<'model, exn>> =
        let model, msg = proc.Init initArg
        run sideEffects proc.Core model msg

    let resume (sideEffects: SimpleSideEffects<'effect, 'msg>) (proc: SimpleProcess<'model, 'msg, 'effect, 'initArg, 'resumeArg>) (model: 'model) (resumeArg: 'resumeArg) : Task<Result<'model, exn>> =
        match proc.Resume with
        | None -> task { return Ok model }
        | Some resume ->
            let msg = resume resumeArg
            run sideEffects proc.Core model msg
