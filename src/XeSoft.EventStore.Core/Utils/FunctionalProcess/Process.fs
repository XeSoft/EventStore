namespace XeSoft.EventStore.Core.Utils.FunctionalProcess

open FSharp.Control
open System.Threading
open System.Threading.Tasks

type ProcessCore<'model, 'msg, 'effect, 'service> = {
    Update: 'model -> 'msg  -> 'model * List<'effect>
    Services: 'model -> List<ServiceId * 'service>
    IsStopped: 'model -> bool
}

type Process<'model, 'msg, 'effect, 'service, 'initArg, 'resumeArg> = {
    Core: ProcessCore<'model, 'msg, 'effect, 'service>
    Init: 'initArg -> 'model * 'msg
    Resume: Option<'resumeArg -> 'msg>
}

type SideEffects<'effect, 'msg, 'service> = {
    Perform: 'effect -> Task<'msg>
    StartService: 'service -> TaskSeq<'msg> * CancellationTokenSource
}

module Process =

    open Microsoft.Extensions.Logging
    open System.Threading.Channels
    open XeSoft.EventStore.Core.Utils

    type ProcessResult<'effect> = {
        Effects: List<'effect>
        Stopped: bool
    }

    let test
        (proc: ProcessCore<'model, 'msg, 'effect, 'service>)
        (model: 'model)
        (msgs: List<'msg>)
        : ProcessResult<'effect> =

        let rec loop
            (state: ProcessResult<'effect>)
            (remaining: List<'msg>)
            : ProcessResult<'effect> =

            match state.Stopped, remaining with
            | true, _
            | false, [] ->
                state
            | false, msg :: nRemaining ->
                let nModel, nEffects = proc.Update model msg
                let nStopped = proc.IsStopped nModel
                let nState = {
                    Effects = List.append state.Effects nEffects
                    Stopped = nStopped
                }
                loop nState nRemaining

        let initState = {
            Effects = []
            Stopped = false
        }
        loop initState msgs

    let run
        ((log: ILogger) as _deps)
        (sideEffects: SideEffects<'effect, 'msg, 'service>)
        (proc: ProcessCore<'model, 'msg, 'effect, 'service>)
        (model: 'model)
        (msg: 'msg)
        : Task<Result<'model, exn>> * CancellationTokenSource =

        // mechanisms of stopping
        // - msgCh.Complete() stops the read loop
        // - procCancelSource is for stopping from other threads

        let cts: CancellationTokenSource = new CancellationTokenSource()

        let msgCh =
            let options = BoundedChannelOptions(
                capacity = Default.MsgChannelSize, SingleReader = true, SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait)
            Channel.CreateBounded<'msg>(options)

        // this fn only to declutter, so inline
        let inline writeIfPossible msg =
            task { // will not throw if channel is closed
                let mutable done_ = false
                while not done_ do
                    let! canWrite = msgCh.Writer.WaitToWriteAsync()
                    done_ <- not canWrite || msgCh.Writer.TryWrite(msg)
            }

        task {
            use procCancelSource = cts
            let cancelToken = procCancelSource.Token
            let mutable model_ = model
            let mutable stopped_ = false
            let mutable activeSvcs_ = []
            try // no try..catch..finally in F#, so we use try..finally in try..catch
                try
                    log.LogDebug("starting")
                    // write initial msg
                    do! msgCh.Writer.WriteAsync(msg)
                    let mutable msg_ = Unchecked.defaultof<'msg>
                    // WaitToReadAsync returns false if channel is marked complete
                    while! msgCh.Reader.WaitToReadAsync(cancelToken) do
                        while msgCh.Reader.TryRead(&msg_) && not stopped_ do
                            log.LogDebug("read msg @Msg", msg)
                            let nModel, nEffects = proc.Update model_ msg_
                            model_ <- nModel
                            stopped_ <- proc.IsStopped model_
                            if not stopped_ then
                                let nServices = proc.Services model_
                                let stopped, dupes, toStop, toKeep, toStart = Services.diff activeSvcs_ nServices
                                // service change effects
                                for (svcId, _) in stopped do
                                    log.LogWarning("service stopped unexpectedly @ServiceId", svcId)
                                for svcId in dupes do
                                    log.LogWarning("service duplicate id @ServiceId", svcId)
                                for (svcId, cancelSource) in toStop do
                                    log.LogDebug("service stopping @ServiceId", svcId)
                                    cancelSource.Cancel()
                                let msgSources, startedSvcs =
                                    toStart
                                    |> List.map (fun (svcId, svc) ->
                                        log.LogDebug("service starting @ServiceId @Service", svcId, svc)
                                        let msgSource, cancelSource = sideEffects.StartService svc
                                        msgSource, (svcId, cancelSource)
                                    )
                                    |> List.unzip
                                activeSvcs_ <- List.append toKeep startedSvcs
                                // new service msg sources, send to msg channel
                                for msgSource in msgSources do
                                    backgroundTask {
                                        do! msgSource |> TaskSeq.iterAsync writeIfPossible
                                    } |> ignore
                                // execute effects, send to msg channel
                                for effect in nEffects do
                                    backgroundTask {
                                        try
                                            cancelToken.ThrowIfCancellationRequested()
                                            log.LogDebug("starting effect @Effect", effect)
                                            let! effectMsg = sideEffects.Perform effect
                                            do! writeIfPossible effectMsg
                                        with
                                        | ex when Exn.isCancellation ex ->
                                            log.LogDebug("effect canceled @Effect", effect)
                                        | ex ->
                                            // an effect should inform logic of an error
                                            // else it is intentionally crashing the process
                                            log.LogError(ex, "effect failed @Effect", effect)
                                            procCancelSource.Cancel()
                                    } |> ignore
                        if stopped_ then
                            log.LogDebug("stopped")
                            msgCh.Writer.Complete()
                    // stop effects
                    procCancelSource.Cancel()
                finally
                    log.LogDebug("cleanup")
                    // in case we got here via crash so further writes are skipped
                    msgCh.Writer.TryComplete() |> ignore
                    // stop services
                    for (svcId, cancelSource) in activeSvcs_ do
                        log.LogDebug("service stopping @ServiceId", svcId)
                        cancelSource.Cancel()
                return Ok model_
            with
            | ex when Exn.isCancellation ex ->
                log.LogDebug("canceled")
                return Error ex
            | ex ->
                log.LogError(ex, "failed")
                procCancelSource.Cancel()
                return Error ex
        }, cts


