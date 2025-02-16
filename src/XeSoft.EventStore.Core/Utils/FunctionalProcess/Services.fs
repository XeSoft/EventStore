namespace XeSoft.EventStore.Core.Utils.FunctionalProcess

type ServiceId = List<string>

module Services =

    open System.Threading

    /// returns: 
    /// stopped - services which have internally stopped, 
    /// dupes - service IDs which were listed twice (last one wins), 
    /// toStop - services which were active but are missing in the new service list, 
    /// toKeep - services which were active and still in the new service list, 
    /// toStart - services which were not active but are in the new service list
    let diff (activeServices: List<ServiceId * CancellationTokenSource>) (services: List<ServiceId * 'service>) =
        let stopped, running = activeServices |> List.partition (fun (_svcId, cancelSource) -> cancelSource.IsCancellationRequested)
        let keys = running |> List.map fst |> Set.ofList
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
            stopped, dupes, [], running, []
        else
            let toKeep, toStop = running |> List.partition (fun (k, _) -> Set.contains k newKeys)
            let toStart = newSvcs |> List.filter (fun (k, _) -> not (Set.contains k keys))
            stopped, dupes, toStop, toKeep, toStart
