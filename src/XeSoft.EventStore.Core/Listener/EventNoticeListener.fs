namespace XeSoft.EventStore.Core.Listener

module EventNoticeListener =

    open FSharp.Control
    open Microsoft.Extensions.Logging

    type EventTypeStr = string

    let fromNotifyListener
        (log: ILogger)
        (eventTypeFilter: Set<EventTypeStr>)
        (payloadSeq: TaskSeq<NotifyListener.Payload>)
        : TaskSeq<EventNotice>
        =
        payloadSeq
        |> TaskSeq.choose (fun payload ->
            match EventNotice.fromPayload payload with
            | Ok x  when Set.isEmpty eventTypeFilter
                    || Set.contains x.EventType eventTypeFilter ->
                Some x
            | Ok _ ->
                None
            | Error err ->
                log.LogWarning("invalid notification {Payload} error {Error}", payload, err)
                None
        )
