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

module TagControllerTest

// ConcurrentDictionary
open System.Collections.Concurrent
// File
open System.IO
// MenuItem
open System.Windows.Controls

open Xunit
open ICSharpCode.AvalonEdit

// TaggerTextEditor, TaggerTabControl, TaggerMargin
open TaggerControls
// PaneController
open PaneController
// TagController
open TagController
// FindInProjectWindow
open FindInProjectWindow
// DocumentPosition
open AddOnServerController
open TestHelpers

(*
Log events:
x CopyTextSamePaneError
x MoveTextSamePaneError
x CopyTextDifferentPaneError
x MoveTextDifferentPaneError
N MoveTextToClosedFileError (currently, this is not used)
x OpenFileError
x    insert_text_at_end_of_closed_file
N    find_words_in_files (currently, this is not used)
x    find_word_in_files
x    find_all_tags_in_files
N AddTagWindowError (currently, we do not know how to trigger this)

Functions:
N swap
N pane_to_opposite_pane
N pane_to_pcs
N pane_to_opposite_pcs
N pane_to_pc
N pane_to_opposite_pc
N pane_to_editors
N pane_to_editor
N pc_to_open_file
N editor_to_pc
N get_pane_with_focus (UI function)
N get_checksum_single_editor
N get_checksum_both_editors
N get_checksum_single_pane
N get_checksum_both_panes
N get_checksum_closed_file
x insert_text_at_end_of_closed_file
x get_next_tag_number
x replace_built_in_tags
x add_tags_to_mru
N add_tags_helper (just calls replace_built_in_tags and add_tags_to_mru, which are tested.)
N add_tags_to_lines
x tag_lines_helper
N tag_lines (UI function)
x get_lines_to_highlight
/ check_copy_checksum_single_pane (in copy_lines_same_pane)
/ check_move_checksum_single_pane (in move_lines_same_pane)
x copy_lines_same_pane
x move_lines_same_pane
N drop_helper_same_pane (just calls copy_lines_same_pane/move_lines_same_pane/tag_lines)
/ check_copy_checksum_both_panes (in copy_lines_different_pane)
/ check_move_checksum_both_panes (in move_lines_different_pane and move_lines_to_open_file, but not move_lines_to_closed_file)
x copy_lines_different_pane
x move_lines_different_pane
N drop_helper_different_pane (just calls copy_lines_different_pane/move_lines_different_pane/tag_lines)
N margin_drop_helper (UI function)
x move_lines_to_open_file
x move_lines_to_closed_file
N move_lines (just calls move_lines_to_open_file or move_lines_to_closed_file)
x add_files
x get_pane_to_open_file
N show_text (UI function)
x find_file_in_panes
x find_word_in_files
x show_find_in_project_result_helper
N show_find_in_project_result (just calls show_find_in_project_result_helper and show_text)
/ show_find_in_project_dialog_helper (see test)
N show_find_in_project_dialog (just calls find_word_in_files and show_find_in_project_dialog_helper)
x find_all_tags_in_files
N find_all_tags_in_project (just calls find_all_tags_in_files)
 get_find_in_project_menu_item
 get_move_to_menu_item
 get_add_tag_menu_item
 get_word_count_menu_item
N show_open_url_dialog (UI function)
 get_add_on_commands_menu_item
 get_find_all_tags_menu_item
x get_context_menu
N show_context_menu (UI function)
N get_pane_and_position (calls get_pane_with_focus, a UI function)
x copy_text_helper
x prepare_text_to_copy
x prepare_url_to_copy

Methods:
x find_all_tags_in_closed_files
x open_file
N right_click (just calls show_find_in_project_dialog, get_context_menu, and show_context_menu)
N margin_right_click (just calls get_context_menu and show_context_menu)
N get_find_in_project_word (UI function)
N show_tag_completion_window (just calls Editor.show_tag_completion_window)
N tag_selected_handler (just calls List.move_to_head)
N find_in_project (just calls show_find_in_project_dialog)
N copy_text (just calls prepare_text_to_copy, find_word_in_files, get_pane_and_position, and show_find_in_project_dialog_helper)
N copy_url (just calls prepare_url_to_copy, find_word_in_files, get_pane_and_position, and show_find_in_project_dialog_helper)

Properties:
N built_in_tags
N tag_number
N move_to_mru
N tag_mru

Events:
x _save_project (in tag_lines)
 _expand_right_sidebar
 _command_sent
*)

