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

module PaneControllerTest

// ConcurrentDictionary
open System.Collections.Concurrent
// File
open System.IO
// MessageBox
open System.Windows

open Xunit
open ICSharpCode.AvalonEdit

// TaggerTextEditor, TaggerTabControl
open TaggerControls
// PaneController
open PaneController
open TestHelpers

(*
Log events to test:
OpenFileError
x    add_and_select_tab
x    load_file_into_document
SaveFileError
x    close_file
x    save_file_error_handler

Functions to test:
x save_vertical_position (via close_file, which we call in close_file_save_vertical_positions)
x save_undo_stack
x open_file_helper
x open_file
x select_previous_tab (in select_tab_handler)
x select_tab_handler
x close_all_tabs_handler
N tabs_left_mouse_button_up_handler (UI function)
x save_file_error_handler (in log events tests)
 word_wrap
N margin_selection_changed_handler (UI function)
 editor_document_changed_handler
N margin_drag_over_handler (UI function)
N margin_select_handler (UI function). We tried moving code to a non-UI helper function so we could test that, but the UI and non-UI parts didn't separate cleanly.
N margin_drag_start_handler (UI function)
N margin_right_click_handler (UI function)
N editor_margin_drag_handler (just calls TaggerMargin.drag_over_helper)
x editor_margin_drop_handler
N scroll_changed_handler (UI function)
N extent_height_changed_handler (UI function)
N editor_is_enabled_changed_handler
/ close_file_helper (in close_file)
x load_file_into_document
x close_tab
x close_file
x add_and_select_tab
N highlight_lines (UI function)
N select_lines (just calls Editor.update_selected_lines, highlight_lines, and Editor.most_recently_selected_line)
N display_line (UI function)

Events to test:
N _margin_drop (No function triggers this event. It is simply re-fired from TaggerMargin.)
N _margin_right_click (margin_right_click_handler)
N _open_tag (No function triggers this event. It is simply re-fired from TaggerTextEditor.)
N _right_click (No function triggers this event. It is simply re-fired from TaggerTextEditor.)
N _find_in_project (No function triggers this event. It is simply re-fired from TaggerTextEditor.)
N _file_closed (No function triggers this event. It is simply re-fired from TaggerTextEditor.)
N _save_project (No function triggers this event. It is simply re-fired from TabControl.)
N _tag_symbol_entered (No function triggers this event. It is simply re-fired from TaggerTextEditor.)
N _tag_selected (No function triggers this event. It is simply re-fired from TaggerTextEditor.)
*)

(* Helper functions. *)
//#region
(* This is copied from TabControlTest, because we also add tabs to tab controls here. *)
/// <summary>Helper function that adds a tab to tab control (1) based on name (2) and number (3). Returns the tab and the tab name.</summary>
let get_tab (tabs : TaggerTabControl) name number =
    let tab_name = CreateTestTextFileWithNumber name number ""
    let tab = tabs.add_tab tab_name
    tab.IsSome |> Assert.True
    tab.Value, tab_name

/// <summary>Helper function that returns a new PaneController.</summary>
let get_pc (editor : TaggerTextEditor option) (tabs : TaggerTabControl option) (left_margin : TaggerMargin option) (right_margin : TaggerMargin option) =
    let editor_ =
        match editor with
        | Some editor__ -> editor__
        | None -> new TaggerTextEditor ()
    let tabs_ =
        match tabs with
        | Some tabs__ -> tabs__
        | None -> new TaggerTabControl ()
    let left_margin_ =
        match left_margin with
        | Some margin__ -> margin__
        | None -> new TaggerMargin ()
    let right_margin_ =
        match right_margin with
        | Some margin__ -> margin__
        | None -> new TaggerMargin ()
    let status = new System.Windows.Controls.TextBlock ()
    let vertical_positions = new ConcurrentDictionary<string, float> () |> ref
    let documents = new DocumentMap () |> ref
    new PaneController (editor_, tabs_, left_margin_, right_margin_, status, vertical_positions, documents)

(* Note: This is borrowed from EditorTest. *)
/// <summary>Helper function that determines whether an editor (1) is empty. Returns true if empty; otherwise, false.</summary>
let editor_is_empty (editor : TaggerTextEditor) =
    editor.IsEnabled = false &&
    editor.file.IsNone &&
    editor.Text.Length = 0
