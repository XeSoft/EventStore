namespace XeSoft.EventStore

[<RequireQualifiedAccess>]
module Sql =

    open Npgsql
    open System.Threading
    open System.Threading.Tasks
    open XeSoft.EventStore.InternalTypes

    let write (cfg: EventStoreConfig) (cmds: NpgsqlBatchCommand seq) =
        task {
            use batch =  new NpgsqlBatch()
            for cmd in cmds do
                batch.BatchCommands.Add(cmd)
            use connection = new NpgsqlConnection(cfg.ConnectString)
            batch.Connection <- connection
            do! connection.OpenAsync(cfg.Cancel)
            do! batch.PrepareAsync(cfg.Cancel)
            let! _affectedRows = batch.ExecuteNonQueryAsync(cfg.Cancel)
            return ()
        }

    let read (processRead: NpgsqlDataReader -> CancellationToken -> Task<'T>) (cfg: EventStoreConfig) (cmds: NpgsqlBatchCommand seq) =
        task {
            use batch =  new NpgsqlBatch()
            for cmd in cmds do
                batch.BatchCommands.Add(cmd)
            use connection = new NpgsqlConnection(cfg.ConnectString)
            batch.Connection <- connection
            do! connection.OpenAsync(cfg.Cancel)
            do! batch.PrepareAsync(cfg.Cancel)
            use! reader = batch.ExecuteReaderAsync(cfg.Cancel)
            return! processRead reader cfg.Cancel
        }


