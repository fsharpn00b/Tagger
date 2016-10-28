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

module AddTagWindow

// XAML type provider
open FSharpx

// TagInfo
open TaggerControls

(* Add Tag dialog. *)
//#region
/// <summary>The Add Tag dialog.</summary>
type private AddTagWindowXAML = XAML<"AddTagWindow.xaml">

(* We can't expose the XAML-based control directly. We don't inherit from UserControl, because we don't need to use this class within another XAML file and therefore we don't need the Content property. For more information see TaggerGrid. *)
/// <summary>The Add Tag dialog.</summary>
type AddTagWindow () =
(* Member values. *)

/// <summary>The actual control based on the XAML.</summary>
    let _add_tag_window = new AddTagWindowXAML ()
/// <summary>The tags entered or selected by the user.</summary>
    let mutable _tags : TagWithoutSymbol list = []
/// <summary>True if the user wants to copy and tag the source lines.</summary>
    let mutable _copy_tag = false
/// <summary>True if the user wants to copy the source lines.</summary>
    let mutable _copy = false
/// <summary>True if the user wants to move the source lines.</summary>
    let mutable _move = false
/// <summary>True if the dialog was canceled; otherwise, false.</summary>
    let mutable _canceled = false

(* Events. *)

/// <summary>A general non-critical error occurred. (1) The error message.</summary>
    let _general_error = new Event<string> ()

(* Event handler helpers. *)

/// <summary>Add tag (1) to tag list. Return true if the tag is valid; otherwise, false.</summary>
    let add_tag tag =
(* Create a version of the tag with the tag symbol. We want this to validate the tag and to show the tag in the Tags label. *)
        let tag_with_symbol = tag |> TagInfo.add_tag_symbol_to_start
(* If the tag is valid, add it to the list. *)
        if TagInfo.whole_string_is_valid_tag_with_symbol tag_with_symbol then
            do
(* Add the tag without the symbol to the list. *)
                _tags <- _tags @ [tag]
(* Add the tag with the symbol to the Tags label. *)
                _add_tag_window.Tags.Content <- sprintf "%s%s" (_add_tag_window.Tags.Content.ToString ()) (tag_with_symbol.ToString ())
            true
(* Otherwise, display an error message. *)
        else
            do _add_tag_window.Error.Content <- "Please ensure the tag contains only lower- and upper-case letters and numbers."
            false

(* TagController has a reference to an instance of this dialog. If we close it, that reference becomes invalid. So we hide it instead. *)
/// <summary>Hide the window. That causes _add_tag_window.Root.ShowDialog to return. Return unit.</summary>
    let close () = _add_tag_window.Root.Hide ()

/// <summary>Cancel the dialog. Return unit.</summary>
    let cancel_helper () =
        do
(* Set the canceled flag. *)
            _canceled <- true
(* Hide the window. *)
            close ()

(* Event handlers. *)

(* We do not mark this event handled. *)
/// <summary>Activated event handler. Return unit.</summary>
(* See the comments for TagCompletionComboBox.OnApplyTemplate. *)
    let activated_handler _ = do _add_tag_window.Tag.textbox.Focus () |> ignore

(* We do not mark this event handled. *)
/// <summary>Add button event handler. Validate the tag. If it is valid, add it to the tag list and clear the combo box. Do not close the window. Return unit.</summary>
    let add_handler _ =
(* See the comments for TagCompletionComboBox.OnApplyTemplate. *)
(* Get the tag from the text box. *)
        let tag = _add_tag_window.Tag.textbox.Text |> TagWithoutSymbol
        if add_tag tag then do _add_tag_window.Tag.clear ()

/// <summary>TagCompletionComboBox commit handler. Return unit.</summary>
    let commit_handler (tag, _) = if add_tag tag then do _add_tag_window.Tag.clear ()

/// <summary>TagCompletionComboBox cancel handler. Return unit.</summary>
    let cancel_handler () =
(* If the combo box drop down is open, close it and clear the text box. *)
        if _add_tag_window.Tag.IsDropDownOpen then do _add_tag_window.Tag.clear ()
(* Otherwise, cancel the Add Tag Window. *)
        else do cancel_helper ()

(* We do not mark this event handled. *)
/// <summary>Done button event handler. Return unit.</summary>
    let done_handler _ = do close ()

(* We do not mark this event handled. *)
/// <summary>Copy and Tag button event handler. Return unit.</summary>
    let copy_tag_handler _ =
        do
            _copy_tag <- true
            close ()

(* We do not mark this event handled. *)
/// <summary>Copy Only button event handler. Return unit.</summary>
    let copy_handler _ =
        do
            _copy <- true
            close ()

