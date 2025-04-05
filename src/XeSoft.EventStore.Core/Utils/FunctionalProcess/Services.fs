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
    let diff (activeServices: List<ServiceId * CancellationTokenSource>) (requestedServices: List<ServiceId * 'service>) =
        let stopped, running = activeServices |> List.partition (fun (_, cts) -> cts.IsCancellationRequested)
        let oldServiceIds = running |> List.map fst |> Set.ofList
        let dupes, serviceIds, services =
            let update (serviceId, service) (dupes, serviceIds, services) =
                if Set.contains serviceId serviceIds then
                    serviceId :: dupes, serviceIds, services
                else
                    dupes, Set.add serviceId serviceIds, (serviceId, service) :: services
            (List.empty, Set.empty, List.empty)
            |> List.foldBack update requestedServices 
        if serviceIds = oldServiceIds then
            stopped, dupes, [], running, []
        else
            let toKeep, toStop = running |> List.partition (fun (k, _) -> Set.contains k serviceIds)
            let toStart = services |> List.filter (fun (k, _) -> not (Set.contains k oldServiceIds))
            stopped, dupes, toStop, toKeep, toStart
