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

module PaneController

// DateTime
open System
// ConcurrentDictionary
open System.Collections.Concurrent
// File
open System.IO
// StringBuilder
open System.Text
// MessageBox
open System.Windows

open ICSharpCode.AvalonEdit

// LoggerWrapper
open LoggerWrapper
// TaggerTextEditor, TaggerTabControl
open TaggerControls
// List extensions
open ListHelpers

/// <summary>Maps a file path (1) to a tuple. (2a) The document for the file. (2b) The time when the document was saved to this map.</summary>
type DocumentMap = ConcurrentDictionary<string, Document.TextDocument * DateTime>

/// <summary>Coordinates events between the editor (1), the tab control (2), and the margin (3). (4) is the vertical position map for all instances. (5) is the document map for all instances.</summary>
type PaneController (
    editor : TaggerTextEditor,
    tabs : TaggerTabControl,
    left_margin : TaggerMargin,
    right_margin : TaggerMargin,
    status : System.Windows.Controls.TextBlock,
    vertical_positions : ConcurrentDictionary<string, float> ref,
    documents : DocumentMap ref
    ) as this =

(* Member values. *)
//#region
/// <summary>The lines currently highlighted as Drag From.</summary>
    let mutable _drag_from_lines = []
/// <summary>The lines currently highlighted as Drag To.</summary>
    let mutable _drag_to_lines = []
/// <summary>The lines currently highlighted as Open Tag.</summary>
    let mutable _open_tag_lines = []
//#endregion

(* Events. *)
//#region
(* These are caught from the Margin and simply re-fired to TagController. *)
(* The user drops on the margin. *)
    let _margin_drop = new Event<MarginDropData> ()
/// <summary>The user right-clicked in the margin.</summary>
    let _margin_right_click = new Event<unit> ()
(* These are caught from the Editor and simply re-fired to MainController. *)
(* The user right-clicks. (1) The right-click data. *)
    let _right_click = new Event<RightClickData> ()
/// <summary>The user pressed the key combination for Find in Project.</summary>
    let _find_in_project = new Event<TextViewPosition> ()
/// <summary>The user entered the tag symbol. (1) The text after the tag symbol, or None.</summary>
    let _tag_symbol_entered = new Event<string option> ()
/// <summary>The user selected a tag in the TagCompletionWindow. (1) The tag. (2) Additional text the user entered, if any.</summary>
    let _tag_selected = new Event<TagWithoutSymbol * string> ()
(* These are caught from the TabControl and simply re-fired to MainController. *)
(* The tab control changes, for example because a tab is opened, closed, or drag/dropped. *)
    let _save_project = new Event<unit> ()
//#endregion

(* Vertical position map helpers. *)
//#region
/// <summary>Save the vertical position (2) for file (1). Return unit.</summary>
    let save_vertical_position file position =
(* The third parameter is a function that generates the new value based on the old value. *)
        do (!vertical_positions).AddOrUpdate (file, position, (fun _ _ -> position)) |> ignore
//#endregion

(* Document collection helpers. *)
//#region
/// <summary>Save the document (2) for file (1). Return unit.</summary>
    let save_document file document =
(* The third parameter is a function that generates the new value based on the old value. *)
        do (!documents).AddOrUpdate (file, (document, DateTime.Now), (fun _ _ -> document, DateTime.Now)) |> ignore
//#endregion

(* TabControl event handler helpers. *)
//#region

/// <summary>Open file (1). (2) A function that takes a file (2a) and returns a MessageBoxResult to indicate whether to reload a file that has changed outside of Tagger. Return a tuple. (R1) A new document that contains the file. If we cannot read the file, or the user chooses not to reload it, None. (R2) If we cannot read the file, the error message. Otherwise, None.</summary>
    let open_file_helper file get_reload_file =
(* Try to find the document in the documents collection. *)
        match (!documents).TryGetValue file with
(* If we find the document... *)
        | true, (document, timestamp) ->
(* If the file was modified outside of Tagger after we saved it to the documents collection, ask the user whether to reload it. *)
            if file |> File.GetLastWriteTime > timestamp then
                match file |> get_reload_file with
(* If the user says yes, load the file and create a new document for it. *)
                | MessageBoxResult.Yes -> file |> PaneController.load_file_into_document
(* If the user says no, return the document. *)
                | MessageBoxResult.No -> Some document
(* If the user says cancel, return None. *)
                | _ -> None
(* If the file was not modified outside of Tagger after we saved it to the documents collection, return the document. *)
            else Some document
