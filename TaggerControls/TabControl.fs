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

// File
open System.IO
// TabControl
open System.Windows.Controls

/// <summary>Data about a tab to send to PaneController.</summary>
type TaggerTabData = {
/// <summary>The index of this tab.</summary>
    index : int;
/// <summary>The file path associated with this tab.</summary>
    file : string;
}

type TaggerTabControl () as this =
    inherit ListView ()

(* Events. *)
//#region
/// <summary>The user selected tab (1).</summary>
    let _tab_selected = new Event<TaggerTabData option> ()
(* User closes a tab. We fire this to PaneController so the Editor can be disabled until a new tab is selected. *)
    let _tab_closed = new Event<string> ()
(* User closes all tabs. *)
    let _tab_all_closed = new Event<unit> ()
(* The tab control changes, for example because a tab is opened, closed, or drag/dropped. *)
    let _save_project = new Event<unit> ()
(* User left clicks a tab. *)
    let _tabs_left_mouse_button_up = new Event<unit> ()
(* Unlike with the FileTreeView, we don't fire the drag and drop event up to a config module to have it logged, because it isn't a significant event. *)
//#endregion

(* Member values. *)
//#region
//#endregion

(* Event handler helpers. *)
//#region
/// <summary>Return the index of the currently selected tab; if no tab is currently selected, return -1.</summary>
    let get_selected_index () =
        match this.Items |> Seq.cast<TaggerTabItem> |> Seq.tryFindIndex (fun tab -> tab.is_selected) with
        | Some index -> index
        | _ -> -1

/// <summary>Return the tab at index (1); if (1) is not valid, return None.</summary>
    let try_get_tab index =
        if index = -1 then None
        else if index >= this.Items.Count then None
        else
            match this.Items.[index] with
            | :? TaggerTabItem as tab -> Some tab
            | _ -> None
//#endregion

(* Event handlers. *)
//#region
/// <summary>Insert the source tab (1) before the target tab (2). Return unit.</summary>
    let drop_handler (source : TaggerTabItem) target =
(* IndexOf returns -1 if the item is not found. See:
https://msdn.microsoft.com/en-us/library/ms589122.aspx
*)
        let source_index = this.Items.IndexOf source
        let target_index = this.Items.IndexOf target
(* If the source and target items have the same index, or the source item is already immediately before the target item, stop. *)
        if source_index = target_index || source_index = target_index - 1 then ()
        else
