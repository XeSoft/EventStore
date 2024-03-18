namespace XeSoft.EventStore

module Extensions =

    open Dapper
    open Npgsql
    open System.Threading

    type NpgsqlDataReader with

        member reader.getParser<'T>() =
            // not using GetRowParser<'T> because of bug:
            // https://github.com/DapperLib/Dapper/issues/1822
            let parse = reader.GetRowParser(typeof<'T>)
            let f reader =
                parse.Invoke(reader) :?> 'T
            f

        member reader.resizeArray<'T>(?cancel: CancellationToken) =
            let cancel = defaultArg cancel CancellationToken.None
            task {
                let parse = reader.getParser<'T>()
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

        member reader.array<'T>(?cancel: CancellationToken) =
            let cancel = defaultArg cancel CancellationToken.None
            task {
                let! items = reader.resizeArray<'T>(cancel)
                return items.ToArray()
            }

        member reader.list<'T>(?cancel: CancellationToken) =
            let cancel = defaultArg cancel CancellationToken.None
            task {
                let! items = reader.resizeArray<'T>(cancel)
                return List.ofSeq items
            }

        member reader.tryFirst<'T>(?cancel: CancellationToken) =
            let cancel = defaultArg cancel CancellationToken.None
            task {
                let parse = reader.getParser<'T>()
                let mutable itemOpt = None
                let! wasRead = reader.ReadAsync(cancel)
                if wasRead then
                    let item = parse reader
                    itemOpt <- Some item
                let! _ = reader.NextResultAsync(cancel)
                return itemOpt
            }

        member reader.first<'T>(?cancel: CancellationToken) =
            let cancel = defaultArg cancel CancellationToken.None
            task {
                let parse = reader.getParser<'T>()
                let! _ = reader.ReadAsync(cancel)
                let item = parse reader
                let! _ = reader.NextResultAsync(cancel)
                return item
            }

        member reader.scalar<'T>(?cancel: CancellationToken) =
            let cancel = defaultArg cancel CancellationToken.None
            task {
                let! _ = reader.ReadAsync(cancel)
                let item = reader.GetFieldValue<'T>(0)
                let! _ = reader.NextResultAsync(cancel)
                return item
            }

        member reader.foldWhile<'state, 'T>(initial: 'state, apply: 'state -> 'T -> 'state, canContinue: 'state -> bool, ?cancel: CancellationToken) =
            let cancel = defaultArg cancel CancellationToken.None
            task {
                let parse = reader.getParser<'T>()
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

        member reader.fold<'state, 'T>(initial: 'state, apply: 'state -> 'T -> 'state, ?cancel: CancellationToken) =
            let cancel = defaultArg cancel CancellationToken.None
            let alwaysContinue _ = true
            reader.foldWhile(initial, apply, alwaysContinue, cancel)


    type System.Exception with
        // to detect concurrency violations
        // catch PostgresException
        // check for the constraint on StreamId and Version
        static member isConcurrencyViolation (ex: exn) =
            match ex with
            | :? PostgresException as ex when ex.ConstraintName = Const.Constraint.StreamIdVersion ->
                true
            | _ ->
                false

        static member isEventStoreRetriable (ex: exn) =
            match ex with
            | :? PostgresException as ex
                // concurrency violation or PG transient
                when ex.ConstraintName = Const.Constraint.StreamIdVersion
                || ex.IsTransient ->
                true
            | :? NpgsqlException as ex when ex.IsTransient ->
                true
            | _ ->
                false

