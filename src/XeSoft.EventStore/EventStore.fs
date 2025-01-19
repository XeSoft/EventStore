namespace XeSoft.EventStore

module EventStore =

    open System
    open XeSoft.EventStore.InternalTypes
    open XeSoft.EventStore.Types

    module Stream =

        let count (config: EventStoreConfig) (streamId: Guid) =
            Sql.read config ReadFn.scalar<int> [DbOp.readCount streamId]

        let tryReadFirst (config: EventStoreConfig) (streamId: Guid) =
            Sql.read config ReadFn.tryFirst<StreamEvent> [DbOp.readHead streamId]

        let read (config: EventStoreConfig) (streamId: Guid) (sinceVersion: int) (count: int) =
            Sql.read config ReadFn.list<StreamEvent> [DbOp.read streamId sinceVersion count]

        let append (config: EventStoreConfig) (commit: StreamCommit) =
            commit.Events
            |> List.mapi (fun i e ->
                let version = commit.ExpectedVersion + i
                DbOp.append commit.StreamId version e.Type e.Data e.Meta
            )
            |> Sql.write config 


    module Replay =

        let fromStateWhile
            (config: EventStoreConfig)
            (canContinue: 'state -> bool)
            (apply: 'state -> StreamEvent -> 'state)
            (streamId: Guid)
            (sinceVersion: int)
            (initial: 'state)
            =
            let initial_ = { State = initial; Version = sinceVersion }
            let canContinue_ (streamState: StreamState<'state>) =
                canContinue streamState.State
            let apply_ (streamState: StreamState<'state>) (event: StreamEvent) =
                let state = apply streamState.State event
                { State = state; Version = event.Version }
            Sql.read config (ReadFn.foldWhile canContinue_ apply_ initial_) [DbOp.readUnbounded streamId sinceVersion]

        let fromState
            (config: EventStoreConfig)
            (apply: 'state -> StreamEvent -> 'state)
            (streamId: Guid)
            (sinceVersion: int)
            (initial: 'state)
            =
            let canContinue _ = true
            fromStateWhile config canContinue apply streamId sinceVersion initial

        let allWhile
            (config: EventStoreConfig)
            (canContinue: 'state -> bool)
            (apply: 'state -> StreamEvent -> 'state)
            (streamId: Guid)
            (initial: 'state)
            =
            fromStateWhile config canContinue apply streamId 0 initial

        let all
            (config: EventStoreConfig)
            (apply: 'state -> StreamEvent -> 'state)
            (streamId: Guid)
            (initial: 'state)
            =
            fromState config apply streamId 0 initial


    module AllStreams =

        let read (config: EventStoreConfig) (sincePosition: int64) (count: int) =
            Sql.read config ReadFn.list<StreamEvent> [DbOp.readAll sincePosition count]

        let readOfTypes (config: EventStoreConfig) (sincePosition: int64) (types: string[]) (count: int) =
            Sql.read config ReadFn.list<StreamEvent> [DbOp.readAllOfTypes sincePosition types count]