(* If we did not find the document, load the file and create a new document for it. *)
        | _ -> file |> PaneController.load_file_into_document

/// <summary>Open the file (1) in the editor. If we succeed, return true. If we fail, or the user cancels opening the file, return false.</summary>
    let open_file file =
/// <summary>Ask the user whether to reload a file (1) that has changed outside of Tagger. Return the user's choice.</summary>
        let get_reload_file file =
            let text = file |> sprintf "File \"%s\" has been changed outside of Tagger. Do you want to reload it?\nWARNING: If you reload the file, Tagger will clear its Undo/Redo stack for the file. If you do not reload the file, Tagger's auto-save feature might overwrite the changes you made outside of Tagger."
            MessageBox.Show (text, "", Windows.MessageBoxButton.YesNoCancel)
        match open_file_helper file get_reload_file with
(* If we fail to open the file, or the user cancels opening the file, return false. *)
        | None -> false
(* If we open the file successfully... *)
        | Some document ->
            do
(* Send the file path and document to the editor. *)
                editor.open_file file document
(* Enable the margins. *)
                left_margin.IsEnabled <- true
                right_margin.IsEnabled <- true
// TODO1 Previously, in close_file_helper, we called editor.VerticalOffset, and here we passed the value to editor.ScrollToVerticalOffset. This did not work as we expected. However it might have been that we needed to call editor.UpdateLayout after editor.ScrollToVerticalOffset. See the comments for Editor.scroll_line.
(* Try to get the caret offset for the file. *)
            match (!vertical_positions).TryGetValue file with
            | true, offset ->
                do
(* Scroll the document so the line that contains the caret offset is visible. *)
                    editor.scroll_to_offset ((int) offset)
(* It seems the editor does not get focus unless we do this. *)
                    editor.Focus () |> ignore
            | _ -> ()
            true
//#endregion

(* TabControl event handlers. *)
//#region
/// <summary>Save the file for the previously selected tab, if any. If we succeed, open the file for the selected tab in the editor. Return unit.</summary>
    let select_tab_handler (new_tab_data : TaggerTabData option) =
(* Save the currently open file (the one associated with the previously opened tab), and close it. *)
        if this.close_file () then
(* If we successfully save the file, get the file for the currently selected tab and open that. *)
            match new_tab_data with
            | None -> ()
            | Some new_tab_data_ ->
(* If we fail to open the new file, then if we have a previously selected tab, select it. *)
(* In this case, leave the select tab handler enabled. The old file has been closed, and the new one has not been opened. The select tab handler will try to close the old file, which does nothing, then open it. *)
                if open_file new_tab_data_.file then
                    do tabs.select_tab new_tab_data_.index
                else do tabs.select_tab -1

/// <summary>Handle the user closing all tabs.</summary>
    let close_all_tabs_handler () =
(* Save the file currently open in the editor and close it. If we fail, stop. *)
        if this.close_file () = false then ()
(* Since we have closed the currently open file, unselect the tab. *)
        do
(* Clear the tab control. *)
            tabs.close_all_tabs ()
(* Notify MainController to save the project. *)
            _save_project.Trigger ()

/// <summary>Handle the MouseLeftButtonUp event on the tab control. (1) The event args. Return unit.</summary>
    let tabs_left_mouse_button_up_handler _ =
        do editor.Focus () |> ignore

//#endregion

(* Editor handler helpers. *)
//#region
/// <summary>Log the error (1) that was reported by the Editor. Return unit.</summary>
    let editor_general_error_handler error =
        do _logger.Log_ ("PaneController.EditorGeneralError", ["message", error])

/// <summary>Log the error (2) that occurred while saving file (1). Return unit.</summary>
    let save_file_error_handler file error =
        do _logger.Log_ ("PaneController.SaveFileError", ["path", file; "message", error])

/// <summary>Word wrap text (1). (2) The maximum length of each line. Return the word wrapped text.</summary>
    let word_wrap (text : string) max_length : string =
(* See:
https://msdn.microsoft.com/en-us/library/b873y76a%28v=vs.110%29.aspx
"If the separator argument is null or contains no characters, the method treats white-space characters as the delimiters."
We could also use Regex.Split and specify the \W (non-word) pattern as the delimiter.
*)
(* Split the text into words. *)
        let words = text.Split null
        let result =
