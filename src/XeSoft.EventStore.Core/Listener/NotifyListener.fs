namespace XeSoft.EventStore.Core.Listener

module NotifyListener =

    open FSharp.Control
    open Microsoft.Extensions.Logging
    open Npgsql
    open System.Threading
    open XeSoft.EventStore.Core.Utils

    type Payload = string

    (*
    ### Implementation explanation

    Npgsql delivers notifications via the NpgsqlConnection.Notification event delegate.

    By default, notifications are passively fetched during queries.
    Instead we want to actively listen for new notifications using WaitAsync.
    It only waits. Notifications are still delivered via the event delegate.
    It may be possible for multiple notifications to be delivered after each wait.
    *)

    /// Listen for notifications using Postgres LISTEN.
    /// The returned TaskSeq provides notification payloads.
    /// 
    /// The listener stops when CancelSource is canceled.
    /// Errors are logged using Log. On error, CancelSource is also canceled.
    let start
        ((log: ILogger, cancelSource: CancellationTokenSource, connectString: string) as _deps)
        (channelName: string)
        : TaskSeq<Payload> * CancellationTokenSource
        =
        taskSeq {
            try
                // mainly to rule out injection attacks for LISTEN cmd
                if PgIdentifier.isInvalid channelName then
                    invalidArg (nameof channelName) "not a valid postgres identifier"
                let connStr =
                    let builder = NpgsqlConnectionStringBuilder(connectString)
                    // connection needs to stay open
                    builder.TcpKeepAlive <- true
                    // no need to pay overhead for these
                    builder.Pooling <- false
                    builder.Enlist <- false
                    builder.ToString()
                use conn = new NpgsqlConnection(connStr)
                let queue = System.Collections.Concurrent.ConcurrentQueue<Payload>()
                use _ = conn.Notification.Subscribe(fun e -> queue.Enqueue(e.Payload))
                do!
                    log.LogDebug("opening connection")
                    conn.OpenAsync()
                let! _ =
                    log.LogDebug("starting LISTEN")
                    use cmd = new NpgsqlCommand($"LISTEN {channelName}")
                    cmd.Connection <- conn
                    cmd.ExecuteNonQueryAsync()
                let mutable item = ""
                while not cancelSource.IsCancellationRequested do
                    log.LogDebug("waiting for notification")
                    do! conn.WaitAsync()
                    while queue.TryDequeue(&item) do
                        log.LogDebug("yielding payload @Payload", item)
                        yield item

                log.LogDebug("canceled")
            with
            | ex when Exn.isCancellation ex ->
                log.LogDebug("canceled")
            | ex ->
                log.LogCritical(ex, "failed")
                cancelSource.Cancel()
        }, cancelSource


