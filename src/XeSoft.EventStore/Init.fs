namespace XeSoft.EventStore

module Init =

    open Dapper
    open Npgsql
    open System

    module Internal =

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


    open Internal

    let addDapperOptionTypeHandlers () =
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