(* Loop through the words. We have two accumulators: the word-wrapped text, and the current line. *)
            ((new StringBuilder (), new StringBuilder ()), words) ||> Array.fold (fun (acc : StringBuilder, line : StringBuilder) word ->
(* If adding the word to the current line would cause the line length to exceed the maximum line length, then add the line to the word-wrapped text and use the word as the start of the next line. *)
                if line.Length + word.Length + 1 > max_length then
                    acc.AppendFormat ("\n{0}", line), new StringBuilder (word)
(* Otherwise, add the word to the current line. *)
                else acc, line.AppendFormat (" {0}", word)
                )
(* Append the last line to the word-wrapped text. *)
        let result, rest = fst result, snd result
        let result = result.AppendFormat ("\n{0}", rest)
(* Trim the space and newline at the start of the string. *)
        result.ToString().TrimStart ()
//#endregion

(* Editor event handlers. *)
//#region

/// <summary>Handle a change in the margin selection. Return unit.</summary>
    let margin_selection_changed_handler () =
(* Show the drag from highlights. *)
        do this.highlight_lines DragFrom editor.margin_selected_lines 0

/// <summary>Handle insertion and deletion of text. (1) The text. (2) True if the user inserted text; false if the user deleted text.</summary>
    let editor_document_changed_handler (text : string, inserted) =
        let message_text =
            if text.Length < 41 then text
            else sprintf "%s..." <| text.Substring (0, 40)
        let message_text = message_text.Replace ("\r", "")
        let message_text = message_text.Replace ("\n", "\\n")
        let message = if inserted then "Ins: " else "Del: "
        let message = sprintf "%s%s" message message_text
        do status.Text <- message
        do status.ToolTip <- word_wrap text 100
//#endregion

(* Margin event handlers. *)
//#region

