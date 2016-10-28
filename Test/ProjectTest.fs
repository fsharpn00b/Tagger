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

module ProjectTest

open System
// ConcurrentDictionary
open System.Collections.Concurrent
// Directory, File
open System.IO

open Xunit

// FileTreeView
open TaggerControls
// Project, ProjectController
open Project
open TestHelpers

(*
Log events to test:
OpenProjectInformation - open_project
x OpenProjectInformationError - open_project
x OpenProjectInformationNotFound - open_project
x NewProjectCreateDirectoryError - new_project
x SaveProjectInformation - save_project
x SaveProjectInformationError - save_project

Functions to test:
N get_project_directory (UI function)
x open_project
x new_project
x save_project (in open_project)
*)

(* Helper to get an empty project. *)
let get_empty_project path = {
    sort_order = { dir = path; dirs = []; files = [] }
    left_tabs = []
    right_tabs = []
    vertical_positions = null
    tag_number = 0
    move_to_mru = []
    tag_mru = []
    }

type ProjectTest () =
    interface ITestGroup with

(* Tests that log. *)
//#region
    member this.tests_log with get () = [
(* These tests should log an event. *)
    {
(* Try to read a missing project information file. *)
        part = "ProjectController"
        name = "OpenProjectInformationNotFound"
        test = fun name ->
            let path = getTestFolderName name
(* Don't actually create the folder or the file. *)
            let file_path = sprintf "%s\\project.ini" path
            do ProjectController.open_project file_path |> ignore
    };
    {
(* Try to read a corrupt project.ini file. *)
        part = "ProjectController"
        name = "OpenProjectInformationError"
        test = fun name ->
            let path = getTestFolderName name
            do
                Directory.CreateDirectory path |> ignore
                WriteToFile_ (sprintf "%s\\project.ini" path) "This project file is corrupt."
                ProjectController.open_project path |> ignore
    };
(* Try to create a new project in a directory with an invalid path. *)
    {
        part = "ProjectController"
        name = "NewProjectCreateDirectoryError"
        test = fun name ->
(* We use an invalid directory instead of GetTestFolderName because we don't actually create any folders. *)
            Assert.False <| ProjectController.new_project ":"
    };
(* Try to save over a read-only project.ini file. *)
    {
        part = "ProjectController"
        name = "SaveProjectInformationError"
        test = fun name ->
            let path = getTestFolderName name
            let file_path = sprintf "%s\\project.ini" path
(* Create the test directory. *)
            do Directory.CreateDirectory path |> ignore
(* Create the config file. *)
            let file = File.Create file_path
(* File.Create returns an open FileStream, which we close. *)
            do
                file.Close ()
(* Set the config file to read-only and try to save over it. *)
                File.SetAttributes (file_path, FileAttributes.ReadOnly)
(* ProjectController.save_project expects the sort order to be taken from the FileTreeView, so it expects the root directory of the sort order to be the root directory of the project, and that's where it tries to save or find project.ini. *)
            let project = get_empty_project path
            do
                ProjectController.save_project (new FileTreeView ()) project |> ignore
(* Remove the read-only attribute so the config file can be deleted. It doesn't have "deleteme" in its name, so it won't be caught by the CleanUp function. *)
                File.SetAttributes (file_path, FileAttributes.Normal)
    };
    {
(* Save a project. *)
        part = "ProjectController"
        name = "SaveProjectInformation"
        test = fun name ->
            let test_folder = getTestFolderName name
(* Create the folder, so we can save the project.ini file in it. *)
            do Directory.CreateDirectory test_folder |> ignore
(* ProjectController.save_project expects the sort order to be taken from the FileTreeView, so it expects the root directory of the sort order to be the root directory of the project, and that's where it tries to save or find project.ini. *)
            let project = get_empty_project test_folder
(* Save the project to disk. *)
            do ProjectController.save_project (new FileTreeView ()) project |> ignore
    };
    ]
//#endregion

    member this.tests_throw with get () = []

(* Tests that don't log. *)
//#region
    member this.tests_no_log with get () = [
    {
        part = "ProjectController"
        name = "open_project"
        test = fun name ->
            let test_folder = getTestFolderName name
(* Create the folder, so we can save the project.ini file in it. *)
            do Directory.CreateDirectory test_folder |> ignore
(* Create a project. *)
            let vertical_positions = new ConcurrentDictionary<string, float> ()
            do vertical_positions.TryAdd ("1", 1.0) |> ignore
            let project = {
                sort_order =
                    {
                        dir = test_folder;
                        dirs = [];
                        files = ["1"; "2"; "3"]
                    }
                left_tabs = ["1"; "2"; "3"]
                right_tabs = ["4"; "5"; "6"]
                vertical_positions = vertical_positions
                tag_number = 10
                move_to_mru = ["3"; "2"; "1"]
                tag_mru = ["3"; "2"; "1"]
                }
(* Save the project. *)
            let tree = new FileTreeView ()
            do ProjectController.save_project tree project |> ignore
(* Verify that the project folder was saved to the recent project list. *)
            let recent_folders = tree.recent_projects
            do Assert.True (recent_folders.Length > 0)
            let most_recent_folder = recent_folders.Head
            do Assert.True (String.Compare (most_recent_folder, test_folder) = 0)
(* Open the project. *)
            let project2 = ProjectController.open_project test_folder
            do Assert.True project2.IsSome
(* Verify that the project is the same as before we saved and opened it. *)
(* For some reason, we can't compare the vertical position dictionaries except item by item. *)
            let project2_ = project2.Value
            do
                Assert.True (project2_.sort_order = project.sort_order)
                Assert.True (project2_.left_tabs = project.left_tabs)
                Assert.True (project2_.right_tabs = project.right_tabs)
                Assert.True (project2_.tag_number = project.tag_number)
                Assert.True (project2_.move_to_mru = project.move_to_mru)
                Assert.True (project2_.tag_mru = project.tag_mru)
                (project.vertical_positions.ToArray (), project2_.vertical_positions.ToArray ()) ||> compare_arrays_with_compare (fun kv1 kv2 -> kv1.Equals kv2) |> Assert.True
    };
    {
        part = "ProjectController"
        name = "new_project"
        test = fun name ->
            let test_folder = getTestFolderName name
            Assert.True <| ProjectController.new_project test_folder
(* Verify the folder was created. *)
            Assert.True <| Directory.Exists test_folder
    };
    ]
//#endregion