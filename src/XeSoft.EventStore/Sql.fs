namespace XeSoft.EventStore

module Sql =

    open Npgsql
    open NpgsqlTypes
    open System

    module Internal =
        let inline v<'T> (value: 'T)  (dbType: NpgsqlDbType option)
            : NpgsqlParameter =
            let param = NpgsqlParameter<'T>()
            param.TypedValue <- value
            if dbType.IsSome then
                param.NpgsqlDbType <- dbType.Value
            param

        let p2<'T> (value: 'T) (dbType: NpgsqlDbType) (cmd: NpgsqlBatchCommand) =
            let parameter = v value (Some dbType)
            cmd.Parameters.Add(parameter) |> ignore
            cmd

        let p<'T> (value: 'T) (cmd: NpgsqlBatchCommand) =
            let parameter = v value None
            cmd.Parameters.Add(parameter) |> ignore
            cmd


    open Internal

    let append
        (streamId: Guid) (version: int)
        (type_: string) (data: string option) (meta: string)
        =
        NpgsqlBatchCommand(
            """
            INSERT
              INTO Event
                 ( Position
                 , StreamId
                 , Version
                 , Type
                 , Data
                 , Meta
                 )
            VALUES
                 ( NextPosition()
                 , $1
                 , $2
                 , $3
                 , $4
                 , $5
                 )
            """
        )
        |> p streamId
        |> p version
        |> p type_
        |> p2 data NpgsqlDbType.Jsonb
        |> p2 meta NpgsqlDbType.Jsonb


    let read (streamId: Guid) (sinceVersion: int) (count: int) =
        NpgsqlBatchCommand(
            """
            SELECT Position
                 , StreamId
                 , Version
                 , Type
                 , Data
                 , Meta
                 , LogDate
              FROM Event
             WHERE StreamId = $1
               AND Version > $2
             ORDER
                BY Version ASC
             LIMIT $3
            """
        )
        |> p streamId
        |> p sinceVersion
        |> p count


    let readUnbounded (streamId: Guid) (sinceVersion: int) =
        NpgsqlBatchCommand(
            """
            SELECT Position
                 , StreamId
                 , Version
                 , Type
                 , Data
                 , Meta
                 , LogDate
              FROM Event
             WHERE StreamId = $1
               AND Version > $2
             ORDER
                BY Version ASC
            """
        )
        |> p streamId
        |> p sinceVersion


    let findLastEventOfType (streamId: Guid) (eventType: string) =
        // Why the HAVING clause?
        // MAX will always cause a row to be returned, even if no rows match the WHERE clause.
        // And in that case, the returned row will have a NULL value for Version.
        // Adding the HAVING clause will cause this row to be discarded, resulting in no rows.
        // So this query ensures either no rows, or a single row with an non-null version.
        NpgsqlBatchCommand(
            """
            SELECT MAX(Version) AS Version
              FROM Event
             WHERE StreamId = $1
               AND Type = $2
            HAVING MAX(Version) IS NOT NULL
            """
        )
        |> p streamId
        |> p eventType


    let readHead (streamId: Guid) =
        NpgsqlBatchCommand(
            """
            SELECT Position
                 , StreamId
                 , Version
                 , Type
                 , Data
                 , Meta
                 , LogDate
              FROM Event
             WHERE StreamId = $1
             ORDER
                BY Version ASC
             LIMIT 1
            """
        )
        |> p streamId


    let readCount (streamId: Guid) =
        NpgsqlBatchCommand(
            """
            SELECT COUNT(*)
              FROM Event
             WHERE StreamId = $1
            ;
            """
        )
        |> p streamId


    let readAll (sincePosition: int64) (count: int) =
        NpgsqlBatchCommand(
            """
            SELECT Position
                 , StreamId
                 , Version
                 , Type
                 , Data
                 , Meta
                 , LogDate
              FROM Event
             WHERE Position > $1
             ORDER
                BY Position ASC
             LIMIT $2
            """
        )
        |> p sincePosition
        |> p count


    let readAllOfTypes (sincePosition: int64) (types: string[]) (count: int) =
        NpgsqlBatchCommand(
            """
            SELECT Position
                 , StreamId
                 , Version
                 , Type
                 , Data
                 , Meta
                 , LogDate
              FROM Event
             WHERE Position > $1
               AND Type = ANY($2)
             ORDER
                BY Position ASC
             LIMIT $3
            """
        )
        |> p sincePosition
        |> p types
        |> p count



