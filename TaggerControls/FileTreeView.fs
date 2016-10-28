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

(* Reference UIAutomationTypes needed to inherit TreeView. *)

// TimeSpan
open System
// Directory
open System.IO
// Regex
open System.Text.RegularExpressions
// TreeView
open System.Windows.Controls
// DispatcherTimer
open System.Windows.Threading

open FileTreeViewHelpers

type FileTreeView (root_ftn : FileTreeNode option) as this =
    inherit TreeView ()

(* Events. *)
//#region
(* The tree changes, for example due to _drag_drop or _new_file. *)
    let _tree_changed = new Event<unit> ()
(* See FileTreeViewItem for descriptions. We simply handle these events and re-fire them. *)
(* These events are handled by FileTreeViewController. *)
    let _open_file = new Event<string * LeftOrRightPane> ()
    let _rename_file = new Event<FileTreeViewItem> ()
    let _new_file = new Event<FileTreeViewItem> ()
    let _new_folder = new Event<FileTreeViewItem> ()
    let _new_project = new Event<unit> ()
    let _open_project = new Event<unit> ()
    let _save_project = new Event<unit> ()
    let _drag_drop = new Event<string * string> ()
    let _open_config = new Event<unit> ()
//#endregion

(* Member values. *)
//#region
(* For now, we've decided to stop saving the project automatically, and only save it in response to events like the tree or tab control changing. *)
(* DispatcherTimer lets us specify an event to happen at a specified interval.
http://msdn.microsoft.com/en-us/magazine/cc163328.aspx
We expose the interval as a property. *)
//    let _save_timer = new DispatcherTimer ()
(* Set the default interval to 10 seconds. *)
//    do _save_timer.Interval <- TimeSpan.FromSeconds 10.0

/// <summary>The root folder for the tree.</summary>
    let mutable _root_dir = None
/// <summary>The sort order to apply to the files in the tree.</summary>
    let mutable _sort_order = None
(* Only add files to the tree that match this pattern. *)
    let mutable _file_filter_patterns : string list = []
(* New files get this extension unless another is specified. *)
    let mutable _default_file_extension : string = ""
(* The most recently opened projects. *)
    let mutable _recent_projects : string list = []
//#endregion

(* Event handlers. *)
//#region
(* Handles the drag_drop event. *)
    let drag_drop_handler (source_path, target_path) =
        do
(* Fire the drag_drop event up to the FileTreeViewController. *)
            _drag_drop.Trigger (source_path, target_path)
(* Fire the tree_changed event as well. *)
            _tree_changed.Trigger ()
//#endregion

(* General helper functions. *)
//#region
///<summary>Add event handlers to the node (1). Return unit.</summary>
(* We simply re-fire the events, to be handled by FileTreeView. *)
    let add_event_handlers_to_node (node : FileTreeViewItem) =
        match node.item_type with
        | File -> do
            node.open_file.Add _open_file.Trigger
            node.rename_file.Add _rename_file.Trigger
            node.drag_drop.Add drag_drop_handler
        | Directory -> do
            node.new_file.Add _new_file.Trigger
            node.new_folder.Add _new_folder.Trigger
            node.drag_drop.Add drag_drop_handler
        | Root -> do
            node.new_project.Add _new_project.Trigger
            node.open_project.Add _open_project.Trigger
            node.save_project.Add _save_project.Trigger
            node.new_file.Add _new_file.Trigger
            node.new_folder.Add _new_folder.Trigger
            node.open_config.Add _open_config.Trigger
        | Empty -> do
            node.new_project.Add _new_project.Trigger
            node.open_project.Add _open_project.Trigger
            node.open_config.Add _open_config.Trigger

/// <summary>Return an empty FileTreeViewItem with event handlers added.</summary>
    let add_empty_node () =
        let ftvi = new FileTreeViewItem (None, FileTreeViewItemType.Empty, "", "")
        do
            ftvi.Header <- "No project loaded."
            ftvi |> add_event_handlers_to_node
        ftvi

// <summary>Return a root FileTreeViewItem with path (1) and with event handlers added.</summary>
    let add_root_node path =
        let ftvi = new FileTreeViewItem (None, FileTreeViewItemType.Root, path, Path.GetFileName path)
        do ftvi |> add_event_handlers_to_node
        ftvi

///<summary>Convert the FileTreeNode (1) to a FileViewTreeItem to be used as the root node of a FileTreeView. Return the FileTreeViewItem.</summary>
    let ftn_to_ftv (root_node : FileTreeNode) =
        let rec ftn_to_ftv_ (parent : FileTreeViewItem) (node : FileTreeNode) =
(* For each directory in the FTN... *)
            do node.dirs |> List.iter (fun dir_node ->
(* Create an ftvi and add it to the parent ftvi and add event handlers. *)
(* For some reason we have to specify this. *)
                let ftvi : FileTreeViewItem option = this.add_child_node parent FileTreeViewItemType.Directory dir_node.dir false
(* If the ftvi is not a duplicate, recurse into the directory (which is another FTN) to get all directories and files that we need to add as children to this ftvi. *)
                if ftvi.IsSome then do ftn_to_ftv_ ftvi.Value dir_node)
(* Create ftvis for the files in this FTN, add them to the parent ftvi, and add event handlers to them. *)
            do node.files |> List.iter (fun file -> do this.add_child_node parent FileTreeViewItemType.File file false |> ignore)
(* Create the root FileTreeViewItem. *)
        let ftvi = add_root_node root_node.dir
(* Add the child ftvis to the root ftvi. *)
        ftn_to_ftv_ ftvi root_node
(* Return the root ftvi. *)
        ftvi

///<summary>Return a list of directories and a list of files under FileTreeViewItem (1). This function does not recurse into the directories.</summary>
    let get_children (node : FileTreeViewItem) =
(* Split the FTVI's children into directories and files. *)
        let children = node.Items |> Seq.cast<FileTreeViewItem> |> Seq.groupBy (fun item -> item.item_type) |> Map.ofSeq
        let dirs = match children.TryFind (FileTreeViewItemType.Directory) with | Some dirs_ -> dirs_ |> Seq.toList | None -> []
        let files = match children.TryFind (FileTreeViewItemType.File) with | Some files_ -> files_ |> Seq.toList | None -> []
        dirs, files

///<summary>Create FileTreeViewItems and add them to the FileTreeView based on the FileTreeNode option (1). If (1) is None, create an empty root node. Return unit.</summary>
    let init_tree (root_ftn : FileTreeNode option) =
(* Clear the tree. *)
        do this.Items.Clear ()
(* The root node of the tree. *)
        let node =
            match root_ftn with
(* If we have a FileTreeNode, build the FileTreeView from it. *)
            | Some root_ftn -> ftn_to_ftv root_ftn
(* If not, create an empty node. *)
            | None -> add_empty_node ()
(* Add the node to the tree. *)
        do this.Items.Add node |> ignore

/// <summary>Sort the FileTreeNode (2) using the sort order in FTN (1). Return the sorted FTN.</summary>
    let get_sorted_ftn sort_order dir_ftn =
        match sort_order with
        | Some sort_order_ ->
(* We have to sort the files and folders separately. For details, see sort_folders. First, sort the files. *)
            let dir_ftn_ = sort_all_files dir_ftn sort_order_
(* Sort the folders and return the resulting FTN. *)
            sort_all_folders dir_ftn_ sort_order_
(* If the sort order is None, return the FTN unchanged. *)
        | None -> dir_ftn

/// <summary>Load the tree using root folder (1), file filter patterns (2), and sort order (3). Return true if the root folder (1) was found; otherwise, false.</summary>
    let load_tree_helper root_dir file_filter_patterns sort_order =
(* Verify the root folder is set. *)
        match root_dir with
        | Some root_dir ->
(* Get a FileTreeNode (i.e. representation of the tree we want to build) from the root folder. *)
            match dir_to_ftn root_dir with
            | Some dir_ftn ->
(* Filter the FTN, sort it, and populate the tree. *)
                do dir_ftn |> filter_ftn file_filter_patterns |> get_sorted_ftn sort_order |> Some |> init_tree
                true
(* If the root folder was not found, the tree will display the empty message. Return false. *)
            | None ->
                do init_tree None
                false
(* If the root folder is not set, the tree will display the empty message. Return false. *)
        | None ->
            do init_tree None
            false

//#endregion

(* Constructors. *)
//#region
    do
(* Add event handlers. *)
//        _save_timer.Tick.Add (fun _ -> do _save_project.Trigger ())
(* Start the auto-save timer. *)
//        _save_timer.Start ()
(* Initialize the tree. *)
        init_tree root_ftn

///<summary>Default constructor so we can instantiate this class in XAML.</summary>
    new () = FileTreeView (None)
//#endregion

(* Methods. *)
//#region
(*
/// <summary>The interval, in milliseconds, at which the project is saved.</summary>
    member this.save_timer_interval
        with get () = _save_timer.Interval.TotalMilliseconds
        and set value = do _save_timer.Interval <- TimeSpan.FromMilliseconds value
*)

(* This is currently not used. *)
(*
///<summary>Find node with path (1). Return the node if found; otherwise, None.</summary>
    member this.find_node path =
        let rec find_node_ (node : FileTreeViewItem) =
            if node.path = path then Some node else
                node.Items |> Seq.cast<FileTreeViewItem> |> Seq.tryPick (fun child -> find_node_ child)
        this.root_node |> find_node_
*)

///<summary>Create a FileTreeViewNode with parent (1), node type (2), and path (3). Add event handlers to the new node. If (4) is true, fire the tree_changed event. (4) is false when this function is called by the TreeView constructor. Return the new FTVI.</summary>
    member this.add_child_node parent item_type path fire_tree_changed =
(* See if the parent already contains a node with the same path. *)
        let duplicate_node = parent.Items |> Seq.cast<FileTreeViewItem> |> Seq.tryFind (fun item -> item.path = path)
(* If there is a duplicate node, return None. *)
        match duplicate_node with
        | Some ftvi -> None
(* Add the new node to the parent. Note that we're calling FTVI.add_node, not recursing into FTV.add_node. *)
        | None ->
            let ftvi = parent.add_node item_type path
(* Add the event handlers to the node. *)
            do ftvi |> add_event_handlers_to_node
(* Fire the tree_changed event if needed. *)
            if fire_tree_changed then do _tree_changed.Trigger () else do ()
(* Return the new node. *)
            ftvi |> Some

///<summary>Set the root directory to (1) and the sort order to (2). Load the FileTreeView. Return true if we succeed.</summary>
    member this.initialize root_dir sort_order =
        do
            _root_dir <- Some root_dir
            _sort_order <- sort_order
        this.load_tree ()

/// <summary>Load the FileTreeView. Return true if we succeed.</summary>
    member this.load_tree () = load_tree_helper _root_dir _file_filter_patterns _sort_order

///<summary>Convert this FileTreeView to a FileTreeNode. Return the FileTreeNode.</summary>
    member this.get_ftn () =
(* Get an FTN that represents this FileTreeViewItem. *)
        let rec get_ftn_ (node : FileTreeViewItem) =
(* Split the FTVI's children into directories and files. *)
            let dirs, files = get_children node
(* Return a new FileTreeNode. *)
            {
                dir = node.path
(* Get the path from each file. *)
                files = files |> List.map (fun file -> file.path )
(* Get an FTN for each directory. *)
                dirs = dirs |> List.map get_ftn_
            }
(* Get an FTN for the root node of the tree. *)
        get_ftn_ this.root_node

///<summary>Return a flat list of all files in the FileTreeView.</summary>
    member this.get_file_list () =
        let rec get_files (node : FileTreeViewItem) =
(* Split the FTVI's children into directories and files. *)
            let dirs, files = get_children node
(* Convert the list of file nodes to file paths, then append it to the current list. *)
            let file_list = files |> List.map (fun file -> file.path)
(* Recurse into each directory, get the list of files under it, and concatenate the resulting lists. *)
            let results = dirs |> List.map (fun dir -> get_files dir) |> List.concat
(* Return the list of files from this directory combined with the lists from subdirectories. *)
            file_list @ results
(* Start with the root node of the tree. *)
        get_files this.root_node

///<summary>Add the project in folder (1) to the list of recently opened projects.</summary>
    member this.add_recent_project value =
(* Helper function. If the list (1) contains item (2), remove the item. In either case, add the item as the head of the list. Return the updated list. *)
        let move_to_head list item =
(* If the list contains the item, remove it. *)
            let list_ = list |> List.filter (fun item_ -> item_ <> item)
(* Add the item as the head of the list. *)
            item :: list_
        do _recent_projects <- move_to_head _recent_projects value

(* Expose the root node. The FTV always has one and only one root node, even when no project is loaded. *)
    member this.root_node
        with get () =
(* For now, this fails gracefully, but it should return None or raise an exception instead. *)
            let handle_missing_root_node () =
                let node = add_empty_node ()
                do node |> this.Items.Add |> ignore
                node
            if this.Items.Count > 0 then
                match this.Items.[0] with
                | :? FileTreeViewItem as item -> item
                | _ -> handle_missing_root_node ()
            else handle_missing_root_node ()

/// <summary>Get and set the file filter patterns. Setting the file filter patterns reloads the FileTreeView.</summary>
    member this.file_filter_patterns
        with get () = _file_filter_patterns
        and set value =
            do
                _file_filter_patterns <- value
(* The return value from load_tree indicates whether the root folder was found. We have not changed the root folder, so we ignore the return value. *)
                this.load_tree () |> ignore

(* Expose the default file extension. *)
    member this.default_file_extension
        with get () = _default_file_extension
        and set value = do _default_file_extension <- value

(* Expose the most recent projects. *)
    member this.recent_projects
        with get () = _recent_projects
        and set value = do _recent_projects <- value
//#endregion

(* Expose methods for testing. *)

(* Expose events. *)
    member this.new_project = _new_project.Publish
    member this.open_project = _open_project.Publish
    member this.save_project = _save_project.Publish
    member this.new_file = _new_file.Publish
    member this.new_folder = _new_folder.Publish
    member this.open_file = _open_file.Publish
    member this.rename_file = _rename_file.Publish
    member this.drag_drop = _drag_drop.Publish
    member this.tree_changed = _tree_changed.Publish
    member this.open_config = _open_config.Publish

(* Expose the tree_changed event for testing. We do this because we can't test it directly as we can with, for example, open_project. That's because it's fired indirectly, as a result of other events, such as new_file. *)
    member this.TestTreeChangedEvent () = do _tree_changed.Trigger ()
(* Expose function for testing. *)
    member this.test_get_children = get_children
    member this.test_get_sorted_ftn = get_sorted_ftn
    member this.test_load_tree_helper = load_tree_helper
(* Expose the auto-save timer for testing. *)
//    member this.test_auto_save_timer = _save_timer