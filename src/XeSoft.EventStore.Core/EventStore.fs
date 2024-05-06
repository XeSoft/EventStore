namespace XeSoft.EventStore.Core

module EventStore =

    open System
    open System.Threading
    open XeSoft.EventStore.Core.Types

    module Stream =

        let count (connectString: string) (cancelToken: CancellationToken) (streamId: Guid) =
            Sql.read connectString cancelToken ReadFn.scalar<int> [DbOp.readCount streamId]

        let tryReadFirst (connectString: string) (cancelToken: CancellationToken) (streamId: Guid) =
            Sql.read connectString cancelToken ReadFn.tryFirst<StreamEvent> [DbOp.readHead streamId]

        let read (connectString: string) (cancelToken: CancellationToken) (streamId: Guid) (sinceVersion: int) (count: int) =
            Sql.read connectString cancelToken ReadFn.list<StreamEvent> [DbOp.read streamId sinceVersion count]

        let append (connectString: string) (cancelToken: CancellationToken) (commit: StreamCommit) =
            let streamOps = commit.StreamMeta |> Option.map (DbOp.setStreamMeta commit.StreamId) |> Option.toList
            commit.Events
            |> List.mapi (fun i e ->
                let version = commit.ExpectedVersion + 1 + i
                DbOp.append commit.StreamId version e.Type e.Data e.Meta
            )
            |> List.append streamOps
            |> Sql.write connectString cancelToken

        let setMeta (connectString: string) (cancelToken: CancellationToken) (streamId: Guid) (meta: MetaJson) =
            Sql.write connectString cancelToken [DbOp.setStreamMeta streamId meta]


    module AllStreams =

        let read (connectString: string) (cancelToken: CancellationToken) (sincePosition: int64) (count: int) =
            Sql.read connectString cancelToken ReadFn.list<StreamEvent> [DbOp.readAll sincePosition count]

        let readOfTypes (connectString: string) (cancelToken: CancellationToken) (sincePosition: int64) (types: string[]) (count: int) =
            Sql.read connectString cancelToken ReadFn.list<StreamEvent> [DbOp.readAllOfTypes sincePosition types count]

