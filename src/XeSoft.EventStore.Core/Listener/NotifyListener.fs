namespace XeSoft.EventStore.Core.Listener

module NotifyListener =

    open FSharp.Control
    open Microsoft.Extensions.Logging
    open Npgsql
    open System.Threading
    open XeSoft.EventStore.Core
    open XeSoft.EventStore.Core.Utils

    type Payload = string

    type private LogMarker = interface end
    let private logPrefix = Logger.getModuleFullName<LogMarker> ()

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
        { Log = log; ConnectString = connectString; EventNotifyChannel = channelName }
        : TaskSeq<Payload> * CancellationTokenSource
        =
        let cts = new CancellationTokenSource()
        taskSeq {
            use _ = log.BeginScope($"{logPrefix}.start")
            use cancelSource = cts // dispose when stopped
            let cancelToken = cancelSource.Token
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
                // using queue as interim buffer b/c of synchronous delegate API
                let queue = System.Collections.Concurrent.ConcurrentQueue<Payload>()
                use _ = conn.Notification.Subscribe(fun e -> queue.Enqueue(e.Payload))
                do!
                    log.LogInformation("opening connection")
                    conn.OpenAsync(cancelToken)
                let! _ =
                    log.LogInformation("starting LISTEN")
                    use cmd = new NpgsqlCommand($"LISTEN {channelName}")
                    cmd.Connection <- conn
                    cmd.ExecuteNonQueryAsync(cancelToken)
                let mutable item = ""
                while true do
                    log.LogDebug("waiting for notification")
                    do! conn.WaitAsync(cancelToken)
                    while queue.TryDequeue(&item) do
                        log.LogDebug("yielding payload @Payload", item)
                        yield item
            with
            | ex when Exn.isCancellation ex ->
                log.LogInformation("canceled")
            | ex ->
                log.LogCritical(ex, "failed")
                cancelSource.Cancel()
        }, cts


