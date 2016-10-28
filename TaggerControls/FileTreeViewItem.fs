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

(* Reference UIAutomationTypes needed to inherit TreeViewItem. *)

// Type
open System
// Path
open System.IO
// Thickness
open System.Windows
// ContextMenu, TreeViewItem
open System.Windows.Controls

open FileTreeViewHelpers
open Monads

type FileTreeViewItemType = File | Directory | Root | Empty
type LeftOrRightPane = LeftPane = 0 | RightPane = 1

///<summary>(1) The parent node of this node (None if this is the root node). (2) The type of the node. (3) The path of the file or folder represented by this node. (4) The name of the file represented by this node.</summary>
type FileTreeViewItem (parent_node : FileTreeViewItem option, item_type : FileTreeViewItemType, path, name) as this =
    inherit TreeViewItem ()

(* Member values. *)
//#region
(* The parent node of this node. *)
    let mutable _parent_node = parent_node
(* Whether this node represents a file or directory. *)
    let _item_type = item_type
(* The path of the file or directory represented by this node. *)
    let mutable _path = path
//#endregion

(* Events. *)
//#region
(* User selects New Project from the context menu of the root node or the empty node. *)
    let _new_project = new Event<unit> ()
(* User selects Open Project from the context menu of the root node or the empty node. *)
    let _open_project = new Event<unit> ()
(* User selects Save Project from the context menu of the root node. *)
    let _save_project = new Event<unit> ()
(* User selects Open File in Left Pane/Right Pane from the context menu of a file node. *)
    let _open_file = new Event<string * LeftOrRightPane> ()
(* User selects Rename File from the context menu of a file node. *)
    let _rename_file = new Event<FileTreeViewItem> ()
(* User selects New File from the context menu of a directory node or the root node. *)
    let _new_file = new Event<FileTreeViewItem> ()
(* User selects New Folder from the context menu of a directory node or the root node. *)
    let _new_folder = new Event<FileTreeViewItem> ()
(* User drops one node on another. *)
    let _drag_drop = new Event<string * string> ()
(* User opens the configuration dialog. *)
    let _open_config = new Event<unit> ()
//#endregion

(* General helper functions. *)
//#region
///<summary>Helper function for initializing context menus.</summary>
    let add_item (menu : ContextMenu) name handler =
        let i = new MenuItem ()
        do
            i.Header <- name
            i.Click.Add handler
(* The return value is the index at which the item is added. *)
            menu.Items.Add i |> ignore
//#endregion

(* MouseMove event handler. *)
//#region
(* We do not mark this event handled. *)
///<summary>Handler for MouseMove event. (1) Event args. Return unit.</summary>
    let mouse_move (args : Input.MouseEventArgs) =
