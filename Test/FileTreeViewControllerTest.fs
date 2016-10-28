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

module FileTreeViewControllerTest

(* Added reference: Microsoft.VisualBasic. *)

// ConcurrentDictionary
open System.Collections.Concurrent
// Directory
open System.IO

open Xunit

// FileTreeView
open TaggerControls
// FileTreeNode
open FileTreeViewHelpers
// TaggerConfig
open Config
// FileTreeViewController
open FileTreeViewController
// FileTreeNodeDescription and helpers
open FileTreeViewTest
// Project, ProjectController
open Project
open TestHelpers

(*
Log events to test:
x NewFileCreateDirectoryError
x NewFileCreateFileError
x NewFolderCreateDirectoryError
x RenameFileIOError
x RenameFileError
x DragDrop
x OpenProjectFolderNotFound
x OpenProject

Functions to test:
x convert_input_string
N get_new_file (UI function)
x add_default_extension
x new_file_helper
N new_file_handler (this just calls get_new_file, a UI function, and then new_file_helper)
N get_new_folder (UI function)
x new_folder_helper
N new_folder_handler (this just calls get_new_folder, a UI function, and then new_folder_helper)

N get_rename_file_name (UI function)
x rename_file_helper
N rename_file_handler (this just calls get_rename_file_name and then rename_file_helper)
x drag_drop_handler (under tests_log)
x tree_changed_handler
x open_project

Events:
N _file_opened (No function triggers this event. It is simply re-fired from FileTreeView.)
x _file_renamed (in rename_file)
x _project_created (No function triggers this event. It is simply re-fired from FileTreeView.)
x _project_opened (No function triggers this event. It is simply re-fired from FileTreeView.)
x _project_saved (No function triggers this event. It is simply re-fired from FileTreeView. However, we also re-fire FileTreeView.tree_changed as _project_saved. We test that in tree_changed.)
*)
(* Note that FTVC has an open_project but no save_project. open_project gets the allowed file patterns and updates the FTV based on the specified folder. When saving a project, we don't have to do the reverse of those (i.e. we don't write any of the FTV's files to disk). All we have to do is get the FTV's FTN and write it to the project.ini file. That's handled by MC and ProjectController.
Now that saving the project.ini file (which includes the sort order) is handled by PC, we need to use ProjectController if we want to test FTVC.open_project with a valid sort order.
*)

(* Helper functions. *)
//#region
/// <summary>Helper function for testing sorting of an FTN. Reverses the files field of FTN (1) and its child FTNs. Returns the FTN.</summary>
let rec reverse_ftn_file_order (ftn : FileTreeNode) =
(* Return a new FTN with the files reversed. *)
    {
        ftn with
            files = ftn.files |> List.rev
            dirs = ftn.dirs |> List.map (fun dir -> dir |> reverse_ftn_file_order)
    }

/// <summary>Helper function for saving an FTN to disk. Saves the files and folders of FTN (1) to disk, as well as the project information. Return unit.</summary>
let save_ftn_to_disk (ftn : FileTreeNode) =
(* Write the project files to disk. *)
    do make_test_dirs_ ftn
(* Save the project. Note that this just saves the project.ini file. ProjectController.save_project saves the project.ini file in the folder specified by the dir field of the FileTreeNode. *)
    let project = {
        sort_order = ftn
        left_tabs = []
        right_tabs = []
        vertical_positions = new ConcurrentDictionary<string, float> ()
        tag_number = 0
        move_to_mru = []
        tag_mru = []
        }
    do ProjectController.save_project (new FileTreeView ()) project |> ignore
//#endregion

type FileTreeViewControllerTest () =
    interface ITestGroup with

