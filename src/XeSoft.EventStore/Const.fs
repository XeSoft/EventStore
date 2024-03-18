namespace XeSoft.EventStore.Const

module Table =
    let [<Literal>] Event = "event"
    let [<Literal>] PositionCounter = "positioncounter"

module Constraint =
    let [<Literal>] StreamIdVersion = "uk_event_streamid_version"

module Channel =
    let [<Literal>] EventRecorded = "eventrecorded"

module Meta =
    let [<Literal>] Causation = "Causation"
    let [<Literal>] Permission = "Permission"
    let [<Literal>] TraceId = "TraceId"

module Default =
    let [<Literal>] BatchSize = 1000
