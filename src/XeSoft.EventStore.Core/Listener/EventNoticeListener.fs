namespace XeSoft.EventStore.Core.Listener

module EventNoticeListener =

    open FSharp.Control
    open Microsoft.Extensions.Logging
    open System.Threading
    open XeSoft.EventStore.Core
    open XeSoft.EventStore.Core.Utils

    type private LogMarker = interface end
    let private logPrefix = Logger.getModuleFullName<LogMarker> ()

    type EventTypeStr = string

    let start
        ({ Log = log } as env)
        (eventTypeFilter: Set<EventTypeStr>)
        : TaskSeq<EventNotice> * CancellationTokenSource =

        let chooserFiltered payload =
            match EventNotice.fromPayload payload with
            | Ok notice when Set.contains notice.EventType eventTypeFilter ->
                Some notice
            | Ok _notice ->
                None // ignored
            | Error err ->
                log.LogWarning("invalid notification {Payload} error {Error}", payload, err)
                None

        let chooserUnfiltered payload =
            match EventNotice.fromPayload payload with
            | Ok notice ->
                Some notice
            | Error err ->
                log.LogWarning("invalid notification {Payload} error {Error}", payload, err)
                None

        let payloadSource, cts = NotifyListener.start env
        let chooser =
            if Set.isEmpty eventTypeFilter then // make this decision once
                chooserUnfiltered
            else
                chooserFiltered
        taskSeq {
            use _ = log.BeginScope($"{logPrefix}.start")
            yield! payloadSource |> TaskSeq.choose chooser
        }, cts
