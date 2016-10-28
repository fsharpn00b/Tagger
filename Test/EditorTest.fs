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

module EditorTest

// File
open System.IO
// MessageBox
open System.Windows

open ICSharpCode.AvalonEdit
open Xunit

// Editor
open TaggerControls
open TestHelpers

// TODO3 Put TI, TCCB, and TCW in separate test file.
(*
TagInfo:
x add_tag_symbol_to_start
x remove_tag_symbol_from_start
x whole_string_is_valid_tag_with_symbol
/ find_tags_without_symbol (in find_tags_with_symbol)
x find_tags_with_symbol
x find_all_tags_in_closed_file_with_symbol
x starts_with_valid_tag_no_symbol
x position_to_tag_with_symbol
x position_to_text_bordered_by_whitespace
x find_words
x find_links
x format_temp_tag
x is_temp_tag
/ starts_with_valid_tag_no_symbol_pattern (in starts_with_valid_tag_no_symbol)
/ find_tag_with_symbol_pattern (in find_tags_with_symbol)
/ position_to_tag_with_symbol_pattern (in position_to_tag_with_symbol)
N tag_symbol
N regex_escaped_tag_symbol
N tag_pattern_no_symbol

TagCompletionComboBox:
Functions:
N get_items (currently not used)
N preview_mouse_left_up_handler
N preview_key_down_handler (UI function)
N preview_text_input_handler (UI function)
N text_changed_handler (UI function)
N add_items

Methods:
N OnApplyTemplate (UI function)
N filter
N find_index
N clear

Properties:
N base_items
N base_text
N find
N textbox

Events:
N commit (fired by preview_mouse_left_up_handler, preview_key_down_handler, preview_text_input_handler, and text_changed_handler, which are UI functions)
N cancel (fired by preview_key_down_handler and text_changed_handler, which are UI functions)
N general_error (fired by OnApplyTemplate)

TagCompletionWindow:
Functions:
N activated_handler
N commit_handler
N cancel_handler
N closing_handler (UI function)
N is_keyboard_focus_within_changed_handler (UI function)
N draw_window (UI function)

Methods:
N Update (UI function)
N Show (UI function)
N Hide (UI function)

Properties:

Events:
N commit (fired by commit_handler)
N general_error (re-fired from the TagCompletionComboBox)

TaggerTextEditorColorizer
Methods:
N ColorizeLine (UI function)

TaggerTextEditor:
Functions:
N nullable_to_option
N get_position
x position_to_words_links_tag
x safe_margin_select_from_position (not currently used)
x validate_lines
x validate_line
N draw_lines (UI function)
N clear_selected_lines (just calls draw_lines)
N highlight_selected_lines (just calls draw_lines)
x line_to_start_end_length
/ lines_to_start_end_length (in line_to_start_end_length)
N get_text_line_top_bottom (UI function)
x insert_line_at_offset_in_text
x lines_to_text_helper_cut
x lines_to_text_helper_copy
 offset_to_line_index
 margin_selection_to_clipboard
x move_and_copy_helper
x find_text
x find_tags_in_line
/ find_text_in_line (in find_text_in_closed_file and find_text_in_open_file)
N mouse_hover_helper
x mouse_right_up_helper
N scroll_changed_debug
N set_drag_scroll_timer_intervals
N end_drag_helper
N drag_drop_helper (UI function)
x save_timer_tick_handler
N mouse_hover_handler
N mouse_move_handler
N mouse_right_up_handler (UI function)
N drag_leave_handler (just calls end_drag_helper)
N query_continue_drag_handler (UI function)
N drag_over_handler (UI function)
N drop_handler (UI function)
N mouse_scroll_handler (UI function)
N scroll_changed_handler (UI function)
N get_scroll_viewer (UI function)
N page_up_or_down_helper (UI function)
N page_up_or_down (UI function)
N preview_key_down_handler (UI function)
N search_key_down_handler (UI function)
 document_changed_handler
 documentchanged_handler
x text_changed_handler
N text_entered_handler (UI function)

Methods:
x save_file
x open_file
x rename_file
x close_file
N position_to_y_position (UI function)
N add_newline_to_start_of_text
N add_newline_to_end_of_text
x update_selected_lines
N safe_y_position_to_line (UI function)
N lines_to_text (just calls lines_to_text_helper_cut and lines_to_text_helper_copy)
x insert_line_at_line
x insert_text_at_start_of_current_file
x insert_text_at_end_of_current_file
x insert_text_at_cursor_in_current_file
x move_lines
x copy_lines
N get_line_top_bottom (UI function)
N get_lines_top_bottom (UI function)
N scroll_to_offset (UI function)
N scroll_line (UI function)
x safe_line_number_to_line
x safe_position_to_line
x add_tags_to_lines
x find_text_in_closed_file
N find_all_tags_in_closed_file_with_symbol (just calls find_tags_in_line)
x find_text_in_open_file
N find_all_tags_in_open_file_with_symbol (just calls find_tags_in_line)
N stop_drag_scroll_timers
N show_tag_completion_window
N insert_tag_at_cursor

Properties:
N file
N save_timer_interval
N mouse_hover_delay
N mouse_scroll_speed
N mouse_drag_scroll_speed
N case_sensitive_find
N selected_lines
N most_recently_selected_line

Events:
N _general_error (in get_scroll_viewer)
x _save_file_error (in save_timer_tick_handler)
x _right_click (in mouse_right_up_helper)
N _find_in_project (fires in preview_key_down_handler, which is a UI function)
N _selection_changed
N _margin_drag (fires in drag_over_handler, which is a UI function)
N _margin_drop (fires in drop_handler, which is a UI function)
N _end_drag (fires in drag_leave_handler, which is a UI function)
N _scroll_changed (fires in scroll_changed_handler, which is a UI function)
N _extent_height_changed (fires in scroll_changed_handler, which is a UI function)
N _tag_symbol_entered (fires in text_entered_handler, which is a UI function)
N _tag_selected (fires in response to TagCompletionWindow.commit, which fires in TagCompletionWindow.commit_handler, which fires in response to TagCompletionComboBox.commit, which fires in TagCompletionComboBox.preview_mouse_left_up_handler, which is a UI function.)
*)

(* Helper functions. *)
//#region
/// <summary>Helper function that determines whether an editor (1) is empty. Returns true if empty; otherwise, false.</summary>
let editor_is_empty (editor : TaggerTextEditor) =
    editor.IsEnabled = false &&
    editor.file.IsNone &&
    editor.Text.Length = 0

/// <summary>Helper function that returns a TextViewPosition based on line (1) and column (2).</summary>
let get_position (line : int) column = new TextViewPosition (line, column)

/// <summary>Helper function that describes a TextAnchor. Return unit.<summary>
let show_anchor (anchor : Document.TextAnchor) =
    let message =
        if anchor.IsDeleted then "Anchor is deleted."
        else sprintf "Anchor is not deleted.\nOffset: %d" anchor.Offset
    do message |> MessageBox.Show |> ignore
