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

module Project

// ConcurrentDictionary
open System.Collections.Concurrent
// FileStream
open System.IO
// DataContractSerializer, DataContract, DataMember
open System.Runtime.Serialization
// DialogResult
open System.Windows.Forms

// LoggerWrapper
open LoggerWrapper
// FileTreeNode
open FileTreeViewHelpers
// FileTreeView
open TaggerControls

[<DataContract>]
type Project = {
(* Fields. *)
//#region
(* http://stackoverflow.com/questions/4034932/f-serialization-of-record-types
The compiler turns the field into a read-only property, so you have to apply the attribute to the field directly. *)
(* The order in which the files should be displayed in the FileTreeView. *)
    [<field: DataMember(Name="sort_order") >]
    sort_order : FileTreeNode;
(* The tabs that are currently open in the center pane. *)
    [<field: DataMember(Name="left_tabs") >]
    left_tabs : string list;
(* The tabs that are currently open in the right pane. *)
    [<field: DataMember(Name="right_tabs") >]
    right_tabs : string list;
(* The vertical position for each file in the project. *)
    [<field: DataMember(Name="vertical_positions") >]
    vertical_positions : ConcurrentDictionary<string, float>;
(* The next available tag number for the project. *)
    [<field: DataMember(Name="tag_number") >]
    tag_number : int;
(* The files most recently selected from the Move To context menu. *)
    [<field: DataMember(Name="move_to_mru") >]
    move_to_mru : string list;
(* The tags most recently selected from the Add Tag context menu. *)
    [<field: DataMember(Name="tag_mru") >]
    tag_mru : string list;
    }
//#endregion

(*
As files are deleted, the project data can become cluttered with entries for missing files in the FTV sort order, the Move To MRU, and the vertical position map. We handle this as follows.

FTV sort order is replaced with FTV.get_ftn in MC.save_project.
Move To MRU is merged with FTV.get_file_list in MC.save_project.
Vertical position map is matched with FTV.get_file_list in MC.save_project.
*)

///<summary>Provides methods to open and save project information.</summary>
type ProjectController () =

(* Methods. *)
//#region
///<summary>Ask the user to select a folder. Get the list of recent projects from the FileTreeView (1) and pre-select the first one in the folder selection dialog. Return the folder if the user clicks OK; otherwise, None.</summary>
    static member get_project_directory (tree : FileTreeView) =
(* http://bernholdtech.blogspot.com/2013/01/WPF-FolderBrowserDialog-and-DialogResult-OK-error.html
It's necessary to use the full path when creating this dialog. *)
        let dialog = new System.Windows.Forms.FolderBrowserDialog ()
(* Preset the dialog to the most recent project folder, if available. Also verify that the folder still exists. *)
        match tree.recent_projects with
        | hd :: tl when Directory.Exists hd -> do dialog.SelectedPath <- hd
        | _ -> ()
        let result = dialog.ShowDialog ()
        if result = DialogResult.OK then dialog.SelectedPath |> Some else None

///<summary>Read the project information from the file "project.ini" in folder (1). If successful, return the project information; otherwise return None.</summary>
    static member open_project dir =
        let project_file = sprintf "%s\\project.ini" dir
        if File.Exists project_file then
            let reader = new DataContractSerializer (typeof<Project>)
(* We define this here so we can call Dispose on it in the finally block. If we define it in the try block, the finally block can't see it. *)
            let mutable stream : FileStream = null
(* We can't use with and finally in the same try block. *)
            try
                try
                    do stream <- new FileStream (project_file, FileMode.Open)
(* Normally, we check the type before downcasting, but here we want to report an error because it means an error in the information file. *)
                    let project = reader.ReadObject (stream) :?> Project
                    do _logger.Log_ ("ProjectController.OpenProjectInformation", ["path", dir])
                    project |> Some
(* Calling Dispose on the FileStream closes it. *)
                finally if stream <> null then do stream.Dispose ()
            with | ex ->
                do _logger.Log_ ("ProjectController.OpenProjectInformationError", ["path", project_file; "message", ex.Message])
                None
(* This isn't an error. *)
        else
            do _logger.Log_ ("ProjectController.OpenProjectInformationNotFound", ["path", project_file])
            None

///<summary>Create the folder (1). If the folder exists, or we create it successfully, return true; if we fail to create it, return false.</summary>
    static member new_project dir =
(* If the directory exists, return true. Otherwise, try to create it. *)
        if Directory.Exists dir then true
        else
            try
(* If we successfully create the directory, return true. *)
                do Directory.CreateDirectory dir |> ignore
                true
(* If there is an error, return false. *)
            with | ex ->
                do _logger.Log_ ("ProjectController.NewProjectCreateDirectoryError", ["path", dir; "message", ex.Message])
                false

///<summary>Save the project information (2). Also add the project folder to the recent project list in the FileTreeView (1). Return true if we save the project successfully; otherwise, false.</summary>
    static member save_project (tree : FileTreeView) (project : Project) =
(* Get the project folder, which is the dir property of the root node of the tree. *)
        let dir = project.sort_order.dir
(* Add the project folder to the recent project list. *)
        do tree.add_recent_project dir
(* Save the project information in file "project.ini" in the project folder. *)
        let project_file = sprintf "%s\\project.ini" dir
        let writer = new DataContractSerializer (typeof<Project>)
(* See comments in open_project. *)
        let mutable stream : FileStream = null
        try
            try
(* Create the folder, if it does not already exist, so we can save the project.ini file in it. *)
                if Directory.Exists dir = false then do dir |> Directory.CreateDirectory |> ignore
                do
(* Use FileMode.Create to ensure the file is overwritten if it exists. *)
                    stream <- new FileStream (project_file, FileMode.Create)
                    writer.WriteObject (stream, project)
            finally
                if stream <> null then
                    do
(* Make sure the FileStream writes everything to disk. *)
                        stream.Flush ()
                        stream.Dispose ()
(* If an exception occurs in the inner try block above, execution will go to the finally block, then skip this section and go to the with statement below. *)
            do _logger.Log_ ("ProjectController.SaveProjectInformation", ["path", dir])
            true
        with | ex ->
            do _logger.Log_ ("ProjectController.SaveProjectInformationError", ["path", project_file; "message", ex.Message])
            false
//#endregion