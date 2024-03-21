namespace XeSoft.EventStore

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


    let resizeArray<'T> (reader: NpgsqlDataReader) (cancel: CancellationToken) =
        task {
            let parse = Dapper.getParser<'T> reader
            let items = ResizeArray<'T>(5)
            let! _keepReading = reader.ReadAsync(cancel)
            let mutable keepReading = _keepReading
            while keepReading do
                let item = parse reader
                items.Add(item)
                let! _keepReading = reader.ReadAsync(cancel)
                keepReading <- _keepReading
            let! _ = reader.NextResultAsync(cancel)
            return items
        }

    let array<'T> (reader: NpgsqlDataReader) (cancel: CancellationToken) =
        task {
            let! items = resizeArray<'T> reader cancel
            return items.ToArray()
        }

    let list<'T> (reader: NpgsqlDataReader) (cancel: CancellationToken) =
        task {
            let! items = resizeArray<'T> reader cancel
            return List.ofSeq items
        }

    let tryFirst<'T> (reader: NpgsqlDataReader) (cancel: CancellationToken) =
        task {
            let parse = Dapper.getParser<'T> reader
            let mutable itemOpt = None
            let! wasRead = reader.ReadAsync(cancel)
            if wasRead then
                let item = parse reader
                itemOpt <- Some item
            let! _ = reader.NextResultAsync(cancel)
            return itemOpt
        }

    let first<'T> (reader: NpgsqlDataReader) (cancel: CancellationToken) =
        task {
            let parse = Dapper.getParser<'T> reader
            let! _ = reader.ReadAsync(cancel)
            let item = parse reader
            let! _ = reader.NextResultAsync(cancel)
            return item
        }

    let scalar<'T> (reader: NpgsqlDataReader) (cancel: CancellationToken) =
        task {
            let! _ = reader.ReadAsync(cancel)
            let item = reader.GetFieldValue<'T>(0)
            let! _ = reader.NextResultAsync(cancel)
            return item
        }

    let foldWhile<'state, 'T>
        (canContinue: 'state -> bool) (apply: 'state -> 'T -> 'state) (initial: 'state)
        (reader: NpgsqlDataReader) (cancel: CancellationToken)
        =
        task {
            let parse = Dapper.getParser<'T> reader
            let mutable state = initial
            let! _keepReading = reader.ReadAsync(cancel)
            let mutable keepReading = _keepReading
            while keepReading do
                let item = parse reader
                state <- apply state item
                let! _keepReading = reader.ReadAsync(cancel)
                keepReading <- _keepReading && canContinue state
            let! _ = reader.NextResultAsync(cancel)
            return state
        }

    let fold<'state, 'T>
        (apply: 'state -> 'T -> 'state) (initial: 'state)
        (reader: NpgsqlDataReader) (cancel: CancellationToken)
        =
        let alwaysContinue _ = true
        foldWhile alwaysContinue apply initial reader cancel

