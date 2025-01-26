namespace XeSoft.EventStore.Core.Listener

open System

type EventNotice =
    {
        Position: int64
        StreamId: Guid
        Version: int
        EventType: string
    }
with
    static member fromPayload (payload: string) =
        // { position }/{ stream id }/{ version }/{ event type }
        match payload.Split('/') with
        | [| positionStr; streamIdStr; versionStr; eventType |] ->
            match Int64.TryParse(positionStr) with
            | false, _ -> Error $"position {positionStr}"
            | true, position ->
                match Guid.TryParse(streamIdStr) with
                | false, _ -> Error $"stream id {streamIdStr}"
                | true, streamId ->
                    match Int32.TryParse(versionStr) with
                    | false, _ -> Error $"version {versionStr}"
                    | true, version ->
                        Ok {
                            Position = position
                            StreamId = streamId
                            Version = version
                            EventType = eventType
                        }
        | _ ->
            Error "payload format"

