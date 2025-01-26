namespace XeSoft.EventStore.Core.Utils

module Exn =

    open System

    let isCancellation (ex: exn) =
        match ex with
        | :? AggregateException as ex_ ->
            ex_.Flatten().InnerExceptions
            |> Seq.exists (fun e -> e :? OperationCanceledException)
        | :? OperationCanceledException ->
            true
        | _ ->
            false

