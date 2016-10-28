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

module MainController

(* ZipFile requires assembly System.IO.Compression.FileSystem. Also, for some reason we can't reference System.IO.Compression. *)

// DateTime
open System
// ConcurrentDictionary
open System.Collections.Concurrent
// KeyValuePair
open System.Collections.Generic
// ZipFile
open System.IO

open ICSharpCode.AvalonEdit

// FileTreeView, TaggerTextEditor, TaggerTabControl
open TaggerControls
// TaggerConfig
open Config
// FileTreeViewController
open FileTreeViewController
// PaneController
open PaneController
// TagController
open TagController
// AddOnServerController
open AddOnServerController
// Project, ProjectController
open Project
// MaybeMonad
open Monads
// LoggerWrapper
open LoggerWrapper
// List extensions
open ListHelpers

(* Notes. *)
//#region
(*
We have a problem where TaggerConfig and MainController each need to communicate with the other, but both are created at the level of the main program. Right now, the config is passed to MC.

1. The project auto-save interval timer belongs to MC. Config needs to set this.
2. The open config dialog function belongs to config. MC needs to call this based on an event fired from the FileTreeView.

Possible solutions:

1. Have MC keep the timer. Pass MC to config so config can set the timer. When FTV fires the open config dialog event, MC re-fires it. It is caught by either the main program or config, which then calls open config dialog.
2. Move the timer to FTV. When it ticks, FTV fires an event to MC, which then calls save project. FTV is already passed to config, so config can set the timer. Pass config to MC so MC can call open config dialog on config.
3. Have MC keep the timer. Pass config to MC so MC can call open config dialog on config. When config changes the timer, it fires an event to MC.

We decided on #2 because FTV already fires a save_project event. 

20130928: For now, we've decided to stop saving the project automatically, and only save it in response to events like the tree or tab control changing. That eliminates this problem for now, though I haven't removed the related code yet.

20131006: We considered adding a project-level is modified flag, like the file-level one, without which the project would not auto-save. The downside is that we'd like to put the flag in MainController, but the project auto-save timer is down in FileTreeView. We could change FTV from firing the save event to firing an auto save event, and then the MC decides whether to save or not, based on the flag.
We would then have increased the file and project save delays - files to 10 seconds, projects to 30 seconds. They're already saved when we close them, we're just worrying about crashes.
We considered having the project auto-save periodically, except for certain events (such as the tag number incrementing) that would cause it to be saved at once. The downside is that this is more complicated than simply having most events cause it to be saved at once, but offers no real advantages over that.

We decided to make it so that most events that change the project also cause it to be saved. The downside of this approach is that if we add more controls, it might become unwieldy to respond to save-project events from all of them. On the other hand, if we used a project-level is modified flag, all of those controls would have to fire a project-changed event instead anyway.
We left the project auto-save timer in FTV, and the config setting for it, in case we want it again, but for now it's disabled.

The following project data are saved when the following functions fire the save_project event.
x FTV sort order. FileTreeViewController handlers for FileTreeView.save_project and tree_changed.
x Tabs. TaggerTabControl.drop_handler, close_tab, add_tab.
x Vertical positions. PaneController.close_file.
x Tag MRU. TagController.add_tags_helper.
x Tag number. TagController.add_tags_helper.
x Move to MRU. TagController.add_files.

20140123: We added a config setting for the margin highlight display time. We'd like to store the setting in TagController, which uses it most. But we again have the problem that TaggerConfig is passed to the constructor of MainController, which creates the TagController. We solved it by putting the setting in TaggerConfig, having MainController pass the TaggerConfig to the TagController, and having TagController query the TaggerConfig for the setting when it needs it.
*)
//#endregion

/// <summary>Coordinates events between the configuration (1) and the controls (2-8).</summary>
type MainController (
    config : TaggerConfig,
    tree : FileTreeView,
    left_editor : TaggerTextEditor,
    right_editor : TaggerTextEditor,
    left_pane_left_margin : TaggerMargin,
    left_pane_right_margin : TaggerMargin,
    right_pane_left_margin : TaggerMargin,
    right_pane_right_margin : TaggerMargin,
    left_tabs : TaggerTabControl,
    right_tabs : TaggerTabControl,
    left_status : System.Windows.Controls.TextBlock,
    right_status : System.Windows.Controls.TextBlock) =

(* Events. *)
(* This event is simply re-fired from TagController. *)
/// <summary>The right sidebar needs to be expanded.</summary>
    let _expand_right_sidebar = new Event<unit> ()

(* Member values. *)
//#region
(* FileTreeViewController handles some events from the FileTreeView, and re-fires some events to this class. *)
    let _ftvc = new FileTreeViewController (tree)
(* The vertical positions for the files in the currently open project. *)
(*
PaneController needs access to the vertical position map and documents. Originally, we simply passed the values to the PC constructor. We assumed that since objects are reference values and not stack values, the PC would get references to these values and not copies of them. However, in MC.open_project_helper, we overwrite the vpos map with the one loaded from the project file. This breaks the PC's reference to it. I.e., if you test:
PC.vpos_map.Equals TC.vpos_map
you get true before opening the project, and false afterward.
We found the answer here, in the section "Aliasing Ref Cells":
https://en.wikibooks.org/wiki/F_Sharp_Programming/Mutable_Data
It seems that if you want a reference to work like a C++ pointer - that is, if one object changes the value, the changes are seen by all objects that have references to the value - then ALL objects must have references to the value, not direct access. So this is incorrect:
let _vertical_positions = new ConcurrentDictionary<string, float>()
Even if all other objects have references to the value, a change to the value itself by the "owner" of the value (that is, the one that has direct access) will break those references.
*)
(* We considered not passing the vpos map to the PC, and having it fire an event to the MC when it wants to make a change to it. However, it also needs read access to the vpos map when it opens a file. *)
    let _vertical_positions = new ConcurrentDictionary<string, float>() |> ref
(* The documents and timestamps for the files in the currently open project. Unlike vertical positions, we don't save these to disk. *)
    let _documents = new DocumentMap () |> ref
(* PaneController handles events from the TabControl and Editor. *)
    let _left_pc = new PaneController (left_editor, left_tabs, left_pane_left_margin, left_pane_right_margin, left_status, _vertical_positions, _documents)
    let _right_pc = new PaneController (right_editor, right_tabs, right_pane_left_margin, right_pane_right_margin, right_status, _vertical_positions, _documents)
(* TagController handles drag and drop events from the left and right PaneControllers. *)
    let _tc = new TagController (_left_pc, _right_pc, _documents, config)
(* AddOnServerController handles events from the AddOnServer. *)
    let _aosc = new AddOnServerController (_left_pc, _right_pc)
(* We don't want to save the project until one is loaded. *)
    let mutable _enable_save = false
//#endregion

(* Event handler helpers for FileTreeViewController. *)
//#region

/// <summary>Save the current project. If we save the project successfully, return true; otherwise, false.</summary>
    let save_project () =
(* Helper. Convert each tab in the TabControl (1) to the file it represents. Return the list of files. *)
        let get_tabs (tabs : TaggerTabControl) = tabs.Items |> Seq.cast<TaggerTabItem> |> Seq.toList |> List.map (fun tab -> tab.file)
(* Only save the project if one is loaded. If not, just return true. *)
        if _enable_save = false then true
        else
            let project = {
(* Get the FileTreeNode from the FileTreeView. The FTN represents the sort order, and its root directory is where ProjectController saves the project information file. *)
                sort_order = tree.get_ftn ()
                left_tabs = left_tabs |> get_tabs
                right_tabs = right_tabs |> get_tabs
                vertical_positions = !_vertical_positions
                tag_number = _tc.tag_number
                move_to_mru = _tc.move_to_mru
                tag_mru = _tc.tag_mru |> List.map (fun tag -> tag.ToString ())
                }
(* ProjectController.save_project returns true or false. *)
            let result = ProjectController.save_project tree project
(* ProjectController.save_project updates the recent projects list in the FTV. Propagate that to the appropriate TaggerConfig setting. *)
            do config.Update "RecentProjectFolders" tree.recent_projects
(* Return the result from ProjectController.save_project. *)
            result

(* I considered making this a ProjectController static method, but we would just have to pass the results of calls to TaggerConfig and FileTreeView to it any way, and one of the two possible errors (from calling TaggerConfig) would still be here. *)
/// <summary>Back up the files and folders contained in the currently open project to the project backup folder specified in the TaggerConfig. Return true if the backup succeeded; otherwise, false.</summary>
    let backup_project () =
(* Helper function to get the zip file path. (1) The project folder. (2) The project backup folder. If we successfully get the zip file path, return it; otherwise, return None. *)
        let get_zip_file_path project_folder backup_folder =
            try
(* There's no Path method to get just the folder name - even GetDirectoryName returns the full path. The DirectoryInfo constructor does not check for the existence of the path. *)
                if Directory.Exists project_folder = false then raise <| new Exception ("Path does not exist.")
                let project_folder_name = new DirectoryInfo (project_folder)
(* The name of the zip file is the project folder name plus the date and time. *)
                sprintf "%s\\%s_%s.zip" backup_folder project_folder_name.Name <| DateTime.Now.ToString ("yyyyMMdd_HHmmss") |> Some
            with | ex ->
(* If we can't get the project folder name, log an error and return None. *)
                do _logger.Log_ ("MainController.ProjectBackupCurrentFolder", ["project_folder", project_folder; "message", ex.Message])
                None
(* Helper function to create the zip file. (1) The project folder. (2) The path of the zip file to create. If we successfully create the zip file, return true; otherwise, false. *)
        let create_zip_file project_folder zip_file_path =
            try
(* Create the zip file and return true. *)
                do Compression.ZipFile.CreateFromDirectory (project_folder, zip_file_path)
                true
            with | ex ->
(* If the compression fails, log an error. *)
                do _logger.Log_ ("MainController.ProjectBackupCompression", ["project_folder", project_folder; "zip_file_path", zip_file_path; "message", ex.Message])
                false

(* Only back up the project if one is loaded. If not, just return true. *)
        if _enable_save = false then true
        else
(* Get the backup folder from the config. The value should be a single-item list. *)
            match config.TryFind "ProjectBackupFolder" with
            | Some (backup_folder :: _) ->
(* Get the path of the currently loaded project. *)
                let project_folder = tree.root_node.path
(* Try to get the zip file path. *)
                match get_zip_file_path project_folder backup_folder with
(* If we couldn't get the project folder name, and thus the zip file path, return false. *)
                | None -> false
(* Try to create the zip file. *)
                | Some zip_file_path -> zip_file_path |> create_zip_file project_folder
            | _ ->
(* If we don't find the backup folder, log an error. *)
                do _logger.Log_ ("MainController.ProjectBackupFolderLookup", [])
                false

/// <summary>Save and back up the current project to prepare to close it. If we succeed, return true; otherwise, false.</summary>
    let save_backup_project () =
        MaybeMonad () {
(* Save the file currently open in the editor and close it. If we fail, stop. *)
            do! _left_pc.close_file ()
            do! _right_pc.close_file ()
(* Since we have closed the currently open file, unselect the tab in case we aren't able to open the new project and have to leave the current one open. *)
            do left_tabs.SelectedIndex <- -1
            do right_tabs.SelectedIndex <- -1
(* Save the current project. If we fail, stop. *)
            do! save_project ()
(* Back up the current project. If we fail, stop. *)
            do! backup_project ()
            return true
        } |> function | Some true -> true | _ -> false

/// <summary>Remove deleted files from the vertical positions map (1). Return a new map.</summary>
    let match_vertical_positions (tree : FileTreeView) (vertical_positions : ConcurrentDictionary<string, float>) =
        let vertical_positions_ = vertical_positions.ToArray () |> Array.toList
        let files = tree.get_file_list ()
        let compare (a : KeyValuePair<_,_>) b = a.Key = b
        let vertical_positions__ = List.match_with_compare vertical_positions_ files compare
        new ConcurrentDictionary<string, float> (vertical_positions__)

/// <summary>Set the Move To MRU, Tag MRU, and tag number in the TagController when no project information is available.</summary>
    let set_tc_default_values () =
        do
(* Clear the Move To MRU in the TagController. *)
            _tc.move_to_mru <- []
(* Note we must initialize the Tag MRU to the built-in tags. The Tag MRU is treated as an up to date list of the tags available in the project, because it is too expensive to call TC.find_all_tags_in_files constantly. On the other hand, the Move To MRU is not treated as an up to date list of the files available in the project, because it is not expensive to call FTV.get_file_list. *)
(* Set the Tag MRU in the TagController to the built-in tags. *)
            _tc.tag_mru <- TagController.built_in_tags
(* Clear the tag number in the TagController. *)
            _tc.tag_number <- 0

/// <summary>Open the project described by (1) with root folder (2). If we succeed, return true; otherwise, false.</summary>
    let open_project_helper project dir =
        match project with
        | Some project ->
(* If we have the project information, then open the project in the FileTreeView, and provide the sort order. *)
(* Note if the sort order contains files that have since been deleted, this removes them. *)
            if _ftvc.open_project dir (Some project.sort_order) then
(* Get the tags found in the list of files from the FileTreeView. *)
                let tags_in_files = tree.get_file_list () |> TagController.find_all_tags_in_closed_files
(* Add the built-in tags to the tags found in the files, because the built-in tags won't be found in the files. *)
                let existing_tags = TagController.built_in_tags @ tags_in_files
(* Merge the tag MRU with the tags found in the files and the built-in tags. This prevents deleted tags from accumulating in the tag MRU. *)
                let tag_mru = List.merge_sort (project.tag_mru |> List.map TagWithoutSymbol) existing_tags
                do
(* Get the vertical positions for the files. Match the files in the vertical position map with the files from the FileTreeView. This prevents deleted files from accumulating in the vertical position map. *)
                    _vertical_positions := match_vertical_positions tree project.vertical_positions
(* Open the tabs that were last open in the project. Note this does not select any of the tabs. *)
                    project.left_tabs |> List.iter (fun file -> do left_tabs.add_tab file |> ignore)
                    project.right_tabs |> List.iter (fun file -> do right_tabs.add_tab file |> ignore)
(* Set the tag number in the TagController. *)
                    _tc.tag_number <- project.tag_number
(* Set the Move To MRU in the TagController. Merge the Move To MRU with the list of files from the FileTreeView. This prevents deleted files from accumulating in the Move To MRU. *)
                    _tc.move_to_mru <- List.merge_sort project.move_to_mru <| tree.get_file_list ()
(* Set the Tag MRU in the TagController. *)
                    _tc.tag_mru <- tag_mru
(* Return true to indicate there was no error in opening the project. *)
                true
(* If there was an error opening the project... *)
            else
(* Set the Move To MRU, Tag MRU, and tag number in the TagController. *)
                set_tc_default_values ()
(* Return false. *)
                false
(* If we don't have the project information... *)
        | None ->
(* Set the Move To MRU, Tag MRU, and tag number in the TagController. *)
            set_tc_default_values ()
(* Open the project in the FileTreeView, without the sort order. Return the boolean result. *)
            _ftvc.open_project dir None

/// <summary>Update the FileTreeView based on folder (1). Use project information stored in folder (1), if any. Return unit.</summary>
    let open_project dir =
        MaybeMonad () {
(* Save and back up the project. If we fail to do either, stop. *)
            do! save_backup_project ()
(* If the specified folder does not exist, create it; otherwise, do nothing. If we fail to create it, stop. *)
            do! ProjectController.new_project dir
(* Note that we have not yet closed the old project or opened the new one. That happens when we call FileTreeViewController.open_project, which changes the FileTreeView, which contains the directory to which the project information will be saved. Also note that closing the currently open file doesn't affect the project information, because we don't record which file is open. Up until now, we could stop without leaving the project information in an inconsistent state. *)
            do
(* Temporarily disable auto-save, so it doesn't accidentally save the project between the time we close the old project and open the new one. *)
                _enable_save <- false
(* Clear the tab control. *)
                left_tabs.Items.Clear ()
                right_tabs.Items.Clear ()
(* Try to get the project information. *)
            let project = ProjectController.open_project dir
(* Try to open the project. *)
            do! open_project_helper project dir
(* Note that if we had an error opening the project, we are now in an inconsistent state. Auto-save is disabled and the tab control is cleared. The FileTreeView should still have the old project loaded, because the only way FileTreeViewController.open_project should return false is if, in FileTreeView.initialize, the call to dir_to_ftn (which reads the directory structure into an FTN) raises an exception, and that means FileTreeView.init_tree is not called. And we do not know how to cause that. *)
(* If there was no error in opening the project, enable auto-save. *)
            do _enable_save <- true
            return true
        } |> ignore
//#endregion

(* FileTreeViewController event handlers. *)
//#region
/// <summary>Open file (2) in pane (1).</summary>
    let open_file_handler (pane : LeftOrRightPane) file = do _tc.open_file pane file |> ignore

/// <summary>If the tab control contains a tab for file (1), change its path to (2) and its name to (3). If the file is open in the editor, change its path there as well. Return unit.</summary>
    let rename_file_handler old_path new_path new_name =
        do
            left_tabs.rename_tab old_path new_path new_name
            right_tabs.rename_tab old_path new_path new_name
(* We checked with ILSpy that ICSharpCode.AvalonEdit.TextEditor.Load does not set any property, it just reads the specified file and updates the Text property. So we don't need to set anything in the TextEditor. *)
            left_editor.rename_file old_path new_path
            right_editor.rename_file old_path new_path
(* Save the project. A file rename originates with FileTreeView, but since it can also affect the TabControl, we need to save the project at the level of MainController (as opposed to simply having FileTreeView fire a save_project event). *)
            save_project () |> ignore

(* This is a UI function, so it's not exposed for testing. We test open_project instead. *)
/// <summary>Open a project. Return unit.</summary>
    let open_project_handler () =
(* Ask the user to specify a folder. If they cancel, stop. *)
        match ProjectController.get_project_directory tree with
        | Some dir -> open_project dir
        | None -> ()
//#endregion

(* Margin event handlers. *)
//#region

/// <summary>Call the margin right-click handler helper in TagController. (1) The pane of the margin that fired the event. Return unit.</summary>
    let margin_right_click_handler pane =
(* Get a list of files from the FileTreeView. *)
        let files = tree.get_file_list ()
        do _tc.margin_right_click pane files

//#endregion

(* Editor event handlers. *)
//#region

/// <summary>Call the right-click handler helper in TagController. (1) The pane of the editor that fired the event. (2) The right-click data. Return unit.</summary>
    let editor_right_click_handler pane data =
(* Get a list of files from the FileTreeView. *)
        let files = tree.get_file_list ()
        do _tc.right_click pane files data

/// <summary>Handle the Find in Project key combination in pane (1). (2) The cursor position.</summary>
    let editor_find_in_project_handler pane position =
(* Get a list of files from the FileTreeView. *)
        let files = tree.get_file_list ()
(* We get the position from the cursor, not from the mouse, so the position always has a value. *)
        do _tc.get_find_in_project_word pane files <| Some position

//#endregion

(* AddOnServerController event handlers. *)
//#region

/// <summary>If a project is open, call TagController.copy_text. If not, log an error. Return unit.</summary>
    let try_copy_text (text, find, url, tags, files, position) =
        if _enable_save then do _tc.copy_text (text, find, url, tags, files, position)
        else do _logger.Log_ ("MainController.NoProjectOpen", ["command", "copy_text"])

/// <summary>If a project is open, call TagController.copy_url. If not, log an error. Return unit.</summary>
    let try_copy_url (url, find, title, tags, files, position) =
        if _enable_save then do _tc.copy_url (url, find, title, tags, files, position)
        else do _logger.Log_ ("MainController.NoProjectOpen", ["command", "copy_url"])

/// <summary>Handle the find_in_project event from AddOnServerController. (1) The word to find. Return unit.</summary>
    let find_in_project_handler word =
(* Verify a project is open. Get a list of files from the FileTreeView. Use it to find the word. *)
        if _enable_save then do _tc.find_in_project word <| tree.get_file_list ()
(* If a project is not open, log an error. *)
        else do _logger.Log_ ("MainController.NoProjectOpen", ["command", "find_in_project"])

/// <summary>Handle the get_files_in_project event from AddOnServerController. Return unit.</summary>
    let get_files_in_project_handler () =
        let parameters = new AddOnCommandParameters ()
(* Verify a project is open. *)
        if _enable_save then
(* Get a list of files from the FileTreeView. Sort the files based on the Move To MRU list. *)
            let files = List.merge_sort _tc.move_to_mru <| tree.get_file_list ()
(* Add the file list to the parameters. *)
            do parameters.Add ("files", files)
(* If a project is not open, log an error. Add an empty list to the parameters. *)
        else do
(* Previously, we used a value of []. However, that causes an additional error in verify_outgoing_command because it does not match the type list<string>. *)
            parameters.Add ("files", list<string>.Empty)
            _logger.Log_ ("MainController.NoProjectOpen", ["command", "get_files_in_project"])
(* Send the response. *)
        do _aosc.send_command "get_files_in_project" parameters            

/// <summary>Handle the get_tags_in_project event from AddOnServerController. Return unit.</summary>
    let get_tags_in_project_handler () =
        let parameters = new AddOnCommandParameters ()
(* Verify a project is open. *)
        if _enable_save then
(* Get a list of tags from the tag MRU. Convert the tags to strings. *)
            let tags = _tc.tag_mru |> List.map (fun tag -> tag.ToString ())
(* Add the tags to the parameters. *)
            do parameters.Add ("tags", tags)
(* If a project is not open, log an error. Add an empty list to the parameters. *)
        else do
(* Previously, we used a value of []. However, that causes an additional error in verify_outgoing_command because it does not match the type list<string>. *)
            parameters.Add ("tags", list<string>.Empty)
            _logger.Log_ ("MainController.NoProjectOpen", ["command", "get_tags_in_project"])
(* Send the response. *)
        do _aosc.send_command "get_tags_in_project" parameters
//#endregion

(* Constructor. *)
//#region
(* Add event handlers. *)
    do
        tree.open_config.Add (fun _ -> do config.show_settings_dialog ())
        _ftvc.file_opened.Add (fun (file, pane) -> do open_file_handler pane file)
        _ftvc.file_renamed.Add <| fun (old_path, new_path, new_name) -> do rename_file_handler old_path new_path new_name
        _ftvc.project_created.Add open_project_handler
        _ftvc.project_opened.Add open_project_handler
        _ftvc.project_saved.Add (save_project >> ignore)
        _left_pc.margin_drop.Add <| _tc.handle_margin_drop LeftOrRightPane.LeftPane
        _right_pc.margin_drop.Add <| _tc.handle_margin_drop LeftOrRightPane.RightPane
        _left_pc.margin_right_click.Add <| fun () -> margin_right_click_handler LeftOrRightPane.LeftPane
        _right_pc.margin_right_click.Add <| fun () -> margin_right_click_handler LeftOrRightPane.RightPane
        _left_pc.right_click.Add <| editor_right_click_handler LeftOrRightPane.LeftPane
        _right_pc.right_click.Add <| editor_right_click_handler LeftOrRightPane.RightPane
        _left_pc.find_in_project.Add <| editor_find_in_project_handler LeftOrRightPane.LeftPane
        _right_pc.find_in_project.Add <| editor_find_in_project_handler LeftOrRightPane.RightPane
        _left_pc.save_project.Add (save_project >> ignore)
        _right_pc.save_project.Add (save_project >> ignore)
        _left_pc.tag_symbol_entered.Add <| _tc.show_tag_completion_window LeftOrRightPane.LeftPane
        _right_pc.tag_symbol_entered.Add <| _tc.show_tag_completion_window LeftOrRightPane.RightPane
        _left_pc.tag_selected.Add <| _tc.tag_selected_handler LeftOrRightPane.LeftPane
        _right_pc.tag_selected.Add <| _tc.tag_selected_handler LeftOrRightPane.RightPane
        _tc.save_project.Add <| fun () -> do save_project () |> ignore
        _tc.expand_right_sidebar.Add _expand_right_sidebar.Trigger
        _tc.command_sent.Add <| fun (command, parameters) -> do _aosc.send_command command parameters
(* When we handle AddOnServerController commands, we first verify a project is open. *)
        _aosc.copy_text.Add try_copy_text
        _aosc.copy_url.Add try_copy_url
        _aosc.find_in_project.Add find_in_project_handler
        _aosc.get_files_in_project.Add get_files_in_project_handler
        _aosc.get_tags_in_project.Add get_tags_in_project_handler
//#endregion

(* Methods. *)

/// <summary>Save and close the current file and save the current project and back up the current project. Return true if we do all of these successfully; otherwise, false.</summary>
    member this.shutdown () = _left_pc.close_file () && _right_pc.close_file () && save_project () && backup_project ()

(* Events. *)

/// <summary>The right sidebar needs to be expanded.</summary>
    member this.expand_right_sidebar = _expand_right_sidebar.Publish

(* Expose methods for testing. *)
    member this.test_save_project = save_project
    member this.test_backup_project = backup_project
    member this.test_open_project = open_project

    member this.test_open_file_handler = open_file_handler
    member this.test_rename_file_handler = rename_file_handler

(* Expose the controls for testing. *)
    member this.test_config = config
    member this.test_tree = tree
    member this.test_left_pc = _left_pc
    member this.test_right_pc = _right_pc
    member this.test_tc = _tc

(* Expose the vertical position map. *)
    member this.test_vertical_positions = !_vertical_positions