(* Helper functions. *)
//#region
/// <summary>Helper function that returns a new TagController.</summary>
let get_tc () =
    let vertical_positions = new ConcurrentDictionary<string, float> () |> ref
    let documents = new DocumentMap () |> ref
(* Gets a new PaneController. *)
    let get_pc () = new PaneController (
        new TaggerTextEditor (),
        new TaggerTabControl (),
        new TaggerMargin (),
        new TaggerMargin (),
        new System.Windows.Controls.TextBlock (),
        vertical_positions,
        documents
        )
(* TagController requires a TaggerConfig, so we borrow the get_default_config function from ConfigTest. *)
    let config = ConfigTest.get_default_config ()
    new TagController (get_pc (), get_pc (), documents, config)

/// <summary>Helper function that returns a TextViewPosition based on line (1) and column (2).</summary>
let get_position (line : int) column = new TextViewPosition (line, column)
//#endregion

type TagControllerTest () =
    interface ITestGroup with

(* Tests that log. *)
//#region
    member this.tests_log with get () = [
    {
        part = "TagController"
        name = "CopyTextSamePaneError"
        test = fun name ->
            let tc = get_tc ()
            let pc = tc.test_left_pc
            let test_file = CreateTestTextFile name "0123"
            do
(* Open the file. *)
                pc.test_open_file test_file |> ignore
(* The file has length 4. Here, we assume it had length 2 before the copy, and we copied text with length 2. *)
                tc.test_check_copy_checksum_single_pane pc 2 2 = true |> Assert.True
(* This also logs an error. *)
                tc.test_check_copy_checksum_single_pane pc 0 2 = false |> Assert.True
    };
    {
        part = "TagController"
        name = "MoveTextSamePaneError"
        test = fun name ->
            let tc = get_tc ()
            let pc = tc.test_left_pc
            let test_file = CreateTestTextFile name "0123"
            do
(* Open the file. *)
                pc.test_open_file test_file |> ignore
(* The file has length 4. Here, we assume it had length 4 before the move. *)
                tc.test_check_move_checksum_single_pane pc 4 = true |> Assert.True
(* This also logs an error. *)
                tc.test_check_move_checksum_single_pane pc 2 = false |> Assert.True
    };
    {
        part = "TagController"
        name = "CopyTextDifferentPaneError"
        test = fun name ->
            let tc = get_tc ()
            let left_pc = tc.test_left_pc
            let right_pc = tc.test_right_pc
            let test_file_1 = CreateTestTextFile name "0123"
            let test_file_2 = CreateTestTextFile name "0123"
            do
(* Open the files. *)
                left_pc.test_open_file test_file_1 |> ignore
                right_pc.test_open_file test_file_2 |> ignore
(* The files have length 8. Here, we assume they had length 4 before the copy, and we copied text with length 4. *)
                tc.test_check_copy_checksum_both_panes left_pc right_pc 4 4 = true |> Assert.True
(* This also logs an error. *)
                tc.test_check_copy_checksum_both_panes left_pc right_pc 0 4 = false |> Assert.True
    };
    {
        part = "TagController"
        name = "MoveTextDifferentPaneError"
        test = fun name ->
            let tc = get_tc ()
            let left_pc = tc.test_left_pc
            let right_pc = tc.test_right_pc
            let test_file_1 = CreateTestTextFile name "0123"
            let test_file_2 = CreateTestTextFile name "0123"
            do
(* Open the files. *)
                left_pc.test_open_file test_file_1 |> ignore
                right_pc.test_open_file test_file_2 |> ignore
(* The files have length 8. Here, we assume they had length 8 before the move. *)
                tc.test_check_move_checksum_both_panes left_pc right_pc 8 = true |> Assert.True
(* This also logs an error. *)
                tc.test_check_move_checksum_both_panes left_pc right_pc 4 = false |> Assert.True
    };
(* Try to insert text at the end of a non-existent file. *)
    {
        part = "TagController"
        name = "OpenFileError"
        test = fun name ->
            let tc = get_tc ()
            do
(* If insert_text_at_end_of_closed_file does not find the file in the document map, it calls PaneController.load_file_into_document. If that fails to read the file, it returns false and insert_text_at_end_of_closed_file stops. So in order to test OpenFileError for insert_text_at_end_of_closed_file, we need to add the file to the document map. *)
                tc.test_document_map.AddOrUpdate (":", (new Document.TextDocument (), System.DateTime.Now), (fun _ _ -> new Document.TextDocument (), System.DateTime.Now)) |> ignore
                tc.test_insert_text_at_end_of_closed_file "test" ":"
    };
(* Try to find a word in a non-existent file. *)
    {
        part = "TagController"
        name = "OpenFileError"
        test = fun name ->
            let tc = get_tc ()
            do tc.test_find_word_in_files "a" [":"] |> ignore
    };
(* Try to find all tags in a non-existent file. *)
    {
        part = "TagController"
        name = "OpenFileError"
        test = fun name ->
            let tc = get_tc ()
            do tc.test_find_all_tags_in_files [":"] |> ignore
    };
    ]
