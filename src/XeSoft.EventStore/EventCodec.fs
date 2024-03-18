namespace XeSoft.EventStore

module EventCodec =

    open Microsoft.FSharp.Reflection
    open System
    open XeSoft.EventStore.Types

    /// Generate an event codec from an F# union type with the following properties.
    /// 
    /// - The union case name maps to the event type string.
    /// - The union case data, if present, is encoded using the provided encode function.
    /// 
    /// NOTE: Multiple case parameters are not supported. Each case should have a single parameter or no parameters.
    /// If multiple parameters are needed, consider using a record as your single parameter.
    let generateFromUnion<'eventUnion>
        (decode: Type -> string -> obj)
        (encode: obj -> string)
        : EventCodec<'eventUnion>
        =
        let decodes_ =
            FSharpType.GetUnionCases(typeof<'eventUnion>)
            |> Array.map (fun case ->
                let eventType = case.Name
                let decode =
                    match Array.tryHead (case.GetFields()) with
                    | None ->
                        let inst =
                            FSharpValue.MakeUnion(case, Array.empty)
                            :?> 'eventUnion
                        fun (_: string option) -> inst
                    | Some field ->
                        fun (sOpt: string option) ->
                            let o = decode field.PropertyType sOpt.Value
                            FSharpValue.MakeUnion(case, [|o|]) :?> 'eventUnion
                eventType, decode
            )
            |> Map.ofArray
        let encode_ (value: 'eventUnion) =
            let case, fieldValues = FSharpValue.GetUnionFields(value, typeof<'eventUnion>)
            let jsonOpt =
                Array.tryHead fieldValues
                |> Option.map encode
            (case.Name, jsonOpt)
        {
            Decodes = decodes_
            Encode = encode_
        }


    let generateMetaCodec
        (decode: Type -> string -> obj)
        (encode: obj -> string)
        : MetaCodec
        =
        let decode_ metaString =
            decode typeof<Map<string, obj>> metaString :?> Map<string, obj>
        let encode_ map =
            encode map
        {
            Decode = decode_
            Encode = encode_
        }


    /// Merge codecs for old events into the current event codec.
    /// The resulting parser will deserialize both current and old events.
    /// Old events will be translated to a current event using the upgrade function.
    ///
    /// NOTE: Old and current type names cannot overlap. This will cause new types with the same name as the old types to fail to decode.
    let merge
        (old: EventCodec<'old>)
        (upgrade: 'old -> 'event)
        (codec: EventCodec<'event>)
        : EventCodec<'event>
        =
        let update newDecodes oldType oldParse =
            let newParse sOpt =
                oldParse sOpt |> upgrade
            Map.add oldType newParse newDecodes
        let decodes = old.Decodes |> Map.fold update codec.Decodes
        { codec with
            Decodes = decodes
        }

