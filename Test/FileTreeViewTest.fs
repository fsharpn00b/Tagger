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

module FileTreeViewTest

// String, RoutedEventArgs
open System
// Directory, File
open System.IO
// MenuItem
open System.Windows.Controls

open Xunit

// MaybeMonad
open Monads
// FileTreeView
open TaggerControls
// FileTreeNode
open FileTreeViewHelpers
// List extensions
open ListHelpers
open TestHelpers

(* Functions to test:
Helpers:
x search_ftn
x filter_ftn
x dir_to_ftn
x convert_ftn_paths_to_relative
x convert_ftn_paths_to_absolute
x sort_files
x sort_all_files
x sort_folders
x sort_all_folders

FTVI:
x context menus
N event handlers (we can't find a way to check for handlers programmatically)
N add_item
N mouse_move
x drophelper
N drop (this is the event handler, which we can't test directly because it takes a DropEventArgs, which is sealed)
N right_button_mouse_up
/ setup/setup_directory/setup_file/setup_root/setup_empty (via context menus test)
x add_node
N get_source_and_target (takes a DragEventArgs, which is sealed)

FTVI events:
x _new_project (in context_menus_event_handlers)
x _open_project (in context_menus_event_handlers)
x _save_project (in context_menus_event_handlers)
x _open_file (in context_menus_event_handlers)
x _rename_file (in context_menus_event_handlers)
x _new_file (in context_menus_event_handlers)
x _new_folder (in context_menus_event_handlers)
x _drag_drop (in drop)
x _open_config (in context_menus_event_handlers)

FTV:
N event handlers (we can't find a way to check for handlers programmatically)
N drag_drop_handler (just fires the drag_drop and tree_changed events)
N add_event_handlers_to_node
N add_empty_node (just calls FTVI constructor and add_event_handlers_to_node)
N add_root_node (same as add_empty_node)
/ ftn_to_ftv (via init_tree)
x get_children
/ init_tree (via constructor)
N get_sorted_ftn (just calls sort_all_files, sort_all_folders)
N load_tree_helper (just calls dir_to_ftn, filter_ftn, get_sorted_ftn, init_tree)
N save_timer_interval
N find_node (not currently used)
x add_child_node
N initialize (just calls load_tree)
N load_tree (just calls load_tree_helper)
/ get_ftn (via FTVC.save_project, conversion test)
x get_file_list
x add_recent_project

FTV events:
x _tree_changed (in DragDropEvent and AddNode)
N _open_file (No function triggers this event. It is simply re-fired from FileTreeViewItem.)
N _rename_file (Same as _open_file.)
N _new_file (Same as _open_file.)
N _new_folder (Same as _open_file.)
N _new_project (Same as _open_file.)
N _open_project (Same as _open_file.)
N _save_project (Same as _open_file.)
x _drag_drop (in DragDropEvent)
x _open_config (Same as _open_file.)
*)

(* Helper types, functions, and values. *)
//#region
(* Default file patterns. *)
let default_file_patterns = ["\.txt$"]

///<summary>Similar to a FileTreeNode, but used to generate FTNs. This contains a directory hierarchy, but no full paths. Those need to be added based on a root directory.</summary>
type FileTreeNodeDescription = {
(* The name of the directory represented by this FTN. *)
    name : string;
(* The number of files contained in this FTN. *)
    count : int;
(* The subnodes contained in this FTN. *)
    dirs : FileTreeNodeDescription list
    }

(* Standard FTNDs for use in testing. *)
//let ftnd_path0 = { name = ""; count = 0; dirs = [] }

(* This is used for testing saving and opening projects. *)
let ftnd_path0 = { name = ""; count = 0; dirs = [] }
(* This is used for when we want to test sorting the files in a folder. *)
let ftnd_path3 = { name = ""; count = 3; dirs = [] }
(* This is used to create a tree with one root node, one folder node, and one file node. *)
let ftnd_path0_a1 = { name = ""; count = 0; dirs = [ { name = "a"; count = 1; dirs = [] } ] }
(* This is used to test searching for FTNs within an FTN. *)
let ftnd_path0_a0b0 = { name = ""; count = 0; dirs = [ { name = "a"; count = 0; dirs = [ { name = "b"; count = 0; dirs = [] } ] } ] }
(* This is used to test converting absolute paths to relative and vice versa. *)
let ftnd_path1_a1b1 = { name = ""; count = 1; dirs = [ { name = "a"; count = 1; dirs = [ { name = "b"; count = 1; dirs = [] } ] } ] }
(* This is used to test sorting folders. *)
let ftnd_path0_a0b0c0 = { name = ""; count = 0; dirs = [ { name = "a"; count = 0; dirs = [] }; { name = "b"; count = 0; dirs = [] };     { name = "c"; count = 0; dirs = [] }; ] }
(* This is used to test dragging and dropping. *)
let ftnd_path0_a2b2 = { name = ""; count = 0; dirs = [ { name = "a"; count = 2; dirs = [] }; { name = "b"; count = 2; dirs = [] }; ] }
(* This is used to test get_file_list. *)
let ftnd_path1_a1b1_parallel = { name = ""; count = 1; dirs = [ { name = "a"; count = 1; dirs = [] }; { name = "b"; count = 1; dirs = [] }; ] }

///<summary>Create directories and files based on the FileTreeNode (1). Return unit.</summary>
let rec make_test_dirs_ (node : FileTreeNode) =
    do
(* Create a directory based on this FTN. *)
        node.dir |> Directory.CreateDirectory |> ignore
(* Create the files contained in this FTN. File.Create returns a stream to the file, which we need to close. *)
        node.files |> List.iter (fun file ->
            let stream = file |> File.Create
            do stream.Close ()
            )
(* For each subnode contained in this FTN, recurse. *)
        node.dirs |> List.iter make_test_dirs_

///<summary>Convert a FTNDescription (2) to an FTN, using (1) as the root directory. Return the FTN.</summary>
let rec ftnd_to_ftn dir (ftnd : FileTreeNodeDescription) =
(* Add the directory to the FTND name. *)
    let new_dir =
(* The caller might give an FTND name of "" if they want the FTN to represent the root directory itself. In that case, don't append the empty name to the root directory because it introduces a trailing backslash. *)
        if ftnd.name.Length > 0 then sprintf "%s\\%s" dir ftnd.name
        else dir
(* Generate files based on the directory and the FTND name and count. Use an extension that matches one of the default file patterns. *)
    let files = [for i in 1 .. ftnd.count -> sprintf "%s\\%s_%d.txt" new_dir ftnd.name i]
(* For each subnode in the FTND, recurse and get an FTN. *)
    let dirs = ftnd.dirs |> List.map (ftnd_to_ftn new_dir)
(* Return a FileTreeNode based on the above information. *)
    { dir = new_dir; dirs = dirs; files = files; }

/// <summary>Test that FileTreeNodes (1) and (2) are the same, using compare function (3). Return true if they are the same; otherwise, false.</summary>
let rec compare_ftns_with_compare (ftn1 : FileTreeNode) (ftn2 : FileTreeNode) (compare : string -> string -> bool) =
    MaybeMonad () {
        printfn "Node 1 name: %s. Node 2 name: %s." ftn1.dir ftn2.dir
        do! compare ftn1.dir ftn2.dir
        printfn "Node 1 dir count: %d. Node 2 dir count: %d." ftn1.dirs.Length ftn2.dirs.Length
        do! ftn1.dirs.Length = ftn2.dirs.Length
        printfn "Node 1 file count: %d. Node 2 file count: %d." ftn1.files.Length ftn2.files.Length
        do! ftn1.files.Length = ftn2.files.Length
(* For each file pair in the two FTNs, test that the paths are the same. *)
        do! (ftn1.files, ftn2.files) ||> List.forall2 (fun file1 file2 ->
            do printfn "Node 1 file: %s. Node 2 file: %s." file1 file2
            compare file1 file2
            )
(* For each subnode pair in the FTNs, recurse. *)
        do! (ftn1.dirs, ftn2.dirs) ||> List.forall2 (fun node1 node2 -> compare_ftns_with_compare node1 node2 compare)
        return true
    } |> function | Some true -> true | _ -> false

///<summary>Test that FileTreeNodes (1) and (2) are the same. Return true if they are the same; otherwise, false.</summary>
let compare_ftns (ftn1 : FileTreeNode) (ftn2 : FileTreeNode) = compare_ftns_with_compare ftn1 ftn2 (fun item1 item2 -> String.Compare (item1, item2) = 0)
//#endregion

type FileTreeViewTest () =
    interface ITestGroup with

    member this.tests_log with get () = [
    ]

    member this.tests_throw with get () = [
    ]

    member this.tests_no_log with get () = [
(* FileTreeViewHelpers *)
//#region
(* Test the search_ftn function. *)
    {
        part = "FileTreeViewHelpers"
        name = "search_ftn"
        test = fun name ->
(* Helper function to test ftn_search. Check that the FTN (1) based on root directory (2) has dir field value (3), files field length (4), and dirs field length (5). *)
            let check_ftn ftn root_dir (dir : string) filecount dircount =
(* ftnd_to_ftn converts the name of the FTND to a path based on a given root directory, so we need to get the same path in order to search the dir field of the FTN. *)
                let new_dir =
(* The FTN's dir field value might be "" if the FTN represents the root directory itself. In that case, don't append the dir field value to the root directory because it introduces a trailing backslash. *)
                    if dir.Length > 0 then sprintf "%s\\%s" root_dir dir
                    else root_dir
                MaybeMonad () {
(* First test that we can find the FTN. *)
                    let! ftn_ = search_ftn new_dir ftn
(* Check the FTN field values. *)
                    do! String.Compare (ftn_.dir, new_dir) = 0
                    do! ftn_.files.Length = filecount
                    do! ftn_.dirs.Length = dircount
                    return true
                } |> function | Some true -> true | _ -> false

            let test_folder = getTestFolderName name
            let ftn = ftnd_to_ftn test_folder ftnd_path0_a0b0
            check_ftn ftn test_folder "" 0 1 |> Assert.True
            check_ftn ftn test_folder "a" 0 1 |> Assert.True
            check_ftn ftn test_folder "a\\b" 0 0 |> Assert.True
(* Search for an FTN that exists, but using the wrong path. *)
            check_ftn ftn test_folder "b" 0 0 |> Assert.False
(* Search for an FTN that does not exist. *)
            check_ftn ftn test_folder "d" 0 0 |> Assert.False
    };
    {
        part = "FileTreeViewHelpers"
        name = "filter_ftn"
        test = fun name ->
            let test_folder_path = getTestFolderName name
(* Use the test folder as the root directory for the FTN. *)
            let ftn = ftnd_to_ftn test_folder_path ftnd_path3
(* Do not filter the FTN. *)
            (ftn |> filter_ftn []).files.Length = 3 |> Assert.True
(* Filter the FTN to remove file _1.txt. *)
            (ftn |> filter_ftn ["_[2-9]+"]).files.Length = 2 |> Assert.True
(* Filter the FTN to remove text files. *)
            (ftn |> filter_ftn ["\.html$"]).files.Length = 0 |> Assert.True
    };
(* Test the dir_to_ftn function. *)
    {
        part = "FileTreeViewHelpers"
        name = "dir_to_ftn"
        test = fun name ->
            let test_folder_path = getTestFolderName name
(* Use the test folder as the root directory for the FTN. *)
            let ftn1 = ftnd_to_ftn test_folder_path ftnd_path1_a1b1
(* Create the folder hierarchy from the FTN. *)
            do make_test_dirs_ ftn1
(* Read the folder hierarchy back into an FTN. *)
            let ftn2 = dir_to_ftn test_folder_path
            do Assert.True ftn2.IsSome
            let ftn2_ = ftn2.Value
(* Make sure the FTN has the expected folder hierarchy. *)
            compare_ftns ftn1 ftn2_ |> Assert.True
    };
(* Test the convert_ftn_paths_to_relative and convert_ftn_paths_to_absolute functions. *)
    {
        part = "FileTreeViewHelpers"
        name = "convert_ftn_paths_to_relative_and_convert_ftn_paths_to_absolute"
        test = fun name ->
            let test_folder = getTestFolderName name
            let ftn = ftnd_to_ftn test_folder ftnd_path1_a1b1
(* Convert the file and folder paths in the FTN and its children to relative paths. *)
            let ftn_ = ftn |> convert_ftn_paths_to_relative
(* The root directory of the FTN is test_folder, so its relative path is "" and its children are a and a\\b. *)
            Assert.True (ftn_.dir = "")
            Assert.True (ftn_.files = ["_1.txt"])
            let node_a = ftn_.dirs.[0]
            Assert.True (node_a.dir = "a")
            Assert.True (node_a.files = ["a_1.txt"])
            let node_b = node_a.dirs.[0]
            Assert.True (node_b.dir = "a\\b")
            Assert.True (node_b.files = ["b_1.txt"])

(* Convert the FTN back to absolute paths. *)
            let ftn__ = ftn_ |> convert_ftn_paths_to_absolute test_folder
(* Make sure the reconverted FTN is the same as the original. *)
            do compare_ftns ftn ftn__ |> Assert.True
    };
(* Test the sort_files function. *)
    {
        part = "FileTreeViewHelpers"
        name = "sort_files"
        test = fun name ->
            let test_folder = getTestFolderName name
(* This FTN is the sort order. I.e. a_1, a_2, a_3. *)
            let sort_ftn_desc = ftnd_path3
            let sort_ftn = ftnd_to_ftn test_folder sort_ftn_desc
(* The set of files to be sorted. I.e. a_5 .. a_1. *)
            let files = [for i in 5 .. -1 .. 1 -> sprintf "%s\\_%d.txt" test_folder i]
(* Sort the files a_5 .. a_1 according to the sort order in the FTN. *)
            let files_ = sort_files test_folder files sort_ftn
(* Make sure we still have 5 files. *)
            Assert.Equal (files_.Length, 5)
(* Get the file names without the full path. *)
            let files__ = files_ |> List.map Path.GetFileName
(* Make sure the files are in the expected order. *)
            Assert.True (files__.Equals ["_1.txt"; "_2.txt"; "_3.txt"; "_5.txt"; "_4.txt"])
    };
(* Test the sort_all_files function. *)
    {
        part = "FileTreeViewHelpers"
        name = "sort_all_files"
        test = fun name ->
            let test_folder = getTestFolderName name
(* This FTN is the sort order. I.e. a_1, a_2, a_3. *)
            let sort_ftn_desc = ftnd_path3
            let sort_ftn = ftnd_to_ftn test_folder sort_ftn_desc
(* This FTN contains the files to be sorted. *)
            let ftn_to_sort = {
                dir = test_folder
                files = [for i in 5 .. -1 .. 1 -> sprintf "%s\\_%d.txt" test_folder i]
                dirs = []
                }
(* Sort the FTN according to the sort FTN. *)
            let sorted_ftn = sort_all_files ftn_to_sort sort_ftn
(* Make sure we still have 5 files. *)
            Assert.Equal (sorted_ftn.files.Length, 5)
(* Get the file names without the full path. *)
            let files = sorted_ftn.files |> List.map Path.GetFileName
(* Make sure the files are in the expected order. *)
            Assert.True (files.Equals ["_1.txt"; "_2.txt"; "_3.txt"; "_5.txt"; "_4.txt"])
    };
(* Test the sort_folders function. *)
    {
        part = "FileTreeViewHelpers"
        name = "sort_folders"
        test = fun name ->
            let test_folder = getTestFolderName name
(* This FTN is the sort order. I.e. a, b, c. *)
            let sort_ftn_desc = ftnd_path0_a0b0c0
            let sort_ftn = ftnd_to_ftn test_folder sort_ftn_desc
(* The set of folders to be sorted. *)
            let folders = ["e"; "d"; "c"; "b"; "a"] |> List.map (fun dir -> { name = dir; count = 0; dirs = [] } )
(* Turn each FTN description into an FTN, using currentdir\a as the directory that contains them. *)
            let folders_ = folders |> List.map (fun ftn_desc -> ftnd_to_ftn test_folder ftn_desc)
(* Sort the folders according to the sort order in the FTN. *)
            let folders__ = sort_folders test_folder folders_ sort_ftn
(* Make sure we still have 5 folders. *)
            Assert.Equal (folders__.Length, 5)
(* Make sure the folders are in the expected order. *)
            let sorted_folder_names = ["a"; "b"; "c"; "e"; "d"] |> List.map (fun dir -> sprintf "%s\\%s" test_folder dir) 
            do (folders__, sorted_folder_names) ||> compare_lists_with_compare (fun item1 item2 -> item1.dir = item2) |> Assert.True
    };
(* Test the sort_all_folders function. *)
    {
        part = "FileTreeViewHelpers"
        name = "sort_all_folders"
        test = fun name ->
            let test_folder = getTestFolderName name
(* This FTN is the sort order. I.e. b, c, d. *)
            let sort_ftn_desc = ftnd_path0_a0b0c0
            let sort_ftn = ftnd_to_ftn test_folder sort_ftn_desc
(* The set of folders to be sorted. *)
            let folders = ["e"; "d"; "c"; "b"; "a"] |> List.map (fun dir -> { name = dir; count = 0; dirs = [] } )
(* This FTN contains the folders to be sorted. *)
            let ftn_to_sort = {
                dir = test_folder;
                files = [];
(* Turn each FTN description into an FTN, using currentdir\a as the directory that contains them. *)
                dirs = folders |> List.map (fun ftn_desc -> ftnd_to_ftn test_folder ftn_desc)
            }
(* Sort the FTN according to the sort FTN. *)
            let sorted_ftn = sort_all_folders ftn_to_sort sort_ftn
(* Make sure we still have 5 folders. *)
            Assert.Equal (sorted_ftn.dirs.Length, 5)
(* Make sure the folders are in the expected order. *)
            let sorted_folder_names = ["a"; "b"; "c"; "e"; "d"] |> List.map (fun dir -> sprintf "%s\\%s" test_folder dir) 
            do (sorted_ftn.dirs, sorted_folder_names) ||> compare_lists_with_compare (fun item1 item2 -> item1.dir = item2) |> Assert.True
    };
//#endregion

(* FileTreeViewItem *)
//#region
(* Test the Drop event handler. *)
    {
        part = "FileTreeViewItem"
        name = "drop"
        test = fun name ->
            let test_folder = getTestFolderName name
(* Get a FileTreeView that we can use for testing. *)
            let ftn = ftnd_to_ftn test_folder ftnd_path0_a2b2
            let ftv = new FileTreeView (Some ftn)
(* Try some illegal drops.
1. Source = target.
2. Source item type <> target item type.
3. Source parent <> target parent.
Note that we can't drop anything on the root node, because (1) there's only one such node and that violates the rule that the source and target must be different, and (2) AllowDrop is false on the root node.
 *)
(* Get node a (folder) and node b (folder). *)
            let node_a = ftv.root_node.Items.[0] :?> FileTreeViewItem
            do Assert.True (node_a.Header.ToString () = "a")
            let node_b = ftv.root_node.Items.[1] :?> FileTreeViewItem
            do Assert.True (node_b.Header.ToString () = "b")
(* Get node_a_1 (file). *)
            let node_a_1 = node_a.Items.[0] :?> FileTreeViewItem
            do Assert.True (node_a_1.Header.ToString () = "a_1.txt")
(* Get node a_2 (file). *)
            let node_a_2 = node_a.Items.[1] :?> FileTreeViewItem
            do Assert.True (node_a_2.Header.ToString () = "a_2.txt")
(* Get node b_1 (file). *)
            let node_b_1 = node_b.Items.[0] :?> FileTreeViewItem
            do
                Assert.True (node_b_1.Header.ToString () = "b_1.txt")
(* TestDrop should not be called on a root node. The source and target are described by the DragEventArgs passed to the Drop handler, not by the this identifier. However, TestDrop exposes a private method called drophelper, which fires a private instance event called _drag_drop, which in the case of the root node will not have a handler added by FileTreeView. Other than that, TestDrop can be called on any non-root node, and its identity will not be used in determining the source and target nodes. *)
(* 1. Source = target. *)
                node_a_1.test_drop_helper node_a_1 node_a_1 |> Assert.False
(* 2. Source item type <> target item type. *)
                node_a_1.test_drop_helper node_a_1 ftv.root_node |> Assert.False
(* 3. Source parent <> target parent. *)
                node_a_1.test_drop_helper node_a_1 node_b_1 |> Assert.False
(* Make sure the tree is unchanged. *)
                compare_ftns ftn (ftv.get_ftn ()) |> Assert.True
(* Do some legal drops.
1. File onto file, same parent.
2. File onto file, different parent.
3. Folder onto folder, same parent.
4. Folder onto folder, different parent.
For now, we're not testing 2 and 4, because the source and target must have the same parent.
*)
(* This is to make sure the drag_drop event fires. *)
            let drag_drop_fired = ref false
            do
                node_a_1.drag_drop.Add <| fun _ -> do drag_drop_fired := true
(* 1. Drop a_2 onto a_1, inserting a_2 before a_1. *)
                node_a_1.test_drop_helper node_a_2 node_a_1 |> Assert.True
(* Verify that the nodes have changed places. *)
                Assert.True ((node_a.Items.[0] :?> FileTreeViewItem).Header.ToString () = "a_2.txt")
                Assert.True ((node_a.Items.[1] :?> FileTreeViewItem).Header.ToString () = "a_1.txt")
(* Verify that the drag_drop event fired. *)
                Assert.True !drag_drop_fired
(*
Note that if we restore tests 2 and 4, we'll have to make sure the changing of nodes in other tests doesn't affect them.
(* 2. Drop a_2 onto b_1, inserting a_2 before b_1. *)
                Assert.True (node_a_1.TestDropEvent node_a_2 node_b_1).IsSome
(* Verify that the nodes have changed places. *)
*)
(* 3. Drop folder b onto folder a, inserting b before a. *)
                node_a_1.test_drop_helper node_b node_a |> Assert.True
(* Verify that the nodes have changed places. *)
                Assert.True ((ftv.root_node.Items.[0] :?> FileTreeViewItem).Header.ToString () = "b")
                Assert.True ((ftv.root_node.Items.[1] :?> FileTreeViewItem).Header.ToString () = "a")
(* 4. For now, we're not testing this. *)
    };
(* Test the add function. *)
    {
        part = "FileTreeViewItem"
        name = "add_node"
        test = fun name ->
            let test_folder = getTestFolderName name
(* Create a root node. *)
            let root_node = new FileTreeViewItem (None, FileTreeViewItemType.Root, test_folder, Path.GetFileName test_folder)
(* Add a folder named "a". *)
            let folder_a = root_node.add_node FileTreeViewItemType.Directory (sprintf "%s\\a" test_folder)
(* Add a file named "a_1". *)
            let file_a_1 = folder_a.add_node FileTreeViewItemType.File (sprintf "%s\\a\\a_1.txt" test_folder)
(* Get an FTN of this tree and make sure it has the structure we expect. *)
            let ftv = new FileTreeView ()
(* This isn't the correct way to build an FTV, but we would need an FTN to do that. This works well enough to let us verify the structure. *)
            do ftv.Items.[0] <- root_node
            let ftn = ftv.get_ftn ()
(* Using name "" will cause the root FTN to have a dir field value equal to the root directory used to generate it. The file generated by this FTND (count = 1) will have name "a_1", matching what's in our tree. *)
            let ftn_expected_desc = { name = ""; count = 0; dirs = [ { name = "a"; count = 1; dirs = [] } ] }
            let ftn_expected = ftnd_to_ftn test_folder ftn_expected_desc
            do compare_ftns ftn ftn_expected |> Assert.True
    };

(* Test that each node type has the proper context menu and event handlers. *)
(* Unfortunately, I can't find a way to programmatically check that an event has a handler assigned. Reflection doesn't work because the event handler is added at runtime, and reflection is intended for types/method/etc that are known at compile time. *)
    {
        part = "FileTreeViewItem"
        name = "context_menus_event_handlers"
        test = fun name ->
(* Helper function. Verify that the ContextMenu for FileTreeViewItem (1) has the list of items (2). *)
            let check_context_menu (node : FileTreeViewItem) (headers : string list) =
(* ContextMenu.MenuItems is an ItemCollection that must be cast to a sequence. We don't compare the lengths of the two sequences; if they weren't equal, we would raise an exception anyway, so we let Seq.forall2 do it. *)
                (node.ContextMenu.Items |> Seq.cast<MenuItem>, headers) ||> Seq.forall2 (fun menu_item header ->
                    menu_item.Header.ToString () = header)
(* Helper function. Make sure the FileTreeViewItem (1) has the expected context menu items (2) and that clicking them fires the expected events (3). *)
            let check_event (node : FileTreeViewItem) menu_item_name (event : IEvent<_>) =
                let event_fired = ref false
(* Add a handler to each event that simply lets us know it fired. *)
                do event.Add <| fun _ -> event_fired := true
(* Try to find the context menu item. *)
                match node.ContextMenu.Items |> Seq.cast<MenuItem> |> Seq.tryFind (fun item -> item.Header.ToString () = menu_item_name) with
                | Some menu_item ->
(* Click the context menu item and make sure the event fires. *)
                    do menu_item.RaiseEvent (new Windows.RoutedEventArgs (MenuItem.ClickEvent))
                    !event_fired
                | None -> false
            let check_event2 (node : FileTreeViewItem) menu_item_names (events : IEvent<_> list) =
                (menu_item_names, events) ||> List.zip |> List.forall (fun (menu_item_name, event) -> check_event node menu_item_name event)
(* Set up a tree as it would typically be set up by the application. First, get an empty tree using the default constructor, which is what XAML does. *)
            let ftv = new FileTreeView ()
(* Check the empty node context menu. *)
            let empty_node = ftv.root_node
            let empty_node_menu_item_names = ["New Project"; "Open Project"; "Open Configuration"]
            do
                check_context_menu empty_node empty_node_menu_item_names |> Assert.True
(* Check the root node events. *)
                check_event2 empty_node empty_node_menu_item_names [empty_node.new_project; empty_node.open_project; empty_node.open_config] |> Assert.True
(* Now use FileTreeView.initialize, which is what happens when the user opens a project. *)
(* First, create a directory and add a folder and files. *)
            let test_folder = getTestFolderName name
            do
                ftnd_path0_a1 |> ftnd_to_ftn test_folder |> make_test_dirs_
(* Specify the file patterns allowed for the tree. *)
                ftv.file_filter_patterns <- default_file_patterns
(* Make a tree from the directory. *)
                ftv.initialize test_folder None |> Assert.True
            let root_node = ftv.root_node
(* Check the root node context menu. *)
            let root_node_menu_item_names = ["New Project"; "Open Project"; "Save Project"; "New File"; "New Folder"; "Open Configuration"]
            do
                check_context_menu root_node root_node_menu_item_names |> Assert.True
(* Check the root node events. *)
(* I can't put all the events into a single list, because they're of different types (i.e. IEvent<unit>, IEvent<string>, etc.) Generics don't help because F# internally typecasts based on the first item in the list, which means that later items don't fit. *)
                check_event root_node "New Project" root_node.new_project |> Assert.True
                check_event root_node "Open Project" root_node.open_project |> Assert.True
                check_event root_node "Save Project" root_node.save_project |> Assert.True
                check_event root_node "New File" root_node.new_file |> Assert.True
                check_event root_node "Open Configuration" root_node.open_config |> Assert.True
(* Check the folder node context menu. *)
            let folder_a_node = root_node.Items.[0] :?> FileTreeViewItem
            let folder_node_menu_item_names = ["New File"]
            do
                check_context_menu folder_a_node folder_node_menu_item_names |> Assert.True
(* Check the folder node events. *)
                check_event2 folder_a_node folder_node_menu_item_names [folder_a_node.new_file] |> Assert.True
(* Check the file node context menu. *)
            let file_a_1_node = folder_a_node.Items.[0] :?> FileTreeViewItem
            let file_node_menu_item_names = ["Open File in Left Pane"; "Open File in Right Pane"; "Rename File"]
            do
                check_context_menu file_a_1_node file_node_menu_item_names |> Assert.True
(* Check the file node events. *)
                check_event file_a_1_node "Open File in Left Pane" file_a_1_node.open_file |> Assert.True
                check_event file_a_1_node "Open File in Right Pane" file_a_1_node.open_file |> Assert.True
                check_event file_a_1_node "Rename File" file_a_1_node.rename_file |> Assert.True
    };
//#endregion

(* FileTreeView *)
//#region
(* For now, we've decided to stop saving the project automatically, and only save it in response to events like the tree or tab control changing. *)
(* Test that the save_project event fires due to the auto-save timer. *)
(*
    {
        part = "FileTreeView"
        name = "save_project"
        test = fun name ->
            let ftv = new FileTreeView ()
            let save_project_fired = ref false
            do
                ftv.save_project.Add (fun _ -> do save_project_fired := true)
(* Wait for the auto save. See EditorTest save_timer_tick for notes on waiting for a tick. *)
                sprintf "%s. Please wait 10 seconds before clicking OK." name |> System.Windows.MessageBox.Show |> ignore
(* Verify the save_project event has fired. *)
                Assert.True !save_project_fired
    };
*)
(* Test the get_children function. *)
    {
        part = "FileTreeView"
        name = "get_children"
        test = fun name ->
(* We don't create this folder, we just need it to get an FTN. *)
            let test_folder = getTestFolderName name
(* Create an FTN whose immediate children are one directory and one file. *)
            let ftn = ftnd_to_ftn test_folder ftnd_path1_a1b1
(* Create the tree from the FTN. *)
            let tree = new FileTreeView (Some ftn)
(* Get the children of the root node. *)
            let dirs, files = tree.test_get_children tree.root_node
(* Conver the directory and file FTVIs to their paths. *)
            let dirs_ = dirs |> List.map (fun dir -> dir.path)
            let files_ = files |> List.map (fun file -> file.path)
            do
                let compare = dirs_ = [(sprintf "%s\\a" test_folder)]
                Assert.True compare
                let compare = files_ = [(sprintf "%s\\_1.txt" test_folder)]
                Assert.True compare
    };
(* Test add_child_node. *)
    {
        part = "FileTreeView"
        name = "add_child_node"
        test = fun name ->
            let ftv = new FileTreeView ()
(* This is to make sure the tree_changed event fires. *)
            let tree_changed_fired = ref false
            do
                ftv.tree_changed.Add <| fun _ -> do tree_changed_fired := true
(* Add a file. *)
                ftv.add_child_node ftv.root_node FileTreeViewItemType.File "test" true |> ignore
(* Make sure the tree_changed event fired. *)
                Assert.True !tree_changed_fired
(* Try to add a duplicate file and make sure add_child returns None. *)
                (ftv.add_child_node ftv.root_node FileTreeViewItemType.File "test" true).IsNone |> Assert.True
(* If the node is not a duplicate, add_child simply calls FTVI.add_node, which is tested elsewhere. *)
    };
(* Test the get_file_list function. *)
    {
        part = "FileTreeView"
        name = "get_file_list"
        test = fun name ->
(* We don't create this folder, we just need it to get an FTN. *)
            let test_folder = getTestFolderName name
(* Create an FTN. *)
            let ftn = ftnd_to_ftn test_folder ftnd_path1_a1b1_parallel
(* Create the tree from the FTN. *)
            let tree = new FileTreeView (Some ftn)
(* Get the file list. *)
            let file_list = tree.get_file_list ()
(* Make sure the list has the expected contents. *)
            do
                Assert.True (file_list.Length = 3)
                Assert.True (file_list |> List.tryFind (fun file -> file = sprintf "%s\\_1.txt" test_folder)).IsSome
                Assert.True (file_list |> List.tryFind (fun file -> file = sprintf "%s\\a\\a_1.txt" test_folder)).IsSome
                Assert.True (file_list |> List.tryFind (fun file -> file = sprintf "%s\\b\\b_1.txt" test_folder)).IsSome
    };
(* Test the add_recent_project function. *)
    {
        part = "FileTreeView"
        name = "add_recent_project"
        test = fun name ->
(* We don't create this folder, we just need it to get an FTN. *)
            let test_folder = getTestFolderName name
(* Create a tree. *)
            let tree = new FileTreeView ()
(* Add the folder to the recent project list. *)
            do tree.add_recent_project test_folder
(* Verify that the project folder was saved to the recent project list. *)
            let recent_folders = tree.recent_projects
            do Assert.True (recent_folders.Length > 0)
            let most_recent_folder = recent_folders.Head
            do Assert.True (String.Compare (most_recent_folder, test_folder) = 0)
    };
(* Convert an FTND to an FTN, then to an FTV, then back to an FTN, then to a directory, then back to an FTN, and test that the final FTN is the same as the first one. This tests the following functions: dir_to_ftn, FileTreeView.ftn_to_ftv (in the constructor), FileTreeView.get_ftn. *)
    {
        part = "FileTreeView"
        name = "Conversions"
        test = fun name ->
            let test_folder = getTestFolderName name
            let ftn1 = ftnd_to_ftn test_folder ftnd_path0_a1
            let ftv1 = new FileTreeView (Some ftn1)
            let ftn2 = ftv1.get_ftn ()
            do make_test_dirs_ ftn2
            let ftn3 = dir_to_ftn test_folder
            do compare_ftns ftn1 ftn3.Value |> Assert.True
    };
(* Test the _drag_drop event via drag_drop_handler. Also test the tree_changed event. *)
    {
        part = "FileTreeView"
        name = "DragDropEvent"
        test = fun name ->
            let test_folder = getTestFolderName name
(* Get a FileTreeView that we can use for testing. *)
            let ftn = ftnd_to_ftn test_folder ftnd_path0_a2b2
            let ftv = new FileTreeView (Some ftn)
(* Get nodes a_1 and a_2. *)
            let node_a = ftv.root_node.Items.[0] :?> FileTreeViewItem
            let node_a_1 = node_a.Items.[0] :?> FileTreeViewItem
            let node_a_2 = node_a.Items.[1] :?> FileTreeViewItem
(* This is to make sure the drag_drop and tree_changed events fire. *)
            let drag_drop_fired = ref false
            let tree_changed_fired = ref false
            do
                ftv.drag_drop.Add <| fun _ -> do drag_drop_fired := true
                ftv.tree_changed.Add <| fun _ -> do tree_changed_fired := true
(* 1. Drop a_2 onto a_1, inserting a_2 before a_1. *)
                node_a_1.test_drop_helper node_a_2 node_a_1 |> Assert.True
(* Make sure the drag_drop and tree_changed events fired. *)
                Assert.True !drag_drop_fired
                Assert.True !tree_changed_fired
    };
    ]
//#endregion
