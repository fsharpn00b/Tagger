module AddOnServerController

// Action
open System
// Dictionary
open System.Collections.Generic
// JavaScriptSerializer
open System.Web.Script.Serialization
// Application
open System.Windows
// DispatcherPriority
open System.Windows.Threading

(* 
References: System.Runtime.Serialization, System.Web.Extensions.
*)

// AddOnServer
open AddOnServer
// PaneController
open PaneController
// LoggerWrapper
open LoggerWrapper
// LeftOrRightPane
open TaggerControls
// OpenUrlTab
open AddOnCommandWindows

/// <summary>Indicates whether a command was issued in regard to the top, buttom, or cursor position of a document.</summary>
type DocumentPosition = Top = 0 | Bottom = 1 | Cursor = 2

/// <summary>The parameters for an add on command.</summary>
type AddOnCommandParameters = Dictionary<string,obj>

/// <summary>Coordinates events between the AddOnServer and the left and right PaneControllers.</summary>
type AddOnServerController (left_pc : PaneController, right_pc : PaneController) =

(* Events. *)
//#region
/// <summary>The add on server received a copy_text command.</summary>
    let _copy_text = new Event<string * bool * string * TagWithoutSymbol list * string list * int> ()
/// <summary>The add on server received a copy_url command.</summary>
    let _copy_url = new Event<string * bool * string * TagWithoutSymbol list * string list * int> ()
/// <summary>The add on server received a find_in_project command.</summary>
    let _find_in_project = new Event<string> ()
/// <summary>The add on server received a get_files_in_project command.</summary>
    let _get_files_in_project = new Event<unit> ()
/// <summary>The add on server received a get_tags_in_project command.</summary>
    let _get_tags_in_project = new Event<unit> ()
//#endregion

(* Member values. *)
//#region
/// <summary>Serializes outgoing commands and deserializes incoming ones.</summary>
    let _serializer = new JavaScriptSerializer()
//#endregion

(* Functions: Command handler helper. *)
//#region

(* These functions are copied from TagController. Ideally, we could make them static methods of TagController or MainController, which can see both panes. However, the panes are instance values and would need to be passed to these functions as parameters. *)
/// <summary>For pane (1), return a tuple: (1) The corresponding PaneController. (2) The opposite PC.</summary>
    let pane_to_pcs = function | LeftOrRightPane.LeftPane -> left_pc, right_pc | LeftOrRightPane.RightPane -> right_pc, left_pc | _ -> failwith "pane_to_pcs was passed an invalid pane value."
/// <summary>For pane (1), return the PaneController.</summary>
    let pane_to_pc = pane_to_pcs >> fst
/// <summary>For pane (1), return a tuple: (1) The corresponding editor. (2) The opposite editor.</summary>
    let pane_to_editors pane =
        let x, y = pane |> pane_to_pcs
        x.editor, y.editor
/// <summary>For pane (1), return the corresponding editor.</summary>
    let pane_to_editor = pane_to_editors >> fst

(* This is needed because the AddOnServer receives messages and dispatches commands on a different thread than the rest of the application, and controls raise exceptions when they are called from outside their own threads. *)
/// <summary>Run handler (2) that uses control (1). Return unit.</summary>
    let run_handler handler = do Application.Current.Dispatcher.Invoke (DispatcherPriority.Input, Action (handler)) |> ignore
//#endregion

(* Functions: Event handler helper. *)
//#region

