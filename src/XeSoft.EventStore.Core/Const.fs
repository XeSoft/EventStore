namespace XeSoft.EventStore.Core.Const

module Table =
    let [<Literal>] Event = "event"
    let [<Literal>] PositionCounter = "positioncounter"
    let [<Literal>] Stream = "stream"

module Constraint =
    let [<Literal>] StreamIdVersion = "uk_event_streamid_version"

module Channel =
    let [<Literal>] EventRecorded = "eventrecorded"

