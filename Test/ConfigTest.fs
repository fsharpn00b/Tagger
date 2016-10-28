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

module ConfigTest

// Directory
open System.IO
// IPAddress
open System.Net

open Xunit

// FileTreeView, TaggerGrid, TaggerTextEditor
open TaggerControls
// AddOnServer
open AddOnServer
// TaggerConfig
open Config
open TestHelpers

(*
Errors:
x LoadConfigurationError
x SaveConfigurationError

Events:
x LoadConfiguration
x SaveConfiguration

Functions:
x load_default_settings (in load_config_helper)
x save_config_helper
x load_config_helper (in load_config_helper and save_config_helper)
x apply_settings_to_controls
x verify_config_dialog
x apply_config_dialog_to_settings
N ok_button_click_handler (UI function)
x TryFind (in apply_config_dialog_to_settings)
x Update (in apply_settings_to_controls)
N save_config (just calls Update and save_config_helper)
N load_config (just calls load_config_helper and apply_settings_to_controls)
N show_settings_dialog (UI function)
*)

(* Helper functions. *)
//#region
/// <summary>Helper function. Compare two settings dictionaries (1) and (2). Return unit.</summary>
let compare_settings (settings1 : ConfigSettings) (settings2 : ConfigSettings) =
(* For some reason, ConcurrentDictionary.ToArray () seems to yield elements in inconsistent order. *)
    let list1 = settings1.ToArray () |> Array.map (fun kv -> kv.Key, kv.Value) |> Array.toList |> List.sort
    let list2 = settings2.ToArray () |> Array.map (fun kv -> kv.Key, kv.Value) |> Array.toList |> List.sort