//#endregion

    member this.tests_throw with get () = []

(* Tests that don't log. *)
//#region
    member this.tests_no_log with get () = [
    {
        part = "TagController"
        name = "insert_text_at_end_of_closed_file"
        test = fun name ->
            let test_file = CreateTestTextFile name ""
            let tc = get_tc ()
            do
                tc.test_insert_text_at_end_of_closed_file "test" test_file
(* Verify the contents of the file. *)
                test_file |> File.ReadAllText = "\ntest" |> Assert.True
(* Verify the contents of the document. *)
            match tc.test_document_map.TryGetValue test_file with
            | true, (document, _) -> do document.Text = "\ntest" |> Assert.True
            | _ -> true |> Assert.False
    };
    {
        part = "TagController"
        name = "get_next_tag_number"
        test = fun name ->
            let tc = get_tc ()
            do
(* The starting tag number is 0. *)
                tc.test_get_next_tag_number () = TagWithoutSymbol "0" |> Assert.True
(* Verify the tag number did not change. *)
                tc.tag_number = 0 |> Assert.True
(* get_next_tag_number does not add the tag to the Tag MRU, so we need to do that. *)
                tc.tag_mru <- [TagWithoutSymbol "0"]
(* Get another tag number. Since 0 has been added to the Tag MRU, the tag number should increment. *)
                tc.test_get_next_tag_number () = TagWithoutSymbol "1" |> Assert.True
(* Verify the tag number changed. *)
                tc.tag_number = 1 |> Assert.True
(* Change the Tag MRU to cause collisions. *)
                tc.tag_mru <- ["1"; "2"; "3"] |> List.map TagWithoutSymbol
(* Get another tag number. Verify the collisions are avoided. *)
                tc.test_get_next_tag_number () = TagWithoutSymbol "4" |> Assert.True
(* Verify the tag number changed. *)
                tc.tag_number = 4 |> Assert.True
    };

    {
        part = "TagController"
        name = "replace_built_in_tags"
        test = fun name ->
            let tc = get_tc ()
(* Start with the built-in tags list. Add another tag that isn't built-in. *)
            let tags = TagWithoutSymbol "TestTag" :: TagController.built_in_tags
(* Get the updated tag lists and tag number. *)
            let tags_with_built_in_tags, tags_without_built_in_tags = tc.test_replace_built_in_tags tags
            do
(* The tag list with built-in tags should contain the non-built-in tag we added, the built-in tags, and the replacement values. *)
                tags_with_built_in_tags = (["TestTag"; "zzNext"; "0"] |> List.map TagWithoutSymbol) |> Assert.True
(* The tag list without built-in tags should contain only the non-built-in tag we added and the replacement values for the built-in tags. *)
                tags_without_built_in_tags = (["TestTag"; "0"] |> List.map TagWithoutSymbol) |> Assert.True
    };
    {
        part = "TagController"
        name = "add_tags_to_mru"
        test = fun name ->
            let tc = get_tc ()
(* Make sure the tag symbol is set to the same value used by the test. *)
            do TagInfo.tag_symbol <- "#"
            let tags = [TagWithoutSymbol "TestTag"]
            do
                tags |> tc.test_add_tags_to_mru
(* Verify that the tags were added to the tag MRU and the tag symbols were removed. *)
                tc.tag_mru = [TagWithoutSymbol "TestTag"] |> Assert.True
    };
    {
        part = "TagController"
        name = "tag_lines_helper"
        test = fun name ->
            let tc = get_tc ()
(* TagController.add_tags_to_lines checks that a file is open in the editor, so we need to create a file - simply setting the editor text is not enough. *)
            let test_file_1 = CreateTestTextFileWithNumber name 1 "Line0\nLine1\nLine2"
            let test_file_2 = CreateTestTextFileWithNumber name 2 "Line0\nLine1\nLine2"
            let left_editor = tc.test_left_pc.editor
            let right_editor = tc.test_right_pc.editor
            do
(* 1. Tag lines in different panes. *)
(* Open the first file in the left editor. *)
                tc.test_left_pc.test_open_file test_file_1 |> ignore
(* Open the second file in right editor. *)
                tc.test_right_pc.test_open_file test_file_2 |> ignore
(* Tag lines 0 and 1 in the left editor and lines 0 and 1 in the right editor. *)
                tc.test_tag_lines_helper tc.test_left_pc (Some tc.test_right_pc) [left_editor.Document.Lines.[0]; left_editor.Document.Lines.[1]] [right_editor.Document.Lines.[0]; right_editor.Document.Lines.[1]] [TagWithoutSymbol "0"]
(* Verify the tags were added. *)
                left_editor.Text = "Line0#0\nLine1#0\nLine2" |> Assert.True
                right_editor.Text = "Line0#0\nLine1#0\nLine2" |> Assert.True
(* 2. Tag lines in the same pane. *)
                tc.test_tag_lines_helper tc.test_left_pc None [left_editor.Document.Lines.[1]; left_editor.Document.Lines.[2]] [] [TagWithoutSymbol "1"]
(* Verify the tags were added. *)
                left_editor.Text = "Line0#0\nLine1#0#1\nLine2#1" |> Assert.True
    };
    {
        part = "TagController"
        name = "get_lines_to_highlight"
        test = fun name ->
            let tc = get_tc ()
            let editor = tc.test_left_pc.editor
(* Helper function to get the text of a line. *)
            let lines_to_text lines = lines |> List.map (fun (line : Document.DocumentLine) ->
                editor.Document.GetText (line.Offset, line.TotalLength))
(* Helper function to call get_lines_to_highlight with the current document. *)
            let get_lines = tc.test_get_lines_to_highlight editor.Document
(* Helper function to verify results of get_lines_to_highlight. *)
            let test_get_lines_to_highlight source_line_numbers target_line_numbers source_lines_text target_lines_text =
                let source_lines, target_lines = get_lines source_line_numbers target_line_numbers true
                do
                    source_lines |> lines_to_text = source_lines_text |> Assert.True
                    target_lines |> lines_to_text = target_lines_text |> Assert.True
            do
                editor.Text <- "Line0\nLine1\nLine2\nLine3"
(* This is no longer possible, because source_highlight_start_line_number (from which we derive source_highlight_start_line_index) is decremented for every source line that precedes the target line. *)
(* Pass source and target lines such that source_highlight_start_line_index + source_highlight_lines_length exceeds Document.Lines.Count. *)
//                get_lines [1; 2; 3] [4] = ([], []) |> Assert.True
(* Pass valid source and target lines. Note we do not actually move the lines, so the lines returned by get_lines_to_highlight still contain the original text. *)
(* Drop line numbers 2 and 3 on 1. Result: source highlight for line numbers 1 and 2, target highlight for line number 3. *)
                test_get_lines_to_highlight [2; 3] [1] ["Line0\n"; "Line1\n"] ["Line2\n"]
(* Drop line numbers 1 and 3 on 4. Result: source highlight for line numbers 2 and 3, target highlight for line number 4. *)
                test_get_lines_to_highlight [1; 3] [4] ["Line1\n"; "Line2\n"] ["Line3"]
(* Drop line numbers 1 and 3 on 2. Result: source highlight for line numbers 1 and 2, target highlight for line number 3. *)
                test_get_lines_to_highlight [1; 3] [2] ["Line0\n"; "Line1\n"] ["Line2\n"]
(* Drop line numbers 2 and 4 on 1. Result: source highlight for line numbers 1 and 2, target highlight for line number 3. *)
                test_get_lines_to_highlight [2; 4] [1] ["Line0\n"; "Line1\n"] ["Line2\n"]
    };
(* copy_lines_same_pane calls check_copy_checksum_single_pane. If that fails, it logs an error, which fails this test. *)
    {
        part = "TagController"
        name = "copy_lines_same_pane"
        test = fun name ->
            let tc = get_tc ()
            let pc = tc.test_left_pc
            let editor = pc.editor
            do
                editor.Text <- "Line0\nLine1\nLine2"
(* Copy lines 1 and 2 to line 0. *)
                tc.test_copy_lines_same_pane pc [editor.Document.Lines.[1]; editor.Document.Lines.[2]] (editor.Document.Lines.[0])
(* Verify the lines were copied. *)
                editor.Text = "Line1\nLine2\nLine0\nLine1\nLine2" |> Assert.True
    };
(* move_lines_same_pane calls check_move_checksum_single_pane. If that fails, it logs an error, which fails this test. *)
    {
        part = "TagController"
        name = "move_lines_same_pane"
        test = fun name ->
            let tc = get_tc ()
            let pc = tc.test_left_pc
            let editor = pc.editor
            do
                editor.Text <- "Line0\nLine1\nLine2"
(* Move lines 1 and 2 to line 0. *)
                tc.test_move_lines_same_pane pc [editor.Document.Lines.[1]; editor.Document.Lines.[2]] (editor.Document.Lines.[0])
(* Verify the lines were moved. *)
                editor.Text = "Line1\nLine2\nLine0\n" |> Assert.True
    };
(* copy_lines_different_pane calls check_copy_checksum_both_panes. If that fails, it logs an error, which fails this test. *)
    {
        part = "TagController"
        name = "copy_lines_different_pane"
        test = fun name ->
            let tc = get_tc ()
            let left_editor = tc.test_left_pc.editor
            let right_editor = tc.test_right_pc.editor
            do
                left_editor.Text <- "Line0\nLine1\nLine2"
(* Copy lines 0 and 1 from the left editor to line 0 in the right editor. *)
                tc.test_copy_lines_different_pane tc.test_left_pc tc.test_right_pc [left_editor.Document.Lines.[0]; left_editor.Document.Lines.[1]] (right_editor.Document.Lines.[0])
(* Verify the lines were copied. *)
                left_editor.Text = "Line0\nLine1\nLine2" |> Assert.True
                right_editor.Text = "Line0\nLine1\n" |> Assert.True
    };
(* move_lines_different_pane calls check_move_checksum_both_panes. If that fails, it logs an error, which fails this test. *)
    {
        part = "TagController"
        name = "move_lines_different_pane"
        test = fun name ->
            let tc = get_tc ()
            let left_editor = tc.test_left_pc.editor
            let right_editor = tc.test_right_pc.editor
            do
                left_editor.Text <- "Line0\nLine1\nLine2"
(* Move lines 0 and 1 from the left editor to line 0 in the right editor. *)
                tc.test_move_lines_different_pane tc.test_left_pc tc.test_right_pc [left_editor.Document.Lines.[0]; left_editor.Document.Lines.[1]] (right_editor.Document.Lines.[0])
(* Verify the lines were moved. *)
                left_editor.Text = "Line2" |> Assert.True
                right_editor.Text = "Line0\nLine1\n" |> Assert.True
    };
(* move_lines_to_open_file calls check_move_checksum_both_panes. If that fails, it logs an error, which fails this test. *)
    {
        part = "TagController"
        name = "move_lines_to_open_file"
        test = fun name ->
            let tc = get_tc ()
            let test_file_1 = CreateTestTextFileWithNumber name 1 "Line1\nLine2"
            let test_file_2 = CreateTestTextFileWithNumber name 2 ""
            do
(* Open the first file in the left editor. *)
                tc.test_left_pc.test_open_file test_file_1 |> ignore
(* Open the second file in right editor. *)
                tc.test_right_pc.test_open_file test_file_2 |> ignore
(* Move the text from the first file to the second. *)
            let lines = [tc.test_left_pc.editor.Document.Lines.[0]; tc.test_left_pc.editor.Document.Lines.[1]]
            do
                tc.test_move_lines_to_open_file (tc.test_left_pc) (tc.test_right_pc) lines
(* Verify the text was moved. move_lines_to_open_file calls Editor.insert_text_at_end, which adds a newline to the start of the text. *)
                Assert.True (tc.test_left_pc.editor.Text = "")
                Assert.True (tc.test_right_pc.editor.Text = "\nLine1\nLine2")
(* Now move text to a target document that ends in a newline. *)
                tc.test_left_pc.editor.Text <- "Line1\nLine2"
                tc.test_right_pc.editor.Text <- "\n"
(* Move a line from the first file to the second. *)
            let lines = [tc.test_left_pc.editor.Document.Lines.[0]]
            do
                tc.test_move_lines_to_open_file (tc.test_left_pc) (tc.test_right_pc) lines
(* Verify the text was moved. move_lines_to_open_file calls Editor.insert_text_at_end, which adds a newline to the start of the text. *)
                tc.test_left_pc.editor.Text = "Line2" |> Assert.True
                tc.test_right_pc.editor.Text = "\n\nLine1" |> Assert.True
    };
(* move_lines_to_closed_file compares the checksums before and after the move. If that fails, it logs an error, which fails this test. *)
    {
        part = "TagController"
        name = "move_lines_to_closed_file"
        test = fun name ->
            let tc = get_tc ()
            let test_file_1 = CreateTestTextFileWithNumber name 1 "Line1\nLine2"
            let test_file_2 = CreateTestTextFileWithNumber name 2 ""
(* Open the first file in the left editor. *)
            do tc.test_left_pc.test_open_file test_file_1 |> ignore
(* Move the text from the first file to the second. *)
            let lines = [tc.test_left_pc.editor.Document.Lines.[0]; tc.test_left_pc.editor.Document.Lines.[1]]
            do
                tc.test_move_lines_to_closed_file (tc.test_left_pc) test_file_2 lines
(* Verify the text was moved. Note a newline is prepended to the text by TagController.insert_text_at_end_of_closed_file because the target document does not end in a newline, and another is appended by Editor.lines_to_text because we're moving the last line in the source document. *)
                Assert.True (tc.test_left_pc.editor.Text = "")
                Assert.True (File.ReadAllText test_file_2 = "\nLine1\nLine2\n")
(* Move another line to the closed file now that it ends in a newline. *)
                tc.test_left_pc.editor.Text <- "Line3\nLine4"
            let lines = [tc.test_left_pc.editor.Document.Lines.[0]]
            do
                tc.test_move_lines_to_closed_file (tc.test_left_pc) test_file_2 lines
(* Verify the text was moved. Note a newline is not prepended to the text by TagController.insert_text_at_end_of_closed_file, because the target document already ends with a newline. Nor is a newline appended by Editor.lines_to_text, because we're not moving the last line in the source document. *)
                tc.test_left_pc.editor.Text = "Line4" |> Assert.True
                File.ReadAllText test_file_2 = "\nLine1\nLine2\nLine3\n" |> Assert.True
    };
    {
        part = "TagController"
        name = "add_files"
        test = fun name ->
            let tc = get_tc ()
            let test_file_1 = CreateTestTextFileWithNumber name 1 "Line1\nLine2"
            let test_file_2 = CreateTestTextFileWithNumber name 2 ""
            let test_file_3 = CreateTestTextFileWithNumber name 3 ""
            let files = [test_file_1; test_file_2; test_file_3]
            let menu_item = new MenuItem ()
(* Open the first file in the left editor. *)
            do tc.test_left_pc.test_open_file test_file_1 |> ignore
            let lines = [tc.test_left_pc.editor.Document.Lines.[0]; tc.test_left_pc.editor.Document.Lines.[0]]
(* Add the files to the menu item. *)
            do
                tc.test_add_files (tc.test_left_pc) (tc.test_right_pc) menu_item files lines
(* Verify that the files were added to the menu item, except the one currently open in the editor. *)
                Assert.True (menu_item.Items.Count = 2)
                Assert.True ((menu_item.Items.[0] :?> MenuItem).Header.ToString () = Path.GetFileName test_file_2)
                Assert.True ((menu_item.Items.[1] :?> MenuItem).Header.ToString () = Path.GetFileName test_file_3)
    };
    {
        part = "TagController"
        name = "get_pane_to_open_file"
        test = fun name ->
            let tc = get_tc ()
            let test_file = CreateTestTextFile name ""
            do
                tc.test_get_pane_to_open_file LeftOrRightPane.LeftPane test_file = LeftOrRightPane.RightPane |> Assert.True
                tc.test_left_pc.test_open_file test_file |> Assert.True
                tc.test_get_pane_to_open_file LeftOrRightPane.LeftPane test_file = LeftOrRightPane.LeftPane |> Assert.True
    };
    {
        part = "TagController"
        name = "find_file_in_panes"
        test = fun name ->
            let tc = get_tc ()
            let left_editor, right_editor = tc.test_left_pc.editor, tc.test_right_pc.editor
            let left_test_file = CreateTestTextFileWithNumber name 1 ""
            let right_test_file = CreateTestTextFileWithNumber name 2 ""
            do
(* Verify we open the files successfully. *)
                tc.test_left_pc.test_open_file left_test_file |> Assert.True
                tc.test_right_pc.test_open_file right_test_file |> Assert.True
                tc.test_find_file_in_panes left_test_file = Some left_editor |> Assert.True
                tc.test_find_file_in_panes right_test_file = Some right_editor |> Assert.True
                tc.test_find_file_in_panes "deleteme_none" = None |> Assert.True
    };
    {
        part = "TagController"
        name = "find_word_in_files"
        test = fun name ->
            let tc = get_tc ()
(* Find a word in a closed file. *)
            let text0 = "#0 Line 1\nLine 2#0"
            let test_file = CreateTestTextFile name text0
            let results = tc.test_find_word_in_files "#0" [test_file]
(* Verify we found the word in one file. *)
            do results |> List.length = 1 |> Assert.True
            let files = results |> List.head
(* Check the file name. *)
            do files |> fst = test_file |> Assert.True
(* Check the lines in the file. *)
            let lines = files |> snd |> Seq.toList |> List.sortBy (fun result -> result.line_number)
            [
                { file_index = 0; line_number = 1; line_text = "#0 Line 1"; match_case = true; match_whole_word = true; };
                { file_index = 0; line_number = 2; line_text = "Line 2#0"; match_case = true; match_whole_word = false; };
            ] |> compare_lists lines |> Assert.True
(* Open the file, change the text, and find the new text in the open file. *)
            do
                tc.test_left_pc.test_open_file test_file |> ignore
                tc.test_left_pc.editor.insert_text_at_end_of_current_file "#1"
            let results = tc.test_find_word_in_files "#1" [test_file]
(* Verify we found the word in one file. *)
            do results |> List.length = 1 |> Assert.True
            let files = results |> List.head
(* Check the file name. *)
            do files |> fst = test_file |> Assert.True
(* Check the lines in the file. *)
            let lines = files |> snd |> Seq.toList
            [{ file_index = 0; line_number = 3; line_text = "#1"; match_case = true; match_whole_word = true; }] |> compare_lists lines |> Assert.True
    };
    {
        part = "TagController"
        name = "show_find_in_project_result_helper"
        test = fun name ->
            let tc = get_tc ()
            let test_file = CreateTestTextFile name "#0#1"
(* Open a tag that does not appear in any file that is currently open. This causes the file that contains the tag to be opened in the opposite pane. *)
            do tc.test_show_find_in_project_result_helper test_file LeftOrRightPane.LeftPane |> ignore
(* Make sure the file with the tag was added as a tab to the right PaneController and selected. *)
            let result = tc.test_right_pc.tabs.get_tab test_file
            do result.IsSome |> Assert.True
            let tab = fst result.Value
            do tab.IsSelected |> Assert.True
(* Open a tag that does appear in a file that is currently open. This does not open a file. *)
            do tc.test_show_find_in_project_result_helper test_file LeftOrRightPane.RightPane |> ignore
(* Make sure the file is still open in the right pane. *)
            let result = tc.test_right_pc.tabs.get_tab test_file
            do result.IsSome |> Assert.True
            let tab = fst result.Value
            do tab.IsSelected |> Assert.True
(* Make sure the left pane is still empty. *)
            do tc.test_left_pc.tabs.Items.Count = 0 |> Assert.True
    };
(* This is a test of FindInProjectWindow.ShowDialog rather than TagController.show_find_in_project_dialog_helper. I couldn't find a clean way to break up show_find_in_project_dialog_helper so that it just returns the FindInProjectWindow for testing. A single function (FindInProjectWindow.ShowDialog) populates the FindInProjectWindow, shows it, and returns the user's selection. *)
     {
        part = "TagController"
        name = "show_find_in_project_dialog_helper"
        test = fun name ->
            let tc = get_tc ()
            let text_0 = "test#0"
(* 1. Test getting a Find in Project dialog for a target tag that is in a closed file. *)
(* Create a test file that contains a tag. *)
            let test_file = CreateTestTextFile name text_0
(* Get the dialog. *)
            let dialog = new FindInProjectWindow ()
            let matches = tc.test_find_word_in_files "#0" [test_file]
            do dialog.ShowDialog false matches |> ignore
(* Make sure the tab control contains one item, corresponding to the file name (not the path). *)
            let tab_0 = dialog.test_tabs.Items.[0] :?> FindInProjectTab
            do tab_0.Header.ToString () = Path.GetFileName test_file |> Assert.True
(* Make sure the list view contains one item, corresponding to the line that contains the tag. *)
            let lvi_0 = dialog.test_lines.Items.[0] :?> ListViewItem
            do (lvi_0.Content :?> TextBlock).Text.ToString () = text_0 |> Assert.True
(* 2. Test getting a Find in Project dialog for a target tag that is in an open file. *)
(* Open the file in the left editor. *)
            do tc.test_left_pc.test_open_file test_file |> Assert.True
(* Change the text to include a different tag. *)
            let text_1 = "new#1"
            do tc.test_left_pc.editor.Text <- text_1
(* Get the dialog. *)
            let matches_ = tc.test_find_word_in_files "#1" [test_file]
            do dialog.ShowDialog false matches_ |> ignore
(* Again make sure the tab control contains one item, corresponding to the file name (not the path). *)
            let tab1 = dialog.test_tabs.Items.[0] :?> FindInProjectTab
            do tab1.Header.ToString () = Path.GetFileName test_file |> Assert.True
    };
    {
        part = "TagController"
        name = "find_all_tags_in_files"
        test = fun name ->
// TODO2 Add test.
            ()
    };
    {
        part = "TagController"
        name = "get_context_menu"
        test = fun name ->
(* Helper to compare menu item headers to expected headers. *)
            let check_headers (menu : ContextMenu) headers =
                let menu_headers = menu.Items |> Seq.cast<MenuItem> |> Seq.map (fun item -> item.Header) |> Seq.toList
                menu_headers = headers
            let tc = get_tc ()
            let mt = "Move To"
            let at = "Add Tags"
            let wc = "Word Count"
            let fip = "Find in Project"
            let fatip = "Find All Tags in Project"
            let aoc = "Add On Commands"
(* Get the context menu with no word. *)
            let menu = tc.test_get_context_menu LeftOrRightPane.LeftPane [] None [] []
            do check_headers menu [mt; at; wc; fip; fatip] |> Assert.True
(* Get the context menu with a word. *)
            let menu_ = tc.test_get_context_menu LeftOrRightPane.LeftPane [] None ["test"] []
            do check_headers menu_ [mt; at; wc; fip; fatip] |> Assert.True
(* Get the context menu with a link. *)
            let menu_ = tc.test_get_context_menu LeftOrRightPane.LeftPane [] None [] ["http://about:blank"]
            do check_headers menu_ [mt; at; wc; fip; fatip; aoc] |> Assert.True
    };
    {
        part = "TagController"
        name = "copy_text_helper"
        test = fun name ->
            let tc = get_tc ()
            let test_file_1 = CreateTestTextFileWithNumber name 1 ""
            let test_file_2 = CreateTestTextFileWithNumber name 2 ""
            let editor = tc.test_left_pc.editor
            do
                tc.test_left_pc.test_open_file test_file_1 |> ignore
(* Copy text to a nonexistent file, an open file, and a closed file. *)
                tc.test_copy_text_helper "test" ["deleteme_none"; test_file_1; test_file_2] <| (int) DocumentPosition.Bottom
(* Verify the text was added to the open file. *)
                editor.Text = "\ntest" |> Assert.True
(* Verify the text was added to the closed file. *)
                test_file_2 |> File.ReadAllText = "\ntest" |> Assert.True
    };
    {
        part = "TagController"
        name = "prepare_text_to_copy"
        test = fun name ->
            let tc = get_tc ()
            do
                tc.test_prepare_text_to_copy "test" "" [] = "test" |> Assert.True
                tc.test_prepare_text_to_copy "test" "url" [] = "url\ntest" |> Assert.True
                tc.test_prepare_text_to_copy "test" "url" [TagWithoutSymbol "tag"] = "url\ntest#tag" |> Assert.True
    };
    {
        part = "TagController"
        name = "prepare_url_to_copy"
        test = fun name ->
            let tc = get_tc ()
            do
                tc.test_prepare_url_to_copy "test" "" [] = "test" |> Assert.True
                tc.test_prepare_url_to_copy "test" "title" [] = "title\ntest" |> Assert.True
                tc.test_prepare_url_to_copy "test" "title" [TagWithoutSymbol "tag"] = "title\ntest #tag" |> Assert.True
    };
    {
        part = "TagController"
        name = "find_all_tags_in_closed_files"
        test = fun name ->
            let test_file_1 = CreateTestTextFileWithNumber name 1 "#0#1#2"
            let test_file_2 = CreateTestTextFileWithNumber name 2 "#1#2#3"
            let tags = TagController.find_all_tags_in_closed_files [test_file_1; test_file_2]
            tags = (["0"; "1"; "2"; "3"] |> List.map TagWithoutSymbol) |> Assert.True
    }
    {
        part = "TagController"
        name = "open_file"
        test = fun name ->
            let tc = get_tc ()
            let test_file = CreateTestTextFile name "#0"
(* Open the file in the left pane. *)
            do
                tc.test_left_pc.add_and_select_tab test_file |> Assert.True
(* Open the file in the right pane. Verify that we successfully close the file in the left pane and open it in the right pane. *)
                tc.open_file LeftOrRightPane.RightPane test_file |> Assert.True
(* Verify the file is open in the right pane and not the left. *)
                (tc.test_left_pc.tabs.get_tab test_file).IsNone |> Assert.True
                (tc.test_right_pc.tabs.get_tab test_file).IsSome |> Assert.True
    };
    ]
//#endregion