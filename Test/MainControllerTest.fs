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

module MainControllerTest

(* ZipFile requires assembly System.IO.Compression.FileSystem. Also, for some reason we can't reference System.IO.Compression. *)

open System
// File, ZipFile
open System.IO
// MessageBox
open System.Windows

open Xunit

// FileTreeView, TaggerTextEditor, TaggerTabControl
open TaggerControls
// MainController
open MainController
// AddOnServer
open AddOnServer
// TaggerConfig
open Config
// FileTreeNodeDescription and helpers
open FileTreeViewTest
// Helpers
open FileTreeViewControllerTest
// Project
open Project
open TestHelpers

(*
Errors:
x ProjectBackupCompression
x ProjectBackupFolderLookup
x ProjectBackupCurrentFolder

Functions to test:
x save_project (in open_project and open_project_new_project, which call MainControllerTest.save_project, which calls MainController.save_project)
x backup_project
/ save_backup_project (is called by open_project, which we test, and just calls PaneController.close_file, save_project, and backup_project)
x match_vertical_positions
N set_tc_default_values
/ open_project_helper (called by open_project)
x open_project
x open_file_handler
x rename_file_handler
N open_project_handler (UI function)
N margin_right_click_handler (just calls FileTreeView.get_file_list and TagController.margin_right_click)
N editor_right_click_handler (just calls FileTreeView.get_file_list and TagController.right_click)
N editor_find_in_project_handler (just calls FileTreeView.get_file_list and TagController.get_find_in_project_word)
N find_in_project_handler (just calls FileTreeView.get_file_list and TagController.find_in_project)
N get_files_in_project_handler (just calls FileTreeView.get_file_list and AddOnServerController.send_command)
N get_tags_in_project_handler (just calls TagController.tag_mru and AddOnServerController.send_command)
N open_tag_handler (just calls TagController.open_tag)
N shutdown (just calls close_file and save_project)
*)

(* Helper functions. *)
//#region
/// <summary>Helper function that returns a new MainController.</summary>
let get_main_controller
    (config : TaggerConfig option)
    (tree : FileTreeView option)
    (left_editor : TaggerTextEditor option)
    (right_editor : TaggerTextEditor option)
    (left_pane_left_margin : TaggerMargin option)
    (left_pane_right_margin : TaggerMargin option)
    (right_pane_left_margin : TaggerMargin option)
    (right_pane_right_margin : TaggerMargin option)
    (left_tabs : TaggerTabControl option)
    (right_tabs : TaggerTabControl option) =
    let tree_ =
        match tree with
        | Some tree__ -> tree__
        | None -> new FileTreeView ()
    let left_editor_ =
        match left_editor with
        | Some editor -> editor
        | None -> new TaggerTextEditor ()
    let right_editor_ =
        match right_editor with
        | Some editor -> editor
        | None -> new TaggerTextEditor ()
    let left_pane_left_margin_ =
        match left_pane_left_margin with
        | Some margin -> margin
        | None -> new TaggerMargin ()
    let left_pane_right_margin_ =
        match left_pane_right_margin with
        | Some margin -> margin
        | None -> new TaggerMargin ()
    let right_pane_left_margin_ =
        match right_pane_left_margin with
        | Some margin -> margin
        | None -> new TaggerMargin ()
    let right_pane_right_margin_ =
        match right_pane_right_margin with
        | Some margin -> margin
        | None -> new TaggerMargin ()
    let left_tabs_ =
        match left_tabs with
        | Some tabs -> tabs
        | None -> new TaggerTabControl ()
    let right_tabs_ =
        match right_tabs with
        | Some tabs -> tabs
        | None -> new TaggerTabControl ()
    let left_status = new System.Windows.Controls.TextBlock ()
    let right_status = new System.Windows.Controls.TextBlock ()
    let add_on_server = new AddOnServer ()
    let config_ =
        match config with
        | Some config__ -> config__
(* MainController expects a TaggerConfig, and that in turn expects controls. *)
(* TaggerConfig expects a TaggerGrid and AddOnServer, but these are not involved in testing MainController, so we simply create them here rather than taking them as parameters. *)
        | None -> new TaggerConfig (new TaggerGrid (), tree_, left_editor_, right_editor_, add_on_server)
(* Config settings are applied to controls in TaggerConfig.apply_settings_to_controls, which is called by TaggerConfig.load_config, which is called by Program.load_window. Here, we need to call load_config manually. *)
    do
        config_.load_config ()
(* Config.load_config calls Config.apply_settings_to_controls, which calls AddOnServer.update_host_port, which starts the server. We don't use it here, so we stop it to prevent it from causing conflict with other tests that do use it. *)
        add_on_server.stop ()
    new MainController (config_, tree_, left_editor_, right_editor_, left_pane_left_margin_, left_pane_right_margin_, right_pane_left_margin_, right_pane_right_margin_, left_tabs_, right_tabs_, left_status, right_status)

(* Note: This is borrowed from EditorTest. *)
/// <summary>Helper function that determines whether an editor (1) is empty. Returns true if empty; otherwise, false.</summary>
let editor_is_empty (editor : TaggerTextEditor) =
    editor.IsEnabled = false &&
    editor.file.IsNone &&
    editor.Text.Length = 0

(* Used by save_project and open_project. *)
/// <summary>Helper function that creates a project in folder (1) and saves it. Return the project information, so we can make sure the project saves and opens correctly.</summary>
let save_project dir =
(* Create a FileTreeNode. *)
    let ftn = ftnd_to_ftn dir ftnd_path3
(* Save the files to disk. *)
    do save_ftn_to_disk ftn
(* Create a MainController. *)
    let main = get_main_controller None None None None None None None None None None
(* Create a new project. *)
    do main.test_open_project dir
(* Change the sort order of the files in the tree. *)
    let ftvi1 = main.test_tree.root_node.Items.[0] :?> FileTreeViewItem
    let ftvi2 = main.test_tree.root_node.Items.[1] :?> FileTreeViewItem
    do ftvi1.test_drop_helper ftvi2 ftvi1 |> Assert.True
(* Add the files to the Move To MRU in reverse order. *)
    do main.test_tc.move_to_mru <- ftn.files |> List.rev
(* Open the files in tabs. Verify that each one opened successfully. *)
    let tabs = ftn.files |> List.map (fun file ->
        let tab = main.test_left_pc.tabs.add_tab file
        tab.IsSome |> Assert.True
        tab.Value
        )
(* Select and close each tab, to add them to the vertical position map. *)
    do
        tabs |> List.iter (fun tab ->
            do tab.IsSelected <- true
(* Verify that each file was successfully saved. *)
            do Assert.True <| main.test_left_pc.close_tab tab.file)
(* Open the tabs again. This time, discard the tabs returned from add_tab. *)
        ftn.files |> List.iter (fun file -> main.test_left_pc.tabs.add_tab file |> ignore)
(* Save the project. *)
        main.test_save_project () |> Assert.True
(* Verify that the project was added to the recent project list in the TaggerConfig. *)
    let recent_projects = main.test_config.TryFind "RecentProjectFolders"
    do Assert.True (recent_projects.IsSome && recent_projects.Value |> List.exists (fun item -> item = dir))
(* For now, we've decided to stop saving the project automatically, and only save it in response to events like the tree or tab control changing. *)
(* Disable the auto-save, or it will continue adding to the log, which interferes with the auto-save test. *)
//    do main.test_tree.test_auto_save_timer.Stop ()
(* Return the project information, so we can make sure the project saves and opens correctly. *)
    {
        sort_order = main.test_tree.get_ftn ()
        left_tabs = tabs |> List.map (fun tab -> tab.file)
        right_tabs = []
        vertical_positions = main.test_vertical_positions
        tag_number = 0
        move_to_mru = main.test_tc.move_to_mru
        tag_mru = main.test_tc.tag_mru |> List.map (fun tag -> tag.ToString ())
    }

/// <summary>Helper function that compares the contents of the zip file (2) to the folder from which it was created (1). Return unit.</summary>
let verify_backup original_folder zip_file_path =
(* Create a temporary folder to extract the zip file contents. Add the number of ticks to the name, so it isn't duplicated. *)
    let unzip_folder = getTestFolderName <| sprintf "verify_backup_%d" DateTime.Now.Ticks
(* Get the contents of the backup. *)
    do Compression.ZipFile.ExtractToDirectory (zip_file_path, unzip_folder)
(* Compare the contents of the project folder to those of the extracted backup folder. *)
    let rec compare_dirs dir1 dir2 =
(* Compare only the file names, because the paths will be different. *)
        let get_files = Directory.GetFiles >> Array.map Path.GetFileName >> Array.sort
        let dirs1 = Directory.GetDirectories dir1
        let dirs2 = Directory.GetDirectories dir2
        let files1 = dir1 |> get_files
        let files2 = dir2 |> get_files
(* Compare the files and all subfolders. *)
        (files1 = files2) && (dirs1, dirs2) ||> Array.forall2 compare_dirs
    do compare_dirs original_folder unzip_folder |> Assert.True
//#endregion

type MainControllerTest () =
    interface ITestGroup with

(* Tests that log. *)
//#region
    member this.tests_log with get () = [
(* These tests should log an event. *)
    {
        part = "MainController"
        name = "ProjectBackupCompression"
        test = fun name ->
(* Create a test project. *)
            let test_folder = getTestFolderName name
(* Create a MainController. *)
            let main = get_main_controller None None None None None None None None None None
            do
(* Load the project. For our purpose it can just be an empty directory. *)
                Directory.CreateDirectory test_folder |> ignore
                main.test_open_project test_folder
(* Set the backup folder in the config to an invalid one. We could also set the current project folder to an invalid one, but then backup_folder raises an exception when trying to get the folder name, before it enters the try/with block for the compression. See backup_folder for more information. *)
                main.test_config.Update "ProjectBackupFolder" [":"]
(* Try to back up the project. *)
                main.test_backup_project () |> Assert.False
    };
    {
        part = "MainController"
        name = "ProjectBackupFolderLookup"
        test = fun name ->
(* Create a test project. *)
            let test_folder = getTestFolderName name
(* Create a MainController. *)
            let main = get_main_controller None None None None None None None None None None
            do
(* Remove the backup folder from the config. *)
                main.test_config.test_settings.TryRemove "ProjectBackupFolder" |> ignore
(* Load the project. For our purpose it can just be an empty directory. *)
                Directory.CreateDirectory test_folder |> ignore
                main.test_open_project test_folder
(* Try to back up the project. *)
                main.test_backup_project () |> Assert.False
    };
    {
        part = "MainController"
        name = "ProjectBackupCurrentFolder"
        test = fun name ->
(* Create a test project. *)
            let test_folder = getTestFolderName name
(* Create a MainController. *)
            let main = get_main_controller None None None None None None None None None None
            do
(* Load the project. For our purpose it can just be an empty directory. *)
                Directory.CreateDirectory test_folder |> ignore
                main.test_open_project test_folder
(* Delete the current project folder. *)
                Directory.Delete test_folder
(* Try to back up the project. *)
                main.test_backup_project () |> Assert.False
    };
    ]
//#endregion

    member this.tests_throw with get () = []

(* Tests that don't log. *)
//#region
    member this.tests_no_log with get () = [
(* For now, we've decided to stop saving the project automatically, and only save it in response to events like the tree or tab control changing. *)
(* This test should perhaps be moved to FileTreeViewTest, because FileTreeView now owns the auto-save timer. However, I've decided to keep it here to make sure the project actually is saved. *)
(*
    {
        part = "MainController"
        name = "save_project_auto_save"
        test = fun name ->
(* Create a MainController. *)
            let main = get_main_controller None None None None None None None None None None
(* Verify that the project doesn't auto save, since none is loaded. Normally, we would attach an event handler to the project save event and make sure it doesn't fire. However, there is no such event. We could instead expose the FileTreeViewController from MainController and attach to FTVC's project_saved event. For now, we simply check the log string. *)
(* Wait for the auto save. See EditorTest save_timer_tick for notes on waiting for a tick. *)
            do
                sprintf "%s. Please wait 10 seconds before clicking OK." name |> MessageBox.Show |> ignore
(* Verify the project has not been saved. *)
                Assert.True (_log_string.ToString().Contains "ProjectController.SaveProjectInformation" = false)
(* Create a new project. *)
            let test_folder = getTestFolderName name
            do
                main.test_open_project test_folder
(* Wait for the auto save. *)
                sprintf "%s. Please wait 10 seconds before clicking OK." name |> MessageBox.Show |> ignore
(* Verify the project has been saved. *)
                Assert.True (_log_string.ToString().Contains (sprintf "ProjectController.SaveProjectInformation Saved project %s." test_folder))
    };
*)
    {
        part = "MainController"
        name = "backup_project"
        test = fun name ->
(* We'll use this backup folder to create a zip file. *)
            let backup_folder = getTestFolderNameWithNumber name 2
(* Create a test project. *)
            let test_folder = getTestFolderNameWithNumber name 1
(* Save the project and get the project information. *)
            let project = save_project test_folder
(* Create a MainController. *)
            let main = get_main_controller None None None None None None None None None None
            do
(* Load the project. *)
                main.test_open_project test_folder
(* Set the backup folder. *)
                Directory.CreateDirectory backup_folder |> ignore
                main.test_config.Update "ProjectBackupFolder" [backup_folder]
(* Back up the project. *)
                main.test_backup_project () |> Assert.True
(* Get the path of the zip file. *)
            let zip_file_path = Directory.EnumerateFiles (backup_folder, "*.zip") |> Seq.cast<string> |> Seq.head
(* Verify the contents of the backup. *)
            do verify_backup test_folder zip_file_path
    };
    {
        part = "MainController"
        name = "match_vertical_positions"
        test = fun name ->
// TODO2 Add test.
            ()
    };
(* In this test, we open two projects. The first is to verify that when we open another project (the second one), the following things happen:
Changes to open files in the first project are saved.
The project is saved.
The project is backed up correctly.

The second is to verify that when a project (the second one) is saved and reopened, the following things happen:
The FileTreeView sort order is the same.
The tab lists are the same.
The vertical position map is the same.
The Move To MRU is the same.
The Tag MRU is the same.

We then open the first project again to verify that deleted files are removed from the FTV, the Move To MRU, and the vertical position map. Deleted files are removed from the FTV (based on the file system), and from the Move To MRU and vertical position map (based on the FTV), on project open.

Notes:
save_project creates three test files and changes their order in the FTV. It opens and closes them so they are added to the vertical position map. It also adds them to the Move To MRU and changes their order there.
*)
    {
        part = "MainController"
        name = "open_project"
        test = fun name ->
(* Helper to verify the PC's reference to the vertical position map in the MC has not been broken. *)
            let test_vpos (main : MainController) = do main.test_left_pc.test_vertical_positions.Equals main.test_vertical_positions |> Assert.True
(* Helper to convert a TabControl to a list of files. *)
            let get_tabs (tabs : TaggerTabControl) = tabs.Items |> Seq.cast<TaggerTabItem> |> Seq.toList |> List.map (fun tab -> tab.file)
(* Helper to compare project data in a MainController to expected project data. *)
            let compare_projects (main : MainController) (project : Project) =
                compare_ftns project.sort_order (main.test_tree.get_ftn ()) |> Assert.True
(* For the vertical position map, we can only verify the file names, since without a UI we do not set vertical position on the files we open. See close_file_save_vertical_positions for more information. *)
                (project.vertical_positions.ToArray (), main.test_vertical_positions.ToArray ()) ||> compare_arrays |> Assert.True
                (project.left_tabs, main.test_left_pc.tabs |> get_tabs) ||> compare_lists |> Assert.True
                (project.right_tabs, main.test_right_pc.tabs |> get_tabs) ||> compare_lists |> Assert.True
                project.tag_number = main.test_tc.tag_number |> Assert.True
                (project.move_to_mru, main.test_tc.move_to_mru) ||> compare_lists |> Assert.True
                (project.tag_mru |> List.map TagWithoutSymbol, main.test_tc.tag_mru) ||> compare_lists |> Assert.True
(* Helper to get project data from a MainController. *)
            let get_project_data_from_main_controller (main : MainController) = {
                sort_order = main.test_tree.get_ftn ();
                vertical_positions = main.test_vertical_positions;
                left_tabs = main.test_left_pc.tabs |> get_tabs;
                right_tabs = main.test_right_pc.tabs |> get_tabs;
                tag_number = main.test_tc.tag_number;
                move_to_mru = main.test_tc.move_to_mru;
                tag_mru = main.test_tc.tag_mru |> List.map (fun tag -> tag.ToString ());
                }
(* Create a test project. *)
            let test_folder_1 = getTestFolderNameWithNumber name 1
(* Create files in the project folder. *)
(* Note save_project changes the order of the first two items in the tree, to help the caller verify the sort order is applied when the project is opened again. save_project uses FTN ftnd_path3, so the files should be named _1, _2, and _3. Any test files we create here should be named delete_me_*, so they should come after the files created by save_project and their sort order should not be changed. *)
(* Note we create these before calling save_project. We want to make sure they're included in the FTV, the Move To MRU and the vertical position map, so we can delete one of them and test that it is removed from those data structures. *)
            let test_file_1 = CreateTestTextFileInFolderWithNumber name 1 test_folder_1 ""
            let test_file_2 = CreateTestTextFileInFolderWithNumber name 2 test_folder_1 ""
(* We use this file to verify the Tag MRU works right. *)
            let test_file_3 = CreateTestTextFileInFolderWithNumber name 3 test_folder_1 "#1#2#3#4#5"
(* Save the project and get the project information. *)
            let project_1 = save_project test_folder_1
(* Create a MainController. *)
            let main = get_main_controller None None None None None None None None None None
(* Set up a backup folder. *)
            let backup_folder = getTestFolderNameWithNumber name 2
            do
                Directory.CreateDirectory backup_folder |> ignore
                main.test_config.Update "ProjectBackupFolder" [backup_folder]
(* Open the first project. *)
                main.test_open_project test_folder_1
(* Add a tab for the first file and open it in the editor. *)
                main.test_open_file_handler LeftOrRightPane.LeftPane test_file_1
(* Close the first file, to make sure it is added to the vertical position map. *)
                main.test_left_pc.close_file () |> ignore
(* Add the first file to the Move To MRU. *)
                main.test_tc.move_to_mru <- test_file_1 :: main.test_tc.move_to_mru
(* Add tags to the tag MRU. The tag MRU will already have built-in tags added by MainController.open_project_helper, so we append these tags instead of simply overwriting the tag MRU. *)
                main.test_tc.tag_mru <- (["5"; "4"; "3"] |> List.map TagWithoutSymbol) @ main.test_tc.tag_mru
(* Delete the first file. We do this to verify later that it is removed from the FTV, the Move To MRU, and the vertical position map. *)
(* We could possibly do this more simply by renaming the file. The old name would be removed from the FTV at once, and then removed from the Move To MRU and the vertical position map upon saving the project, when those data structures are merged with the FTV. So we would only have to save and reopen the project once. But I think a deleted file is the more likely (and rigorous) scenario so I want to test that. *)
                File.Delete test_file_1
(* Add a tab for the second file and open it in the editor. *)
                main.test_open_file_handler LeftOrRightPane.LeftPane test_file_2
(* Change the text for the second file in the editor. *)
                main.test_left_pc.editor.Text <- "test"
(* Create a second test project. *)
            let test_folder_2 = getTestFolderNameWithNumber name 3
(* Save the second project and get the project information. *)
            let project_2 = save_project test_folder_2
(* The first project has already been saved once, so delete the project file so we can make sure it is saved again. *)
            let project_1_config = sprintf "%s\\project.ini" test_folder_1
            do
                File.Delete project_1_config
(* Open the second project. This should:
1. Save the first project.
2. Save and close all files in the first project.
3. Add all files in the first project to the vertical position map for that project. *)
                main.test_open_project test_folder_2
(* Verify the changes to the second file from the first project (that was previously open) were saved. *)
                Assert.True (File.ReadAllText test_file_2 = "test")
(* Verify the first project was saved. *)
                Assert.True <| File.Exists project_1_config
(* Verify the first project was backed up. Get the path of the zip file. *)
            let zip_file_path = Directory.EnumerateFiles (backup_folder, "*.zip") |> Seq.cast<string> |> Seq.head
(* Verify the contents of the backup. *)
            do
                verify_backup test_folder_1 zip_file_path
(* This is the second part of the test, involving the second project, which is currently open. *)
(* Compare the project data in the MainController to that of the saved second project. *)
                compare_projects main project_2
(* save_project changes the FTV sort order, so the files should be in a different order than the default sort order. Verify that by getting a fresh FTN from the folder. *)
            let ftn_no_sort = FileTreeViewHelpers.dir_to_ftn test_folder_2
            do
                ftn_no_sort.IsSome |> Assert.True
                compare_ftns (ftn_no_sort.Value) (main.test_tree.get_ftn ()) |> Assert.False
(* save_project changes the Move To MRU sort order, so the files should be in a different order than the default sort order. *)
                (main.test_tc.move_to_mru, main.test_tc.move_to_mru |> List.sort) ||> compare_lists |> Assert.False
(* The vertical position map has no sort order to verify. *)
(* Open the first project again. *)
                main.test_open_project test_folder_1
(* Verify the deleted file is gone from the FTV. *)
                main.test_tree.get_file_list () |> List.exists (fun file -> file = test_file_1) |> Assert.False
(* Verify the deleted file is gone from the Move To MRU. *)
                main.test_tc.move_to_mru |> List.exists (fun file -> file = test_file_1) |> Assert.False
(* We added the tags 5, 4, 3, in that order, to the tag MRU. The tag MRU also included the built-in tags. We added tags 1, 2, 3, 4, 5, in that order, to test_file_3. open_project applies the sort order in the tag MRU. Verify that it did so. *)
                main.test_tc.tag_mru = (["5"; "4"; "3"; "zzNext"; "1"; "2"] |> List.map TagWithoutSymbol) |> Assert.True
(* Verify that opening different projects has not broken the PC's reference to the vertical position map in the MC. *)
                main |> test_vpos
(* We had a bug where the PC's reference to the vertical position map in the MC was broken when we opened a project. As a result, the PC's vpos map was always empty, and it incorrectly passed the test after this one. *)
                main.test_vertical_positions.Keys.Count > 0 |> Assert.True
(* Verify the deleted file is gone from the vertical position map. *)
                main.test_vertical_positions.Keys |> Seq.exists (fun file -> file = test_file_1) |> Assert.False
(* Open the first project again and verify nothing has changed. *)
            let project_1 = main |> get_project_data_from_main_controller
            do
                main.test_open_project test_folder_1
                compare_projects main project_1
(* For now, we've decided to stop saving the project automatically, and only save it in response to events like the tree or tab control changing. *)
(* Disable the auto-save, or it will continue adding to the log, which interferes with the auto-save test. *)
//                main.test_tree.test_auto_save_timer.Stop ()
    };
    {
        part = "MainController"
        name = "open_file_handler"
        test = fun name ->
(* Create a file. *)
            let test_file = CreateTestTextFile name "test"
(* Create a MainController. *)
            let main = get_main_controller None None None None None None None None None None
            let left_editor = main.test_left_pc.editor
            let right_editor = main.test_right_pc.editor
            do
(* Add a tab for the file and open it in the left pane. *)
                main.test_open_file_handler LeftOrRightPane.LeftPane test_file
(* Open the file in the right pane. *)
                main.test_open_file_handler LeftOrRightPane.RightPane test_file
(* Verify the file is open in the right pane but not the left pane. *)
                Assert.True (main.test_left_pc.tabs.Items.Count = 0)
                Assert.True <| editor_is_empty left_editor
                Assert.True (main.test_right_pc.tabs.get_tab test_file).IsSome
                Assert.True (right_editor.Text = "test")
(* Change the file and mark it as read-only. *)
                right_editor.Text <- "test2"
                File.SetAttributes (test_file, FileAttributes.ReadOnly)
(* Try to open the file in the left pane. *)
                main.test_open_file_handler LeftOrRightPane.LeftPane test_file
(* Verify that the file is still open in the right pane but not the left pane. *)
                Assert.True (main.test_left_pc.tabs.Items.Count = 0)
                Assert.True <| editor_is_empty left_editor
                Assert.True (main.test_right_pc.tabs.get_tab test_file).IsSome
                Assert.True (right_editor.Text = "test2")
    };
    {
        part = "MainController"
        name = "open_project_new_project"
        test = fun name ->
            let test_folder = getTestFolderName name
(* We open a file in the editor before we open the project, so we can be sure the tab control and editor are cleared. *)
(* Create a file. *)
            let test_file = CreateTestTextFile name ""
(* Create a MainController. *)
            let main = get_main_controller None None None None None None None None None None
(* Add a tab for the file and open it in the editor. *)
            do
                main.test_open_file_handler LeftOrRightPane.LeftPane test_file
(* Change the text for the file currently open in the editor. *)
                main.test_left_pc.editor.Text <- "test"
(* Open a new project. We don't create the project folder; we'll let MainController do that. *)
                main.test_open_project test_folder
(* Verify the file was saved. *)
                Assert.True (File.ReadAllText test_file = "test")
(* Verify that the tab control is empty. *)
                Assert.True (main.test_left_pc.tabs.Items.Count = 0)
(* Verify that the editor is disabled, has its file set to None, and its text set to empty. *)
                Assert.True (editor_is_empty main.test_left_pc.editor)
(* Verify that the folder was created. *)
                Assert.True <| Directory.Exists test_folder
(* For now, we've decided to stop saving the project automatically, and only save it in response to events like the tree or tab control changing. *)
(* Disable the auto-save, or it will continue adding to the log, which interferes with the auto-save test. *)
//                main.test_tree.test_auto_save_timer.Stop ()
    };
    {
        part = "MainController"
        name = "rename_file_handler"
        test = fun name ->
(* Create a file. *)
            let test_file = CreateTestTextFile name "test"
(* Create a MainController. *)
            let main = get_main_controller None None None None None None None None None None
(* Add a tab for the file and open it in the editor. *)
            do main.test_open_file_handler LeftOrRightPane.LeftPane test_file
(* Rename the file. *)
            let test_file_2 = sprintf "%s_2" test_file
            do main.test_rename_file_handler test_file test_file_2 ""
(* Make sure the tab for this file has been renamed. *)
            let tabs = main.test_left_pc.tabs
            do
                Assert.True (tabs.get_tab test_file).IsNone
                Assert.True (tabs.get_tab test_file_2).IsSome
(* Make sure the file has been renamed in the editor. *)
            let editor = main.test_left_pc.editor
            do Assert.True (editor.file.IsSome && editor.file.Value = test_file_2)
    };
    ]
//#endregion