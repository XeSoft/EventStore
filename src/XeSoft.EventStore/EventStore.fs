namespace XeSoft.EventStore

module EventStore =

    open System
    open XeSoft.EventStore.InternalTypes
    open XeSoft.EventStore.Types

    module Stream =

        let count (config: EventStoreConfig) (streamId: Guid) =
            Sql.read ReadFn.scalar<int> config [DbOp.readCount streamId]

        let tryReadFirst (config: EventStoreConfig) (streamId: Guid) =
            Sql.read ReadFn.tryFirst<StreamEvent> config [DbOp.readHead streamId]

        let read (config: EventStoreConfig) (streamId: Guid) (sinceVersion: int) (count: int) =
            Sql.read ReadFn.list<StreamEvent> config [DbOp.read streamId sinceVersion count]

        let append (config: EventStoreConfig) (commit: StreamCommit) =
            commit.Events
            |> List.mapi (fun i e ->
                let version = commit.ExpectedVersion + i
                DbOp.append commit.StreamId version e.Type e.Data e.Meta
            )
            |> Sql.write config 


    module Replay =

        let fromStateWhile
            (canContinue: 'state -> bool)
            (apply: 'state -> StreamEvent -> 'state)
            (initial: 'state)
            (config: EventStoreConfig)
            (streamId: Guid)
            (sinceVersion: int)
            =
            let initial_ = { State = initial; Version = sinceVersion }
            let canContinue_ (streamState: StreamState<'state>) =
                canContinue streamState.State
            let apply_ (streamState: StreamState<'state>) (event: StreamEvent) =
                let state = apply streamState.State event
                { State = state; Version = event.Version }
            Sql.read (ReadFn.foldWhile canContinue_ apply_ initial_) config [DbOp.readUnbounded streamId sinceVersion]

        let fromState
            (apply: 'state -> StreamEvent -> 'state)
            (initial: 'state)
            (config: EventStoreConfig)
            (streamId: Guid)
            (sinceVersion: int)
            =
            let canContinue _ = true
            fromStateWhile canContinue apply initial config streamId sinceVersion

        let allWhile
            (canContinue: 'state -> bool)
            (apply: 'state -> StreamEvent -> 'state)
            (initial: 'state)
            (config: EventStoreConfig)
            (streamId: Guid)
            =
            fromStateWhile canContinue apply initial config streamId 0

        let all
            (apply: 'state -> StreamEvent -> 'state)
            (initial: 'state)
            (config: EventStoreConfig)
            (streamId: Guid)
            =
            fromState apply initial config streamId 0


    module AllStreams =

        let read (config: EventStoreConfig) (sincePosition: int64) (count: int) =
            Sql.read ReadFn.list<StreamEvent> config [DbOp.readAll sincePosition count]

        let readOfTypes (config: EventStoreConfig) (sincePosition: int64) (types: string[]) (count: int) =
            Sql.read ReadFn.list<StreamEvent> config [DbOp.readAllOfTypes sincePosition types count]

