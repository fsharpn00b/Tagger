module AddOnServerControllerTest

(* Copyright 2014 FSharpN00b.
This file is part of Tagger.

Tagger is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

Tagger is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Tagger.  If not, see <http://www.gnu.org/licenses/>.

Tagger uses AvalonEdit, which is copyright 2014 AlphaSierraPapa for the SharpDevelop Team under the terms of the MIT License. For more information see www.avalonedit.net. *)

// ConcurrentDictionary
open System.Collections.Concurrent
// Dictionary
open System.Collections.Generic
// JavaScriptSerializer
open System.Web.Script.Serialization

(* 
References: System.Runtime.Serialization, System.Web.Extensions.
*)

open ICSharpCode.AvalonEdit
open Xunit

// TaggerTextEditor, TaggerTabControl, TaggerMargin
open TaggerControls
// PaneController
open PaneController
// AddOnServerController
open AddOnServerController
open TestHelpers

(*
Log events:
x InvalidMessageError
x InvalidCommandError
x MissingParameterError

Functions:
N pane_to_pcs
N pane_to_pc
N pane_to_editors
N pane_to_editor
N run_handler
Contains
log_dispatch_error
param_to_string_list
param_to_tag_without_symbol_list
dispatch_incoming_command
verify_outgoing_command
get_command_and_parameters
parse_message
N handle_message_received

Methods:
send_command

Properties:


Events:
_copy_text
_find_in_project
_get_files_in_project
_get_tags_in_project
*)

(* Helper types, functions, and values. *)
//#region

/// <summary>Serializes outgoing commands and deserializes incoming ones.</summary>
let _serializer = new JavaScriptSerializer()

(* This is a modified version of get_tc from TagControllerTest. *)
/// <summary>Helper function that returns a new TagController.</summary>
let get_aosc () =
    let vertical_positions = new ConcurrentDictionary<string, float> () |> ref
    let documents = new DocumentMap () |> ref
(* Gets a new PaneController. *)
    let get_pc () = new PaneController (
        new TaggerTextEditor (),
        new TaggerTabControl (),
        new TaggerMargin (),
        new TaggerMargin (),
        new System.Windows.Controls.TextBlock (),
        vertical_positions,
        documents
        )
    new AddOnServerController (get_pc (), get_pc ())
//#endregion

type AddOnServerControllerTest () =
    interface ITestGroup with
    member this.tests_log with get () = [
    {
        part = "AddOnServerController"
        name = "InvalidMessageError"
        test = fun name ->
            let aosc = get_aosc ()
(* Send a message that isn't a dictionary. *)
            do aosc.test_parse_message "not a dictionary"
(* Send a message that doesn't contain a command and parameters. *)
            do new Dictionary<string, obj> () |> _serializer.Serialize |> aosc.test_parse_message
(* Send a message that contains parameters that aren't in a dictionary. *)
            let message = new Dictionary<string, obj> ()
            do
                message.Add ("command", "copy_text")
                message.Add ("parameters", ["text"; "text"])
                message |> _serializer.Serialize |> aosc.test_parse_message
    };
    {
        part = "AddOnServerController"
        name = "InvalidCommandError"
        test = fun name ->
            let aosc = get_aosc ()
(* Send an invalid command. *)
            let message = new Dictionary<string, obj> ()
            do
                message.Add ("command", "no_such_command")
                message.Add ("parameters", new AddOnCommandParameters ())
                message |> _serializer.Serialize |> aosc.test_parse_message
    };
    {
        part = "AddOnServerController"
        name = "MissingParameterError"
        test = fun name ->
            let aosc = get_aosc ()
(* Send a command with missing parameters. *)
            let message = new Dictionary<string, obj> ()
            let parameters = new AddOnCommandParameters ()
            do
                message.Add ("command", "copy_text")
                parameters.Add ("text", "text")
                message.Add ("parameters", parameters)
                message |> _serializer.Serialize |> aosc.test_parse_message
    };
    ]

    member this.tests_throw with get () = [
    ]

    member this.tests_no_log with get () = [
    ]