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

module FileTreeViewController

(* Added references: System.Runtime.Serialization, System.Xml (DataContractSerializer.WriteObject), System.Windows.Forms, Microsoft.VisualBasic. *)

// InputBox
open Microsoft.VisualBasic
// Directory, File, FileStream
open System.IO
// DataContractSerializer
open System.Runtime.Serialization
// Regex
open System.Text.RegularExpressions

// LoggerWrapper
open LoggerWrapper
// MaybeMonad
open Monads
// FileTreeNode
open FileTreeViewHelpers
// FileTreeView
open TaggerControls

///<summary>Contains event handlers for the FileTreeView (1).</summary>
type FileTreeViewController (tree : FileTreeView) =

(* Events. *)
//#region
(* These events are re-fired to MainController, because they affect the TabControl or Editor. *)
    let _file_opened = new Event<string * LeftOrRightPane> ()
    let _file_renamed = new Event<string * string * string> ()
    let _project_created = new Event<unit> ()
    let _project_opened = new Event<unit> ()
    let _project_saved = new Event<unit> ()
(* Note: If we ever allow dragging and dropping between folders, we'll need to fire that up to MainController, because it means changing the path of a file, and the TabControl and Editor must know of it. *)
//#endregion

(* "new file" functions. *)
//#region
/// <summary>Helper function to convert a comma delimited input string (1) to a list of files or folders. Return the list, or None if the string is empty or all items are empty.</summary>
    let convert_input_string (value : string) =
(* Remove leading/trailing whitespace. *)
        let value_ = value.Trim ()
        if value_.Length > 0 then
            let results =
(* Note that splitting a string that does not contain the delimiter will simply return a list of one item, which is the original string. *)
                value_.Split ','
                |> Array.toList
(* Remove leading/trailing whitespace and filter out any empty strings that result. *)
                |> List.map (fun item -> item.Trim ())
                |> List.filter (fun item -> item.Length > 0)
            if results.Length > 0 then results |> Some else None
        else None

/// <summary>Ask the user for the names of new files. Return the file names if the user enters them; otherwise, None.</summary>
    let get_new_file () =
(* InputBox is a function. *)
        Microsoft.VisualBasic.Interaction.InputBox ("Please enter the new file name, or multiple file names separated by commas, or no name to cancel.", "New File") |> convert_input_string

/// <summary>Add the default file extension to file name (1) if it does not have a file extension. Return the new file name.</summary>
    let add_default_extension file =
(* If there is no default file extension specified, leave the file path unchanged. *)
        let extension = tree.default_file_extension
        if extension.Length = 0 then file
(* If the file name already has an extension, leave the file path unchanged. *)
        else if Regex.IsMatch (file, "\.\w+$") then file
(* Otherwise, add the default extension to the file path. *)
        else sprintf "%s%s" file extension

/// <summary>Create a new file with name (2) to be added as a node to the FileTreeView with (1) as parent. Return unit.</summary>
    let new_file_helper (parent : FileTreeViewItem) file =
        MaybeMonad () {
(* If the directory exists, proceed. Otherwise, try to create it. *)
            do! if Directory.Exists parent.path then true else
                try
(* If we successfully create the directory, proceed. Otherwise, stop. *)
                    do Directory.CreateDirectory parent.path |> ignore
                    true
                with | ex ->
                    do _logger.Log_ ("FileTreeViewController.NewFileCreateDirectoryError", ["path", parent.path; "message", ex.Message])
                    false
(* Add the default file extension to the file name if it does not have a file extension. Append the file name to the path of the parent node, which is a folder. *)
            let file_path = file |> add_default_extension |> sprintf "%s\\%s" parent.path
            do!
                try
(* If we successfully create the file, proceed. Otherwise, stop. *)
                    let file_stream = File.Create file_path
                    do file_stream.Close ()
                    true
                with | ex ->
                    do _logger.Log_ ("FileTreeViewController.NewFileCreateFileError", ["path", file_path; "message", ex.Message])
                    false
(* Add a node to the FileTreeView for the file. *)
            do tree.add_child_node parent FileTreeViewItemType.File file_path true |> ignore
            return true
        } |> ignore

/// <summary>Create new files to be added as nodes to the FileTreeView with (1) as parent. Return unit.</summary>
    let new_file_handler (parent : FileTreeViewItem) =
(* Ask the user for the file names. If the user clicks OK, proceed. Otherwise, stop. *)
        match get_new_file () with
(* For each file, call the new file helper. *)
        | Some files -> do files |> List.iter (fun file -> new_file_helper parent file)
        | None -> ()
//#endregion

(* "new folder" functions. *)
//#region
/// <summary>Ask the user for the names of new folders. Return the folder names if the user enters them; otherwise, None.</summary>
    let get_new_folder () =
(* InputBox is a function. *)
        Microsoft.VisualBasic.Interaction.InputBox ("Please enter the new folder name, or multiple folder names separated by commas, or no name to cancel.", "New File") |> convert_input_string

/// <summary>Create a new folder with name (2) to be added as a node to the FileTreeView with (1) as parent. Return unit.</summary>
    let new_folder_helper (parent : FileTreeViewItem) folder =
        let folder_path = sprintf "%s\\%s" parent.path folder
        try
            do
(* If we successfully create the folder, add a node to the FileTreeView for the folder. *)
                Directory.CreateDirectory folder_path |> ignore
                tree.add_child_node parent FileTreeViewItemType.Directory folder_path true |> ignore
        with | ex ->
            do _logger.Log_ ("FileTreeViewController.NewFolderCreateDirectoryError", ["path", folder_path; "message", ex.Message])

/// <summary>Create new folders to be added as nodes to the FileTreeView with (1) as parent. Return unit.</summary>
    let new_folder_handler (parent : FileTreeViewItem) =
(* Ask the user for the folder names. If the user clicks OK, proceed. Otherwise, stop. *)
        match get_new_folder () with
(* For each folder, call the new folder helper. *)
        | Some folders -> do folders |> List.iter (fun folder -> new_folder_helper parent folder)
        | None -> ()
//#endregion

(* "rename file" functions. *)
//#region
/// <summary>Ask the user for name to rename a file. Return the name if the user enters one; otherwise, None.</summary>
    let get_rename_file_name () =
(* InputBox is a function. *)
        let result = Microsoft.VisualBasic.Interaction.InputBox ("Please enter the new file name, or enter no name to cancel.", "Rename File")
        if result.Length > 0 then Some result
        else None

/// <summary>Rename the file represented by FileTreeViewItem (1) to (2).</summary>
    let rename_file_helper (node : FileTreeViewItem) name =
(* Add the default file extension to the file name if it does not have a file extension. *)
        let new_name = add_default_extension name
(* If we successfully rename the file, proceed. Otherwise, stop. *)
        let result =
            try
(* The new name for the file should simply be the name, not a relative or absolute path. *)
                do Microsoft.VisualBasic.FileIO.FileSystem.RenameFile (node.path, new_name)
                true
            with | ex ->
                if ex :? IOException then
                    do _logger.Log_ ("FileTreeViewController.RenameFileIOError", ["path", node.path; "name", new_name; "message", ex.Message])
                else
                    do _logger.Log_ ("FileTreeViewController.RenameFileError", ["path", node.path; "name", new_name; "message", ex.Message])
                false
(* If the file rename succeeded... *)
        if result then
(* Get the old path. *)
            let old_path = node.path
(* Get the folder that contains the file. Unlike new_file_helper, the path property of the FileTreeViewItem we are given contains the path of the file itself rather than the parent folder. *)
            let folder = node.path |> Path.GetDirectoryName
(* Append the new file name to the folder. *)
            let new_path = sprintf "%s\\%s" folder new_name
            do
(* Set the FileTreeViewItem path to the new file path. *)
                node.path <- new_path
                node.Header <- new_name
(* Notify MainController. *)
                _file_renamed.Trigger (old_path, new_path, new_name)

/// <summary>Rename the file represented by FileTreeViewItem (1). Return unit.</summary>
    let rename_file_handler (node : FileTreeViewItem) =
(* Ask the user for the name file name. If the user enters one, proceed. Otherwise, stop. *)
        match get_rename_file_name () with
        | Some name -> rename_file_helper node name
        | None -> ()
//#endregion

(* Event handlers. *)
//#region
/// <summary>Log the drag and drop event with source path (1) and target path (2). Return unit.</summary>
    let drag_drop_handler source_path target_path =
        do _logger.Log_ ("FileTreeViewController.DragDrop", ["source_path", source_path; "target_path", target_path])
//#endregion

(* Constructor. *)
//#region
(* Add event handlers. *)
    do
(* We don't actually do anything with these events, just re-fire them up to MainController. MainController then calls methods in this class if there is additional work to be done. *)
        tree.open_file.Add _file_opened.Trigger
        tree.open_project.Add _project_opened.Trigger
        tree.save_project.Add _project_saved.Trigger
        tree.new_project.Add _project_created.Trigger
(* If the tree has changed, save the project. *)
        tree.tree_changed.Add _project_saved.Trigger
(* These events can be handled here. MainController might be informed so it can change the editor or tab list, but it won't have to call any methods in this class. *)
        tree.new_file.Add new_file_handler
        tree.new_folder.Add new_folder_handler
        tree.rename_file.Add (fun node -> rename_file_handler node)
        tree.drag_drop.Add (fun (source_path, target_path) -> drag_drop_handler source_path target_path)
//#endregion

(* Methods. *)
//#region
///<summary>Read the project in folder (1) and update the FileTreeView using the sort order (2). Only read files with the file extensions specified by the appropriate FileTreeView property. If there was no error, return true; otherwise, false.</summary>
    member this.open_project dir sort_order =
(* Update the tree. Check whether the folder is found. *)
        if tree.initialize dir sort_order = false then
(* Not finding the folder isn't necessarily an error, but we want to let the user know. *)
            do _logger.Log_ ("FileTreeViewController.OpenProjectFolderNotFound", ["path", dir])
            true
        else
            do _logger.Log_ ("FileTreeViewController.OpenProject", ["path", dir])
            true
//#endregion

(* Expose functions for testing. *)
    member this.test_convert_input_string = convert_input_string
    member this.test_add_default_extension = add_default_extension
    member this.test_new_file_helper = new_file_helper
    member this.test_new_folder_helper = new_folder_helper
    member this.test_rename_file_helper = rename_file_helper

(* Expose events. *)
    member this.project_created = _project_created.Publish
    member this.project_opened = _project_opened.Publish
    member this.project_saved = _project_saved.Publish
    member this.file_opened = _file_opened.Publish
    member this.file_renamed = _file_renamed.Publish
