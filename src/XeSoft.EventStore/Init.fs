namespace XeSoft.EventStore

module Init =

    open Dapper
    open Npgsql
    open System

    module Dapper =

        let DbNullObj = box DBNull.Value

        type OptionHandler<'T>() =
            inherit SqlMapper.TypeHandler<Option<'T>>()

            override __.SetValue(param, value) =
                match value with
                | Some x ->
                    match param with
                    | :? NpgsqlParameter<'T> as p ->
                        p.TypedValue <- x
                    | _ ->
                        param.Value <- box x
                | None ->
                    param.Value <- null

            override __.Parse value =
                if value = DbNullObj || isNull value then
                    None
                else
                    Some (value :?> 'T)

        type VOptionHandler<'T>() =
            inherit SqlMapper.TypeHandler<ValueOption<'T>>()

            override __.SetValue(param, value) =
                match value with
                | ValueSome x ->
                    match param with
                    | :? NpgsqlParameter<'T> as p ->
                        p.TypedValue <- x
                    | _ ->
                        param.Value <- box x
                | ValueNone ->
                    param.Value <- null

            override __.Parse value =
                if value = DbNullObj || isNull value then
                    ValueNone
                else
                    ValueSome (value :?> 'T)

        let addOptionTypeHandlers () =
            SqlMapper.AddTypeHandler(OptionHandler<Boolean>())
            SqlMapper.AddTypeHandler(OptionHandler<Byte>())
            SqlMapper.AddTypeHandler(OptionHandler<Byte[]>())
            SqlMapper.AddTypeHandler(OptionHandler<Char>())
            SqlMapper.AddTypeHandler(OptionHandler<DateTime>())
            SqlMapper.AddTypeHandler(OptionHandler<DateTimeOffset>())
            SqlMapper.AddTypeHandler(OptionHandler<Decimal>())
            SqlMapper.AddTypeHandler(OptionHandler<Double>())
            SqlMapper.AddTypeHandler(OptionHandler<Guid>())
            SqlMapper.AddTypeHandler(OptionHandler<Int16>())
            SqlMapper.AddTypeHandler(OptionHandler<Int32>())
            SqlMapper.AddTypeHandler(OptionHandler<Int64>())
            SqlMapper.AddTypeHandler(OptionHandler<Single>())
            SqlMapper.AddTypeHandler(OptionHandler<String>())
            SqlMapper.AddTypeHandler(OptionHandler<TimeSpan>())
            SqlMapper.AddTypeHandler(OptionHandler<UInt16>())
            SqlMapper.AddTypeHandler(OptionHandler<UInt32>())
            SqlMapper.AddTypeHandler(OptionHandler<UInt64>())

            SqlMapper.AddTypeHandler(VOptionHandler<Boolean>())
            SqlMapper.AddTypeHandler(VOptionHandler<Byte>())
            SqlMapper.AddTypeHandler(VOptionHandler<Byte[]>())
            SqlMapper.AddTypeHandler(VOptionHandler<Char>())
            SqlMapper.AddTypeHandler(VOptionHandler<DateTime>())
            SqlMapper.AddTypeHandler(VOptionHandler<DateTimeOffset>())
            SqlMapper.AddTypeHandler(VOptionHandler<Decimal>())
            SqlMapper.AddTypeHandler(VOptionHandler<Double>())
            SqlMapper.AddTypeHandler(VOptionHandler<Guid>())
            SqlMapper.AddTypeHandler(VOptionHandler<Int16>())
            SqlMapper.AddTypeHandler(VOptionHandler<Int32>())
            SqlMapper.AddTypeHandler(VOptionHandler<Int64>())
            SqlMapper.AddTypeHandler(VOptionHandler<Single>())
            SqlMapper.AddTypeHandler(VOptionHandler<String>())
            SqlMapper.AddTypeHandler(VOptionHandler<TimeSpan>())
            SqlMapper.AddTypeHandler(VOptionHandler<UInt16>())
            SqlMapper.AddTypeHandler(VOptionHandler<UInt32>())
            SqlMapper.AddTypeHandler(VOptionHandler<UInt64>())


    module Db =

        type Pos = { Event: int64; Counter: int64 }

        let taskTryEx f arg =
            task {
                try
                    let! ret = f arg
                    return Ok ret
                with ex ->
                    return Error ex
            }

        let getSqlOps tables =
            let inline missing table =
                not (List.contains table tables)
            [
                if missing Const.Table.Event then
                    NpgsqlBatchCommand(DbInit.event)
                    NpgsqlBatchCommand(DbInit.eventNotification)
                if missing Const.Table.PositionCounter then
                    NpgsqlBatchCommand(DbInit.positionCounter)
            ]

        let runCreate cfg ops =
            match ops with
            | [] ->
                task { return Ok () }
            | ops ->
                taskTryEx (Sql.write cfg) ops


        let init cfg =
            task {
                let getTables = [NpgsqlBatchCommand(DbInit.getTableNames)]
                match! taskTryEx (Sql.read cfg ReadFn.list<string>) getTables with
                | Error ex -> return Error ex
                | Ok tables ->
                    let ops = getSqlOps tables
                    match! runCreate cfg ops with
                    | Error ex -> return Error ex
                    | Ok () ->
                        match! taskTryEx (Sql.read cfg ReadFn.tryFirst<Pos>) [NpgsqlBatchCommand(DbInit.getPositions)] with
                        | Error ex -> return Error ex
                        | Ok None -> return Error (exn "position counter missing")
                        | Ok (Some pos) ->
                            if pos.Event <> pos.Counter then
                                return! taskTryEx (Sql.write cfg) [NpgsqlBatchCommand(DbInit.syncPosition)]
                            else
                                return Ok ()
            }


    let run cfg =
        Dapper.addOptionTypeHandlers ()
        Db.init cfg

