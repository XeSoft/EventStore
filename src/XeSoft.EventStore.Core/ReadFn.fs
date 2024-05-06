namespace XeSoft.EventStore.Core

module ReadFn =

    open Npgsql
    open System.Threading

    module Dapper =

        open Dapper

        let getParser<'T> (reader: NpgsqlDataReader) =
            // not using GetRowParser<'T> because of bug:
            // https://github.com/DapperLib/Dapper/issues/1822
            let parse = reader.GetRowParser(typeof<'T>)
            let f reader =
                parse.Invoke(reader) :?> 'T
            f


    let resizeArray<'T> (cancelToken: CancellationToken) (reader: NpgsqlDataReader) =
        task {
            let parse = Dapper.getParser<'T> reader
            let items = ResizeArray<'T>(5)
            let! _keepReading = reader.ReadAsync(cancelToken)
            let mutable keepReading = _keepReading
            while keepReading do
                let item = parse reader
                items.Add(item)
                let! _keepReading = reader.ReadAsync(cancelToken)
                keepReading <- _keepReading
            let! _ = reader.NextResultAsync(cancelToken)
            return items
        }

    let array<'T> (cancelToken: CancellationToken) (reader: NpgsqlDataReader) =
        task {
            let! items = resizeArray<'T> cancelToken reader
            return items.ToArray()
        }

    let list<'T> (cancelToken: CancellationToken) (reader: NpgsqlDataReader) =
        task {
            let! items = resizeArray<'T> cancelToken reader
            return List.ofSeq items
        }

    let tryFirst<'T> (cancelToken: CancellationToken) (reader: NpgsqlDataReader) =
        task {
            let parse = Dapper.getParser<'T> reader
            let mutable itemOpt = None
            let! wasRead = reader.ReadAsync(cancelToken)
            if wasRead then
                let item = parse reader
                itemOpt <- Some item
            let! _ = reader.NextResultAsync(cancelToken)
            return itemOpt
        }

    let first<'T> (cancelToken: CancellationToken) (reader: NpgsqlDataReader) =
        task {
            let parse = Dapper.getParser<'T> reader
            let! _ = reader.ReadAsync(cancelToken)
            let item = parse reader
            let! _ = reader.NextResultAsync(cancelToken)
            return item
        }

    let scalar<'T> (cancelToken: CancellationToken) (reader: NpgsqlDataReader) =
        task {
            let! _ = reader.ReadAsync(cancelToken)
            let item = reader.GetFieldValue<'T>(0)
            let! _ = reader.NextResultAsync(cancelToken)
            return item
        }

    let foldWhile<'state, 'T>
        (cancelToken: CancellationToken)
        (canContinue: 'state -> bool) (apply: 'state -> 'T -> 'state) (initial: 'state)
        (reader: NpgsqlDataReader)
        =
        task {
            let parse = Dapper.getParser<'T> reader
            let mutable state = initial
            let! _keepReading = reader.ReadAsync(cancelToken)
            let mutable keepReading = _keepReading
            while keepReading do
                let item = parse reader
                state <- apply state item
                let! _keepReading = reader.ReadAsync(cancelToken)
                keepReading <- _keepReading && canContinue state
            let! _ = reader.NextResultAsync(cancelToken)
            return state
        }

    let fold<'state, 'T>
        (cancelToken: CancellationToken)
        (apply: 'state -> 'T -> 'state) (initial: 'state)
        (reader: NpgsqlDataReader)
        =
        let alwaysContinue _ = true
        foldWhile cancelToken alwaysContinue apply initial reader