(* This function is for when the user margin selects multiple lines in one pane, then drags from the other pane over those lines. It highlights all the margin selected lines to indicate that the user can drop there and add tag(s) to all of the target lines (as well as the source line(s)). Also see TagController.drop_helper_different_pane.
The margin only fires the drag_over event, which is handled here, if the user is not dragging over the source line(s) in the source pane. See TaggerMargin.drag_over_helper. Also see Editor.move_and_copy_helper. *)
(* Unlike TaggerMargin.Drop, we don't need to re-fire this event up to the TagController. *)
/// <summary>Handle the user dragging over the margin at Y position (1). Return unit.</summary>
    let margin_drag_over_handler y_position =
(* Get the line the user is dragging over. *)
        let target_line = y_position |> editor.safe_y_position_to_line
(* Get the margin selected lines. *)
        let margin_selected_lines = editor.margin_selected_lines
(* Get the top and bottom Y positions... *)
        let tops_and_bottoms =
(* If one or more lines are margin selected, and the user dragged over one of them, show the drag to highlights for all of them. *)
            if margin_selected_lines |> List.exists (fun line -> line.LineNumber = target_line.LineNumber) then
                margin_selected_lines |> editor.get_lines_top_bottom
(* Otherwise, show the drag to highlights only for the line the user dragged over. *)
            else [target_line] |> editor.get_lines_top_bottom
        do
(* Clear the drag to highlight lines list. We don't need to redraw the drag to highlights in response to an extent height change because (1) the user is dragging the mouse and cannot, for example, expand the right sidebar, and (2) the drag to highlights are redrawn every time the user drags the mouse to a new line anyway. *)
            _drag_to_lines <- []
(* Show the drag to highlights with no time limit. *)
            left_margin.show DragTo tops_and_bottoms 0
            right_margin.show DragTo tops_and_bottoms 0

// TODO2 Allow the user to set default drag and drop behavior for same pane and different pane.
// TODO3 Add settings to remap the Ctrl, Shift, and Alt buttons for drag and drop.

/// <summary>Handle the user left-clicking the margin at Y position (1). (2) True if the Control key was pressed. (3) True if the Shift key was pressed. Return unit.</summary>
    let margin_select_handler (y_position, ctrl, shift) =
/// <summary>Return the lines from line number (2) to line number (3), inclusive, from lines (1).</summary>
        let get_multiple_lines (lines : Document.DocumentLine list) start_line_number end_line_number =
            if lines.Length > start_line_number - 1 then
(* We use seq.truncate instead of seq.take because it doesn't throw if the sequence doesn't have the expected number of items. We would also use a safe alternative for seq.skip if we knew what it was. *)
(* We add 1 to end_line_number - start_line_number to include the end line. For example, if the start line number is 3 and the end line number is 5, we skip (start line index = 2) lines, numbers 1 and 2, then get (5 - 3 + 1 = 3) lines. *)
                lines |> Seq.skip (start_line_number - 1) |> Seq.truncate (end_line_number - start_line_number + 1) |> Seq.toList
            else []
/// <summary>Return a tuple of the lines with the lowest and highest line number. (1) The first line. (2) The second line. (R1) The line with the lowest line number. (R2) The line with the highest line number.</summary>
        let get_start_and_end_line_numbers line1 line2 =
            let results = [line1; line2] |> List.map (fun (line : Document.DocumentLine) -> line.LineNumber) |> List.sort
            results.Head, results.last
/// <summary>Extend the previous margin selection to include new lines. (1) All document lines. (2) The line that was margin selected. (3) The most recently margin selected line. (4) The previously margin selected lines. Return the extended margin selection.</summary>
        let extend_margin_selection document_lines new_line most_recently_margin_selected_line previous_lines =
(* Get the lowest and highest line numbers of the new margin selected line and the most recently margin selected line. *)
            let start_line_number, end_line_number = get_start_and_end_line_numbers new_line most_recently_margin_selected_line
(* Get the newly margin selected lines using the lowest and highest line numbers. *)
            let lines = get_multiple_lines document_lines start_line_number end_line_number                
(* Combine the newly margin selected lines and the previously margin selected lines, and remove duplicates. *)
            [lines; previous_lines] |> List.concat |> List<_>.distinctBy (fun line -> line.LineNumber)
(* Get all document lines. *)
        let document_lines = editor.Document.Lines |> Seq.toList
(* Get the line that corresponds to the Y position the user clicked. *)
        let new_line = y_position |> editor.safe_y_position_to_line
(* See if there are previously margin selected lines. *)
        let previous = editor.margin_selected_lines.Length > 0
(* Get the previously margin selected lines. *)
        let previous_lines = editor.margin_selected_lines
(* See if the margin selected lines contain the new margin selected line. *)
        let inside = previous_lines |> List.exists (fun margin_selected_line -> margin_selected_line.LineNumber = new_line.LineNumber)
(* When the user left-clicks a margin, we expect one of the following cases.

    Ctrl    Shift   Prev    Inside
1.  F       F       F       F
2.  F       F       T       F
3.  F       F       T       T
4.  F       T       F       F
5.  F       T       T       F
6.  F       T       T       T
7.  T       F       F       F
8.  T       F       T       F
9.  T       F       T       T
10. T       T       F       F
11. T       T       T       F
12. T       T       T       T

Inside cannot be T when Prev is F.

1. Select the new line.
2. Clear the previous selection. Select the new line. (margin_drag_start_handler handles this differently.)
3. Clear the previous selection. Select the new line. (margin_drag_start_handler handles this differently.)
4. Select the new line.
5. Select from the line that was most recently selected without pressing Shift to the new line.
6. Select from the line that was most recently selected without pressing Shift to the new line.
7. Select the new line.
8. Add the new line to the selection.
9. Remove the new line from the selection.
10. Select the new line.
11. Get all lines from the most recently selected line to the new line. Add them to the previous selection.
12. No change.
*)
        let lines =
            match ctrl, shift, previous, inside with
(* 1. Select the new line. *)
            | false, false, false, false
(* 2. Clear the previous selection. Select the new line. *)
            | false, false, true, false
(* 3. Clear the previous selection. Select the new line. *)
            | false, false, true, true
(* 4. Select the new line. *)
            | false, true, false, false -> [new_line]
(* 5. Clear the previous selection. Select from the line that was most recently selected without pressing Shift to the new line. *)
            | false, true, true, false
(* 6. Clear the previous selection. Select from the line that was most recently selected without pressing Shift to the new line. *)
            | false, true, true, true ->
                match editor.most_recently_margin_selected_line_no_shift with
(* If we cannot get the line that was most recently selected without pressing Shift, then just select the new line. *)
                | None -> [new_line]
                | Some most_recently_selected_line_no_shift ->
                    extend_margin_selection document_lines new_line most_recently_selected_line_no_shift []
(* 7. Select the new line. *)
            | true, false, false, false -> [new_line]
(* 8. Add the new line to the selection. *)
            | true, false, true, false -> new_line :: previous_lines
(* 9. Remove the new line from the selection. *)
            | true, false, true, true -> previous_lines |> List.filter (fun line -> line.LineNumber <> new_line.LineNumber)
(* 10. Select the new line. *)
            | true, true, false, false -> [new_line]
(* 11. Get all lines from the most recently selected line to the new line. Add them to the previous selection. *)
            | true, true, true, false ->
                match editor.most_recently_margin_selected_line with
(* If we cannot get the most recently selected line, then just select the new line. *)
                | None -> [new_line]
                | Some most_recently_selected_line ->
                    extend_margin_selection document_lines new_line most_recently_selected_line previous_lines
(* 12. No change. *)
            | true, true, true, true -> previous_lines
(* This should never happen. *)
            | _ -> []
(* Margin select the lines in the margin and editor. Save the most recently margin selected line. *)
        do this.margin_select_lines lines (Some new_line) shift

(* See the comments for margin_select_handler. *)
/// <summary>Handle the user starting a drag from the margin. (1) The Y position from which the user started dragging. (2) True if the Control key was pressed. (3) True if the Shift key was pressed. Return unit.</summary>
    let margin_drag_start_handler (y_position, ctrl, shift) =
(* Get the line that corresponds to the Y position the user clicked. *)
        let new_line_number = (y_position |> editor.safe_y_position_to_line).LineNumber
(* See if there are previously margin selected lines. *)
        let previous = editor.margin_selected_lines.Length > 0
(* See if the margin selected lines contain the new line. *)
        let inside = editor.margin_selected_lines |> List.exists (fun line -> line.LineNumber = new_line_number)
        match ctrl, shift, previous, inside with
(* We handle the following cases differently than margin_select_handler. *)
(* If there is an existing margin selection, and the user begins a drag from inside the margin selection, we do not require the user to press Control or Shift to maintain the margin selection. *)
(* 3. No change. *)
        | false, false, true, true -> ()
(* We handle the remaining cases in the same way as margin_select_handler. *)
        | _ -> margin_select_handler (y_position, ctrl, shift)

/// <summary>Handle when the user right-clicks on the margin. (1) The Y position where the user clicked. (2) True if the Control key was pressed. (3) True if the Shift key was pressed. Return unit.</summary>
    let margin_right_click_handler (y_position, ctrl, shift) =
        do
(* Call margin_drag_start_handler to change the margin selection. We do not call margin_select_handler directly because we want to change the margin selection in the same way margin_drag_start_handler does. *)
            margin_drag_start_handler (y_position, ctrl, shift)
(* Fire the event. *)
            _margin_right_click.Trigger ()

/// <summary>Handle when the user drags out of a margin. Return unit.</summary>
    let margin_drag_leave_handler () =
(* Hide the drag to highlights. *)
        left_margin.hide_drag_to_rects ()
        right_margin.hide_drag_to_rects ()

(* This is technically an editor event handler, but since it involves dragging from a margin, and it fires a margin-related event, we've grouped it with the margin event handlers. *)
/// <summary>Handle when the user drags from a margin and over the editor. (1) The margin from which the user dragged. (2) The drag event args. Return unit.</summary>
    let editor_margin_drag_handler (source_margin, args) =
        do
(* We verified the source is a margin in editor.drag_drop_helper. We know the target is an editor because the editor fired the event to the PC that resulted in calling this handler. We proceed as though the target was the margin in this pane, rather than the editor. *)
(* For some reason, we have to specify the type of this.margin. *)
            left_margin.drag_over_helper source_margin args
            right_margin.drag_over_helper source_margin args

(* This is technically an editor event handler, but since it involves dragging from a margin, and it fires a margin-related event, we've grouped it with the margin event handlers. *)
/// <summary>Handle when the user drags from a margin and drops on the editor. (1) MarginDropData provided by the editor. (2) The margin from which the user dragged. Return unit.</summary>
    let editor_margin_drop_handler ((data : MarginDropData), source_margin) =
(* The only thing the editor didn't know about the drop was whether the drag came from the same margin that's attached to it. Determine that here and add it to the drop data. *)
        {
            data with
                same_margin = (source_margin = left_margin || source_margin = right_margin)
(* Fire the event to the TagController to be handled just as we would a drop on the margin. *)
        } |> _margin_drop.Trigger

/// <summary>Handle the scroll changed event. (1) The vertical scroll offset change. Return unit.</summary>
    let scroll_changed_handler vertical_scroll_offset_change =
(* Tell the margins to scroll any visible highlights to match the vertical scroll offset change. *)
        do
            vertical_scroll_offset_change |> left_margin.scroll
            vertical_scroll_offset_change |> right_margin.scroll

(* We found the problem with highlighted lines being deleted as follows. Suppose we use drag/drop to move a line from pane A to line 1 in pane B. In pane B, line 1 gets the Drag From highlight and line 2 gets the Drag To highlight, to show that we inserted a line before it. Then we move line 1 from pane B back to pane A. The pane B PC still has the Drag To highlighted line set to a DocumentLine with LineNumber 2, which is now deleted. That is, we removed line 1 from pane B, and AvalonEdit does not change the DocumentLine with LineNumber 2 to LineNumber 1. Instead, it deletes the DocumentLine. This is true even if the DocumentLine does not have a LineNumber larger than Document.LineCount.
We could store line numbers instead of DocumentLines, but the line numbers would still become outdated if we added or removed lines to the document in any way, even without using drag/drop as described above. In fact, the current solution is probably the safest one. As soon as the document lines change, the highlighted lines are deleted and that stops us from using them to redraw the highlights incorrectly when the extent height changes.
That is also safer than trying to anticipate all the places where we should clear the highlighted lines manually. This effectively clears them when the document lines change and render them outdated, and when they are assigned new lines, those lines will be valid because they derive from user input such as clicking or drag/dropping. *)
(* See the comments for lines_to_text_helper_cut. We check each line's IsDeleted property here in case it was deleted due to a change in the document. However, when the user deletes the text for a given line, a different line might be deleted instead, because of the red-black tree used by AvalonEdit. As a result, the user might delete the text for a line, then resize Tagger, and the highlight might not be redrawn for a line whose text was not deleted. We decided this is too rare a situation to worry about for now. *)
/// <summary>Handle the extent height changed event. Return unit.</summary>
    let extent_height_changed_handler () =
(* Helper to redraw highlights. *)
        let redraw_highlights (highlight_type, (lines : Document.DocumentLine list)) =
(* If the list of lines is not empty, and the lines are valid lines in the current document, continue. Note that line numbers start from 1. *)
(* Previously we checked that the lines were valid as follows:
lines.last.LineNumber <= editor.Document.LineCount
However, if a line has been deleted, trying to get its LineNumber raises an exception. *)
            if lines.Length > 0 && lines |> List.forall (fun line -> line.IsDeleted = false) then
(* Get the new top and bottom Y positions for the lines and redraw the corresponding highlights. *)
                let y_positions = lines |> List.map editor.get_line_top_bottom
                do
                    left_margin.redraw highlight_type y_positions
                    right_margin.redraw highlight_type y_positions
(* Redraw all highlights. *)
        do
            [DragFrom, _drag_from_lines;
            DragTo, _drag_to_lines;
            OpenTag, _open_tag_lines]
            |> List.iter redraw_highlights

//#endregion

(* Method helpers. *)
//#region
/// <summary>Close file (1). Return unit.</summary>
    let close_file_helper file =
        do
(* Save the caret position of the currently open file. *)
            save_vertical_position file (editor.CaretOffset |> float)
(* Notify the MainController that the project needs to be saved. *)
            _save_project.Trigger ()
(* Clear the lists of highlighted lines, so we don't try to redraw the highlights in extent_height_changed_handler, which is called upon closing the file. *)
            _drag_from_lines <- []
            _drag_to_lines <- []
            _open_tag_lines <- []
(* Disable the margins. *)
            left_margin.IsEnabled <- false
            right_margin.IsEnabled <- false
(* Close the file. Get the document of the closed file. *)
        let document = editor.close_file ()
// TODO2 Consider checking the _is_modified flag in the editor before saving. Editor.close_file would need to record the value of the flag (before it clears it) and return it along with the document.
(* Save the document of the closed file. Documents are not saved in the project data. *)
        do save_document file document
//#endregion

(* Constructor. *)
//#region
(* Add event handlers. *)
    do
        tabs.tab_selected.Add select_tab_handler
        tabs.tab_closed.Add (this.close_tab >> ignore)
        tabs.tab_all_closed.Add close_all_tabs_handler
        tabs.save_project.Add _save_project.Trigger
        tabs.tabs_left_mouse_button_up.Add tabs_left_mouse_button_up_handler
        editor.general_error.Add editor_general_error_handler
        editor.save_file_error.Add (fun (file, error) -> save_file_error_handler file error)
        editor.right_click.Add _right_click.Trigger
        editor.find_in_project.Add _find_in_project.Trigger
        editor.tag_symbol_entered.Add _tag_symbol_entered.Trigger
        editor.tag_selected.Add _tag_selected.Trigger
        editor.margin_selection_changed.Add margin_selection_changed_handler
        editor.margin_drag.Add editor_margin_drag_handler
        editor.margin_drop.Add editor_margin_drop_handler
(* When the user ends a drag operation in the editor, hide the drag to highlights. *)
        editor.end_drag.Add (fun () -> do
            left_margin.hide_drag_to_rects ()
            right_margin.hide_drag_to_rects ()
            )
        editor.scroll_changed.Add scroll_changed_handler
        editor.extent_height_changed.Add extent_height_changed_handler
(* When the editor gets or loses focus, change the border color. *)
        editor.GotFocus.Add (fun _ -> do tabs.BorderBrush <- System.Windows.Media.Brushes.Red)
        editor.LostFocus.Add (fun _ -> do tabs.BorderBrush <- System.Windows.Media.Brushes.Black)
        editor.document_changed.Add editor_document_changed_handler
        left_margin.drop.Add _margin_drop.Trigger
        right_margin.drop.Add _margin_drop.Trigger
        left_margin.select.Add margin_select_handler
        right_margin.select.Add margin_select_handler
        left_margin.drag_start.Add margin_drag_start_handler
        right_margin.drag_start.Add margin_drag_start_handler
        left_margin.right_click.Add margin_right_click_handler
        right_margin.right_click.Add margin_right_click_handler
        left_margin.drag_over.Add margin_drag_over_handler
        right_margin.drag_over.Add margin_drag_over_handler
        left_margin.drag_leave.Add margin_drag_leave_handler
        right_margin.drag_leave.Add margin_drag_leave_handler
(* When the user ends a drag operation, stop any scrolling due to dragging in the editor. *)
        left_margin.end_drag.Add (fun () -> do editor.stop_drag_scroll_timers ())
        right_margin.end_drag.Add (fun () -> do editor.stop_drag_scroll_timers ())
(* Handle the margin firing a debug event. Write the debug information to the end of the document in the editor. *)
        left_margin.debug.Add (fun text -> editor.Document.Insert (editor.Document.TextLength, text) |> ignore)
        right_margin.debug.Add (fun text -> editor.Document.Insert (editor.Document.TextLength, text) |> ignore)
//#endregion

(* Methods. *)
//#region

/// <summary>Load file (1). If we succeed, return a new document that contains the text of the file; otherwise, return None.</summary>
    static member load_file_into_document file =
(* Load the file. Return a new document that contains the file. *)
        try new Document.TextDocument (file |> File.ReadAllText) |> Some
(* If we fail to load the file, log the error. *)
        with | ex ->
            do _logger.Log_ ("PaneController.OpenFileError", ["path", file; "message", ex.Message])
            None

/// <summary>If the file (1) for the tab to be closed is not open in the editor, close the tab. If the file is open in the editor, try to save it. If we succeed, close the file and then close the tab; otherwise, leave the file and the tab open. Return true if we successfully save the file; otherwise, false.</summary>
    member this.close_tab file =
(* See if the file is open in the editor. *)
        match editor.file with
        | Some file_ when file_ = file ->
(* Save the file currently open in the editor and close it. If we fail, stop. If we succeed, close the tab. *)
            if this.close_file () then
                do tabs.close_tab file
                true
            else false
(* If the file isn't open, just close the tab. *)
        | _ ->
            do tabs.close_tab file
            true

(* We tried simply saving the old undo stack and assigning the editor a new undo stack. The problem is, the old undo stack is just a reference, not a copy, so the editor can still change it. In ILSpy, we found the undo stack is cleared whenever (1) the Text property is changed, which happens when we open or close a file, or (2) the UndoStack property is changed, which happens when we assign a new undo stack to the editor.

Per AvalonEdit documentation: "The UndoStack listens to the UpdateStarted and UpdateFinished events". However, it doesn't actually handle any events. If you look at Document.TextDocument.BeginUpdate, for example, it calls UndoStack.StartUndoGroup. In other words, Document notifies the undo stack that is assigned to it.

Possible solutions:
1. Subclass UndoStack and override ClearAll to do nothing. If we actually need ClearAll, call the base implementation.
a. Problem: UndoStack is sealed.
2. Use reflection to access the internal variables of UndoStack, so we can make a copy.
a. Problem: There are a lot of these variables.
3. Use reflection to replace ClearAll.
a. Problem: Not sure we can. Possibly helpful links:
http://stackoverflow.com/questions/9684804/is-there-a-way-to-override-a-method-with-reflection
http://msdn.microsoft.com/en-us/library/system.reflection.emit.typebuilder.definemethodoverride.aspx
4. Get the source for AvalonEdit and recompile.

We're currently doing #4. We left ClearAll alone, but commented out both calls to it.

20141005: Previously, we saved the UndoStack for each file the user had opened, and reattached the UndoStack to the editor when the user opened the file again. However, we realized it would be simpler to do this with the TextDocument for each file, which contains the UndoStack anyway. So we no longer need to set the TextDocument.Text property, so AvalonEdit does not call UndoStack.ClearAll. We reverted the previously described changes to AvalonEdit.
*)

/// <summary>Save the file currently open in the editor. If we succeed, close it and return true. Otherwise, leave it open and return false.</summary>
    member this.close_file () =
(* If no file is open in the editor, return true. *)
        match editor.file with
        | None -> true
        | Some file ->
(* Try to save the currently open file. *)
            match editor.save_file () with
            | Some error ->
(* If we fail, report the error, do not close the file, and return false. *)
                do _logger.Log_ ("PaneController.SaveFileError", ["path", file; "message", error])
                false
            | None ->
(* If we succeed, close the file. *)
                do file |> close_file_helper
(* Return true. *)
                true

/// <summary>Add a tab for the file (1) to the tab control, then select it. Return true if the tab control already had such a tab, or if we added the tab successfully. Return false if we failed to add the tab (for example, if the file does not exist).</summary>
    member this.add_and_select_tab file =
(* The tab control won't open a new tab if it already has a tab for this file. *)
        match tabs.add_tab file with
        | Some tab ->
            do tab.select ()
            true
        | None ->
(* If we failed to add the tab (which happens if the file does not exist), report an error. *)
            do _logger.Log_ ("PaneController.OpenFileError", ["path", file; "message", "File not found."])
            false

(* See the comments for lines_to_text_helper_cut. *)
/// <summary>Show highlight type (1) on lines (2) for (3) seconds. Return unit.</summary>
    member this.highlight_lines highlight_type (lines : Document.DocumentLine list) time =
(* Save the lines to be highlighted, so we can redraw the highlights later if the document extent height changes. See Editor.scroll_changed_handler for more information. *)
        match highlight_type with
        | DragFrom -> do _drag_from_lines <- lines
        | DragTo -> do _drag_to_lines <- lines
        | OpenTag -> do _open_tag_lines <- lines
(* Get the top and bottom Y positions of the lines. *)
        let y_positions = lines |> editor.get_lines_top_bottom
(* Show the highlights. *)
        do
            left_margin.show highlight_type y_positions time
            right_margin.show highlight_type y_positions time

/// <summary>Margin select and highlight lines (1). (2) The most recently margin selected line. (3) True if the user pressed Shift while margin selecting the line. Return unit.</summary>
    member this.margin_select_lines lines most_recently_margin_selected_line shift =
        do
(* We margin select the lines first to make sure the editor scrolls to them and makes them visible for highlighting. *)
(* Margin select the lines in the editor. Save the line the user clicked as the most recently margin selected line. *)
            editor.update_margin_selected_lines lines most_recently_margin_selected_line shift
(* Show the drag from/margin selection highlights indefinitely. *)
            this.highlight_lines DragFrom lines 0

/// <summary>Scroll the line with line number (1) to Y position (2) and display the open tag highlights for time (3). Return unit.</summary>
    member this.display_line line_number y_position time =
(* Convert the line number to a document line, so we can get the visual line. *)
        let line = editor.safe_line_number_to_line line_number
        do
(* Try to scroll the line to the specified Y position. *)
            editor.scroll_line line y_position
(* Move the cursor to the line. *)
            editor.CaretOffset <- line.Offset
(* Show the open tag highlights. *)
            this.highlight_lines OpenTag [line] time

//#endregion

(* Expose the controls. *)
    member this.editor = editor
    member this.tabs = tabs
    member this.left_margin = left_margin
    member this.right_margin = right_margin

(* Expose methods for testing. *)
    member this.test_open_file_helper = open_file_helper
    member this.test_open_file = open_file
    member this.test_select_tab_handler = select_tab_handler
    member this.test_close_all_tabs_handler = close_all_tabs_handler
    member this.test_editor_margin_drop_handler = editor_margin_drop_handler

(* Expose the vertical positions and documents. *)
    member this.test_vertical_positions = !vertical_positions
    member this.test_documents = !documents

(* Expose events. *)
    member this.margin_drop = _margin_drop.Publish
/// <summary>The user right-clicked a line in the margin.</summary>
    member this.margin_right_click = _margin_right_click.Publish
    member this.right_click = _right_click.Publish
/// <summary>The user pressed the key combination for Find in Project.</summary>
    member this.find_in_project = _find_in_project.Publish
    member this.save_project = _save_project.Publish
/// <summary>The user entered the tag symbol.</summary>
    member this.tag_symbol_entered = _tag_symbol_entered.Publish
/// <summary>The user selected a tag in the TagCompletionWindow. (1) The tag.</summary>
    member this.tag_selected = _tag_selected.Publish