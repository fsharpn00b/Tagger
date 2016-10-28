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

namespace TaggerControls

// Path
open System.IO
// Input, *Args
open System.Windows
// TabItem, TabControl, ContextMenu
open System.Windows.Controls

open Monads

(* We're inheriting TabItem so we can add to the constructor, which I don't think we can do with extension methods. *)
(* Requires reference to assembly UIAutomationTypes. *)

/// <summary>Creates a new tab to represent file (1).</summary>
type TaggerTabItem (file) as this =
    inherit Label ()

(* Member values. *)
//#region
(* The file path might change if the file is moved or renamed, so we make it a settable property. *)
/// <summary>The file path associated with this tab.</summary>
    let mutable _file = file
/// <summary>True if this tab is selected.</summary>
    let mutable _is_selected = false
//#endregion

(* Events. *)
//#region
(* User selects Close from the context menu. *)
    let _tab_closed = new Event<TaggerTabItem> ()
(* User selects Close All from the context menu. *)
    let _tab_all_closed = new Event<unit> ()
(* User drops one tab on another. *)
    let _drag_drop = new Event<TaggerTabItem * TaggerTabItem> ()
(* User selects a tab. *)
    let _tab_selected = new Event<TaggerTabItem> ()
//#endregion

(* Constructor helpers. *)
//#region
(* Creates and initializes the context menu to be attached to each tab. *)
    let init_tab_context_menu () =
        let add_item (menu : ContextMenu) name handler =
            let i = new MenuItem ()
            do
                i.Header <- name
(* The return value is the index at which the item is added. *)
                i.Click.Add handler
                menu.Items.Add i |> ignore
        let menu = new ContextMenu ()
        do
(* Create a context menu item for each command, with a handler. The handler is closed over the tab instance.
Do not mark the event handled. *)
            add_item menu "Close" (fun _ -> _tab_closed.Trigger this)
            add_item menu "Close All" (fun _ -> _tab_all_closed.Trigger ())
        menu
//#endregion

(* Event handler helpers. *)
//#region
(* We separated the handler into two parts because we can't construct a DragEventArgs and therefore can't unit test the handler directly. Unlike the handler, this helper can also return a value to indicate whether it succeeded, which again helps with testing. *)
/// <summary>Helper for drop event handler. (1) The source tab. (2) The target tab. Return true if the drop succeeds; otherwise, false.</summary>
    let drop_helper source target =
(* If the source and target tabs are the same, stop. *)
        if source.Equals target then false
        else
(* Fire an event to the TabControl, which does the actual switching of the tabs. *)
            do _drag_drop.Trigger (source, target)
            true
//#endregion

(* Event handlers. *)
//#region
(*
Drag and drop in WPF:
http://msdn.microsoft.com/en-us/library/ms742859.aspx
http://msdn.microsoft.com/en-us/library/system.windows.dragdrop.dodragdrop.aspx
Drag and drop tabs:
http://stackoverflow.com/questions/10738161/is-it-possible-to-rearrange-tab-items-in-tab-control-in-wpf
http://social.msdn.microsoft.com/Forums/en-US/wpf/thread/ed077477-a742-4c3d-bd4e-3efdd5dd6ba2
*)
(* We do not mark this event handled. *)
///<summary>Handler for MouseMove event. (1) Event args. Return unit.</summary>
    let mouse_move_handler (args : Input.MouseEventArgs) =
(* Don't proceed until the mouse is moving and the left button is pressed. *)
        if Input.Mouse.PrimaryDevice.LeftButton <> Input.MouseButtonState.Pressed then ()
        else
(* Downcast the source to a Tab. *)
            match args.Source with
            | null -> ()
(* The Tab is both the source and the data. *)
            | :? TaggerTabItem as tab -> do DragDrop.DoDragDrop (tab, tab, DragDropEffects.Move) |> ignore
            | _ -> ()

(* We do not mark this event handled. *)
/// <summary>Handler for Drop event. (1) Event args. Return unit.</summary>
    let drop_handler (args : DragEventArgs) =
(* If we successfully validate the source and target, call the handler helper. *)
        match FileTreeViewItem.get_source_and_target<TaggerTabItem> args with
        | Some (source, target) -> do drop_helper source target |> ignore
        | _ -> ()

(* We do not mark this event handled. *)
///<summary>Handler for MouseRightButtonUp event. (1) Event args. Return unit.</summary>
    let right_mouse_up_handler (args : Input.MouseEventArgs) =
(* Display the context menu for this tab. *)
(* Since the ContextMenu already exists, use Visibility to show it. For more information see FileTreeViewItem.right_button_mouse_up. *)
        do this.ContextMenu.Visibility <- Visibility.Visible
//#endregion

(* Constructor. *)
//#region
    do
        this.Content <- Path.GetFileName _file
(* This is not true by default. *)
        this.AllowDrop <- true
(* Attach the context menu to this tab. *)
        this.ContextMenu <- init_tab_context_menu ()
(* Attach event handlers. *)
        this.MouseMove.Add mouse_move_handler
        this.Drop.Add drop_handler
        this.MouseRightButtonUp.Add right_mouse_up_handler
        this.MouseLeftButtonUp.Add (fun _ -> do this.select ())
//#endregion

(* Methods. *)
(* We notify TabControl, which notifies PaneController, which opens the file associated with the tab, and then calls TabControl.select_tab. *)
/// <summary>Select the tab.</summary>
    member this.select () = do _tab_selected.Trigger this
//#region
//#endregion

(* Properties. *)
//#region
/// <summary>The file path associated with this tab.</summary>
    member this.file
        with get () = _file
        and set file = do
            _file <- file
(* Update the tab header. *)
            this.Content <- Path.GetFileName file

/// <summary>True if this tab is selected.</summary>
    member this.is_selected
        with get () = _is_selected
        and set value =
            do
                _is_selected <- value
                this.FontWeight <- if value then FontWeights.Bold else FontWeights.Normal
//#endregion

(* Events. *)
//#region
    member this.tab_closed = _tab_closed.Publish
    member this.tab_all_closed = _tab_all_closed.Publish
    member this.drag_drop = _drag_drop.Publish
    member this.tab_selected = _tab_selected.Publish
//#endregion

(* Test methods. *)
//#region
    member this.test_drop_helper = drop_helper
//#endregion