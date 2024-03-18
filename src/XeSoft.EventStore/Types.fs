namespace XeSoft.EventStore

module InternalTypes =

    open System

    type EventType = string
    type EventData = string
    type MetaJson = string

    type CommitEvent =
        {
            Type: EventType
            Data: EventData option
            EventMeta: MetaJson option
        }

    type StreamCommit =
        {
            StreamId: Guid
            ExpectedVersion: int
            Events: CommitEvent list
            StreamMeta: MetaJson option
        }

    type StreamEvent =
        {
            Position: int64
            StreamId: Guid
            Version: int
            Type: EventType
            Data: EventData option
            Meta: string
            LogDate: DateTime
        }


[<AutoOpen>]
module Types =

    open System

    type EventType = string
    type EventData = string
    type MetaJson = string

    type EventCodec<'event> =
        {
            Decodes: Map<EventType, EventData option -> 'event>
            Encode: 'event -> EventType * EventData option
        }

    type MetaCodec =
        {
            Decode: MetaJson -> Map<string, obj>
            Encode: Map<string, obj> -> MetaJson
        }

    type CommitEvent<'event> =
        {
            Event: 'event
            EventMeta: Map<string, obj>
        }

    type StreamCommit<'event> =
        {
            StreamId: Guid
            ExpectedVersion: int
            Events: CommitEvent<'event> list
            StreamMeta: Map<string, obj>
        }

    type StreamEvent<'event> =
        {
            Position: int64
            StreamId: Guid
            Version: int
            Event: 'event
            Meta: Map<string, obj>
            LogDate: DateTimeOffset
        }

    type EventStoreWriteError =
        | OperationFailed of exn
        | ConcurrencyViolation


