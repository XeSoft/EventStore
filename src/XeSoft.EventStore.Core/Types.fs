namespace XeSoft.EventStore.Core

module Types =

    open System

    type EventType = string
    type EventData = string
    type MetaJson = string

    type EventStoreConfig =
        {
            ConnectString: string
        }

    type CommitEvent =
        {
            Type: EventType
            Data: EventData option
            Meta: MetaJson option
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
            LogDate: DateTime
            Type: EventType
            EventData: EventData option
            EventMeta: MetaJson option
        }