(* An item must be removed before it can be inserted again (that is, we can't insert it at the new index before removing it at the old one. *)
            do this.Items.RemoveAt source_index
(* Removing the source item might have changed the index of the target item. *)
            let target_index = this.Items.IndexOf target
            if target_index > -1 then do
(* Insert the source item immediately before the target item. *)
                this.Items.Insert (target_index, source)
(* Notify MainController that the project should be saved. *)
                _save_project.Trigger ()
//#endregion

/// <summary>Handle when the user selects a tab. (1) The selected tab. Return unit.</summary>
    let tab_selected_handler (new_tab : TaggerTabItem) =
(* Notify the PaneController to bring focus to the editor in this pane, even if we do not select a new tab. *)
        do _tabs_left_mouse_button_up.Trigger ()
(* IndexOf returns -1 if the item is not found. See:
https://msdn.microsoft.com/en-us/library/ms589122.aspx
*)
        let new_index = this.Items.IndexOf new_tab
        let old_index = get_selected_index ()
(* If the tab is already selected, stop. *)
        if new_index = old_index then ()
(* Notify PaneController. *)
        else do _tab_selected.Trigger (Some { index = new_index; file = new_tab.file; })

(* Constructor. *)
//#region
(* Note we add event handlers for individual tabs in this.add_tab. *)
//#endregion

(* Methods. *)
//#region
/// <summary>Remove the tab with header (1). Return unit.</summary>
    member this.close_tab file =
(* See whether the control contains the specified tab. *)
        match this.get_tab file with
        | Some (tab, remove_index) ->
(* Get the index of the currently selected tab. *)
            let current_index = get_selected_index ()
            do this.Items.RemoveAt remove_index
(* If we just removed the selected tab, then if the tab control still has any tabs, select the first one. Otherwise, leave the selection empty. *)
            if remove_index = current_index && this.Items.Count > 0 then
                match this.Items.[0] with
                | :? TaggerTabItem as tab -> do tab_selected_handler tab
                | _ -> ()
(* Notify MainController that the project should be saved. *)
            do _save_project.Trigger ()
        | None -> ()

(* Note this is meant to be called externally (for example, from PaneController). *)
/// <summary>Close all tabs. Return unit.</summary>
    member this.close_all_tabs () =
        do
            this.Items.Clear ()
(* Notify MainController that the project should be saved. *)
            _save_project.Trigger ()

(* Note this is meant to be called externally (for example, from PaneController). *)
/// <summary>Select the tab at index (1), if available. Otherwise, do nothing. Return unit.</summary>
    member this.select_tab new_index =
        let old_index = get_selected_index ()
(* If the tab is already selected, stop. *)
        if new_index = old_index then ()
        else
(* De-select the old tab and select the new tab. *)
            let old_tab = try_get_tab old_index
            let new_tab = try_get_tab new_index
            match old_tab with | Some tab -> tab.is_selected <- false | _ -> ()
            match new_tab with | Some tab -> tab.is_selected <- true | _ -> ()

/// <summary>Find the tab for file (1). If it exists, return it and its index in the TabControl item list; otherwise return None.</summary>
    member this.get_tab file =
(* Return the first tab, and its index, where the tab file name matches (1). *)
        this.Items |> Seq.cast<TaggerTabItem> |> Seq.trypicki (fun index item ->
            if item.file = file then Some (item, index) else None)

/// <summary>Add a tab that represents file (1) to the tab control. If the tab control already contains such a tab, return that tab. If not, return the new tab. If the file does not exist, return None.</summary>
    member this.add_tab file =
(* Check to see if the tab control already contains a tab for this file. *)
        match this.get_tab file with
        | Some (tab, _) -> Some tab
        | None ->
            if File.Exists file = false then None
            else
                let tab = new TaggerTabItem (file)
(* Add the event handlers to the tab. *)
                do
(* We notify PaneController, which closes the file associated with the tab if needed, and then calls close_tab. *)
                    tab.tab_closed.Add (fun tab -> tab.file |> _tab_closed.Trigger)
(* We notify PaneController, which closes the currently open file, and then calls close_all_tabs. *)
                    tab.tab_all_closed.Add _tab_all_closed.Trigger
                    tab.drag_drop.Add (fun (source, target) -> drop_handler source target)
                    tab.tab_selected.Add tab_selected_handler
                do
(* Add the tab to the tab control. *)
                    this.Items.Add tab |> ignore
(* Notify MainController that the project should be saved. *)
                    _save_project.Trigger ()
(* Return the tab. *)
                Some tab

(* We don't save the project after this because this event does not originate here. It originates with FileTreeView, and FileTreeViewController already fires a save_project event in this case. *)
(* We specify the type of the new_name parameter to distinguish it from the type of the Header field, which is obj. *)
/// <summary>If we have a tab with path (1), change it to (2), and change its name to (3). Return unit.</summary>
    member this.rename_tab old_path new_path (new_name : string) =
        match this.get_tab old_path with
        | Some (tab, _) ->
            do
                tab.file <- new_path
                tab.Content <- new_name
        | None -> ()
//#endregion

(* Events. *)
//#region
    member this.tab_selected = _tab_selected.Publish
    member this.tab_closed = _tab_closed.Publish
    member this.tab_all_closed = _tab_all_closed.Publish
    member this.save_project = _save_project.Publish
    member this.tabs_left_mouse_button_up = _tabs_left_mouse_button_up.Publish
//#endregion

(* Test methods. *)
//#region
//#endregion