//#endregion

type PaneControllerTest () =
    interface ITestGroup with
(* Tests that log. *)
//#region
    member this.tests_log with get () = [
(* These tests should log an event. *)
(* These events are non-critical errors. *)
(* Try to load a nonexistent file into a document. *)
    {
        part = "PaneController"
        name = "OpenFileError"
        test = fun name ->
(* Create the PaneController. *)
            let pc = get_pc None None None None
            do PaneController.load_file_into_document ":" = None |> Assert.True
    };
(* Try to add a tab for a nonexistent file. *)
    {
        part = "PaneController"
        name = "OpenFileError"
        test = fun name ->
(* Create the PaneController. *)
            let pc = get_pc None None None None
            do pc.add_and_select_tab ":" |> Assert.False
    };
(* Try to close a read-only file in the editor. *)
    {
        part = "PaneController"
        name = "SaveFileError"
        test = fun name ->
(* We use CreateTestTextFileWithNumber because there are two tests with the same name. That's necessary because the log is searched for an error message with the same name as the test. *)
(* Create a file to open in the editor. *)
            let test_file = CreateTestTextFileWithNumber name 1 "test"
(* Create the PaneController. *)
            let pc = get_pc None None None None
            do
(* Add a tab for the file and open it in the editor. *)
                pc.add_and_select_tab test_file |> Assert.True
(* Set the file as read-only. *)
                File.SetAttributes (test_file, FileAttributes.ReadOnly)
(* Try to close the file in the editor, which will try to save the file. What happens in the tab control does not matter for this test. *)
                Assert.False <| pc.close_file ()
(* Verify that the file property has not changed. *)
            let editor = pc.editor
            do
                Assert.True (editor.file.IsSome && editor.file.Value = test_file)
(* Verify that the text has not changed. *)
                Assert.True (editor.Text = "test")
(* Verify that the editor is not disabled. *)
                Assert.True editor.IsEnabled
(* Mark the file as writable, so the next SaveFileError test can overwrite it. The error message has already been logged. *)
                File.SetAttributes (test_file, FileAttributes.Normal)
    };
(* Trigger the SaveFileError using auto save. *)
    {
        part = "PaneController"
        name = "SaveFileError"
        test = fun name ->
(* We use CreateTestTextFileWithNumber because there are two tests with the same name. That's necessary because the log is searched for an error message with the same name as the test. *)
(* Create a file to open in the editor. *)
            let test_file = CreateTestTextFileWithNumber name 2 ""
(* Create the PaneController. *)
            let pc = get_pc None None None None
            do
(* Set the save timer interval to 1/10 second. *)
                pc.editor.save_timer_interval <- 100.0
(* Add a tab for the file and open it in the editor. *)
                pc.test_open_file test_file |> Assert.True
(* Set the file as read-only. *)
                File.SetAttributes (test_file, FileAttributes.ReadOnly)
(* Change the text in the editor to set the IsModified flag. *)
                pc.editor.Text <- "test"
(* Let the thread go idle. *)
                name |> System.Windows.MessageBox.Show |> ignore
    }
    ]
//#endregion

    member this.tests_throw with get () = []

(* Tests that don't log. *)
//#region
    member this.tests_no_log with get () = [
    {
        part = "PaneController"
        name = "save_document"
        test = fun name ->
(* Create test files. *)
            let test_file_1 = CreateTestTextFileWithNumber name 1 "test1"
            let test_file_2 = CreateTestTextFileWithNumber name 2 "test2"
(* Create the PaneController. *)
            let pc = get_pc None None None None
(* Add a tab for the first file and open it in the editor. *)
            do
                pc.add_and_select_tab test_file_1 |> Assert.True
(* Change the text. *)
                pc.editor.Document.Insert (0, "changed")
(* Get the document. *)
            let document_1 = pc.editor.Document
(* Close the tab. We do this to make sure the tab is unselected, so when we select it again, it will open the file. *)
            do
                pc.close_tab test_file_1 |> Assert.True
(* Verify the document map has an entry for this file and it matches the document. *)
                match pc.test_documents.TryGetValue test_file_1 with
                | true, result -> (fst result = document_1) |> Assert.True
                | _ -> do Assert.True false
(* Add a tab for the second file and open it in the editor. *)
                pc.add_and_select_tab test_file_2 |> Assert.True
(* Change the text. *)
                pc.editor.Document.Insert (0, "changed")
(* Get the document. *)
            let document_2 = pc.editor.Document
(* Close the tab. *)
            do
                pc.close_tab test_file_2 |> Assert.True
(* Verify the document map has an entry for this file and it matches the document. *)
                match pc.test_documents.TryGetValue test_file_2 with
                | true, result -> (fst result = document_2) |> Assert.True
                | _ -> do Assert.True false
(* Open the first file again. *)
                pc.add_and_select_tab test_file_1 |> Assert.True
(* Verify the editor has the document for the first file. *)
                (pc.editor.Document = document_1) |> Assert.True
(* Undo the text change. *)
                pc.editor.Undo () |> Assert.True
(* Verify the text has the original content again. *)
                (pc.editor.Text = "test1") |> Assert.True
(* The undo stack should now be empty. *)
                pc.editor.Document.UndoStack.CanUndo |> Assert.False
(* Open the second file again. *)
                pc.add_and_select_tab test_file_2 |> Assert.True
(* Verify we have the document for the second file. *)
                (pc.editor.Document = document_2) |> Assert.True
(* Undo the text change. *)
                pc.editor.Undo () |> Assert.True
(* Verify the text has the original content again. *)
                (pc.editor.Text = "test2") |> Assert.True
(* The undo stack should now be empty. *)
                pc.editor.Document.UndoStack.CanUndo |> Assert.False
    };
    {
        part = "PaneController"
        name = "open_file_helper"
        test = fun name ->
(* Create a file. *)
            let test_file = CreateTestTextFile name "test"
(* Create the PaneController. *)
            let pc = get_pc None None None None
            do
(* Open the file in the editor. *)
                pc.test_open_file test_file |> Assert.True
(* Close the file. This adds the document to the document map. *)
                pc.close_file () |> Assert.True
(* Wait to make sure the last modified time on the file is later than the timestamp in the document map from when we closed the file. *)
                System.Threading.Thread.Sleep (100)
(* Change the file outside of Tagger. *)
                File.AppendAllText (test_file, "_changed")
(* Call open_file_helper. Cancel the reload. Verify the result. *)
                pc.test_open_file_helper test_file (fun _ -> MessageBoxResult.Cancel) = None |> Assert.True
(* Call open_file_helper. Reject the reload. Verify the result. *)
            let old_document = pc.test_open_file_helper test_file (fun _ -> MessageBoxResult.No)
            do
                old_document.IsSome |> Assert.True
                old_document.Value.Text = "test" |> Assert.True
(* Call open_file_helper. Allow the reload. Verify the result. *)
            let new_document = pc.test_open_file_helper test_file (fun _ -> MessageBoxResult.Yes)
            do
                new_document.IsSome |> Assert.True
                new_document.Value.Text = "test_changed" |> Assert.True
    };
    {
        part = "PaneController"
        name = "open_file"
        test = fun name ->
(* Create a file. *)
            let test_file = CreateTestTextFile name "test"
(* Create the PaneController. *)
            let pc = get_pc None None None None
(* Open the file in the editor. *)
            do pc.test_open_file test_file |> Assert.True
(* Verify that the editor has the file open. *)
            let editor = pc.editor
            do
                Assert.True (editor.file.IsSome && editor.file.Value = test_file)
(* Verify the editor is enabled. *)
                Assert.True editor.IsEnabled
(* Verify the text. *)
                Assert.True (editor.Text = "test")
    };
    {
        part = "PaneController"
        name = "select_tab_handler"
        test = fun name ->
(* Create an empty file to open in the editor. *)
            let test_file = CreateTestTextFile name ""
(* Create the PaneController. *)
            let pc = get_pc None None None None
(* Add a tab for the file. *)
            let tab0 = (pc.tabs.add_tab test_file).Value
(* Select the tab. *)
            do pc.test_select_tab_handler None tab0
(* Verify that the editor has the file open. *)
            let editor = pc.editor
            do
                Assert.True editor.IsEnabled
                Assert.True (editor.file.IsSome && editor.file.Value = test_file)
                Assert.True (editor.Text = "")
(* Create another file and add a tab for it. *)
            let test_file_2 = CreateTestTextFileWithNumber name 2 "test2"
            let tab1 = (pc.tabs.add_tab test_file_2).Value
(* Change the text for the first file in the editor. *)
            do
                editor.Text <- "test"
(* Select the second tab. *)
                pc.test_select_tab_handler (Some tab0) tab1
(* Verify the changes to the first file were saved. *)
                Assert.True (File.ReadAllText test_file = "test")
(* Verify the editor has the second file open. *)
                Assert.True (editor.file.IsSome && editor.file.Value = test_file_2)
                Assert.True (editor.Text = "test2")
    };
(* Try to open a tab for another file when the current one cannot be saved, or the other file isn't valid. *)
    {
        part = "PaneController"
        name = "select_tab_handler_error"
        test = fun name ->
(* Helper function to verify that the controls have not changed after a failed attempt to select another tab. *)
            let verify_unchanged (pc : PaneController) (tab : TaggerTabItem) test_file =
(* Verify that the file property has not changed. *)
                let editor = pc.editor
                do
                    Assert.True (editor.file.IsSome && editor.file.Value = test_file)
(* Verify that the text has not changed. *)
                    Assert.True (editor.Text = "test")
(* Verify that the expected tab is still selected. *)
                    Assert.True tab.IsSelected
(* Create the PaneController. *)
            let pc = get_pc None None None None
(* Get the TabControl. *)
            let tabs = pc.tabs
(* 1. Try to select a tab for an invalid file, when there is no previously selected tab. *)
(* Add a tab. This does not select the tab. *)
            let invalid_tab, invalid_tab_name = get_tab tabs name 0
(* Delete the file for the tab, then try to select the tab. *)
            do
                File.Delete invalid_tab_name
                invalid_tab.select ()
(* In this case, we don't use the verify_unchanged helper function, because it's intended for cases with a previously selected tab. *)
(* Verify that no tab is selected. *)
                Assert.True (tabs.SelectedIndex = -1)
(* Verify the editor is empty and disabled. *)
                Assert.True (pc.editor.Text.Length = 0)
                Assert.False pc.editor.IsEnabled
(* Clear the TabControl to prepare for the next tests. *)
                tabs.Items.Clear ()
(* 2. Select a tab for a valid file, then try to select another tab for an invalid file. *)
(* Create a file to open in the editor. *)
(* Note get_tab calls CreateTestTextFileWithNumber as well, so we want to increment the number when calling either one to avoid reusing the same file name. *)
            let test_file_1 = CreateTestTextFileWithNumber name 1 "test"
(* Add a tab for the file and open it in the editor. This also selects the tab. *)
            do pc.add_and_select_tab test_file_1 |> Assert.True
            let tab0 = tabs.Items.[0] :?> TaggerTabItem
            do
(* Try to select the tab for which we deleted the file again. *)
(* Note:
pc.test_select_tab_handler (Some tab_0) invalid_tab
doesn't work for this, because select_tab_handler only responds to a tab selection. We need to actually select the other tab. *)
                invalid_tab.select ()
(* Verify that the controls have not changed. *)
                verify_unchanged pc tab0 test_file_1
(* 3. Try to select another tab for a valid file when the current file can't be saved. *)
(* Mark the file for the first tab as read-only, then try to select another tab, to verify that we get the expected error. *)
                File.SetAttributes (test_file_1, FileAttributes.ReadOnly)
(* In this case, make the other tab point to a valid file. *)
            let tab1, _ = get_tab tabs name 2
            do
(* Try to select the other tab. *)
                tab1.select ()
(* Verify that the controls have not changed. *)
                verify_unchanged pc tab0 test_file_1
    };
    {
        part = "PaneController"
        name = "close_all_tabs_handler"
        test = fun name ->
(* Create files to be added as tabs. *)
            let test_file_1 = CreateTestTextFileWithNumber name 1 ""
            let test_file_2 = CreateTestTextFileWithNumber name 2 ""
(* Create the PaneController. *)
            let pc = get_pc None None None None
(* This is so we can verify the save_project event fires. *)
            let save_project_fired = ref false
            do pc.save_project.Add (fun () -> do save_project_fired := true)
(* Add tabs for the files. *)
            let tab0 = (pc.tabs.add_tab test_file_1).Value
            let tab1 = (pc.tabs.add_tab test_file_2).Value
(* Select the first tab. *)
            do
                pc.test_select_tab_handler None tab0
(* Change the file in the editor. *)
                pc.editor.Text <- "test"
(* Close all tabs. *)
                pc.test_close_all_tabs_handler ()
(* Verify the save_project event fired. *)
                !save_project_fired |> Assert.True
(* Verify the changes to the file were saved. *)
                test_file_1 |> File.ReadAllText = "test" |> Assert.True
(* Verify the tabs were closed. *)
                pc.tabs.Items.Count = 0 |> Assert.True
    };
    {
        part = "PaneController"
        name = "editor_margin_drop_handler"
        test = fun name ->
(* Create the PaneController. *)
            let pc = get_pc None None None None
(* Create a test MarginDropData. *)
            let drop_data = {
                y_position = 0.0;
                same_margin = false;
                key_states = new System.Windows.DragDropKeyStates ()
                }
(* Attach an event handler to the margin_drop event. *)
            let event_handler_drop_data = ref drop_data
            do
                pc.margin_drop.Add (fun drop_data_ -> do event_handler_drop_data := drop_data_)
(* Call the margin drop handler, with a different margin than the one in the PC. *)
                (drop_data, new TaggerMargin ()) |> pc.test_editor_margin_drop_handler            
(* Verify the same_margin value is still false. *)
                (!event_handler_drop_data).same_margin |> Assert.False
(* Call the margin drop handler with the same margin as the PC. *)
                (drop_data, pc.left_margin) |> pc.test_editor_margin_drop_handler            
(* Verify the same_margin value is true. *)
                (!event_handler_drop_data).same_margin |> Assert.True
    };
    {
        part = "PaneController"
        name = "load_file_into_document"
        test = fun name ->
(* Create a file. *)
            let test_file = CreateTestTextFile name "test"
(* Load the file into a document and verify the results. *)
            let document = test_file |> PaneController.load_file_into_document
            do
                document.IsSome |> Assert.True
                document.Value.Text = "test" |> Assert.True
    };
    {
        part = "PaneController"
        name = "close_tab"
        test = fun name ->
(* Create three files. *)
            let test_file = CreateTestTextFileWithNumber name 1 "test"
            let test_file_2 = CreateTestTextFileWithNumber name 2 "test2"
            let test_file_3 = CreateTestTextFileWithNumber name 3 ""
(* Create a TabControl and add tabs for the files. *)
            let tabs = new TaggerTabControl ()
            let tab0 = (tabs.add_tab test_file).Value
            let tab1 = (tabs.add_tab test_file_2).Value
            let tab2 = (tabs.add_tab test_file_3).Value
(* Create the PaneController, using the TabControl. *)
            let pc = get_pc None (Some tabs) None None
(* Select the third tab. *)
            do tab2.select ()
(* Change the text for the third file in the editor. *)
            let editor = pc.editor
            do
                editor.Text <- "test3"
(* Close the second tab. *)
                Assert.True <| pc.close_tab test_file_2
(* Make sure the editor still is enabled and has the third file loaded. *)
                Assert.True editor.IsEnabled
                Assert.True (editor.file.IsSome && editor.file.Value = test_file_3)
                Assert.True (editor.Text = "test3")
(* Now close the third tab. *)
                Assert.True <| pc.close_tab test_file_3
(* Verify the changes to the third file were saved. *)
                Assert.True (File.ReadAllText test_file_3 = "test3")
(* Make sure the first tab is now selected. *)
                Assert.True tab0.IsSelected
(* Make sure the editor is still enabled and has the first file loaded. *)
                Assert.True editor.IsEnabled
                Assert.True (editor.file.IsSome && editor.file.Value = test_file)
                Assert.True (editor.Text = "test")
(* Now close the first tab. *)
                Assert.True <| pc.close_tab test_file
(* Verify that the editor is disabled, has its file set to None, and its text set to empty. *)
                Assert.True (editor_is_empty pc.editor)
    };
(* Try to close the tab associated with a read-only file. *)
    {
        part = "PaneController"
        name = "close_tab_error"
        test = fun name ->
(* Create a file to open in the editor. *)
            let test_file = CreateTestTextFile name "test"
(* Create the PaneController. *)
            let pc = get_pc None None None None
            do
(* Add a tab for the file and open it in the editor. *)
                pc.add_and_select_tab test_file |> Assert.True
(* Set the file as read-only. *)
                File.SetAttributes (test_file, FileAttributes.ReadOnly)
(* Try to close the tab, which will try to save the file. *)
                Assert.False <| pc.close_tab test_file
(* Verify that the file property has not changed. *)
            let editor = pc.editor
            do
                Assert.True (editor.file.IsSome && editor.file.Value = test_file)
(* Verify that the text has not changed. *)
                Assert.True (editor.Text = "test")
(* Verify that the editor is not disabled. *)
                Assert.True editor.IsEnabled
    };
    {
        part = "PaneController"
        name = "close_file"
        test = fun name ->
(* Create a file. *)
            let test_file = CreateTestTextFile name ""
(* Create the PaneController. *)
            let pc = get_pc None None None None
(* Add a tab for the file and open it in the editor. *)
            do
                pc.add_and_select_tab test_file |> Assert.True
(* Change the text for the file currently open in the editor. *)
                pc.editor.Text <- "test"
(* Close the file. *)
                Assert.True <| pc.close_file ()
(* Verify that the file was saved. *)
                Assert.True (File.ReadAllText test_file = "test")
(* Verify that the editor is disabled, has its file set to None, and its text set to empty. *)
                Assert.True (editor_is_empty pc.editor)
    };
(* Unfortunately, this test seems to be mostly useless because, without UI, the vertical position is always 0. *)
    {
        part = "PaneController"
        name = "close_file_save_vertical_positions"
        test = fun name ->
            let rec make_newlines string count =
                if count = 0 then string
                else make_newlines (sprintf "%s\n" string) (count - 1)
(* Create a file. *)
            let test_file = CreateTestTextFile name ""
(* Create the PaneController. *)
            let pc = get_pc None None None None
(* Add a tab for the file and open it in the editor. *)
            let editor = pc.editor
            do
                pc.add_and_select_tab test_file |> Assert.True
(* Change the text for the file currently open in the editor. *)
                editor.Text <- make_newlines "" 100
(* Scroll to the end. *)
                editor.ScrollToEnd ()
(* Get the vertical position. *)
            let vertical_position = editor.VerticalOffset
            printfn "Vertical position: %f" vertical_position
(* Close the tab. *)
            do
                pc.close_tab test_file |> Assert.True
(* Verify the vertical positions map has an entry for this file and it matches the vertical position. *)
                match pc.test_vertical_positions.TryGetValue test_file with
                | true, result ->
                    let compare = (result = vertical_position)
                    do Assert.True compare
                | _ -> do Assert.True false
(* Open the file. *)
                pc.add_and_select_tab test_file |> Assert.True
(* Verify the vertical position has been restored. *)
                Assert.True (editor.VerticalOffset = vertical_position)
    };
    {
        part = "PaneController"
        name = "add_and_select_tab"
        test = fun name ->
(* Create a file. *)
            let test_file = CreateTestTextFile name "test"
(* Create a MainController. *)
            let pc = get_pc None None None None
(* Add a tab for the file and open it in the editor. *)
            do pc.add_and_select_tab test_file |> Assert.True
(* Verify that the tab control has one tab, that it's for the test file, and that it's selected. *)
            let tabs = pc.tabs
            do Assert.True (tabs.Items.Count = 1)
            let tab = tabs.Items.[0] :?> TaggerTabItem
            do
                Assert.True (tab.file = test_file)
                Assert.True tab.IsSelected
(* This exceeds the scope of the class's responsibility. *)
(* Verify that the editor has the file open. *)
            let editor = pc.editor
            do
                Assert.True (editor.file.IsSome && editor.file.Value = test_file)
(* Verify the editor is enabled. *)
                Assert.True editor.IsEnabled
(* Verify the text. *)
                Assert.True (editor.Text = "test")
    };
    ]
//#endregion