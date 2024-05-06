namespace XeSoft.EventStore.Core

[<RequireQualifiedAccess>]
module Sql =

    open Npgsql
    open System.Threading
    open System.Threading.Tasks

    let write (connectString: string) (cancelToken: CancellationToken) (cmds: NpgsqlBatchCommand seq) =
        task {
            use batch =  new NpgsqlBatch()
            for cmd in cmds do
                batch.BatchCommands.Add(cmd)
            use connection = new NpgsqlConnection(connectString)
            batch.Connection <- connection
            do! connection.OpenAsync(cancelToken)
            do! batch.PrepareAsync(cancelToken)
            let! _affectedRows = batch.ExecuteNonQueryAsync(cancelToken)
            return ()
        }

    let read (connectString: string) (cancelToken: CancellationToken) (processRead: CancellationToken -> NpgsqlDataReader -> Task<'T>) (cmds: NpgsqlBatchCommand seq) =
        task {
            use batch =  new NpgsqlBatch()
            for cmd in cmds do
                batch.BatchCommands.Add(cmd)
            use connection = new NpgsqlConnection(connectString)
            batch.Connection <- connection
            do! connection.OpenAsync(cancelToken)
            do! batch.PrepareAsync(cancelToken)
            use! reader = batch.ExecuteReaderAsync(cancelToken)
            return! processRead cancelToken reader
        }