(* List.forall2 raises an exception if the list lengths aren't equal, so we check that first. *)
    Assert.Equal (list1.Length, list2.Length)
(* Split each list item into a key/value tuple. Make sure the first key matches the second. The values are lists themselves, so go through those and again make sure that each item is the same in both lists. *)
    (list1, list2) ||> List.forall2 (fun (k1, v1) (k2, v2) -> k1 = k2 && compare_lists v1 v2) |> Assert.True

/// <summary>Helper function that returns a new TaggerConfig.</summary>
let get_config (grid : TaggerGrid option) (tree : FileTreeView option) (left_editor : TaggerTextEditor option) (right_editor : TaggerTextEditor option) (server : AddOnServer option) =
    let grid_ =
        match grid with
        | Some grid__ -> grid__
        | None -> new TaggerGrid ()
    let tree_ =
        match tree with
        | Some tree__ -> tree__
        | None -> new FileTreeView ()
    let left_editor_ =
        match left_editor with
        | Some editor__ -> editor__
        | None -> new TaggerTextEditor ()
    let right_editor_ =
        match right_editor with
        | Some editor__ -> editor__
        | None -> new TaggerTextEditor ()
    let server_ =
        match server with
        | Some server__ -> server__
        | None -> new AddOnServer ()
    new TaggerConfig (grid_, tree_, left_editor_, right_editor_, server_)

/// <summary>Shortcut for get_config with no controls provided by the caller.</summary>
let get_default_config () = get_config None None None None None
//#endregion

type ConfigTest () =
    interface ITestGroup with

(* Tests that log. *)
//#region
    member this.tests_log with get () = [
(* These events are non-critical errors. *)
    {
(* Attempt to load an unreadable config file. *)
        part = "Configuration"
        name = "LoadConfigurationError"
        test = fun name ->
            let config = get_default_config ()
(* TaggerConfig looks for a file named config.ini, so we can't give it a name that has "deleteme" in it. For that reason, we put the file inside a folder that has "deleteme" in the name. That ensures it will be deleted by the CleanUp function. *)
            let path = getTestFolderName name
            do
(* Create the test directory. *)
                Directory.CreateDirectory path |> ignore
(* TaggerConfig looks for the config.ini file in the current directory, so we set it to be the test folder. *)
                Directory.SetCurrentDirectory path
(* Create an unreadable config file and try to load it. *)
                WriteToFile_ (sprintf "%s\\config.ini" path) "This configuration file is corrupt."
                config.test_load_config_helper ()
(* Restore the current directory. *)
                Directory.SetCurrentDirectory current_dir
    };
    {
(* Attempt to save over a read-only config file. *)
        part = "Configuration"
        name = "SaveConfigurationError"
        test = fun name ->
            let config = get_default_config ()
(* TaggerConfig looks for a file named config.ini, so we can't give it a name that has "deleteme" in it. For that reason, we put the file inside a folder that has "deleteme" in the name. That ensures it will be deleted by the CleanUp function. *)
            let path = getTestFolderName name
            let file_path = sprintf "%s\\config.ini" path
(* Create the test directory. *)
            do Directory.CreateDirectory path |> ignore
(* Create the config file. *)
            let file = File.Create file_path
            do
(* File.Create returns an open FileStream, which we close. *)
                file.Close ()
(* TaggerConfig looks for the config.ini file in the current directory, so we set it to be the test folder. *)
                Directory.SetCurrentDirectory path
(* Set the config file to read-only and try to save over it. *)
                File.SetAttributes (file_path, FileAttributes.ReadOnly)
                config.test_save_config_helper ()
(* Remove the read-only attribute so the config file can be deleted. It doesn't have "deleteme" in its name, so it won't be caught by the CleanUp function. *)
                File.SetAttributes (file_path, FileAttributes.Normal)
(* Restore the current directory. *)
                Directory.SetCurrentDirectory current_dir
    };
(* These events are informational. *)
    {
(* Load a config file. *)
        part = "Configuration"
        name = "LoadConfiguration"
        test = fun name ->
            let config = get_default_config ()
(* TaggerConfig looks for a file named config.ini, so we can't give it a name that has "deleteme" in it. For that reason, we put the file inside a folder that has "deleteme" in the name. That ensures it will be deleted by the CleanUp function. *)
            let path = getTestFolderName name
            do
(* Create the test directory. *)
                Directory.CreateDirectory path |> ignore
(* TaggerConfig looks for the config.ini file in the current directory, so we set it to be the test folder. *)
                Directory.SetCurrentDirectory path
(* Save the config file, then load it. *)
                config.test_save_config_helper ()
                config.test_load_config_helper ()
(* Restore the current directory. *)
                Directory.SetCurrentDirectory current_dir
    };
    {
(* Save a config file. *)
        part = "Configuration"
        name = "SaveConfiguration"
        test = fun name ->
            let config = get_default_config ()
(* TaggerConfig looks for a file named config.ini, so we can't give it a name that has "deleteme" in it. For that reason, we put the file inside a folder that has "deleteme" in the name. That ensures it will be deleted by the CleanUp function. *)
            let path = getTestFolderName name
            do
(* Create the test directory. *)
                Directory.CreateDirectory path |> ignore
(* TaggerConfig looks for the config.ini file in the current directory, so we set it to be the test folder. *)
                Directory.SetCurrentDirectory path
(* Save the config file. *)
                config.test_save_config_helper ()
(* Restore the current directory. *)
                Directory.SetCurrentDirectory current_dir
    };
    ]
//#endregion

    member this.tests_throw with get () = [
    ]

(* Tests that don't log. *)
//#region
    member this.tests_no_log with get () = [
    {
(* Try to load a nonexistent config file. *)
        part = "Configuration"
        name = "load_config_helper"
        test = fun name ->
            let path = getTestFolderName name
            do
(* Create the test directory. *)
                Directory.CreateDirectory path |> ignore
(* TaggerConfig looks for the config.ini file in the current directory, so we set it to be the test folder. This ensures that it won't accidentally find a config.ini file left by another test. We also need to create both the configs with the test folder as the current folder, because the constructor sets the default ProjectBackupFolder as the current folder. *)
                Directory.SetCurrentDirectory path
            let config_default = get_default_config ()
            let config_missing_settings = get_default_config ()
            do
(* Try to load the missing config file. *)
                config_missing_settings.test_load_config_helper ()
(* Restore the current directory. *)
                Directory.SetCurrentDirectory current_dir
(* Compare the settings of the unused configuration to those of the one that tried to load the config file. *)
                compare_settings config_default.test_settings config_missing_settings.test_settings
    };
    {
(* Save a config file with altered settings, load it, and make sure the settings were preserved. *)
        part = "Configuration"
        name = "save_config_helper"
        test = fun name ->
            let config = get_default_config ()
(* Add a test key/value to the settings to differentiate them from the default settings. *)
            do config.Update "MouseHoverDelay" ["1234"]
(* TaggerConfig looks for a file named config.ini, so we can't give it a name that has "deleteme" in it. For that reason, we put the file inside a folder that has "deleteme" in the name. That ensures it will be deleted by the CleanUp function. *)
            let path = getTestFolderName name
            do
(* Create the test directory. *)
                Directory.CreateDirectory path |> ignore
(* TaggerConfig looks for the config.ini file in the current directory, so we set it to be the test folder. *)
                Directory.SetCurrentDirectory path
(* Save the config file. *)
                config.test_save_config_helper ()
(* Get a new TaggerConfig object, to make sure we have the default settings. *)
            let config_ = get_default_config ()
(* Load the config file. *)
            do
                config_.test_load_config_helper ()
(* Verify that the settings were preserved. *)
                compare_settings config.test_settings config_.test_settings
(* Restore the current directory. *)
                Directory.SetCurrentDirectory current_dir
    };
    {
        part = "Configuration"
        name = "apply_settings_to_controls"
        test = fun name ->
            let grid = new TaggerGrid ()
            let tree = new FileTreeView ()
            let left_editor = new TaggerTextEditor ()
            let right_editor = new TaggerTextEditor ()
            let server = new AddOnServer ()
            let config = get_config (Some grid) (Some tree) (Some left_editor) (Some right_editor) (Some server)
            do
(* We don't apply ProjectBackupFolder to a control. *)
(* Update properties to non-default values. *)
(* Update grid property. *)
                config.Update "RightSidebarWidth" ["1.0"]
(* Update tree properties. *)
(* This setting is currently not used. *)
//                config.Update "ProjectSaveDelay" ["1.0"]
                config.Update "DefaultFileExtension" [".abc"]
(* These values are not validated here, so we don't have to use valid patterns or paths. *)
                config.Update "FileFilterPatterns" ["a"; "b"]
                config.Update "RecentProjectFolders" ["x"; "y"]
(* Update editor properties. *)
(* This setting is currently not used. *)
//                config.Update "MouseHoverDelay" ["1"]
                config.Update "MouseScrollSpeed" ["10"]
                config.Update "DragScrollSpeed" ["10"]
                config.Update "FontSize" ["1"]
                config.Update "FileSaveDelay" ["1"]
                config.Update "TagSymbol" ["&"]
(* Update TagController properties (which are stored in the TaggerConfig). *)
                config.Update "MarginHighlightDisplayTime" ["1000"]
(* Update AddOnServer properties. *)
                config.Update "AddOnServerHost" ["127.0.0.2"]
                config.Update "AddOnServerPort" ["1"]
(* Apply the settings to the controls. *)
                config.test_apply_settings_to_controls ()
(* Verify the new settings. *)
                Assert.True (grid.RightSidebarWidth = 1.0)
(* For now, we've decided to stop saving the project automatically, and only save it in response to events like the tree or tab control changing. *)
//                Assert.True (tree.save_timer_interval = 1.0)
                Assert.True (tree.file_filter_patterns = ["a"; "b"])
                Assert.True (tree.default_file_extension = ".abc")
                Assert.True (tree.recent_projects = ["x"; "y"])
(* This setting is currently not used. *)
//                Assert.True (left_editor.mouse_hover_delay = 1)
//                Assert.True (right_editor.mouse_hover_delay = 1)
                Assert.True (left_editor.mouse_scroll_speed = 10)
                Assert.True (right_editor.mouse_scroll_speed = 10)
                left_editor.drag_scroll_speed = 10 |> Assert.True
                right_editor.drag_scroll_speed = 10 |> Assert.True
                left_editor.FontSize = 1.0 |> Assert.True
                right_editor.FontSize = 1.0 |> Assert.True
                Assert.True (left_editor.save_timer_interval = 1.0)
                Assert.True (right_editor.save_timer_interval = 1.0)
                Assert.True (TagInfo.tag_symbol = "&")
                config.margin_highlight_display_time = 1000 |> Assert.True
                server.host = IPAddress.Parse "127.0.0.2" |> Assert.True
                server.port = 1 |> Assert.True
    };
    {
        part = "Configuration"
        name = "verify_config_dialog"
        test = fun name ->
(* Helper function to make sure that invalid input fails verification. (1) is the return value from verify_config_dialog. (2) is the expected error message. *)
            let verify_failure result expected_error =
                let success = fst result
                let error = (snd result).ToString ()
                (success = false) && error.Contains expected_error
(* Create a TaggerConfig to get access to the verify_config_dialog method. *)
            let config = get_default_config ()
            let dialog = new ConfigWindow ()
            do
(* This setting is currently not used. *)
(*
(* Try to set a non-numeric MouseHoverDelay. *)
                dialog.MouseHoverDelay.Text <- "a"
                verify_failure (config.test_verify_config_dialog dialog) "Mouse Hover Delay" |> Assert.True
(* Try to set a MouseHoverDelay < 1000. *)
                dialog.MouseHoverDelay.Text <- "999"
                verify_failure (config.test_verify_config_dialog dialog) "Mouse Hover Delay" |> Assert.True
*)
(* Try to set a non-numeric MouseScrollSpeed. *)
                dialog.MouseScrollSpeed.Text <- "a"
                verify_failure (config.test_verify_config_dialog dialog) "Mouse Scroll Speed" |> Assert.True
(* Try to set a MouseScrollSpeed < 1. *)
                dialog.MouseScrollSpeed.Text <- "0"
                verify_failure (config.test_verify_config_dialog dialog) "Mouse Scroll Speed" |> Assert.True
(* Try to set a non-numeric DragScrollSpeed. *)
                dialog.DragScrollSpeed.Text <- "a"
                verify_failure (config.test_verify_config_dialog dialog) "Drag Scroll Speed" |> Assert.True
(* Try to set a DragScrollSpeed < 1. *)
                dialog.DragScrollSpeed.Text <- "0"
                verify_failure (config.test_verify_config_dialog dialog) "Drag Scroll Speed" |> Assert.True
(* Try to set a non-numeric MarginHighlightDisplayTime. *)
                dialog.MarginHighlightDisplayTime.Text <- "a"
                verify_failure (config.test_verify_config_dialog dialog) "Margin Highlight Display Time" |> Assert.True
(* Try to set a MarginHighlightDisplayTime < 1000. *)
                dialog.MarginHighlightDisplayTime.Text <- "0"
                verify_failure (config.test_verify_config_dialog dialog) "Margin Highlight Display Time" |> Assert.True
(* Try to set a non-numeric FontSize. *)
                dialog.FontSize.Text <- "a"
                verify_failure (config.test_verify_config_dialog dialog) "Font Size" |> Assert.True
(* Try to set a FontSize < 1. *)
                dialog.FontSize.Text <- "0"
                verify_failure (config.test_verify_config_dialog dialog) "Font Size" |> Assert.True
(* Try to set a non-numeric FileSaveDelay. *)
                dialog.FileSaveDelay.Text <- "a"
                verify_failure (config.test_verify_config_dialog dialog) "File Auto-Save Interval" |> Assert.True
(* Try to set an empty TagSymbol. *)
                dialog.TagSymbol.Text <- ""
                verify_failure (config.test_verify_config_dialog dialog) "Tag Symbol" |> Assert.True
(* Try to set a TagSymbol with length > 1. *)
                dialog.TagSymbol.Text <- "!!"
                verify_failure (config.test_verify_config_dialog dialog) "Tag Symbol" |> Assert.True
(* Try to set a TagSymbol that is a letter. *)
                dialog.TagSymbol.Text <- "a"
                verify_failure (config.test_verify_config_dialog dialog) "Tag Symbol" |> Assert.True
(* Try to set an AddOnServer host that is not a valid IP address. *)
                dialog.AddOnServerHost.Text <- "256.0.0.0"
                verify_failure (config.test_verify_config_dialog dialog) "Add On Server Host" |> Assert.True
(* Try to set an AddOnServer port that is not an integer. *)
                dialog.AddOnServerPort.Text <- "1.0"
                verify_failure (config.test_verify_config_dialog dialog) "Add On Server Port" |> Assert.True
(* Try to set an AddOnServer port < 0. *)
                dialog.AddOnServerPort.Text <- "-1"
                verify_failure (config.test_verify_config_dialog dialog) "Add On Server Port" |> Assert.True
(* Try to set an AddOnServer port < 65535. *)
                dialog.AddOnServerPort.Text <- "65536"
                verify_failure (config.test_verify_config_dialog dialog) "Add On Server Port" |> Assert.True
(* Try to set a FileSaveDelay < 1000. *)
                dialog.FileSaveDelay.Text <- "999"
                verify_failure (config.test_verify_config_dialog dialog) "File Auto-Save Interval" |> Assert.True
(* This setting is currently not used. *)
(*
(* Try to set a non-numeric ProjectSaveDelay. *)
                dialog.ProjectSaveDelay.Text <- "a"
                verify_failure (config.test_verify_config_dialog dialog) "Project Auto-Save Interval" |> Assert.True
(* Try to set a ProjectSaveDelay < 1000. *)
                dialog.ProjectSaveDelay.Text <- "999"
                verify_failure (config.test_verify_config_dialog dialog) "Project Auto-Save Interval" |> Assert.True
*)
(* Try to set a non-valid regular expression in FileFilterPatterns. *)
                dialog.FileFilterPatterns.Text <- "["
                verify_failure (config.test_verify_config_dialog dialog) "File Filter Patterns" |> Assert.True
(* Try to set a non-valid extension in DefaultFileExtension. *)
                dialog.DefaultFileExtension.Text <- "abc"
                verify_failure (config.test_verify_config_dialog dialog) "Default File Extension" |> Assert.True
                dialog.DefaultFileExtension.Text <- ".!"
                verify_failure (config.test_verify_config_dialog dialog) "Default File Extension" |> Assert.True
(* Try to set a non-existent path in RecentProjectFolders. *)
                dialog.RecentProjectFolders.Text <- ":"
                verify_failure (config.test_verify_config_dialog dialog) "Recent Projects List" |> Assert.True
(* Try to set a non-existing path in ProjectBackupFolder. *)
                dialog.ProjectBackupFolder.Text <- ":"
                verify_failure (config.test_verify_config_dialog dialog) "Project Backup Folder" |> Assert.True
(* Now set all settings to valid values and make sure verification succeeds. *)
(* This setting is currently not used. *)
//                dialog.MouseHoverDelay.Text <- "1000"
                dialog.MouseScrollSpeed.Text <- "1"
                dialog.DragScrollSpeed.Text <- "1"
                dialog.MarginHighlightDisplayTime.Text <- "1000"
                dialog.FontSize.Text <- "1"
                dialog.FileSaveDelay.Text <- "1000"
                dialog.TagSymbol.Text <- "&"
                dialog.AddOnServerHost.Text <- "127.0.0.1"
                dialog.AddOnServerPort.Text <- "13000"
(* This setting is currently not used. *)
//                dialog.ProjectSaveDelay.Text <- "1000"
                dialog.FileFilterPatterns.Text <- "x"
                dialog.DefaultFileExtension.Text <- ".abc"
                dialog.RecentProjectFolders.Text <- Directory.GetCurrentDirectory ()
                dialog.ProjectBackupFolder.Text <- Directory.GetCurrentDirectory ()
                config.test_verify_config_dialog dialog |> fst |> Assert.True
    };
    {
        part = "Configuration"
        name = "apply_config_dialog_to_settings"
        test = fun name ->
(* Helper function to verify settings. (1) is the TaggerConfig. (2) is the setting name. (3) is the expected value. *)
            let verify_setting (config : TaggerConfig) name expected_value =
                match config.TryFind name with
                | Some value -> value = expected_value
                | None -> false
            let config = get_default_config ()
            let dialog = new ConfigWindow ()
            do
(* RightSidebarWidth isn't set in the configuration dialog. *)
(* Add non-default dialog settings. *)
(* This setting is currently not used. *)
//                dialog.MouseHoverDelay.Text <- "2000"
                dialog.MouseScrollSpeed.Text <- "2"
                dialog.DragScrollSpeed.Text <- "2"
                dialog.MarginHighlightDisplayTime.Text <- "2000"
                dialog.FontSize.Text <- "2"
                dialog.FileSaveDelay.Text <- "2000"
                dialog.TagSymbol.Text <- "&"
                dialog.AddOnServerHost.Text <- "127.0.0.2"
                dialog.AddOnServerPort.Text <- "1"
(* This setting is currently not used. *)
//                dialog.ProjectSaveDelay.Text <- "2000"
                dialog.FileFilterPatterns.Text <- "x"
                dialog.DefaultFileExtension.Text <- ".abc"
                dialog.RecentProjectFolders.Text <- Directory.GetCurrentDirectory ()
                dialog.ProjectBackupFolder.Text <- Directory.GetCurrentDirectory ()
(* Apply the dialog settings to the config settings. *)
                config.test_apply_config_dialog_to_settings dialog
(* Verify the new settings. *)
(* This setting is currently not used. *)
//                verify_setting config "MouseHoverDelay" ["2000"] |> Assert.True
                verify_setting config "MouseScrollSpeed" ["2"] |> Assert.True
                verify_setting config "DragScrollSpeed" ["2"] |> Assert.True
                verify_setting config "MarginHighlightDisplayTime" ["2000"] |> Assert.True
                verify_setting config "FontSize" ["2"] |> Assert.True
                verify_setting config "FileSaveDelay" ["2000"] |> Assert.True
                verify_setting config "TagSymbol" ["&"] |> Assert.True
                verify_setting config "AddOnServerHost" ["127.0.0.2"] |> Assert.True
                verify_setting config "AddOnServerPort" ["1"] |> Assert.True
(* This setting is currently not used. *)
//                verify_setting config "ProjectSaveDelay" ["2000"] |> Assert.True
                verify_setting config "FileFilterPatterns" ["x"] |> Assert.True
                verify_setting config "DefaultFileExtension" [".abc"] |> Assert.True
                verify_setting config "RecentProjectFolders" [Directory.GetCurrentDirectory ()] |> Assert.True
                verify_setting config "ProjectBackupFolder" [Directory.GetCurrentDirectory ()] |> Assert.True
    };
    ]
//#endregion