(* Tests that log. *)
//#region
    member this.tests_log with get () = [
(* These tests should log an event. *)
(* These events are non-critical errors. *)
    {
(* Try to open a project folder that doesn't exist. *)
        part = "FileTreeViewController"
        name = "OpenProjectFolderNotFound"
        test = fun name ->
            let path = getTestFolderName name
            let ftvc = new FileTreeViewController (new FileTreeView ())
(* Don't actually create the folder. open_project should still return true. *)
            do Assert.True <| ftvc.open_project path None
    };
(* Try to create a new file in a directory with an invalid path. *)
    {
        part = "FileTreeViewController"
        name = "NewFileCreateDirectoryError"
        test = fun name ->
(* We use an invalid directory instead of GetTestFolderName because we don't actually create any folders. *)
            let ftn = ftnd_to_ftn ":" ftnd_path0
(* Create the FileTreeView from the FTN. Since we're using the constructor instead of initialize, it won't attempt to read the directory. *)
            let ftv = new FileTreeView (Some ftn)
            let ftvc = new FileTreeViewController (ftv)
(* Add a file to the node with the invalid directory. *)
            do ftvc.test_new_file_helper (ftv.root_node) ""
    };
(* Try to create a new file with an invalid path. *)
    {
        part = "FileTreeViewController"
        name = "NewFileCreateFileError"
        test = fun name ->
            let test_folder = getTestFolderName name
(* Create a project, using the test directory. *)
            let ftn = ftnd_to_ftn test_folder ftnd_path0
            let ftv = new FileTreeView (Some ftn)
            let ftvc = new FileTreeViewController (ftv)
(* Add a file with an invalid path. *)
            do ftvc.test_new_file_helper (ftv.root_node) ":"
    };
(* Try to create a new folder with an invalid path. *)
    {
        part = "FileTreeViewController"
        name = "NewFolderCreateDirectoryError"
        test = fun name ->
            let test_folder = getTestFolderName name
(* Create a project, using the test directory. *)
            let ftn = ftnd_to_ftn test_folder ftnd_path0
            let ftv = new FileTreeView (Some ftn)
            let ftvc = new FileTreeViewController (ftv)
(* Add a folder with an invalid path. *)
            do ftvc.test_new_folder_helper (ftv.root_node) ":"
    };
(* Try to rename a file to an invalid name. *)
    {
        part = "FileTreeViewController"
        name = "RenameFileError"
        test = fun name ->
            let ftvc = new FileTreeViewController (new FileTreeView ())
(* Create a FileTreeViewItem. *)
            let node = new FileTreeViewItem (None, File, "", "")
(* Try to rename the node to an invalid name. *)
            do ftvc.test_rename_file_helper node ":"
    };
(* Try to rename a file while it is in use. *)
    {
        part = "FileTreeViewController"
        name = "RenameFileIOError"
        test = fun name ->
            let test_folder = getTestFolderName name
(* Create a project, using the test directory. *)
            let ftn = ftnd_to_ftn test_folder ftnd_path0
            let ftv = new FileTreeView (Some ftn)
            let ftvc = new FileTreeViewController (ftv)
(* Add a new file to the tree. *)
            let file = getTestTextFileName name
            do ftvc.test_new_file_helper (ftv.root_node) file
(* Verify that the new file exists. *)
            let file_path = sprintf "%s\\%s" test_folder file
            do Assert.True (File.Exists file_path)
(* Open the file. Specify that other applications can only read it. *)
            let filestream = File.Open (file_path, FileMode.Open, FileAccess.Read)
(* Try to rename the file. *)
            let node = ftv.root_node.Items.[0] :?> FileTreeViewItem
            let file_ = getTestTextFileNameWithNumber name 2
            do
                ftvc.test_rename_file_helper node file_
(* Release the file stream. *)
                filestream.Close ()
    };
(* These events are informational. *)
    {
(* Open a project. *)
        part = "FileTreeViewController"
        name = "OpenProject"
        test = fun name ->
            let test_folder = getTestFolderName name
(* Create a project, using the test directory. *)
            let ftn = ftnd_to_ftn test_folder ftnd_path0
(* Save the project to disk. *)
            do save_ftn_to_disk ftn
(* Create an empty FileTreeView to hold the project. *)
            let ftv = new FileTreeView ()
            let ftvc = new FileTreeViewController (ftv)
(* Open the project. *)
            do Assert.True <| ftvc.open_project test_folder None
    };
    {
(* Drag and drop a node. *)
        part = "FileTreeViewController"
        name = "DragDrop"
        test = fun name ->
            let ftn = ftnd_to_ftn current_dir ftnd_path3
            let ftv = new FileTreeView (Some ftn)
            let ftvc = new FileTreeViewController (ftv)
(* 1. Drop _2 onto _1, inserting _2 before _1. *)
            let root_node = ftv.root_node
            let node_1, node_2 = root_node.Items.[0] :?> FileTreeViewItem, root_node.Items.[1] :?> FileTreeViewItem
(* TestDrop should not be called on a root node. The source and target are described by the DragEventArgs passed to the Drop handler, not by the this identifier. However, TestDrop exposes a private method called drophelper, which fires a private instance event called _drag_drop, which in the case of the root node will not have a handler added by FileTreeView. Other than that, TestDrop can be called on any non-root node, and its identity will not be used in determining the source and target nodes. *)
            node_1.test_drop_helper node_2 node_1 |> Assert.True
    }
    ]
//#endregion

    member this.tests_throw with get () = []

(* Tests that don't log. *)
//#region
    member this.tests_no_log with get () = [
    {
        part = "FileTreeViewController"
        name = "convert_input_string"
        test = fun name ->
            let ftv = new FileTreeView (None)
            let ftvc = new FileTreeViewController (ftv)
            let result = " 1 , 2, , 3 ,, " |> ftvc.test_convert_input_string
            do
                result.IsSome |> Assert.True
                result.Value = ["1"; "2"; "3"] |> Assert.True
    };
    {
        part = "FileTreeViewController"
        name = "add_default_extension"
        test = fun name ->
            let test_folder = getTestFolderName name
(* Create a project, using the test directory. *)
            let ftn = ftnd_to_ftn test_folder ftnd_path0
            let ftv = new FileTreeView (Some ftn)
            let ftvc = new FileTreeViewController (ftv)
(* 1. No default file extension is specified. *)
            do ftv.default_file_extension <- ""
            let file_1 = getTestTextFileNameWithNumber name 1
            let file_1_ = ftvc.test_add_default_extension file_1
(* The file extension should be unchanged. *)
            do file_1_ = file_1 |> Assert.True
(* 2. A default file extension is specified, but is overridden by the user-specified file extension. In this case, that is ".txt", because we got the file name from getTestTextFileNameWithNumber. *)
            do ftv.default_file_extension <- ".abc"
            let file_2 = getTestTextFileNameWithNumber name 2
            let file_2_ = ftvc.test_add_default_extension file_2
(* The file extension should be unchanged. *)
            do file_2_ = file_2 |> Assert.True
(* 3. A default file extension is specified, which applies to the new file. *)
(* We leave the default file extension as ".abc". *)
(* We don't use getTestTextFileNameWithNumber because that generates a file with extension .txt. Because of the test naming scheme, we do need to replace existing periods with underscores though. *)
            let file_3 = sprintf "%s_3" <| name.Replace ('.', '_')
            let file_3_ = ftvc.test_add_default_extension file_3
(* The file extension should be the default file extension. *)
            do file_3_ = sprintf "%s.abc" file_3 |> Assert.True
    };
    {
        part = "FileTreeViewController"
        name = "new_file_helper"
        test = fun name ->
            let test_folder = getTestFolderName name
(* Create a project, using the test directory. *)
            let ftn = ftnd_to_ftn test_folder ftnd_path0
            let ftv = new FileTreeView (Some ftn)
            let ftvc = new FileTreeViewController (ftv)
(* Add a new file to the tree. *)
            let file_1 = getTestTextFileNameWithNumber name 1
            do ftvc.test_new_file_helper (ftv.root_node) file_1
(* Verify the new file exists. *)
            let file_path_1 = sprintf "%s\\%s" test_folder file_1
            do
                file_path_1 |> File.Exists |> Assert.True
(* Verify the new node is present. *)
                (ftv.root_node.Items.[0] :?> FileTreeViewItem).path = file_path_1 |> Assert.True
    };
    {
        part = "FileTreeViewController"
        name = "new_folder_helper"
        test = fun name ->
            let test_folder_1 = getTestFolderNameWithNumber name 1
(* Create a project, using the test directory. *)
            let ftn = ftnd_to_ftn test_folder_1 ftnd_path0
            let ftv = new FileTreeView (Some ftn)
            let ftvc = new FileTreeViewController (ftv)
(* Add a new folder to the tree. We don't use getTestFolderNameWithNumber because we don't want a full path. *)
            let test_folder_2 = sprintf "%s_2" name
            do ftvc.test_new_folder_helper (ftv.root_node) test_folder_2
(* Verify that the new folder exists. *)
            let folder_path = sprintf "%s\\%s" test_folder_1 test_folder_2
            do
                Assert.True (Directory.Exists folder_path)
(* Verify that the new node is present. *)
                Assert.True ((ftv.root_node.Items.[0] :?> FileTreeViewItem).path = folder_path)
    };
    {
        part = "FileTreeViewController"
        name = "rename_file_helper"
        test = fun name ->
            let test_folder = getTestFolderName name
(* Create a project, using the test directory. *)
            let ftn = ftnd_to_ftn test_folder ftnd_path0
            let ftv = new FileTreeView (Some ftn)
            let ftvc = new FileTreeViewController (ftv)
(* We use this to make sure the related event fires. *)
            let rename_file_fired = ref false
            do ftvc.file_renamed.Add (fun _ -> do rename_file_fired := true)
(* Add a new file to the tree. *)
            let file = getTestTextFileName name
            do ftvc.test_new_file_helper (ftv.root_node) file
(* Verify that the new file exists. *)
            let file_path = sprintf "%s\\%s" test_folder file
            do Assert.True (File.Exists file_path)
(* Rename the file. *)
            let node = ftv.root_node.Items.[0] :?> FileTreeViewItem
            let file_ = getTestTextFileNameWithNumber name 2
            do
                ftvc.test_rename_file_helper node file_
(* Verify that the node has been renamed. *)
                Assert.True (node.path.EndsWith file_)
                Assert.True (node.Header.ToString () = file_)
(* Verify that the file has been renamed. *)
                Assert.True (File.Exists <| sprintf "%s\\%s" test_folder file_)
(* Verify the path is unchanged. We added this after encountering a bug where the old file name became part of the path instead of being replaced. *)
                node.path |> Path.GetDirectoryName = test_folder |> Assert.True
(* Verify that the event fired. *)
                Assert.True !rename_file_fired
    };
    {
        part = "FileTreeViewController"
        name = "open_project"
        test = fun name ->
(* Append the number 1 to the test folder, because we'll use more than one for this test. *)
            let test_folder_1 = getTestFolderNameWithNumber name 1
(* Create a project, using the test directory. *)
            let ftn = ftnd_to_ftn test_folder_1 ftnd_path3
(* Reverse the files field in the FTN and its child FTNs. *)
            let ftn_ = ftn |> reverse_ftn_file_order
(* Save the project to disk. *)
            do save_ftn_to_disk ftn_
(* Move the project to a new directory. *)
            let test_folder_2 = getTestFolderNameWithNumber name 2
(* http://stackoverflow.com/questions/58744/best-way-to-copy-the-entire-contents-of-a-directory-in-c-sharp
This is apparently the only built-in way to copy a directory in .NET.
*)
            do (new Microsoft.VisualBasic.Devices.Computer ()).FileSystem.CopyDirectory (test_folder_1, test_folder_2)
(* Create an empty FileTreeView to hold the project. *)
            let ftv = new FileTreeView ()
            let ftvc = new FileTreeViewController (ftv)
(* Open the project. The project.ini file should sort the files correctly - that is, in reverse order. *)
            let project = ProjectController.open_project test_folder_2
            do Assert.True project.IsSome
            let sort_order = project.Value.sort_order
            do Assert.True (ftvc.open_project test_folder_2 <| Some sort_order)
(* Get the FTN from the tree. *)
            let ftn__ = ftv.get_ftn ()
(* Remove the project.ini file from the FTN, in case the default file pattern in the TaggerConfig doesn't already exclude it. *)
            let ftn___ = {
                    ftn__ with files = ftn__.files |> List.filter (fun file -> file.Contains ("project.ini") = false)
                }
(* Compare the FTN to the original FTN, but only compare the file names, since the project directory has changed and this changes the paths of all folders and files. *)
            let files1 = ftn.files |> List.map Path.GetFileName
            let files2 = ftn___.files |> List.map Path.GetFileName
            let compare = (files1 = files2)
            do Assert.True compare
    };
    {
        part = "FileTreeViewController"
        name = "open_project_no_sort_order"
        test = fun name ->
            let test_folder = getTestFolderName name
            let ftn = ftnd_to_ftn test_folder ftnd_path3
(* Save the project to disk. *)
            do save_ftn_to_disk ftn
(* Create an empty FileTreeView to hold the project. *)
            let ftv = new FileTreeView ()
            let ftvc = new FileTreeViewController (ftv)
(* Add a filter to the tree, or else it will include the project.ini file when it opens the project. Normally, this is done by the TaggerConfig class. *)
            do ftv.file_filter_patterns <- ["\.txt$"]
(* Open the project. *)
            do Assert.True <| ftvc.open_project test_folder None
(* Get the FTN from the tree. *)
            let ftn_ = ftv.get_ftn ()
(* Compare the FTN to the original (unsorted) FTN. *)
            do compare_ftns ftn ftn_ |> Assert.True
    };
    {
        part = "FileTreeViewController"
        name = "tree_changed"
        test = fun name ->
            let ftv = new FileTreeView ()
            let ftvc = new FileTreeViewController (ftv)
(* This lets us verify that the event fired. *)
            let project_saved_fired = ref false
            do
                ftvc.project_saved.Add (fun () -> do project_saved_fired := true)
                ftv.TestTreeChangedEvent ()
(* Verify that the event fired. *)
                Assert.True !project_saved_fired
    };
    ]
//#endregion