(* We do not mark this event handled. *)
/// <summary>Move Only button event handler. Return unit.</summary>
    let move_handler _ =
        do
            _move <- true
            close ()

(* We do not mark this event handled. *)
(* TagController has a reference to an instance of this dialog. If we close it, that reference becomes invalid. So we hide it instead. See:
http://msdn.microsoft.com/en-us/library/system.windows.window.hide%28v=vs.110%29.aspx
"If you want to show and hide a window multiple times during the lifetime of an application, and you don't want to re-instantiate the window each time you show it, you can handle the Closing event, cancel it, and call the Hide method. Then, you can call Show on the same instance to re-open it." *)
/// <summary>Window closing handler. If the user closes the dialog, hide it instead. (1) The event args. Return unit.</summary>
    let closing_handler (args : System.ComponentModel.CancelEventArgs) =
        do
(* Cancel the closing. *)
            args.Cancel <- true
(* Proceed as if the user clicked the Cancel button. *)
            cancel_helper ()

(* Method helpers. *)
/// <summary>Initialize the dialog. Return unit.</summary>
    let initialize () =
        do
(* Clear the flags. *)
            _copy_tag <- false
            _copy <- false
            _move <- false
            _canceled <- false
(* Clear the tag list. *)
            _tags <- []
(* Clear the Tag combo box item list. *)
            _add_tag_window.Tag.Items.Clear ()
(* See the comments for TagCompletionComboBox.OnApplyTemplate. *)
(* Clear the Tag combo box text. *)
            _add_tag_window.Tag.textbox.Text <- System.String.Empty
(* Clear the Tags label. *)
            _add_tag_window.Tags.Content <- System.String.Empty

(* Constructor. *)
    do
(* Add event handlers. *)
        _add_tag_window.Add.Click.Add add_handler
        _add_tag_window.Done.Click.Add done_handler
        _add_tag_window.CopyTag.Click.Add copy_tag_handler
        _add_tag_window.Copy.Click.Add copy_handler
        _add_tag_window.Move.Click.Add move_handler
        _add_tag_window.Cancel.Click.Add <| fun _ -> do cancel_helper ()
        _add_tag_window.Root.Closing.Add closing_handler
        _add_tag_window.Root.Activated.Add activated_handler
        _add_tag_window.Tag.commit.Add commit_handler
        _add_tag_window.Tag.cancel.Add cancel_handler
        _add_tag_window.Tag.general_error.Add _general_error.Trigger

(* Set properties. *)
(* See the comments for TagCompletionComboBox.OnApplyTemplate and the TagCompletionWindow constructor. *)
(* Note since we specify the TagCompletionComboBox is part of the AddTagWindow in AddTagWindow.xaml, rather than setting the Window.Content property to it as we do in the constructor for TagCompletionWindow, we might be able to call ApplyTemplate earlier. However, we do not know if it would help to better encapsulate the TagCompletionComboBox. *)
        _add_tag_window.Tag.ApplyTemplate () |> ignore

(* Methods. *)
/// <summary>Show the dialog. Populate the tag combo box with the tag MRU (1). (2) True to enable the Copy and Tag and Copy buttons. (3) True to enable the Move button. If the user clicks Done, return a tuple. (R1) The list of tags the user selected. (R2) True to copy and tag the source lines. (R3) True to copy the source lines. (R4) True to move the source lines. If the user clicks Cancel or closes the dialog, return None.</summary>
    member this.ShowDialog tag_mru allow_copy allow_move =
(* Convert the tags in the Tag MRU to strings. *)
        let tag_mru_ = tag_mru |> List.map (fun tag -> tag.ToString ())
        do
(* Initialize the dialog. *)
            initialize ()
(* Populate the Tag combo box with the Tag MRU. *)
            _add_tag_window.Tag.base_items <- tag_mru_
(* Enable or disable the Copy and Tag, Copy, and Move buttons. *)
            _add_tag_window.CopyTag.IsEnabled <- allow_copy
            _add_tag_window.Copy.IsEnabled <- allow_copy
            _add_tag_window.Move.IsEnabled <- allow_move
(* Display the dialog. *)
            _add_tag_window.Root.ShowDialog () |> ignore
(* If the user clicked Cancel, return None; otherwise, return the list of tags and the flags. *)
        if _canceled then None else Some (_tags, _copy_tag, _copy, _move)
//#endregion

(* Events. *)

/// <summary>A general non-critical error occurred. (1) The error message.</summary>
    member this.general_error = _general_error.Publish