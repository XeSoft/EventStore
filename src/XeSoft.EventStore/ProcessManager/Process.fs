namespace XeSoft.EventStore.ProcessManager

module Process =

    open System.Threading.Tasks
    open XeSoft.EventStore.ProcessManager.Types


    /// Test a UMP program.
    /// Provide the model (program state) and messages (side effect results) to replay.
    /// Returns the final model and all generated side effects.
    let test
        (pm: ProcessManager<'initArg, 'model, 'msg, 'effect, 'resumeArg>)
        (model: 'model)
        (msgs: 'msg list)
        : 'model * 'effect list
        =
        let effects = ResizeArray<'effect>()
        let initial = model
        let update model msg =
            let (model, newEffects) = pm.Process model msg
            effects.AddRange(newEffects) // not ideal, but list append every iteration not efficient
            model
        let finalModel = List.fold update initial msgs
        (finalModel, Seq.toList effects)


    module Internal =

        let runLoop
            (em: EffectManager<'setupArg, 'resource, 'effect, 'msg>)
            (setupArg: 'setupArg)
            (process_: 'model -> 'msg -> 'model * List<'effect>)
            (model: 'model)
            (msg: 'msg)
            : Task<'model>
            =
            task {
                let! resource = em.Setup setupArg
                let mutable model = model // intentional shadow
                let mutable msgOpt = Some msg
                let tasks = ResizeArray<Task<'msg>>()
                while (msgOpt.IsSome || tasks.Count > 0) do
                    // process msg if available
                    if (msgOpt.IsSome) then
                        let (newModel, effects) = process_ model msgOpt.Value
                        msgOpt <- None
                        model <- newModel
                        effects |> List.map (em.Perform resource) |> tasks.AddRange
                    // wait for a task to complete
                    if (tasks.Count > 0) then
                        // effect completion order indeterminate
                        let! completedTask = Task.WhenAny(tasks)
                        tasks.Remove(completedTask) |> ignore
                        let! newMsg = completedTask
                        msgOpt <- Some newMsg
                do! em.Teardown resource
                return model
            }


    let run
        (em: EffectManager<'setupArg, 'resource, 'effect, 'msg>)
        (setupArg: 'setupArg)
        (pm: ProcessManager<'initArg, 'model, 'msg, 'effect, 'resumeArg>)
        (initArg: 'initArg)
        : Task<'model>
        =
        task {
            let (initModel, initMsg) = pm.Init initArg
            return! Internal.runLoop em setupArg pm.Process initModel initMsg
        }


    let resume
        (em: EffectManager<'setupArg, 'resource, 'effect, 'msg>)
        (setupArg: 'setupArg)
        (pm: ProcessManager<'initArg, 'model, 'msg, 'effect, 'resumeArg>)
        (model: 'model)
        (resumeArg: 'resumeArg)
        : Task<'model>
        =
        task {
            if pm.Resume.IsNone then
                return model
            else
                let resumeMsg = pm.Resume.Value resumeArg
                return! Internal.runLoop em setupArg pm.Process model resumeMsg
        }

