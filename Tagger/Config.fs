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

module Config

// Int32, Double
open System
// ConcurrentDictionary
open System.Collections.Concurrent
// Directory, FileStream
open System.IO
// IPAddress
open System.Net
// DataContractSerializer, DataContract
open System.Runtime.Serialization
// StringBuilder
open System.Text
// Regex
open System.Text.RegularExpressions
// MessageBox
open System.Windows
// TextBox
open System.Windows.Controls

// XAML type provider
open FSharpx

// LoggerWrapper
open LoggerWrapper
// TaggerGrid, FileTreeView, TaggerTextEditor, TaggerTabControl
open TaggerControls
// AddOnServer
open AddOnServer

(* We tried to change this to use data binding, but it made the code more verbose, not less. See the DataBindingNotes folder. *)

(* The configuration settings dialog. *)
type private ConfigWindowXAML = XAML<"ConfigWindow.xaml">

(* We can't expose the XAML-based control directly. We don't inherit from UserControl, because we don't need to use this class within another XAML file and therefore we don't need the Content property. For more information see TaggerGrid. *)
type ConfigWindow () =
(* The actual control based on the XAML. *)
    let _config_window = new ConfigWindowXAML ()
(* Methods. *)
//#region
(* We can't expose the XAML-based control outside this module, so we provide aliases to its methods. *)
(* This setting is currently not used. *)
//    member this.MouseHoverDelay = _config_window.MouseHoverDelay
    member this.MouseScrollSpeed = _config_window.MouseScrollSpeed
    member this.DragScrollSpeed = _config_window.DragScrollSpeed
    member this.MarginHighlightDisplayTime = _config_window.MarginHighlightDisplayTime
    member this.FontSize = _config_window.FontSize
    member this.FileSaveDelay = _config_window.FileSaveDelay
(* This setting is currently not used. *)
//    member this.ProjectSaveDelay = _config_window.ProjectSaveDelay
    member this.TagSymbol = _config_window.TagSymbol
    member this.AddOnServerHost = _config_window.AddOnServerHost
    member this.AddOnServerPort = _config_window.AddOnServerPort
    member this.ProjectBackupFolder = _config_window.ProjectBackupFolder
    member this.FileFilterPatterns = _config_window.FileFilterPatterns
    member this.DefaultFileExtension = _config_window.DefaultFileExtension
    member this.RecentProjectFolders = _config_window.RecentProjectFolders
    member this.OK = _config_window.OK
    member this.Cancel = _config_window.Cancel
    member this.GetChild = _config_window.GetChild
    member this.ShowDialog = _config_window.Root.ShowDialog
    member this.Close = _config_window.Root.Close
//#endregion

[<DataContract>]
///<summary>The configuration settings.</summary>
type ConfigSettings = ConcurrentDictionary<string, string list>

///<summary>Contains the configuration settings for the application.</summary>
type TaggerConfig (grid : TaggerGrid, tree : FileTreeView, left_editor : TaggerTextEditor, right_editor : TaggerTextEditor, server : AddOnServer) as this =
(* Member values. *)
//#region
/// <summary>Contains the settings.</summary>
    let mutable _settings = new ConfigSettings ()
/// <summary>How long margin highlights are displayed after moving/copying/tagging text or showing a find result.</summary>
    let mutable _margin_highlight_display_time = 10000
//#endregion

(* General helper functions. *)
//#region
///<summary>Load the settings dictionary with default values.</summary>
    let load_default_settings () =
        let default_settings = [
(* The files that will be displayed by the FileTreeView. *)
            "FileFilterPatterns", ["\.txt$"];
(* The extension that is appended to each new file unless another is specified. *)
            "DefaultFileExtension", [".txt"];
(* This is how long the mouse can hover before we display a tool tip. *)
(* This setting is currently not used. *)
//            "MouseHoverDelay", ["1000"];
(* This is how many lines are scrolled for each notch in the mouse wheel. *)
            "MouseScrollSpeed", ["1"];
(* The number of lines scrolled per second while the user drags near the top or bottom of the editor. *)
            "DragScrollSpeed", ["1"];
(* How long margin highlights are displayed after moving/copying/tagging text or showing a find result. *)
            "MarginHighlightDisplayTime", ["10000"];
(* The editor font size. *)
            "FontSize", ["11"];
(* The interval at which we save the open file. *)
            "FileSaveDelay", ["5000"];
(* The tag symbol. *)
            "TagSymbol", ["#"];
(* The interval at which we save the open project. *)
(* This setting is currently not used. *)
//            "ProjectSaveDelay", ["10000"];
(* The address where we listen for add on connections. *)
            "AddOnServerHost", ["127.0.0.1"];
(* The port where we listen for add on connections. *)
            "AddOnServerPort", ["13000"];
(* The width of the right sidebar of the TaggerGrid. *)
            "RightSidebarWidth", ["100"];
(* The project backup folder. *)
            "ProjectBackupFolder", [Directory.GetCurrentDirectory ()]
        ]
        do
            _settings.Clear ()
            default_settings |> List.iter (fun (name, value) -> _settings.AddOrUpdate (name, value, (fun _ _ -> value)) |> ignore)

///<summary>Save the application configuration settings in file "config.ini" in the same directory as the application. Return unit.</summary>
    let save_config_helper () =
        let config_file = sprintf "%s\\config.ini" <| Directory.GetCurrentDirectory ()
        let writer = new DataContractSerializer (typeof<ConfigSettings>)
(* We define this here so we can call Dispose on it in the finally block. If we define it in the try block, the finally block can't see it. *)
        let mutable stream : FileStream = null
(* We can't use with and finally in the same try block. *)
        try
            try
                do
(* Serialize the settings and write them to the file. Use FileMode.Create to ensure the file is overwritten if it exists. *)
                    stream <- new FileStream (config_file, FileMode.Create)
                    writer.WriteObject (stream, _settings)
(* Calling Dispose on the FileStream closes it. *)
            finally if stream <> null then do stream.Dispose ()
(* If an exception occurs in the inner try block above, execution will go to to the finally statement, then skip this statement and go to the with statement below. *)
            do _logger.Log_ ("Configuration.SaveConfiguration", ["path", config_file])
        with | ex -> do _logger.Log_ ("Configuration.SaveConfigurationError", ["path", config_file; "message", ex.Message])

///<summary>Load the application configuration settings from file "config.ini" in the same directory as the application. If unable to do so, use the default settings. Return unit.</summary>
    let load_config_helper () =
        let config_file = sprintf "%s\\config.ini" <| Directory.GetCurrentDirectory ()
        let reader = new DataContractSerializer (typeof<ConfigSettings>)
(* See comments for save_config. *)
        let mutable stream : FileStream = null
        try
            try
                do
                    stream <- new FileStream (config_file, FileMode.Open)
(* Normally, we check the type before downcasting, but here we want to raise an exception if the config file is invalid. *)
                    _settings <- reader.ReadObject (stream) :?> ConfigSettings
                    _logger.Log_ ("Configuration.LoadConfiguration", ["path", config_file])
            finally if stream <> null then do stream.Dispose ()
        with | ex ->
            do
                _logger.Log_ ("Configuration.LoadConfigurationError", ["path", config_file; "message", ex.Message])
(* If we weren't able to load the settings, use the default settings. *)
                load_default_settings ()

(* The configuration settings are modified as follows:
RightSidebarWidth - only in TaggerConfig.save_config, which is called when the program closes.
RecentProjectFolders - in the config dialog, or MainController.save_project.
MouseHoverDelay - only in the config dialog.
MouseScrollSpeed - only in the config dialog.
DragScrollSpeed - only in the config dialog.
MarginHighlightDisplayTime - only in the config dialog.
FontSize - only in the config dialog.
FileSaveDelay - only in the config dialog.
TagSymbol - only in the config dialog.
ProjectSaveDelay - only in the config dialog.
AddOnServerHost - only in the config dialog.
AddOnServerPort - only in the config dialog.
FileFilterPatterns - only in the config dialog.
DefaultFileExtension - only in the config dialog.
ProjectBackupFolder - only in the config dialog.
*)
/// <summary>Apply the config settings to the controls. Return unit.</summary>
    let apply_settings_to_controls () =
(* We don't apply ProjectBackupFolder to a control, because MainController can query TaggerConfig for it when it needs it. *)
(* Set the grid property. *)
        match this.TryFind "RightSidebarWidth" with
        | Some value ->
            match Double.TryParse (value |> List.head) with
            | true, value_ -> do grid.RightSidebarWidth <- value_
            | _ -> ()
        | None -> ()
(* Set the editor properties. *)
(* This setting is currently not used. *)
(*
        match this.TryFind "MouseHoverDelay" with
        | Some value ->
            match Int32.TryParse (value |> List.head) with
            | true, value_ ->
                do
                    left_editor.mouse_hover_delay <- value_
                    right_editor.mouse_hover_delay <- value_
            | _ -> ()
        | None -> ()
*)
        match this.TryFind "MouseScrollSpeed" with
        | Some value ->
            match Int32.TryParse (value |> List.head) with
            | true, value_ ->
                do
                    left_editor.mouse_scroll_speed <- value_
                    right_editor.mouse_scroll_speed <- value_
            | _ -> ()
        | None -> ()
        match this.TryFind "DragScrollSpeed" with
        | Some value ->
            match Int32.TryParse (value |> List.head) with
            | true, value_ ->
                do
                    left_editor.drag_scroll_speed <- value_
                    right_editor.drag_scroll_speed <- value_
            | _ -> ()
        | None -> ()
        match this.TryFind "MarginHighlightDisplayTime" with
        | Some value ->
            match Int32.TryParse (value |> List.head) with
            | true, value_ ->
                do
                    this.margin_highlight_display_time <- value_
                    this.margin_highlight_display_time <- value_
            | _ -> ()
        | None -> ()
        match this.TryFind "FontSize" with
        | Some value ->
            match Int32.TryParse (value |> List.head) with
            | true, value_ ->
                do
(* By default, the font size is in device-independent units (px). See:
http://msdn.microsoft.com/en-us/library/system.windows.controls.control.fontsize%28v=vs.110%29.aspx
Per Pro WPF 4.5 in C#, pp. 7-8, WPF automatically adjusts device-independent units for the system DPI setting. *)
                    left_editor.FontSize <- (float) value_
                    right_editor.FontSize <- (float) value_
            | _ -> ()
        | None -> ()
        match this.TryFind "FileSaveDelay" with
        | Some value ->
            match Int32.TryParse (value |> List.head) with
            | true, value_ ->
                do
                    left_editor.save_timer_interval <- (float) value_
                    right_editor.save_timer_interval <- (float) value_
            | _ -> ()
        | None -> ()
        match this.TryFind "TagSymbol" with
        | Some value -> do TagInfo.tag_symbol <- (value |> List.head)
        | None -> ()
(* Set the tree properties. *)
        match this.TryFind "FileFilterPatterns" with
        | Some value -> do tree.file_filter_patterns <- value
        | None -> ()
        match this.TryFind "DefaultFileExtension" with
        | Some value -> do tree.default_file_extension <- value |> List.head
        | None -> ()
        match this.TryFind "RecentProjectFolders" with
        | Some value -> do tree.recent_projects <- value
        | None -> ()
(* For now, we've decided to stop saving the project automatically, and only save it in response to events like the tree or tab control changing. *)
(*
        match this.TryFind "ProjectSaveDelay" with
        | Some value ->
            match Int32.TryParse (value |> List.head) with
            | true, value_ -> do tree.save_timer_interval <- value_
            | _ -> ()
        | None -> ()
*)
(* Set the server properties. *)
        match this.TryFind "AddOnServerHost", this.TryFind "AddOnServerPort" with
        | Some host, Some port->
            match IPAddress.TryParse (host |> List.head), Int32.TryParse (port |> List.head) with
            | (true, host_), (true, port_) -> do server.update_host_port host_ port_
            | _ -> ()
        | _ -> ()

///<summary>Verify the input in the configuration dialog (1). Return true if verification succeeds; otherwise, return false and the combined error message.</summary>
    let verify_config_dialog (win : ConfigWindow) =
        let errors = new StringBuilder ()
        do
(* Remove extra newlines from multi-line textboxes, to reduce unnecessary errors. *)
            win.FileFilterPatterns.Text <- win.FileFilterPatterns.Text.Trim ()
            win.RecentProjectFolders.Text <- win.RecentProjectFolders.Text.Trim ()
(* This setting is currently not used. *)
(*
        let verify_mouse_hover_delay =
            match Int32.TryParse win.MouseHoverDelay.Text with
            | true, value when value >= 1000 -> true
            | _ ->
                do errors.AppendLine "Mouse Hover Delay must be numeric, and at least 1000." |> ignore
                false
*)
        let verify_mouse_scroll_speed =
            match Int32.TryParse win.MouseScrollSpeed.Text with
            | true, value when value > 0 -> true
            | _ ->
                do errors.AppendLine "Mouse Scroll Speed must be a whole number, and at least 1." |> ignore
                false
        let verify_drag_scroll_speed =
            match Int32.TryParse win.DragScrollSpeed.Text with
            | true, value when value > 0 -> true
            | _ ->
                do errors.AppendLine "Drag Scroll Speed must be a whole number, and at least 1." |> ignore
                false
        let verify_margin_display_highlight_time =
            match Int32.TryParse win.MarginHighlightDisplayTime.Text with
            | true, value when value >= 1000 -> true
            | _ ->
                do errors.AppendLine "Margin Highlight Display Time must be a whole number, and at least 1000." |> ignore
                false
        let verify_font_size =
            match Int32.TryParse win.FontSize.Text with
            | true, value when value >= 1 -> true
            | _ ->
                do errors.AppendLine "Font Size must be a whole number, and at least 1." |> ignore
                false
        let verify_file_save_interval =
            match Int32.TryParse win.FileSaveDelay.Text with
            | true, value when value >= 1000 -> true
            | _ ->
                do errors.AppendLine "File Auto-Save Interval must be a whole number, and at least 1000." |> ignore
                false
        let verify_tag_symbol =
            let tag_symbol = win.TagSymbol.Text
            if tag_symbol.Length <> 1 then
                do errors.AppendLine "Tag Symbol must be one character." |> ignore
                false
            else if Regex.IsMatch (tag_symbol, TagInfo.tag_pattern_no_symbol) ||
                tag_symbol.[0] |> Char.IsWhiteSpace then
                do errors.AppendLine "Tag Symbol must not be a letter or digit or whitespace." |> ignore
                false
            else true
        let verify_add_on_server_host =
            match IPAddress.TryParse win.AddOnServerHost.Text with
            | true, value -> true
            | _ ->
                do errors.AppendLine "Add On Server Host must be a valid IP address." |> ignore
                false
        let verify_add_on_server_port =
            match Int32.TryParse win.AddOnServerPort.Text with
            | true, value when value >= 0 && value <= 65535 -> true
            | _ ->
                do errors.AppendLine "Add On Server Port must be a whole number from 0 to 65535." |> ignore
                false
(* This setting is currently not used. *)
(*
        let verify_project_save_interval =
            match Int32.TryParse win.ProjectSaveDelay.Text with
            | true, value when value >= 1000.0 -> true
            | _ ->
                do errors.AppendLine "Project Auto-Save Interval must be a whole number, and at least 1000." |> ignore
                false
*)
        let verify_project_backup_folder =
            let folder = win.ProjectBackupFolder.Text
(* If the folder exists, return true. *)
            if Directory.Exists folder then true
            else
(* Otherwise, try to create the folder. *)
                try
                    do Directory.CreateDirectory folder |> ignore
                    true
                with | ex ->
(* If we fail, add to the error message. *)
                    do sprintf "Project Backup Folder: Unable to create folder \"%s\": %s" folder ex.Message |> errors.AppendLine |> ignore
                    false
        let verify_file_patterns =
(* Split the value based on newlines and loop through the patterns. *)
            win.FileFilterPatterns.Text.Split '\n' |> Array.forall (fun pattern ->
                try
(* Try to create a Regex using the pattern. *)
                    new Regex (pattern) |> ignore
                    true
                with | ex ->
(* If we fail, add to the error message. *)
                    do sprintf "File Filter Patterns: \"%s\" is not a valid regular expression." pattern |> errors.AppendLine |> ignore
                    false
            )
        let verify_default_file_extension =
(* This pattern requires that the string start with a period and contain one or more characters, all alphanumeric, after that. *)
            let pattern = "^\.[a-zA-Z0-9]+$"
            let value = win.DefaultFileExtension.Text
(* DefaultFileExtension can be blank, in which case no extension is added. *)
            if value.Length = 0 then true
            else if Regex.IsMatch (value, pattern) = false then
                do "Default File Extension: Value must start with a period and contain one or more characters, all alphanumeric, after that." |> errors.AppendLine |> ignore
                false
            else true
        let verify_recent_projects =
            let value = win.RecentProjectFolders.Text
(* String.Split yields a non-empty array even if the string is empty. *)
            if value.Length = 0 then true
(* Split the value based on newlines and loop through the paths. *)
            else value.Split '\n' |> Array.forall (fun folder ->
(* Verify the folder exists. *)
                    if Directory.Exists folder then true
                    else
                        do sprintf "Recent Projects List: path \"%s\" was not found." folder |> errors.AppendLine |> ignore
                        false
                )
        (
(* This setting is currently not used. *)
//            verify_mouse_hover_delay &&
            verify_mouse_scroll_speed &&
            verify_drag_scroll_speed &&
            verify_margin_display_highlight_time &&
            verify_font_size &&
            verify_file_save_interval &&
            verify_tag_symbol &&
//            verify_project_save_interval &&
            verify_add_on_server_host &&
            verify_add_on_server_port &&
            verify_file_patterns &&
            verify_default_file_extension &&
            verify_recent_projects &&
            verify_project_backup_folder), errors

///<summary>Apply the input in the configuration dialog (1) to the settings. Return unit.</summary>
    let apply_config_dialog_to_settings (win : ConfigWindow) =
        do
(* We don't set RightSidebarWidth in the configuration dialog. *)
(* This setting is currently not used. *)
//            this.Update "MouseHoverDelay" [win.MouseHoverDelay.Text]
            this.Update "MouseScrollSpeed" [win.MouseScrollSpeed.Text]
            this.Update "DragScrollSpeed" [win.DragScrollSpeed.Text]
            this.Update "MarginHighlightDisplayTime" [win.MarginHighlightDisplayTime.Text]
            this.Update "FontSize" [win.FontSize.Text]
(* This setting is currently not used. *)
//            this.Update "ProjectSaveDelay" [win.ProjectSaveDelay.Text]
            this.Update "AddOnServerHost" [win.AddOnServerHost.Text]
            this.Update "AddOnServerPort" [win.AddOnServerPort.Text]
            this.Update "FileSaveDelay" [win.FileSaveDelay.Text]
            this.Update "TagSymbol" [win.TagSymbol.Text]
            this.Update "ProjectBackupFolder" [win.ProjectBackupFolder.Text]
            this.Update "FileFilterPatterns" (win.FileFilterPatterns.Text.Split '\n' |> Array.toList)
            this.Update "DefaultFileExtension" [win.DefaultFileExtension.Text]
            this.Update "RecentProjectFolders" (win.RecentProjectFolders.Text.Split '\n' |> Array.toList)
//#endregion

(* Event handlers. *)
//#region
///<summary>Handle the Click event of the OK button in the configuration dialog (1). Return unit.</summary>
    let ok_button_click_handler (win : ConfigWindow) =
(* Verify the contents of the configuration dialog. *)
        match verify_config_dialog win with
(* If we failed, display the error message. *)
        | false, errors -> do errors.ToString () |> MessageBox.Show |> ignore
        | true, _ ->
(* Apply the configuration dialog to the settings. *)
            apply_config_dialog_to_settings win
(* Apply the settings to the control properties. *)
            apply_settings_to_controls ()
(* Save the settings to disk. *)
            save_config_helper ()
(* Close the configuration dialog. *)
            win.Close ()
//#endregion

(* Constructor. *)
//#region
(* Load the default settings. *)
    do load_default_settings ()
//#endregion

(* Methods. *)
//#region
///<summary>Try to get the value for key (1). Return the result if found; otherwise, None.</summary>
    member this.TryFind name =
        match _settings.TryGetValue name with
        | true, value -> Some value
        | _ -> None

/// <summary>Update the setting with name (1) to value (2). Return unit.</summary>
    member this.Update name value =
        do _settings.AddOrUpdate (name, value, (fun _ _ -> value)) |> ignore

/// <summary>Get the settings from the controls and save them to disk. Return unit.</summary>
    member this.save_config () =
        do
(* We don't store ProjectBackupFolder in a control, because MainController can query TaggerConfig for it when it needs it.
Also, we only need to get those settings that can be set outside of the configuration dialog. *)
(* Get the grid properties. *)
            this.Update "RightSidebarWidth" [sprintf "%f" grid.RightSidebarWidth]
(* Get the tree properties. *)
            this.Update "RecentProjectFolders" tree.recent_projects
(* Save the settings to disk. *)
            save_config_helper ()

/// <summary>Load the settings from disk and apply them to the controls. Return unit.</summary>
    member this.load_config () =
(* Load the settings from disk. *)
        do load_config_helper ()
(* Set the control properties. *)
        do apply_settings_to_controls ()

/// <summary>Display the configuration settings dialog. Return unit.</summary>
    member this.show_settings_dialog () =
        let win = new ConfigWindow ()
(* We use these lists to decide which settings should display only one value and which should display all their values. *)
(* We don't display RightSidebarWidth. *)
        let single_value_settings = ["MouseHoverDelay"; "MouseScrollSpeed"; "DragScrollSpeed"; "MarginHighlightDisplayTime"; "FontSize"; "FileSaveDelay"; "TagSymbol"; "AddOnServerHost"; "AddOnServerPort"; "ProjectSaveDelay"; "ProjectBackupFolder"; "DefaultFileExtension"]
        let multi_value_settings = ["FileFilterPatterns"; "RecentProjectFolders"]
        do
(* When the user clicks Cancel, close the window and do nothing. *)
            win.Cancel.Click.Add (fun _ -> do win.Close ())
(* When the user clicks OK, validate, save and apply the settings. *)
            win.OK.Click.Add (fun _ -> do ok_button_click_handler win)
(* Loop through the settings. *)
            _settings.ToArray () |> Array.iter (fun kv ->
                let name = kv.Key
                let value = kv.Value
(* Get the TextBox with the same name as the setting. *)
                match win.GetChild name with
                | :? TextBox as control ->
(* For single-value settings, get the first value in the list, if any. *)
                    if single_value_settings |> List.exists (fun item -> item = name) then
                        do control.Text <- if value.Length > 0 then value.Head else ""
(* For multi-value settings, join all values together using newlines. *)
                    else if multi_value_settings |> List.exists (fun item -> item = name) then
                        do control.Text <- String.Join ("\n", value)
                | _ -> ()
                )
(* Display the dialog. The result is a nullable bool, I don't know why, and I don't want to deal with the null case, so I ignore it and use event handlers for the OK and Cancel buttons instead. *)
            win.ShowDialog () |> ignore
//#endregion

(* Properties. *)
//#region
    member this.margin_highlight_display_time
        with get () = _margin_highlight_display_time
        and set value = _margin_highlight_display_time <- value
//#endregion

(* Expose the settings for testing. *)
    member this.test_settings with get () = _settings

(* Expose methods for testing. *)
    member this.test_load_config_helper = load_config_helper
    member this.test_save_config_helper = save_config_helper
    member this.test_apply_settings_to_controls = apply_settings_to_controls
    member this.test_verify_config_dialog = verify_config_dialog
    member this.test_apply_config_dialog_to_settings = apply_config_dialog_to_settings