//#endregion

type EditorTest () =
    interface ITestGroup with

    member this.tests_log with get () = []

    member this.tests_throw with get () = []

(* Tests that don't log. *)
    member this.tests_no_log with get () = [
(* Experiments *)
//#region
(*
(* Test to see whether Seq.cast raises an exception if given the wrong type. *)
    {
        part = "Editor"
        name = "seq_cast_wrong_type"
        test = fun name ->
(* We most often use Seq.cast with an ItemCollection, but we can't create it directly. *)
            let lv = new System.Windows.Controls.ListView ()
            do
                lv.Items.Add ("test1") |> ignore
                lv.Items.Add ("test2") |> ignore
(* Try an incorrect cast. *)
            let s = lv.Items |> Seq.cast<int>
(* Sequence length is 2. Looking at the sequence in quick watch shows an invalid cast exception. But no exception is raised here. That might be because getting the length doesn't actually evaluate the items. *)
            do s |> Seq.length |> sprintf "seq_cast_wrong_type: seq length: %d" |> MessageBox.Show |> ignore
(* Yes, this raises an exception. *)
            do s |> Seq.iter (fun item -> do item |> sprintf "seq_cast_wrong_type: %d" |> MessageBox.Show |> ignore)
    }
(* In open_file, we might have an exception raised inside a locked section. Make sure that an exception releases the lock. *)
(* Yes, it does. *)
    {
        part = "Editor"
        name = "exception_inside_lock"
        test = fun name ->
            let lock_var = ref 0
(* Catch an exception that is raised inside a locked section. To help verify that the exception lets us exit the locked section, get the message from the exception. *)
            let error =
                try
                    do lock lock_var (fun () -> raise <| new System.Exception (name))
(* If the exception isn't raised, return an empty string. *)
                    ""
                with | ex -> ex.Message
(* Verify the exception was raised. *)
            error.Length > 0 |> Assert.True
(* Make sure we can reacquire the lock. *)
            do lock lock_var (fun () -> error |> MessageBox.Show |> ignore)
    };
(* Test to see whether a TextAnchor survives being cut and paste. *)
(* No, it doesn't. When the text is cut, the anchor is deleted and the offset becomes 0. Then, when the text is pasted again, it's seen as an insertion and the offset is simply pushed forward ahead of the insertion. *)
    {
        part = "Editor"
        name = "text_anchor_copy_paste"
        test = fun name ->
            let editor = new TaggerTextEditor ()
            do editor.Text <- "1234567890"
(* This anchor should be at the number 3. *)
            let anchor = editor.Document.CreateAnchor 2
            do
                anchor.SurviveDeletion <- true
(* Describe the anchor. *)
                anchor |> show_anchor
(* Cut the text containing the anchor. *)
(* This should select "1234". *)
                editor.Select (0, 4)
                editor.Cut ()
(* Describe the anchor. *)
                anchor |> show_anchor
(* Paste the text. *)
(* Describe the anchor. *)
                editor.Paste ()
                anchor |> show_anchor
    };
(* Test to see whether TextAnchors persist beyond the document closing. *)
(* No, it doesn't. When the file is closed, the anchor is deleted and the offset becomes 0. Then, when the first file is loaded again, it's seen as an insertion and the offset is at the end of the file. *)
    {
        part = "Editor"
        name = "persist_text_anchor"
        test = fun name ->
(* Create files to open in the editor. *)
            let test_file_1 = CreateTestTextFileWithNumber name 1 "anchor"
            let test_file_2 = CreateTestTextFileWithNumber name 2 ""
            let editor = new TaggerTextEditor ()
(* Open the file. *)
            do (editor.open_file test_file_1).IsNone |> Assert.True
(* This anchor should be at the letter "c" in line 1. *)
            let anchor = editor.Document.CreateAnchor 2
            do
                anchor.SurviveDeletion <- true
(* Describe the anchor. *)
                anchor |> show_anchor
(* Close the file and open another. *)
                editor.close_file ()
                (editor.open_file test_file_2).IsNone |> Assert.True
(* Verify the file contents. *)
                editor.Text.Length = 0 |> Assert.True
(* Describe the anchor. *)
                anchor |> show_anchor
(* Close the file and open the first file again. *)
                editor.close_file ()
                (editor.open_file test_file_1).IsNone |> Assert.True
(* Verify the file contents. *)
                editor.Text = "anchor" |> Assert.True
(* Describe the anchor. *)
                anchor |> show_anchor
(* Insert text, which should move the anchor. *)
                editor.Document.Insert (0, "This is the ")
(* Describe the anchor. *)
                anchor |> show_anchor
    };
*)
//#endregion

(* TagInfo *)
//#region
    {
        part = "TagInfo"
        name = "add_tag_symbol_to_start"
        test = fun name ->
(* Make sure the tag symbol is set to the same value used by the test. *)
            do
                TagInfo.tag_symbol <- "#"
                "0" |> TagWithoutSymbol |> TagInfo.add_tag_symbol_to_start = TagWithSymbol "#0" |> Assert.True
                "#0" |> TagWithoutSymbol |> TagInfo.add_tag_symbol_to_start = TagWithSymbol "#0" |> Assert.True
    };
    {
        part = "TagInfo"
        name = "remove_tag_symbol_from_start"
        test = fun name ->
(* Make sure the tag symbol is set to the same value used by the test. *)
            do
                TagInfo.tag_symbol <- "#"
                "#0" |> TagWithSymbol |> TagInfo.remove_tag_symbol_from_start = TagWithoutSymbol "0" |> Assert.True
                "0" |> TagWithSymbol |> TagInfo.remove_tag_symbol_from_start = TagWithoutSymbol "0" |> Assert.True
    };
    {
        part = "TagInfo"
        name = "whole_string_is_valid_tag_with_symbol"
        test = fun name ->
(* Make sure the tag symbol is set to the same value used by the test. *)
            do
                TagInfo.tag_symbol <- "#"
(* The tag pattern is #\w+. *)
                TagWithSymbol "" |> TagInfo.whole_string_is_valid_tag_with_symbol |> Assert.False
                TagWithSymbol "1" |> TagInfo.whole_string_is_valid_tag_with_symbol |> Assert.False
                TagWithSymbol "#" |> TagInfo.whole_string_is_valid_tag_with_symbol |> Assert.False
                TagWithSymbol "#." |> TagInfo.whole_string_is_valid_tag_with_symbol |> Assert.False
                TagWithSymbol "##" |> TagInfo.whole_string_is_valid_tag_with_symbol |> Assert.False
(* \w+ includes _. *)
                TagWithSymbol "#_" |> TagInfo.whole_string_is_valid_tag_with_symbol |> Assert.True
                TagWithSymbol "#a" |> TagInfo.whole_string_is_valid_tag_with_symbol |> Assert.True
                TagWithSymbol "#A" |> TagInfo.whole_string_is_valid_tag_with_symbol |> Assert.True
                TagWithSymbol "#1" |> TagInfo.whole_string_is_valid_tag_with_symbol |> Assert.True
    };
    {
        part = "TagInfo"
        name = "find_tags_with_symbol"
        test = fun name ->
(* Make sure the tag symbol is set to the same value used by the test. *)
            do TagInfo.tag_symbol <- "#"
(*
0 Test a tag at the start of a line and the start of the string.
1 Test a tag at the end of a line.
2 Test a tag at the start of a line.
3-4 Test two tags in the same word, with non-whitespace directly preceding them.
5 Test a tag between two invalid tags.
6-7 Test two tags in the same word that is a URL with http.
8 Test a tag in a word that is a URL with https.
9-10 Test two tags in the same work, with non-whitespace directly preceding them, at the end of a line and the end of the string.
*)
            let text = "#0 Line 1#1\n#2 Line 2 test#3#4 ##5#. http://test#6#7 https://#8 2#9#10"
            let results = TagInfo.find_tags_without_symbol text |> List.sort
(* Note line numbers start from 1. *)
            do results |> List.length = 8 |> Assert.True
(* Make sure both lists are sorted. The values are strings, so "10" comes before "2". *)
            let results2 = ["0"; "1"; "2"; "3"; "4"; "5"; "9"; "10"] |> List.sort |> List.map TagWithoutSymbol
            do compare_lists results results2 |> Assert.True
    };
    {
        part = "TagInfo"
        name = "find_all_tags_in_closed_file_with_symbol"
        test = fun name ->
(* Make sure the tag symbol is set to the same value used by the test. *)
            do TagInfo.tag_symbol <- "#"
(* See the comments in the find_tags test. *)
            let text = "#0 Line 1#1\n#2 Line 2 test#3#4 ##5#. http://test#6#7 https://#8 2#9#10"
            let test_file = CreateTestTextFile name text
            let lines = text.Split '\n'
            let results = TaggerTextEditor.find_all_tags_in_closed_file_with_symbol test_file |> List.sortBy (fun result -> result.tag)
(* Note line numbers start from 1. *)
            do results |> List.length = 8 |> Assert.True
(* Make sure both lists are sorted. The values are strings, so "10" comes before "2". *)
            let results2 = [
(* File and file_index field values are added by TagController. *)
                { file = ""; file_index = -1; tag = TagWithoutSymbol "0"; line_number = 1; line_text = lines.[0] };
                { file = ""; file_index = -1; tag = TagWithoutSymbol "1"; line_number = 1; line_text = lines.[0] };
                { file = ""; file_index = -1; tag = TagWithoutSymbol "2"; line_number = 2; line_text = lines.[1] };
                { file = ""; file_index = -1; tag = TagWithoutSymbol "3"; line_number = 2; line_text = lines.[1] };
                { file = ""; file_index = -1; tag = TagWithoutSymbol "4"; line_number = 2; line_text = lines.[1] };
                { file = ""; file_index = -1; tag = TagWithoutSymbol "5"; line_number = 2; line_text = lines.[1] };
                { file = ""; file_index = -1; tag = TagWithoutSymbol "9"; line_number = 2; line_text = lines.[1] };
                { file = ""; file_index = -1; tag = TagWithoutSymbol "10"; line_number = 2; line_text = lines.[1] };] |> List.sortBy (fun result -> result.tag)
            do compare_lists results results2 |> Assert.True
    };
    {
        part = "TagInfo"
        name = "starts_with_valid_tag_no_symbol"
        test = fun name ->
(* Make sure the tag symbol is set to the same value used by the test. *)
            do
                TagInfo.tag_symbol <- "#"
                "" |> TagInfo.starts_with_valid_tag_no_symbol = None |> Assert.True
                "#" |> TagInfo.starts_with_valid_tag_no_symbol = None |> Assert.True
                "##" |> TagInfo.starts_with_valid_tag_no_symbol = None |> Assert.True
                "1" |> TagInfo.starts_with_valid_tag_no_symbol = Some (TagWithoutSymbol "1", "") |> Assert.True
                "1." |> TagInfo.starts_with_valid_tag_no_symbol = Some (TagWithoutSymbol "1", ".") |> Assert.True
                ".a" |> TagInfo.starts_with_valid_tag_no_symbol = None |> Assert.True
    };
    {
        part = "TagInfo"
        name = "position_to_tag_with_symbol"
        test = fun name ->
(* Make sure the tag symbol is set to the same value used by the test. *)
            do
                TagInfo.tag_symbol <- "#"
(* Indices:                              01234 *)
                TagInfo.position_to_tag_with_symbol "" 0 = None |> Assert.True
                TagInfo.position_to_tag_with_symbol "" 1 = None |> Assert.True
                TagInfo.position_to_tag_with_symbol "a" 0 = None |> Assert.True
                TagInfo.position_to_tag_with_symbol "#" 0 = None |> Assert.True
                TagInfo.position_to_tag_with_symbol ".#1." 0 = None |> Assert.True
                TagInfo.position_to_tag_with_symbol ".#1." 1 = Some (TagWithoutSymbol "1") |> Assert.True
                TagInfo.position_to_tag_with_symbol ".#1." 2 = Some (TagWithoutSymbol "1") |> Assert.True
                TagInfo.position_to_tag_with_symbol ".#1." 3 = None |> Assert.True
(* See the comments for _position_to_tag_pattern and position_to_tag for more information. *)
                TagInfo.position_to_tag_with_symbol "#1 #2" 4 = Some (TagWithoutSymbol "2") |> Assert.True
    };
    {
        part = "TagInfo"
        name = "position_to_text_bordered_by_whitespace"
        test = fun name ->
(* Make sure the tag symbol is set to the same value used by the test. *)
            do
(* Indices:                                                     01234567 *)
                TagInfo.position_to_text_bordered_by_whitespace "" 0 = None |> Assert.True
                TagInfo.position_to_text_bordered_by_whitespace "" 1 = None |> Assert.True
                TagInfo.position_to_text_bordered_by_whitespace " " 0 = None |> Assert.True
                TagInfo.position_to_text_bordered_by_whitespace " a " 0 = None |> Assert.True
                TagInfo.position_to_text_bordered_by_whitespace " a " 1 = Some "a" |> Assert.True
                TagInfo.position_to_text_bordered_by_whitespace " a " 2 = None |> Assert.True
                TagInfo.position_to_text_bordered_by_whitespace "  a" 2 = Some "a" |> Assert.True
                TagInfo.position_to_text_bordered_by_whitespace "a  b" 0 = Some "a" |> Assert.True
                TagInfo.position_to_text_bordered_by_whitespace "a  b" 1 = None |> Assert.True
                TagInfo.position_to_text_bordered_by_whitespace "a  b" 2 = None |> Assert.True
                TagInfo.position_to_text_bordered_by_whitespace "a  b" 3 = Some "b" |> Assert.True
                TagInfo.position_to_text_bordered_by_whitespace "a" 0 = Some "a" |> Assert.True
                TagInfo.position_to_text_bordered_by_whitespace "abcde" 2 = Some "abcde" |> Assert.True
                TagInfo.position_to_text_bordered_by_whitespace "a bcdef" 5 = Some "bcdef" |> Assert.True
                TagInfo.position_to_text_bordered_by_whitespace "abc.def" 3 = Some "abc.def" |> Assert.True
    };
    {
        part = "TagInfo"
        name = "find_words"
        test = fun name ->
            do
                TagInfo.find_words "" = [] |> Assert.True
                TagInfo.find_words " " = [] |> Assert.True
                TagInfo.find_words "a" = ["a"] |> Assert.True
                TagInfo.find_words "a b" = ["a"; "b"] |> Assert.True
    };
    {
        part = "TagInfo"
        name = "find_links"
        test = fun name ->
            do
                TagInfo.find_links "http://" = [] |> Assert.True
                TagInfo.find_links "http:// " = [] |> Assert.True
                TagInfo.find_links "http://a" = ["http://a"] |> Assert.True
                TagInfo.find_links "https://a" = ["https://a"] |> Assert.True
                TagInfo.find_links "http://a http://b" = ["http://a"; "http://b"] |> Assert.True
    };
    {
        part = "TagInfo"
        name = "format_temp_tag"
        test = fun name ->
// TODO2 Add test.
            ()
    };
    {
        part = "TagInfo"
        name = "is_temp_tag"
        test = fun name ->
// TODO2 Add test.
            ()
    };
//#endregion

(* Editor *)
//#region
    {
        part = "Editor"
        name = "position_to_words_links_tag"
        test = fun name ->
(* Helper function. *)
            let check_word (editor : TaggerTextEditor) (line : int) offset expected_words expected_links expected_tag =
                let print_tag = function | Some tag -> tag.ToString () | _ -> ""
                printfn "Line: %d. Offset: %d. Expected words: %A. Expected links: %A. Expected tag: %s" line offset expected_words expected_links (print_tag expected_tag)
(* TextViewPosition counts columns from 1, not 0. Convert the offset to a column. *)
                let result = new TextViewPosition (line, offset + 1) |> editor.test_position_to_words_links_tag
                match result with
                | Some (words, links, tag) ->
                    printfn "Result words: %A. Result links: %A. Result tag: %s" words links (print_tag tag)
                    words = expected_words && links = expected_links && tag = expected_tag
                | None ->
                    printfn "Result: none."
                    expected_words.Length = 0 && expected_links.Length = 0 && expected_tag.IsNone
(* Make sure the tag symbol is set to the same value used by the test. *)
            do TagInfo.tag_symbol <- "#"
            let editor = new TaggerTextEditor ()
            let check_word_ = check_word editor
            do
(* Offsets. *)
//                              01234567 01234567
(* Length: 15. *)
                editor.Text <- "Line 1.\nLine 2."
(* TextViewPosition counts lines from 1, not 0. *)
                check_word_ 1 0 ["Line"] [] None |> Assert.True
                check_word_ 1 1 ["Line"] [] None |> Assert.True
                check_word_ 1 2 ["Line"] [] None |> Assert.True
                check_word_ 1 5 ["1"] [] None |> Assert.True
                check_word_ 1 6 ["1"] [] None |> Assert.True
                check_word_ 1 7 [] [] None |> Assert.True
                check_word_ 1 8 [] [] None |> Assert.True
                check_word_ 2 0 ["Line"] [] None |> Assert.True
                check_word_ 2 1 ["Line"] [] None |> Assert.True
                check_word_ 2 5 ["2"] [] None |> Assert.True
                check_word_ 2 6 ["2"] [] None |> Assert.True
                check_word_ 2 7 [] [] None |> Assert.True
(* This time try a string with a delimiter at the beginning but none at the end. *)
(* Offsets. *)
//                              01234567 0123456
(* Length: 14. *)
                editor.Text <- ".Line 1\nLine 2"
                check_word_ 1 0 ["Line"] [] None |> Assert.True
                check_word_ 1 1 ["Line"] [] None |> Assert.True
                check_word_ 2 5 ["2"] [] None |> Assert.True
                check_word_ 2 6 [] [] None |> Assert.True
(* Test getting tags. *)
(* Offsets. *)
//                              0123456789 0123456789012
(* Length: 22. *)
                editor.Text <- "Line 1.#0\n#1 Line 2.#2"
                check_word_ 1 7 ["1"; "0"] [] ("0" |> TagWithoutSymbol |> Some) |> Assert.True
                check_word_ 1 8 ["1"; "0"] [] ("0" |> TagWithoutSymbol |> Some) |> Assert.True
                check_word_ 1 9 [] [] None |> Assert.True
                check_word_ 1 10 [] [] None |> Assert.True
                check_word_ 2 0 ["1"] [] ("1" |> TagWithoutSymbol |> Some) |> Assert.True
                check_word_ 2 1 ["1"] [] ("1" |> TagWithoutSymbol |> Some) |> Assert.True
                check_word_ 2 2 [] [] None |> Assert.True
                check_word_ 2 10 ["2"; "2"] [] ("2" |> TagWithoutSymbol |> Some) |> Assert.True
                check_word_ 2 11 ["2"; "2"] [] ("2" |> TagWithoutSymbol |> Some) |> Assert.True
                check_word_ 2 12 [] [] None |> Assert.True
(* Make sure we don't find a tag where there isn't one. *)
                check_word_ 1 0 ["Line"] [] None |> Assert.True
(* Make sure we don't find a tag inside a link. *)
(* Offsets. *)
//                              0123456789
                editor.Text <- "http://#0"
                check_word_ 1 8 ["http"; "0"] ["http://#0"] None |> Assert.True
    };
(* This is currently not used. *)
(*
    {
        part = "Editor"
        name = "safe_position_to_selected_line"
        test = fun name ->
            let editor = new TaggerTextEditor ()
            do
                editor.Text <- "Line1\nLine2"
(* Select the first line. *)
                get_position 1 1 |> Some |> editor.test_safe_position_to_selected_line
                editor.SelectedText = "Line1\n" |> Assert.True
(* Select the first line using the newline. *)
                get_position 1 6 |> Some |> editor.test_safe_position_to_selected_line
                editor.SelectedText = "Line1\n" |> Assert.True
(* Select the last line using an invalid position. *)
                editor.test_safe_position_to_selected_line None
                editor.SelectedText = "Line2" |> Assert.True
    };
*)
    {
        part = "Editor"
        name = "validate_lines"
        test = fun name ->
            let editor_1 = new TaggerTextEditor ()
            let editor_2 = new TaggerTextEditor ()
(* Try a line whose end offset exceeds the current document length. The only way I know to do this is to pass in a line from another document. For example, if you get a line from a document and then clear the document, the line is also cleared. *)
            do
                editor_1.Text <- "0123"
                editor_2.Text <- "01234"
            let line = editor_2.Document.Lines.[0]
            do
                [line] |> editor_1.test_validate_lines = None |> Assert.True
(* Try multiple, non-sorted, non-contiguous lines. *)
                editor_1.Text <- "Line 1\nLine 2\nLine 3"
            let lines = [editor_1.Document.Lines.[2]; editor_1.Document.Lines.[0]]
(* Verify the lines are validated and sorted. *)
            do lines |> editor_1.test_validate_lines = Some [editor_1.Document.Lines.[0]; editor_1.Document.Lines.[2]] |> Assert.True
    };
    {
// TODO2 This works the same way as validate_lines and we should have one call the other.
        part = "Editor"
        name = "validate_line"
        test = fun name ->
            ()
    };
    {
        part = "Editor"
        name = "line_to_start_end_length"
        test = fun name ->
            let editor = new TaggerTextEditor ()
            do
                editor.Text <- "0123"
(* Try a valid line with no newline. *)
                editor.Document.Lines.[0] |> editor.test_line_to_start_end_length = (0, 4, 4) |> Assert.True
(* Try a valid line with a newline. *)
                editor.Text <- "0123\n456"
                editor.Document.Lines.[0] |> editor.test_line_to_start_end_length = (0, 4, 5) |> Assert.True
                editor.Document.Lines.[1] |> editor.test_line_to_start_end_length = (5, 8, 3) |> Assert.True
    };
    {
        part = "Editor"
        name = "insert_line_at_offset_in_text"
        test = fun name ->
            let editor = new TaggerTextEditor ()
            do
                editor.Text <- "456"
                editor.test_insert_line_at_offset_in_text "123" 0
(* Verify the text was inserted as expected. *)
                Assert.True (editor.Text = "123\n456")
    };
    {
        part = "Editor"
        name = "lines_to_text_helper_cut"
        test = fun name ->
            let editor = new TaggerTextEditor ()
            do
(* Remove one line and verify the results. *)
                editor.Text <- "Line 1\nLine 2\nLine 3"
                editor.test_lines_to_text_helper_cut [editor.Document.Lines.[0]] (Some editor.Document.Lines.[2]) = ("Line 1\n", 7) |> Assert.True
(* Verify the expected text was removed. *)
// Offsets:                    0123456 789012
                editor.Text = "Line 2\nLine 3" |> Assert.True
(* Remove multiple lines and verify the results. *)
                editor.Text <- "Line 1\nLine 2\nLine 3"
                editor.test_lines_to_text_helper_cut [editor.Document.Lines.[0]; editor.Document.Lines.[1]] (Some editor.Document.Lines.[2]) = ("Line 1\nLine 2\n", 0) |> Assert.True
(* Verify the expected text was removed. *)
                editor.Text = "Line 3" |> Assert.True
(* Remove multiple, non-contiguous lines and verify the results. *)
                editor.Text <- "Line 1\nLine 2\nLine 3\nLine 4"
                editor.test_lines_to_text_helper_cut [editor.Document.Lines.[0]; editor.Document.Lines.[2]] (Some editor.Document.Lines.[3]) = ("Line 1\nLine 3\n", 7) |> Assert.True
(* Verify the expected text was removed. *)
// Offsets:                    0123456 789012
                editor.Text = "Line 2\nLine 4" |> Assert.True
    };
    {
        part = "Editor"
        name = "lines_to_text_helper_copy"
        test = fun name ->
            let editor = new TaggerTextEditor ()
            do
(* Get one line and verify the results. *)
                editor.Text <- "Line 1\nLine 2\nLine 3"
                editor.test_lines_to_text_helper_copy [editor.Document.Lines.[0]] = "Line 1\n" |> Assert.True
(* Get multiple lines and verify the results. *)
                editor.Text <- "Line 1\nLine 2\nLine 3"
                editor.test_lines_to_text_helper_copy [editor.Document.Lines.[0]; editor.Document.Lines.[1]] = "Line 1\nLine 2\n" |> Assert.True
(* Get multiple, non-contiguous lines and verify the results. *)
                editor.Text <- "Line 1\nLine 2\nLine 3\nLine 4"
                editor.test_lines_to_text_helper_copy [editor.Document.Lines.[0]; editor.Document.Lines.[2]] = "Line 1\nLine 3\n" |> Assert.True
    };
    {
        part = "Editor"
        name = "move_and_copy_helper"
        test = fun name ->
            let editor = new TaggerTextEditor ()
            do
                editor.Text <- "Line1\nLine2\nLine3"
(* If the source lines are empty, expect false. *)
                editor.test_move_and_copy_helper [] editor.Document.Lines.[0] |> Assert.False
(* If the source lines overlap the target line, expect false. *)
                editor.test_move_and_copy_helper [editor.Document.Lines.[0]] editor.Document.Lines.[0] |> Assert.False
                editor.test_move_and_copy_helper [editor.Document.Lines.[0]; editor.Document.Lines.[1]] editor.Document.Lines.[0] |> Assert.False
(* Try non-sorted, non-contiguous lines. Again, the source lines overlap the target line. *)
                editor.test_move_and_copy_helper [editor.Document.Lines.[2]; editor.Document.Lines.[0]] editor.Document.Lines.[0] |> Assert.False
(* This is a valid move/copy. The source lines do not overlap the target line. *)
                editor.test_move_and_copy_helper [editor.Document.Lines.[0]] editor.Document.Lines.[2] |> Assert.True
(* Try multiple, non-sorted, non-contiguous lines. *)
                editor.test_move_and_copy_helper [editor.Document.Lines.[2]; editor.Document.Lines.[0]] editor.Document.Lines.[1] |> Assert.True
    };
    {
        part = "Editor"
        name = "find_text"
        test = fun name ->
            let source_text = "ABC"
            do
(* Try to find the text. *)
                TaggerTextEditor.test_find_text source_text "a" = Some (false, false) |> Assert.True
(* Try to find the text with a case sensitive match. *)
                TaggerTextEditor.test_find_text source_text "A" = Some (true, false) |> Assert.True
(* Try to find the text with a whole word match. *)
                TaggerTextEditor.test_find_text source_text "abc" = Some (false, true) |> Assert.True
(* Try to find the text with a case sensitive match and whole word match. *)
                TaggerTextEditor.test_find_text source_text "ABC" = Some (true, true) |> Assert.True
    };
    {
        part = "Editor"
        name = "find_tags_in_line"
        test = fun name ->
            ()
// TODO2 Add test.
    };
    {
        part = "Editor"
        name = "mouse_right_up_helper"
        test = fun name ->
(* Helper function to sort lists in right-click data. *)
            let sort_data data = { data with words = data.words |> List.sort; links = data.links |> List.sort; }
(* Helper function to compare right-click data. *)
            let compare_data data_1 data_2 = sort_data data_1 = sort_data data_2
(* Make sure the tag symbol is set to the same value used by the test. *)
            do TagInfo.tag_symbol <- "#"
            let editor = new TaggerTextEditor ()
            let right_click_fired = ref false
            let default_data = {
                position = None
                words = []
                links = []
                tag = None
            }
            let expected_data = ref default_data
(* Right-click handler that is closed over expected data reference, which we can change for different tests. *)
            let right_click_handler data =
                do
                    right_click_fired := true
                    compare_data !expected_data data |> Assert.True
            do
                editor.right_click.Add right_click_handler
(* Right-click the line and verify the right_click event fires. Right-click an invalid position and verify the right click data does not contain a position or word. *)
                editor.test_mouse_right_up_helper None
                Assert.True !right_click_fired
(* Positions:                  1234567 *)
                editor.Text <- "Line1#0"
(* Right-click a word. *)
                expected_data := { default_data with
                    position = get_position 1 1 |> Some
                    words = ["Line1"; "0"]
                    }
                editor.test_mouse_right_up_helper (get_position 1 1 |> Some)
(* Right-click a tag. *)
                expected_data := { default_data with
                    position = get_position 1 7 |> Some
                    words = ["Line1"; "0"]
                    tag = "#0" |> TagWithSymbol |> Some
                    }
                editor.test_mouse_right_up_helper (get_position 1 7 |> Some)
(* Right-click a link. *)
                editor.Text <- "http://about:blank"
                expected_data := { default_data with
                    position = get_position 1 1 |> Some
                    words = ["http"; "about"; "blank"]
                    links = ["http://about:blank"]
                    }
                editor.test_mouse_right_up_helper (get_position 1 1 |> Some)
    };
(* Test the auto-save. *)
(* Previously, the auto-save was broken, probably because of changes we made to the UndoStack. We haven't figured out how UndoStack broke the auto-save, but we replaced the IsModified flag with our own implementation that does not involve the UndoStack. See Editor.save_timer_tick_handler for details.
PaneControllerTest.SaveFileError (the second test, which uses auto-save) was failing, correctly, but this test was passing, incorrectly. *)
    {
        part = "Editor"
        name = "save_timer_tick_handler"
        test = fun name ->
(* Create an empty file to open in the editor. *)
            let test_file = CreateTestTextFile name ""
            let editor = new TaggerTextEditor ()
(* Set the save timer interval to 1/10 second. *)
            do
                editor.save_timer_interval <- 100.0
(* Open the file. The document is not important. *)
                editor.open_file test_file (new Document.TextDocument ())
(* Change the text in the editor. *)
                editor.Text <- "test"
(* Originally, I put the thread to sleep for 1/10 second. However, the dispatcher runs on the same thread as the UI, so that puts it to sleep as well. The dispatcher needs the thread to be idle. To do that, I create a message box. *)
(* Let the thread go idle. *)
                name |> MessageBox.Show |> ignore
(* Verify that the changes were written to the file. *)
                Assert.True (File.ReadAllText test_file = "test")
(* Now we'll trigger the save_file_error event. *)
(* This is to make sure the event fires. *)
            let save_file_error_fired = ref false
            do
                editor.save_file_error.Add <| fun _ -> do save_file_error_fired := true
(* Set the file as read-only. *)
                File.SetAttributes (test_file, FileAttributes.ReadOnly)
(* Change the text in the editor, to set the IsModified flag. *)
                editor.Text <- "test2"
(* Let the thread go idle. *)
                name |> MessageBox.Show |> ignore
(* Verify that the event fired. *)
                Assert.True !save_file_error_fired
(* Reset the error event flag. *)
                save_file_error_fired := false
(* Let the thread go idle. *)
                name |> MessageBox.Show |> ignore
(* Make sure the event hasn't fired again. This is to verify that the error only fires once when auto-save fails, not repeatedly. *)
                Assert.False !save_file_error_fired
(* Verify that the file is unchanged. *)
                test_file |> File.ReadAllText = "test" |> Assert.True
(* Set the file as writable. *)
                File.SetAttributes (test_file, FileAttributes.Normal)
(* Let the thread go idle. *)
                name |> MessageBox.Show |> ignore
(* Verify that the changes were written to the file. *)
                Assert.True (File.ReadAllText test_file = "test2")
(* Set the file as read-only again. *)
                File.SetAttributes (test_file, FileAttributes.ReadOnly)
(* Change the text in the editor, to set the IsModified flag. *)
                editor.Text <- "test3"
(* Let the thread go idle. *)
                name |> MessageBox.Show |> ignore
(* Verify that the event fired again. This is to verify that after auto-save succeeds, it will fire the error event again the next time it fails. *)
                Assert.True !save_file_error_fired
(* Verify that the file is unchanged. *)
                test_file |> File.ReadAllText = "test2" |> Assert.True
    };
    {
        part = "Editor"
        name = "text_changed_handler"
        test = fun name ->
            let editor = new TaggerTextEditor ()
            let selection_changed_fired = ref false
            do
                editor.margin_selection_changed.Add <| fun _ -> do selection_changed_fired := true
                editor.Text <- "Line 1\nLine 2\nLine 3"
(* Select lines in the editor. *)
                editor.update_margin_selected_lines [editor.Document.Lines.[0]; editor.Document.Lines.[1]] None false
(* Change the text. *)
                editor.Text <- ""
(* Verify the selection_changed event fired. *)
                !selection_changed_fired |> Assert.True
(* Verify the selected lines list was cleared. *)
                editor.margin_selected_lines.Length = 0 |> Assert.True
    };
    {
        part = "Editor"
        name = "save_file"
        test = fun name ->
(* Create an empty file. *)
            let test_file = CreateTestTextFile name ""
            let editor = new TaggerTextEditor ()
            do
(* Open the file and add text. The document is not important. *)
                editor.open_file test_file (new Document.TextDocument ()) |> ignore
                editor.Text <- "test"
(* Save the file and verify it returns no error message. *)
                Assert.True <| (editor.save_file ()).IsNone
(* Verify the file contains the expected contents. *)
                Assert.True (File.ReadAllText test_file = "test")
(* Make the file read-only and try to save to it, to verify that we get an error message. *)
                File.SetAttributes (test_file, FileAttributes.ReadOnly)
                Assert.True <| (editor.save_file ()).IsSome
    };
    {
        part = "Editor"
        name = "open_file"
        test = fun name ->
(* Create a file, open it, and make sure it contains the expected contents. *)
            let test_file = CreateTestTextFile name "test"
            let editor = new TaggerTextEditor ()
(* Verify the editor is disabled by default. *)
            do
                Assert.False editor.IsEnabled
(* Open the file in the editor. Note we verify that the same document is attached to and detached from the file in close_file. *)
                editor.open_file test_file (new Document.TextDocument ("test"))
(* Verify the editor is enabled. *)
                Assert.True editor.IsEnabled
(* Verify the file property. *)
                Assert.True (editor.file.IsSome && editor.file.Value = test_file)
(* Verify the text. *)
                Assert.True (editor.Text = "test")
    };
    {
        part = "Editor"
        name = "rename_file"
        test = fun name ->
(* Create a file to open in the editor. *)
            let test_file = CreateTestTextFile name "test"
            let editor = new TaggerTextEditor ()
            do
(* Open the file. The document is not important. *)
                editor.open_file test_file (new Document.TextDocument ())
(* Rename an imaginary file that isn't open in the editor, and make sure it has no effect. *)
                editor.rename_file "a" "b"
                Assert.True (editor.file.IsSome && editor.file.Value = test_file)
(* Rename the file that is open in the editor. *)
            let test_file_2 = sprintf "%s_2" test_file
            do
                editor.rename_file test_file test_file_2
                Assert.True (editor.file.IsSome && editor.file.Value = test_file_2)
    }
    {
        part = "Editor"
        name = "close_file"
        test = fun name ->
(* Create a file and open it. *)
            let test_file = CreateTestTextFile name "test"
            let editor = new TaggerTextEditor ()
(* Create a document to attach to the file. *)
            let document_1 = new Document.TextDocument ()
            do
                editor.open_file test_file document_1
(* Change the file, to change the document. *)
                editor.Text <- "test_change"
(* Close the file. Get the document. *)
            let document_2 = editor.close_file ()
            do
(* Verify the document we get from closing the file is the same one we attached to it when we opened it. *)
                document_1 = document_2 |> Assert.True
(* Verify that the editor is disabled, has its file set to None and its text set to empty. *)
                Assert.True (editor_is_empty editor)
(* Verify that the editor now has a different document. *)
                editor.Document = document_2 |> Assert.False
    };
    {
        part = "Editor"
        name = "update_selected_lines"
        test = fun name ->
            let editor = new TaggerTextEditor ()
            do
(* Make a new selection. *)
// Offsets:                     0123456
                editor.Text <- "Line 1\nLine 2\nLine 3"
                editor.update_margin_selected_lines [editor.Document.Lines.[0]] None false
(* Verify the selection. *)
                editor.margin_selected_lines = [editor.Document.Lines.[0]] |> Assert.True
(* Verify the cursor offset. *)
                editor.CaretOffset = 6 |> Assert.True
(* Select multiple, non-contiguous lines. *)
                editor.update_margin_selected_lines [editor.Document.Lines.[1]; editor.Document.Lines.[2]] None false
(* Verify the selection. *)
                editor.margin_selected_lines = [editor.Document.Lines.[1]; editor.Document.Lines.[2]] |> Assert.True
(* Since this was not a new selection, the cursor offset should be unchanged. *)
                editor.CaretOffset = 6 |> Assert.True
    }
    {
        part = "Editor"
        name = "insert_line_at_line"
        test = fun name ->
            let editor = new TaggerTextEditor ()
            do
                editor.Text <- "Line 1\nLine 3"
(* Insert text at line 2. *)
                editor.insert_line_at_line "Line 2\n" (editor.Document.Lines.[1])
(* Verify the text was inserted as expected. *)
                Assert.True (editor.Text = "Line 1\nLine 2\nLine 3")
    };
    {
        part = "Editor"
        name = "insert_text_at_start_of_current_file"
        test = fun name ->
            let editor = new TaggerTextEditor ()
            do
                editor.Text <- "Line 3\nLine 4"
(* Insert text without a newline, to make sure one is added. *)
                editor.insert_text_at_start_of_current_file "Line 2"
(* Insert text with a newline, to make sure another is not added. *)
                editor.insert_text_at_start_of_current_file "Line 1\n"
(* Verify the text was inserted as expected. *)
                Assert.True (editor.Text = "Line 1\nLine 2\nLine 3\nLine 4")
    };
    {
        part = "Editor"
        name = "insert_text_at_end_of_current_file"
        test = fun name ->
            let editor = new TaggerTextEditor ()
            do
                editor.Text <- "Line 1\nLine 2"
(* Insert text without a newline, to make sure one is added. *)
                editor.insert_text_at_end_of_current_file "Line 3"
(* Insert text with a newline, to make sure another is not added. *)
                editor.insert_text_at_end_of_current_file "\nLine 4"
(* Verify the text was inserted as expected. *)
                Assert.True (editor.Text = "Line 1\nLine 2\nLine 3\nLine 4")
    };
    {
        part = "Editor"
        name = "insert_text_at_cursor_in_current_file"
        test = fun name ->
            let editor = new TaggerTextEditor ()
            do
(* Offsets:
                                0123456 7
*)
                editor.Text <- "Line 1\nLine 4"
                editor.CaretOffset <- 7
(* Insert text without a newline, to make sure one is added. *)
                editor.insert_text_at_cursor_in_current_file "Line 3"
(* Reset the caret offset in case it has changed. *)
                editor.CaretOffset <- 7
(* Insert text with a newline, to make sure another is not added. *)
                editor.insert_text_at_cursor_in_current_file "Line 2\n"
(* Verify the text was inserted as expected. *)
                Assert.True (editor.Text = "Line 1\nLine 2\nLine 3\nLine 4")
    };
    {
        part = "Editor"
        name = "move_lines"
        test = fun name ->
            let editor = new TaggerTextEditor ()
            do
(* Drop lines 1 and 2 on line 0. *)
                editor.Text <- "Line 1\nLine 2\nLine 3"
                editor.move_lines [editor.Document.Lines.[1]; editor.Document.Lines.[2]] editor.Document.Lines.[0] |> Assert.True
                editor.Text = "Line 2\nLine 3\nLine 1\n" |> Assert.True
(* Drop line 0 on itself. *)
                editor.Text <- "Line 1"
                editor.move_lines [editor.Document.Lines.[0]] editor.Document.Lines.[0] |> Assert.False
(* Drop lines 0 and 1 on line 3. *)
                editor.Text <- "Line 1\nLine 2\nLine 3\nLine 4"
                editor.move_lines [editor.Document.Lines.[0]; editor.Document.Lines.[1]] editor.Document.Lines.[3] |> Assert.True
                editor.Text = "Line 3\nLine 1\nLine 2\nLine 4" |> Assert.True
(* Drop lines 0 and 2 on line 3. *)
                editor.Text <- "Line 1\nLine 2\nLine 3\nLine 4"
                editor.move_lines [editor.Document.Lines.[0]; editor.Document.Lines.[2]] editor.Document.Lines.[3] |> Assert.True
                editor.Text = "Line 2\nLine 1\nLine 3\nLine 4" |> Assert.True
    };
    {
        part = "Editor"
        name = "copy_lines"
        test = fun name ->
            let editor = new TaggerTextEditor ()
            do
(* Drop lines 1 and 2 on line 0. *)
                editor.Text <- "Line 1\nLine 2\nLine 3"
                editor.copy_lines [editor.Document.Lines.[1]; editor.Document.Lines.[2]] editor.Document.Lines.[0] |> Assert.True
                editor.Text = "Line 2\nLine 3\nLine 1\nLine 2\nLine 3" |> Assert.True
(* Drop line 0 on itself. *)
                editor.Text <- "Line 1"
                editor.copy_lines [editor.Document.Lines.[0]] editor.Document.Lines.[0] |> Assert.False
(* Drop lines 0 and 1 on line 3. *)
                editor.Text <- "Line 1\nLine 2\nLine 3\nLine 4"
                editor.copy_lines [editor.Document.Lines.[0]; editor.Document.Lines.[1]] editor.Document.Lines.[3] |> Assert.True
                editor.Text = "Line 1\nLine 2\nLine 3\nLine 1\nLine 2\nLine 4" |> Assert.True
(* Drop lines 0 and 2 on line 3. *)
                editor.Text <- "Line 1\nLine 2\nLine 3\nLine 4"
                editor.copy_lines [editor.Document.Lines.[0]; editor.Document.Lines.[2]] editor.Document.Lines.[3] |> Assert.True
                editor.Text = "Line 1\nLine 2\nLine 3\nLine 1\nLine 3\nLine 4" |> Assert.True
    };
    {
        part = "Editor"
        name = "safe_line_number_to_line"
        test = fun name ->
            let editor = new TaggerTextEditor ()
(* Helper function to convert document line to text. *)
            let get_text line = line |> editor.Document.GetText
            let text = "Line 1"
            do
(* Verify that the document contains at least one line, even when empty. *)
                editor.safe_line_number_to_line 1 |> get_text = "" |> Assert.True
(* Add text and test the function. *)
                editor.Text <- text
                editor.safe_line_number_to_line 1 |> get_text = text |> Assert.True
                editor.safe_line_number_to_line editor.Document.LineCount |> get_text = text |> Assert.True
(* Try getting lines with invalid numbers and make sure we get a valid line. *)
                editor.safe_line_number_to_line 0 |> get_text = text |> Assert.True
                editor.safe_line_number_to_line System.Int32.MaxValue |> get_text = text |> Assert.True
    };
    {
        part = "Editor"
        name = "safe_position_to_line"
        test = fun name ->
(* Helper function. *)
            let check_line (editor : TaggerTextEditor) (line : int) column (line_text : string) =
                let result = new TextViewPosition (line, column) |> Some |> editor.safe_position_to_line
                let result_ = editor.Document.GetText (result.Offset, result.EndOffset - result.Offset)
                result_ = line_text
            let editor = new TaggerTextEditor ()
            do
                editor.Text <- "Line 1\nLine 2"
(* Lines and columns as used by TextViewPosition start from 1, not 0. *)
                check_line editor 1 1 "Line 1" |> Assert.True
                check_line editor 1 6 "Line 1" |> Assert.True
                check_line editor 2 1 "Line 2" |> Assert.True
                check_line editor 2 6 "Line 2" |> Assert.True
(* Call get_line with no TextViewPosition and verify we get the last line in the document. *)
                Assert.True (editor.safe_position_to_line None |> editor.Document.GetText = "Line 2")
    }
    {
        part = "Editor"
        name = "add_tags_to_lines"
        test = fun name ->
(* Make sure the tag symbol is set to the same value used by the test. *)
            do TagInfo.tag_symbol <- "#"
            let editor = new TaggerTextEditor ()
            do
                editor.Text <- "Line 1\nLine 2"
(* Add a tag to a single line. *)
                editor.add_tags_to_lines [TagWithoutSymbol "0"] [editor.Document.Lines.[0]]
(* Adding a second tag with the same number should do nothing. *)
                editor.add_tags_to_lines [TagWithoutSymbol"0"] [editor.Document.Lines.[0]]
(* Add tags to multiple lines. *)
                editor.add_tags_to_lines (["1"; "2"] |> List.map TagWithoutSymbol) [editor.Document.Lines.[0]; editor.Document.Lines.[1]]
                editor.Text = "Line 1#0#1#2\nLine 2#1#2" |> Assert.True
(* Add tags to non-contiguous lines. *)
                editor.Text <- "Line 1\nLine 2\nLine 3"
                editor.add_tags_to_lines [TagWithoutSymbol "0"] [editor.Document.Lines.[0]; editor.Document.Lines.[2]]
                editor.Text = "Line 1#0\nLine 2\nLine 3#0" |> Assert.True
(* Add a tag to an empty line. *)
                editor.Text <- "Line 1\n    \nLine 3"
                editor.add_tags_to_lines [TagWithoutSymbol "0"] (editor.Document.Lines |> Seq.toList)
                editor.Text = "Line 1#0\n    \nLine 3#0" |> Assert.True
    };
    {
        part = "Editor"
        name = "find_text_in_closed_file"
        test = fun name ->
(* Make sure the tag symbol is set to the same value used by the test. *)
            do TagInfo.tag_symbol <- "#"
            let text0 = "#0 Line 1\nLine 2#0"
            let test_file = CreateTestTextFile name text0
            let results = TaggerTextEditor.find_text_in_closed_file "#0" test_file |> Seq.sortBy (fun result -> result.line_number)
(* Note line numbers start from 1. *)
            do results |> Seq.length = 2 |> Assert.True
            let results2 = [
(* File_index field values are added by TagController. *)
                { file_index = -1; line_number = 1; line_text = "#0 Line 1"; match_case = true; match_whole_word = true; };
                { file_index = -1; line_number = 2; line_text = "Line 2#0"; match_case= true; match_whole_word = false; };
                ]
            do compare_lists (results |> Seq.toList) results2 |> Assert.True
    };
    {
        part = "Editor"
        name = "find_text_in_open_file"
        test = fun name ->
(* Make sure the tag symbol is set to the same value used by the test. *)
            do TagInfo.tag_symbol <- "#"
            let editor = new TaggerTextEditor ()
            do
(* Try to find a nonexistent tag. *)
                editor.find_text_in_open_file "#0" |> Seq.isEmpty |> Assert.True
                editor.Text <- "#0"
(* Note line numbers start from 1. *)
            let results = editor.find_text_in_open_file "#0" |> Seq.toList
(* File_index field values are added by TagController. *)
            let results2 = [{ file_index = -1; line_number = 1; line_text = "#0"; match_case = true; match_whole_word = true; }]
            do compare_lists results results2 |> Assert.True
    };
//#endregion
    ]
