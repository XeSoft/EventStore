namespace XeSoft.EventStore.Core

open Microsoft.Extensions.Logging

type Env = {
    Log: ILogger
    ConnectString: string
    EventNotifyChannel: string
}