(* Don't proceed until the mouse is moving and the left button is pressed. *)
        if Input.Mouse.PrimaryDevice.LeftButton <> Input.MouseButtonState.Pressed then ()
        else
(* Verify the source is a FileTreeViewItem. *)
            match args.Source with
            | :? FileTreeViewItem as item ->
(* The FileTreeViewItem is both the source and the data. *)
                do DragDrop.DoDragDrop (item, item, DragDropEffects.Move) |> ignore
            | _ -> ()
//#endregion

(* Drop event handler and helper functions. *)
//#region
(* Note: We no longer do this, but for some reason we can't assign the result of DragEventArgs.GetData to a value using monadic bind, even though we have an override of bind that checks for null. It causes an error later when downcasting the value to its derived type. *)
/// <summary>Helper for Drop event handler. (1) The source item. (2) The target item. Return true if the drop succeeds; otherwise, false.</summary>
    let drop_helper (source : FileTreeViewItem) (target : FileTreeViewItem) =
        MaybeMonad () {
(* Proceed only if the item being dropped is not the same as the target item. *)
            do! source.Equals target = false
(* Only allow files to be dropped on files, directories to be dropped on directories, and nothing to be dropped on the root node. *)
            do! source.item_type = target.item_type
(* The source and target items might have different parents. For some reason we have to specify the types of both parents. The parent item exposed by the parent property is an option, so the Maybe monad exits if either the source or target item has parent None (i.e., it is the root item). *)
            let! (source_parent : FileTreeViewItem) = source.parent_node
            let! (target_parent : FileTreeViewItem) = target.parent_node
(* For now, require that the source and target have the same parent. *)
            do! source.parent_node = target.parent_node
            do
(* An item must be removed before it can be inserted again (i.e. we can't insert it at the new index before removing it at the old one. *)
                source_parent.Items.Remove source
(* Insert the source item immediately before the target item. *)
                target_parent.Items.Insert (target_parent.Items.IndexOf target, source)
(* If the source and target parents differ, set the source parent to the target parent. *)
(* This is disabled for now, because dropping a file or folder from one folder to another opens up too many other issues. *)
//                source.parent_node <- target.parent_node
(* Since the drop succeeded, fire the _drag_drop event. I would prefer to put this in the drop event handler itself, but I can't call that event in testing and therefore I can't verify that the event was logged. *)
                _drag_drop.Trigger (source.path, target.path)
            return true
        } |> function | Some true -> true | _ -> false

(* We mark this event handled. *)
/// <summary>Handler for Drop event. (1) Event args. Return unit.</summary>
    let drop (args : DragEventArgs) =
(* Mark the event handled, otherwise this handler seems to be called twice, which causes an error when the source item changes parents. *)
        do args.Handled <- true
(* If we successfully validate the source and target, call the handler helper. *)
        match FileTreeViewItem.get_source_and_target<FileTreeViewItem> args with
        | Some (source, target) -> do drop_helper source target |> ignore
        | None -> do ()

//#endregion

(* Other event handlers. *)
//#region
(* We do not mark this event handled. *)
/// <summary>RightButtonMouseUp event handler. (1) Event args. Return unit.</summary>
    let right_button_mouse_up args =
(* Display the context menu. *)
(* Note: If we add a left-click handler, we must mark the event as handled, because otherwise it also fires for every higher node up to the root. This doesn't seem to happen with right-click though. Also, whatever the default left-click handler is for TreeViewItem, it doesn't seem to interfere with ours. *)
(* Note: If we do this:
do this.ContextMenu.IsOpen <- true
we get improper behavior: all nodes display the root context menu. The rule seems to be:
1. To display a menu that already exists, use Visibility.
2. To display a menu that we just created, use IsOpen.
*)
        do this.ContextMenu.Visibility <- Visibility.Visible
//#endregion

(* Node setup helper functions. *)
//#region
///<summary>Add event handlers that are appropriate to all node types.</summary>
    let setup () =
        do this.MouseRightButtonUp.Add right_button_mouse_up

///<summary>Add event handlers and context menu that are appropriate to a file or directory node.</summary>
    let setup_directory () =
///<summary>Create and initialize the context menu to be added to each directory node. Return the context menu.</summary>
        let setup_directory_context_menu () =
            let menu = new ContextMenu ()
(* Create a context menu item for each command, with a handler. The handler is closed over the directory path parameter.
Do not mark the event handled. *)
            do
                add_item menu "New File" (fun _ -> this |> _new_file.Trigger)
                add_item menu "New Folder" (fun _ -> this |> _new_folder.Trigger)
            menu
        do
(* MouseMove and Drop are for drag and drop. *)
            this.MouseMove.Add mouse_move
            this.Drop.Add drop
            this.ContextMenu <- setup_directory_context_menu ()
            setup ()

///<summary>Add event handlers and context menu that are appropriate to a file node.</summary>
    let setup_file () =
///<summary>Create and initialize the context menu to be added to each file node. Return the context menu.</summary>
        let setup_file_context_menu () =
            let menu = new ContextMenu ()
(* Create a context menu item for each command, with a handler. The handler is closed over the file path parameter.
Do not mark the event handled. *)
            do add_item menu "Open File in Left Pane" (fun _ -> _open_file.Trigger (_path, LeftOrRightPane.LeftPane))
            do add_item menu "Open File in Right Pane" (fun _ -> _open_file.Trigger (_path, LeftOrRightPane.RightPane))
            do add_item menu "Rename File" (fun _ -> this |> _rename_file.Trigger)
            menu
        do
(* MouseMove and Drop are for drag and drop. *)
            this.MouseMove.Add mouse_move
            this.Drop.Add drop
(* Allow double-click to open file. *)
            this.MouseDoubleClick.Add (fun _ -> _open_file.Trigger (_path, LeftOrRightPane.LeftPane))
            this.ContextMenu <- setup_file_context_menu ()
            setup ()

///<summary>Add event handlers and context menu that are appropriate to a root node.</summary>
    let setup_root () =
///<summary>Create and initialize the context menu to be added to the root node. Return the context menu.</summary>
        let setup_root_context_menu () =
            let menu = new ContextMenu ()
(* Create a context menu item for each command, with a handler.
Do not mark the events handled. *)
            do
                add_item menu "New Project" (fun _ -> _new_project.Trigger ())
                add_item menu "Open Project" (fun _ -> _open_project.Trigger ())
                add_item menu "Save Project" (fun _ -> _save_project.Trigger ())
                add_item menu "New File" (fun _ -> this |> _new_file.Trigger)
                add_item menu "New Folder" (fun _ -> this |> _new_folder.Trigger)
                add_item menu "Open Configuration" (fun _ -> _open_config.Trigger ())
            menu
        do
            this.ContextMenu <- setup_root_context_menu ()
            setup ()

///<summary>Add event handlers and context menu that are appropriate to an empty node.</summary>
    let setup_empty () =
///<summary>Create and initialize the content menu to be added to the empty ndoe. Return the context menu.</summary>
        let setup_empty_context_menu () =
            let menu = new ContextMenu ()
(* Create a context menu item for each command, with a handler.
Do not mark the events handled. *)
            do
                add_item menu "New Project" (fun _ -> _new_project.Trigger ())
                add_item menu "Open Project" (fun _ -> _open_project.Trigger ())
                add_item menu "Open Configuration" (fun _ -> _open_config.Trigger ())
            menu
        do
            this.ContextMenu <- setup_empty_context_menu ()
            setup ()
//#endregion

(* Constructor. *)
//#region
    do
(* If statements have to be within a do block, because only do and let are allowed in the constructor. *)
        if _item_type <> Root && _item_type <> Empty then do
(* If this isn't the root or empty node, reduce the indentation. *)
            this.Margin <- new Thickness (-15.0, 0.0, 0.0, 0.0)
(* This is not true by default. *)
(* Currently, we don't allow dragging items to different parents, so we don't allow dropping on the root or empty nodes. *)
            this.AllowDrop <- true
(* Set the header. *)
        this.Header <- name
        match _item_type with
(* Add the appropriate event handlers and context menu. *)
        | File -> do setup_file ()
        | Directory -> do setup_directory ()
        | Root -> do setup_root ()
        | Empty -> do setup_empty ()
//#endregion

(* Methods. *)
//#region
///<summary>Create a new node with type (1), path (2), and add it to this node. Return the node.</summary>
    member this.add_node item_type path =
(* Get the file name from the file path and use that to label the new node. *)
        let name = Path.GetFileName path
(* Pass this node as the parent of the new node. *)
        let node = new FileTreeViewItem (Some this, item_type, path, name)
(* Add the new node as a child of this node. *)
        do this.Items.Add node |> ignore
        node

(* To get this to compile, we had to specify that the return type is the same as the type parameter t1. Otherwise, the compiler decides that 'a is whatever return type is expected by the first caller of this function (i.e. FileTreeViewItem). *)
/// <summary>Verify that the source and target items in the drag event args (1) are not null and are of the expected type (t1). If so, return them; otherwise, return None.</summary>
    static member get_source_and_target<'a> (args : DragEventArgs) : ('a * 'a) option =
(* DragEventArgs.Source is actually the target, i.e. the item being dropped on. We put the source item in DragEventArgs.Data. If the target is empty, or not the expected type, or the source is empty, or not the expected type, then return None. *)
(* Previously, we used args.GetDataPresent to check the types, but pattern matching should work as well. *)
        if args.Source = null || args.Data = null then None
        else
(* GetData returns an obj, even though we specify the type, so we have to downcast it. *)
(* The source of the Drop event is the target item, i.e. the item being dropped on. *)
            match args.Data.GetData typeof<'a>, args.Source with
            | (:? 'a as source), (:? 'a as target) -> Some (source, target)
            | _ -> None

(* Expose the drop handler helper function for testing. *)
    member this.test_drop_helper = drop_helper

(* Fortunately, private seems to mean that this property can still be accessed by other instances of this class. We need that for dragging/dropping, but we don't want to expose it publicly. *)
    member private this.parent_node
        with get () : FileTreeViewItem option = _parent_node
        and set parent_node = do _parent_node <- parent_node
(* Expose the type as a property. We use this when deciding what event handlers to add in the FileTreeView. *)
    member this.item_type with get () = _item_type
(* Expose the path and name as properties. We use these when converting the FileTreeView to a map. *)
    member this.path
        with get () = _path
        and set path = do _path <- path
(* Currently, this isn't used. *)
//    member this.name with get () = name
//#endregion

(* Expose events. *)
    member this.new_project = _new_project.Publish
    member this.open_project = _open_project.Publish
    member this.save_project = _save_project.Publish
    member this.open_file = _open_file.Publish
    member this.new_file = _new_file.Publish
    member this.new_folder = _new_folder.Publish
    member this.rename_file = _rename_file.Publish
    member this.drag_drop = _drag_drop.Publish
    member this.open_config = _open_config.Publish