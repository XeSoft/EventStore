namespace XeSoft.EventStore

module Extensions =

    open Npgsql

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

