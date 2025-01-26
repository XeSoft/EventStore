namespace XeSoft.EventStore.Core.Utils.FunctionalProcess

type ServiceId = List<string>

module Services =

    open System

    let diff (activeServices: List<ServiceId * IDisposable>) (services: List<ServiceId * 'service>) =
        let keys = activeServices |> List.map fst |> Set.ofList
        let (_dupes, _newKeys, _newSvcs) as init =
            List.empty, Set.empty, List.empty
        let update (svcId, svc) (dupes, newKeys, newSvcs) =
            if Set.contains svcId newKeys then
                svcId :: dupes, newKeys, newSvcs
            else
                dupes, Set.add svcId newKeys, (svcId, svc) :: newSvcs
        let dupes, newKeys, newSvcs =
            List.foldBack update services init
        if keys = newKeys then
            dupes, [], activeServices, []
        else
            let toKeep, toStop = activeServices |> List.partition (fun (k, _) -> Set.contains k newKeys)
            let toStart = newSvcs |> List.filter (fun (k, _) -> not (Set.contains k keys))
            dupes, toStop, toKeep, toStart
