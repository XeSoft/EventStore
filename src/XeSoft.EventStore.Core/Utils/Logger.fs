namespace XeSoft.EventStore.Core.Utils

module Logger =

    let getModuleFullName<'TLogMarker> () =
        typeof<'TLogMarker>.DeclaringType.FullName
       