(* See:
https://stackoverflow.com/questions/4206445/decoding-a-java-json-map-into-an-f-object
*)
(*
Original form.
let (?) (o : obj) name : 'a = (o :?> Dictionary<string,obj>).[name] :?> 'a
For some reason, that lets you say the following without quotes around the name.
object?name

In case we need to send a nested object.
{"x" :
    {"y" :
        {"a" : 1, "b" : 2, "c" : 3}
    }
}
printfn "map: %A" (o?result?map
    |> Seq.map (fun (KeyValue (k:string,v)) -> k,v)
    |> Seq.toList)
*)
(* The :?> 'a means to infer the expected type of the result and downcast it accordingly. *)
(* This is currently not used. *)
(*
/// <summary>Get value with name (2) from message (1). </summary>
    let get_value (message : obj) name =
        let value = message :?> Dictionary<string,obj>
        match value.TryGetValue name with
        | true, value_ -> value_ :?> 'a |> Some
        | _ -> None
*)

/// <summary>Command (1) expects parameters with names (2). Verify the parameter map (4) contains the expected names. (3) True if the command is incoming; false if it is outgoing. If the parameter map contains the expected names, return their values; if not, return None.</summary>
    let (|Contains|_|) command names incoming (parameters : AddOnCommandParameters) =
(* Ideally, we would use a combination of List.choose and List.forall. *)
        let values = names |> List.choose (fun name ->
            match parameters.TryGetValue name with
            | true, value -> Some value
            | _ ->
(* If the parameter dictionary does not contain the parameter name, log an error. *)
                do _logger.Log_ ("AddOnServerController.MissingParameterError", ["command", command; "name", name; "incoming", incoming.ToString ()])
                None)
(* If we did not find all the names, return None. *)
        if values.Length = names.Length then Some values else None

/// <summary>Log an error that resulted from trying to find the handler for command (1) with parameters (2). (3) True if the command is incoming; false if it is outgoing. Return unit.</summary>
    let log_dispatch_error command (parameters : AddOnCommandParameters) incoming =
(* Combine the parameter names and values into a single string. *)
        let parameters_ = ("", parameters) ||> Seq.fold (fun result kv -> sprintf "%s%s : %s, " result kv.Key (kv.Value.GetType().ToString()))
(* Remove the trailing delimiter from the string. *)
        let parameters__ = if parameters_.Length > 1 then parameters_.Remove (parameters_.Length - 2) else parameters_
(* Log the error. *)
        do _logger.Log_ ("AddOnServerController.InvalidCommandError", ["command", command; "parameters", parameters__; "incoming", incoming.ToString ()])

/// <summary>Convert a parameter with multiple values (1) to a list. Return the list.</summary>
    let param_to_string_list = Seq.cast<string> >> Seq.toList

/// <summary>Convert a parameter with multiple values (1) to a list of type TagWithoutSymbol. Return the list.</summary>
    let param_to_tag_without_symbol_list = param_to_string_list >> List.map TagWithoutSymbol

(* Notes about how we handle commands.
- The pattern match only downcasts one level. That is, each parameter value is type obj, and the pattern match can downcast it to type string, for example. However, if the parameter value contains nested values, the handler must downcast the underlying values. For example, see the files parameter for the copy_text command. The pattern match can downcast the value from type obj to type seq<obj>, but not to seq<string. The handler must cast the value from type seq<obj> to type seq<string>.
- Similarly, if the parameter value contains nested objects, the handler must unpack them. For example, if the parameter value is the JSON object { x : { y : z }}, the handler must unpack the inner object { y : z }.
- This will not work for a handler that has a signature other than unit -> unit.
*)

/// <summary>Get the handler for the incoming command (1) and parameters (2). If we find the handler, and the parameters are correct, return a function that applies the handler to the parameters; otherwise, return None.</summary>
    let dispatch_incoming_command command (parameters : AddOnCommandParameters) =
        let handler =
            match command, parameters with
(* Use the partial active pattern Contains to verify we have the correct parameters for the handler. If so, verify the parameters are the correct types for the handler. *)
            | "copy_text", Contains command ["text"; "find"; "url"; "tags"; "files"; "position"] true [:? string as text; :? bool as find; :? string as url; :? seq<obj> as tags; :? seq<obj> as files; :? int as position] -> Some <| fun () -> _copy_text.Trigger (text, find, url, tags |> param_to_tag_without_symbol_list, files |> param_to_string_list, position)
            | "copy_url", Contains command ["url"; "find"; "title"; "tags"; "files"; "position"] true [:? string as url; :? bool as find; :? string as title; :? seq<obj> as tags; :? seq<obj> as files; :? int as position] -> Some <| fun () -> _copy_url.Trigger (url, find, title, tags |> param_to_tag_without_symbol_list, files |> param_to_string_list, position)
            | "find_in_project", Contains command ["word"] true [:? string as word] -> Some <| fun () -> word |> _find_in_project.Trigger
            | "get_files_in_project", Contains command [] true [] -> Some <| fun () -> _get_files_in_project.Trigger ()
            | "get_tags_in_project", Contains command [] true [] -> Some <| fun () -> _get_tags_in_project.Trigger ()
            | _ ->
(* If a parameter is missing, Contains log an error. *)
(* If we could not find the handler for the command, or if one or more parameters are of the wrong type, log an error. *)
                do log_dispatch_error command parameters true
                None
        match handler with
        | Some handler -> Some <| fun () -> run_handler handler
        | _ -> None

/// <summary>Return true if the outgoing command (1) and parameters (2) are correct; otherwise, return false.</summary>
    let verify_outgoing_command command (parameters : AddOnCommandParameters) =
(* Note the command parameters do not have to be sorted here. They are sorted in send_command, which calls this function. *)
        match command, parameters with
        | "open_url", Contains command ["url"; "open_url_tab"; "stand_by"; "switch_to_existing_tab"; "switch_to_new_tab"] false [:? string as url; :? OpenUrlTab as open_url_tab; :? bool as stand_by; :? bool as switch_to_existing_tab; :? bool as switch_to_new_tab] -> true
        | "get_files_in_project", Contains command ["files"] false [:? list<string> as files] -> true
        | "get_tags_in_project", Contains command ["tags"] false [:? list<string> as tags] -> true
        | _ ->
(* If a parameter is missing, Contains log an error. *)
(* If we could not find the handler for the command, or if one or more parameters are of the wrong type, log an error. *)
            do log_dispatch_error command parameters false
            false

(* These are previous version of dispatch. They might be helpful as references on pattern matching or if we need an alternative version of dispatch later. *)
(*
(* This was replaced because the types of the parameters are not tested until the handler is run, which raises an exception that must be handled. *)
    let dispatch command (parameters : AddOnCommandParameters) incoming =
        match command, parameters with
        | "copy_text", Contains command ["s"; "i"; "b"] incoming [s; i; b] -> Some <| fun () -> this.copy_text (downcast s) (downcast i) (downcast b)
        | _ -> None
*)
(*
(* This was replaced because it requires the parameters to be in a certain order. *)
    let dispatch command (parameters : AddOnCommandParameters) =
        match command, parameters |> Seq.map (fun kv -> kv.Key, kv.Value) |> Seq.toList with
(* Note the , (tuple) operator apparently has higher precedence than "as". *)
        | "copy_text", ["text", (:? string as text)] -> Some <| fun () -> text |> this.copy_text
        | _ -> None
*)

/// <summary>If message (1) contains a valid command and parameters, return them; otherwise, return None.</summary>
    let get_command_and_parameters (dict : Dictionary<string,obj>) =
        match dict.TryGetValue "command", dict.TryGetValue "parameters" with
        | (true, (:? string as command)), (true, (:? AddOnCommandParameters as parameters)) -> Some (command, parameters)
        | _ -> None

/// <summary>Get the command and parameters from message (1). Apply the handler for the command to the parameters. Return unit.</summary>
    let parse_message message =
(* Deserialize the message. *)
        try
            match message |> _serializer.DeserializeObject with
            | :? Dictionary<string,obj> as message_ ->
(* Get the command and parameters. *)
                match get_command_and_parameters message_ with
                | Some (command, parameters) ->
(* Get the handler for the command and apply it to the parameters. *)
                    match dispatch_incoming_command command parameters with
                    | Some handler -> do handler ()
(* If the command or parameters are not correct, dispatch_incoming_command or Contains logs errors. *)
                    | None -> ()
(* If we could not get the command and parameters, log an error. *)
                | None -> do _logger.Log_ ("AddOnServerController.InvalidMessageError", ["error_message", "Message did not contain a command of type string, or parameters of type AddOnCommandParameters, or neither."; "message", message])
(* If we could not deserialize the message, log an error. *)
            | _ -> do _logger.Log_ ("AddOnServerController.InvalidMessageError", ["error_message", "Message did not deseralize to a Dictionary<string,obj>."; "message", message])
        with ex -> do _logger.Log_ ("AddOnServerController.InvalidMessageError", ["error_message", ex.Message; "message", message])
//#endregion

(* Functions: Event handler. *)
//#region

/// <summary>Handle the receipt of a message (1). Return unit.</summary>
    let handle_message_received message = do message |> parse_message

//#endregion

(* Constructor. *)
//#region

(* Add event handlers. *)
    do _server.message_received.Add handle_message_received
    
//#endregion

(* Methods. *)
//#region

/// <summary>Send command (1) with parameters (2) to the client. Return unit.</summary>
    member this.send_command command (parameters : AddOnCommandParameters) =
(* Verify we have the correct parameters for the command. *)
        if verify_outgoing_command command parameters then
            let message = new Dictionary<string,obj> ()
(* The add on expects the parameters to be sorted. *)
            let parameters_ = parameters |> Seq.sortBy (fun kv -> kv.Key)
            do
                message.Add ("command", command)
                message.Add ("parameters", parameters_)
                message |> _serializer.Serialize |> _server.send_message
(* If the command or parameters are not correct, verify_outgoing_command or Contains logs errors. *)

//#endregion

(* Properties. *)
//#region

//#endregion

(* Events. *)
//#region
/// <summary>The add on server received a copy_text command.</summary>
    member this.copy_text = _copy_text.Publish
/// <summary>The add on server received a copy_url command.</summary>
    member this.copy_url = _copy_url.Publish
/// <summary>The add on server received a find_in_project command.</summary>
    member this.find_in_project = _find_in_project.Publish
/// <summary>The add on server received a get_files_in_project command.</summary>
    member this.get_files_in_project = _get_files_in_project.Publish
/// <summary>The add on server received a get_tags_in_project command.</summary>
    member this.get_tags_in_project = _get_tags_in_project.Publish
//#endregion

(* Expose functions for testing. *)
    member this.test_parse_message = parse_message
