namespace XeSoft.EventStore.ProcessManager

module Types =

    open System.Threading.Tasks

    type ProcessManager<'initArg, 'model, 'msg, 'effect, 'resumeArg> =
        {
            /// Transform input into data needed to run the ProcessManager.
            Init: 'initArg -> 'model * 'msg
            /// Transform input into a message used to resume the ProcessManager.
            Resume: Option<'resumeArg -> 'msg>
            /// Process new msg, update the model and request side effects.
            Process: 'model -> 'msg -> 'model * List<'effect>
        }

    type EffectManager<'setupArg, 'resource, 'effect, 'msg> =
        {
            Setup: 'setupArg -> Task<'resource>
            Perform: 'resource -> 'effect -> Task<'msg>
            Teardown: 'resource -> Task<unit>
        }


