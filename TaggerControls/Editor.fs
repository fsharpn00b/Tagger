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

(* TODO1 Work items.
- Can we remove the title bar? Apparently not without also covering the Windows taskbar.

- Try using StartUndoGroup/EndUndoGroup when we tag multiple lines or make other multi-line changes.

- Note that pressing a key with an editor selection replaces the selection with the keystroke. This does not happen with a margin selection - it just clears the margin selection and processes the keystroke. Should fix that in text_changed_handler?
*)

namespace TaggerControls

// Action, DateTime, Math, Nullable, Reflection, TimeSpan
open System
// File
open System.IO
// Regex
open System.Text.RegularExpressions
// AutoResetEvent
open System.Threading
// Input, Point, Thickness, *Args
open System.Windows
// ScrollViewer, ScrollChangedEventArgs
open System.Windows.Controls
// Key, Keyboard, Mouse, ModifierKeys, *Args
open System.Windows.Input
// Brushes
open System.Windows.Media
// DispatcherTimer
open System.Windows.Threading

open ICSharpCode.AvalonEdit

// MaybeMonad
open Monads
// List extensions
open ListHelpers

/// <summary>Contains information about a right-click event.</summary>
type RightClickData = {
(* A value of None is allowed because we only require the position to scroll the selected find result, which is optional. The Find in Project add on server command might have a position value of None if no pane has focus or no file is open. See TagController.get_pane_and_position and TagController.show_find_in_project_dialog_helper. *)
(* The position, if there is a valid one where the user clicked; otherwise, None. *)
    position : TextViewPosition option;
(* The words at position (1) and the words in the selected text. *)
    words : string list;
(* The links at position (1) and the links in the selected text. *)
    links : string list;
(* The tag at position (1), if any. *)
    tag : TagWithSymbol option;
}

(* Commenting this out for now, as it doesn't work the way we want. The source line is cut off for some reason. Also, not sure how we would use this to highlight lines. We would need to collect all source lines from separate LineInserted events. *)
(*
type LineTracker (doc : Document.TextDocument) =
    interface Document.ILineTracker with
        member this.BeforeRemoveLine _ = ()
        member this.LineInserted (target_line, new_line) =
(* We could simply pass a Document.DocumentLine to Document.GetText, but we are not sure whether that uses DocumentLine.Length or TotalLength. *)
            sprintf "Line inserted.\nSource: %s\ntarget: %s" (doc.GetText (new_line.Offset, new_line.TotalLength)) (doc.GetText (target_line.Offset, target_line.TotalLength))
            |> MessageBox.Show |> ignore
        member this.RebuildDocument () = ()
        member this.SetLineLength (_, _) = ()
*)

/// <summary>Describes find word results in a line of a file.</summary>
type FindWordInFileResult = {
/// <summary>The index of the file.</summary>
    file_index : int;
/// <summary>The line number.</summary>
    line_number : int;
/// <summary>The line text.</summary>
    line_text : string;
/// <summary>True if the match is case-sensitive.</summary>
    match_case : bool;
/// <summary>True if the match is a whole word.</summary>
    match_whole_word : bool;
}

(* We include a file field in FindTagInFileResult, rather than in a FindTagInProjectResults type, so we can group these results by tag first, and then by file. *)
/// <summary>Describes find tag results in a line of a file.</summary>
type FindTagInFileResult = {
(* We get the file so we can sort the results by tag and then by file in TagController.find_all_tags_in_files. *)
/// <summary>The file.</summary>
    file : string;
/// <summary>The index of the file.</summary>
    file_index : int;
/// <summary>The tag.</summary>
    tag : TagWithoutSymbol;
/// <summary>The line number.</summary>
    line_number : int;
/// <summary>The line text.</summary>
    line_text : string;
}

/// <summary>A list of tuples that describe find text results in a project. (1) The file path. (2) The results in the file.</summary>
type FindWordInProjectResults = (string * FindWordInFileResult list) list

// TODO2 Make colors in colorizer configurable.

(* Note in this file, highlighting means changing the background color of text, not showing highlights in the margins. *)
(* For a color reference, see:
msdn.microsoft.com/en-us/library/system.windows.media.brushes.aspx
*)
/// <summary>Colors lines of text in the Editor. (1) A reference to the margin selected lines in the Editor.</summary>
type TaggerTextEditorColorizer (margin_selected_lines) =
    inherit Rendering.DocumentColorizingTransformer ()
/// <summary>This is called by AvalonEdit to decide whether and how to color line (1). Return unit.</summary>
    override this.ColorizeLine (line : Document.DocumentLine) =
(* Change the background color of the first character in the line to distinguish the first TextRun from following TextRuns. *)
        do base.ChangeVisualElements (0, 1, (fun element ->
            element.BackgroundBrush <- Brushes.MediumBlue
        ))
(* If the highlighted lines contain this line... *)
(* Do not try to look up the line numbers of deleted lines. *)
        if !margin_selected_lines |> List.filter (fun (line : Document.DocumentLine) -> line.IsDeleted = false) |> List.exists (fun (highlighted_line : Document.DocumentLine) -> highlighted_line.LineNumber = line.LineNumber) then
(* Change the background color of the remaining characters in the line. *)
            do base.ChangeVisualElements (1, line.EndOffset, (fun element ->
                element.BackgroundBrush <- Brushes.Navy
            ))

type TaggerTextEditor () as this =
    inherit TextEditor ()

(* Member values: configuration settings. *)
//#region
/// <summary>The delay before hovering the mouse causes a tool tip to appear.</summary>
    let mutable _mouse_hover_delay = 0
/// <summary>The number of lines scrolled per mouse wheel notch.</summary>
    let mutable _mouse_scroll_speed = 0
/// <summary>The number of lines scrolled per second while the user drags near the top or bottom of the editor.</summary>
    let mutable _drag_scroll_speed = 5
(* The save_timer_interval config property gets and sets the Interval property of this timer. *)
/// <summary>The interval, in milliseconds, at which the currently open file is auto-saved.</summary>
    let _save_timer = new DispatcherTimer ()
//#endregion

(* Member values. *)
//#region
/// <summary>Used to lock access to the file path, editor contents, and attached document.</summary>
    let _file_lock = ref 0
/// <summary>True if the open file has been changed since it was last opened or saved; otherwise, false. For more information see save_timer_tick_handler.</summary>
    let mutable _is_modified = false
/// <summary>True if the ScrollViewer.ScrollChanged event handler has been added; otherwise, false.</summary>
    let mutable _scroll_changed_handler_added = false
/// <summary>If there is an error when auto-saving the open file, we want to continue attempting to auto-save, but not to fire the event more than once.</summary>
    let mutable _suppress_save_file_error = false
/// <summary>The path of the file that is currently open.</summary>
    let mutable _file = None
/// <summary>This lets us wait for either (1) the mouse to move or (2) the delay to expire, in which case we show the tool tip. Set the initial state to not signaled. We expose the delay as a property.</summary>
    let _mouse_move = new AutoResetEvent (false)
/// <summary>The ScrollViewer of the AvalonEdit.TextEditor.</summary>
    let mutable _scroll_viewer : ScrollViewer = null
(* DispatcherTimer lets us specify an event to happen at a specified interval.
http://msdn.microsoft.com/en-us/magazine/cc163328.aspx
*)
/// <summary>The interval, in milliseconds, at which the document scrolls up one line while the user drags near the top of the editor.</summary>
    let _drag_scroll_up_timer = new DispatcherTimer ()
/// <summary>The interval, in milliseconds, at which the document scrolls up one line while the user drags near the top of the editor.</summary>
    let _drag_scroll_down_timer = new DispatcherTimer ()
(* AvalonEdit SearchPanel. *)
    let _search = Search.SearchPanel.Install (this)
(* Tag completion window. *)
    let _tag_completion_window = new TagCompletionWindow ()
/// <summary>The currently margin selected lines.</summary>
    let _margin_selected_lines = ref []
/// <summary>Colors lines of text.</summary>
    let _colorizer = new TaggerTextEditorColorizer (_margin_selected_lines)
/// <summary>The most recently margin selected line. We use this to decide margin selection behavior.</summary>
    let mutable _most_recently_margin_selected_line : Document.DocumentLine option = None
/// <summary>The line that was most recently margin selected without pressing Shift. We use this to decide margin selection behavior.</summary>
    let mutable _most_recently_margin_selected_line_no_shift = None
//#endregion

(* Events. *)
//#region
/// <summary>A general non-critical error occurred. (1) The error message.</summary>
    let _general_error = new Event<string> ()
/// <summary>There was an error when auto-saving the open file.</summary>
    let _save_file_error = new Event<string * string> ()
/// <summary>The user right-clicked.</summary>
    let _right_click = new Event<RightClickData> ()
/// <summary>The user pressed the key combination for Find in Project.</summary>
    let _find_in_project = new Event<TextViewPosition> ()
/// <summary>The margin selection changed.</summary>
    let _margin_selection_changed = new Event<unit> ()
/// <summary>The user dragged from a margin and over the editor.</summary>
    let _margin_drag = new Event<TaggerMargin * DragEventArgs> ()
/// <summary>The user dragged from a margin and dropped on the editor.</summary>
    let _margin_drop = new Event<MarginDropData * TaggerMargin> ()
/// <summary>The user ended a drag.</summary>
    let _end_drag = new Event<unit> ()
/// <summary>The scroll offset of the document changed. (1) The change in the vertical scroll offset.</summary>
    let _scroll_changed = new Event<float> ()
/// <summary>The extent height of the document changed.</summary>
    let _extent_height_changed = new Event<unit> ()
/// <summary>The user entered the tag symbol. (1) The text after the tag symbol, or None.</summary>
    let _tag_symbol_entered = new Event<string option> ()
/// <summary>The user selected a tag in the TagCompletionWindow. (1) The tag. (2) Additional text the user entered, if any.</summary>
    let _tag_selected = new Event<TagWithoutSymbol * string> ()
/// <summary>The user inserted or deleted text. (1) The text. (2) True if the user inserted text; false if the user deleted text.</summary>
    let _document_changed = new Event<string * bool> ()
//#endregion

(* Helper functions: general. *)
//#region
/// <summary>Convert (1) from nullable type to option. Return the option.</summary>
    let nullable_to_option (x : Nullable<_>) = if x.HasValue then x.Value |> Some else None
//#endregion

(* Helper functions: position operations. *)
//#region

// TODO2 Add a position validation function similar to validate_lines. Only needs to be called for methods though?

/// <summary>Get a TextViewPosition using the GetPosition function (1) from Input.MouseEventArgs, which takes an element as input and returns the mouse position as a Point relative to that element. Return the position if it is inside the document; otherwise, return None.</summary>
    let get_position get_position_function =
(* Editor.GetPositionFromPoint gives us a line and column, which we can use to get an offset. It returns null if the point is outside the document. *)
        this |> get_position_function |> this.GetPositionFromPoint |> nullable_to_option

(* We tried using Document.TextUtilities.GetNextCaretPosition for position_to_word. However, we had to do too much work to compensate for its choice of word borders. For example:
If the offset is at the start of a word, GetNextCaretPosition jumps to the start/end of the previous word, depending on the settings. Or if the offset is at the end of a word, GetNextCaretPosition jumps to the start of the next word. So we have to test if the offset is already on a word border before calling GetNextCaretPosition.
If the word is at the start or end of the document, GetNextCaretPosition returns -1.
 *)

(* This is another version of position_to_word. It works, though it is less aggressive about finding words than the version that uses regular expressions. Unlike that version, if the offset is not a letter, digit, or tag symbol, it returns None. *)
(*
    let position_to_word_border_offsets (pos : TextViewPosition) =
        let is_tag offset = offset |> this.Document.GetCharAt = TaggerTextEditor.tag_symbol
        let is_word offset = offset |> this.Document.GetCharAt |> Char.IsLetterOrDigit
        let is_word_or_tag offset =
            let char_ = offset |> this.Document.GetCharAt
            char_ |> Char.IsLetterOrDigit || char_ = TaggerTextEditor.tag_symbol

        let offset =
            let offset_ = pos.Location |> this.Document.GetOffset
            if offset_ = this.Document.TextLength then offset_ - 1 else offset_
        if offset |> is_word_or_tag = false then None
        else
            let start_offset, is_tag_ =
                let rec scan_left current_offset =
                    if current_offset = 0 then current_offset, current_offset |> is_tag
(* Adding 1 to the current offset is safe, even if this is the first call to scan_left, because we already checked that the offset is no larger than Document.TextLength - 1. *)
                    else if current_offset |> is_tag then current_offset + 1, true
                    else if current_offset |> is_word then scan_left <| current_offset - 1
(* See the above comment about adding 1 to the current offset. *)
                    else current_offset + 1, false
                offset |> scan_left
            let end_offset =
                let rec scan_right current_offset =
                    if current_offset = this.Document.TextLength then current_offset
                    else if current_offset |> is_word then scan_right <| current_offset + 1
                    else current_offset
(* See the above comment about adding 1 to the current offset. *)
                let offset_ = if offset |> is_tag then offset + 1 else offset
                offset_ |> scan_right
            Some (start_offset, end_offset, is_tag)

    let position_to_word (pos : TextViewPosition) =
        match pos |> position_to_word_border_offsets with
        | Some (start_offset, end_offset, is_tag) ->
            Some (this.Document.GetText (start_offset, end_offset - start_offset), is_tag)
        | None -> None
*)

(* AvalonEdit help: Coordinate Systems:
Offsets usually represent the position between two characters. The first offset at the start of the document is 0; the offset after the first char in the document is 1. The last valid offset is document.TextLength, representing the end of the document. This is exactly the same as the index parameter used by methods in the .NET String or StringBuilder classes. *)
/// <summary>Get the words, links, or tag at position (1) in the document. If any of these are found, return a tuple. (R1) The words. (R2) The links. (R3) The tag, if one was found; otherwise, None. If none of these are found, return None. Preconditions: (1) is a valid position in the current document.</summary>
    let position_to_words_links_tag (position : TextViewPosition) =
(* Get the text of the line that contains the position. *)
        let (line : Document.DocumentLine) = position.Line |> this.safe_line_number_to_line
(* We could simply pass a Document.DocumentLine to Document.GetText, but we are not sure whether that uses DocumentLine.Length or TotalLength. *)
        let line_text = this.Document.GetText (line.Offset, line.TotalLength)
(* Columns start from 1. So if the column is 1, the index is 0. We check that we haven't been given a column of 0 or less. *)
        let start_index = if position.Column > 0 then position.Column - 1 else 0
(* Find the text at the index. *)
        match TagInfo.position_to_text_bordered_by_whitespace line_text start_index with
        | Some text ->
(* Find the words in the text. *)
            let words = TagInfo.find_words text
(* Note text is bordered by whitespace, and so are links (according to the regex used by TagInfo.find_links), so there is currently no way to find more than one link based on the position. *)
(* Find the links in the text. *)
            let links = TagInfo.find_links text
(* If the text does not contain links, find the tag at the index. We check that the text does not contain tags first because, depending on the tag symbol, a link could appear to contain a tag. For instance, http://abc#xyz appears to contain the tag #xyz. We currently do not know how to search for a tag at a specified index and use a negative lookbehind to exclude links at the same time. Also see the comments for TagInfo._position_to_tag_with_symbol_pattern.
TODO3 If we solve that, we should consider checking for tags first, because TagController.right_click ignores words and links if RightClickData.tag contains a value. 
*)
            let tag = if links |> Seq.isEmpty = false then None else TagInfo.position_to_tag_with_symbol line_text start_index
            Some (words, links, tag)
        | None -> None
(*
(* We use GetVisualLine when expect the visual line to exist and can fail gracefully without it.
We use GetOrConstructVisualLine when we do not expect the visual line to exist or cannot fail gracefully without it.
Also see the comments for get_line_top_bottom and scroll_line. *)
(* The VisualLine is the entire line, whereas a TextLine is a single row that results from word wrapping and is rendered by WPF. For more information see AvalonEdit help, topic Text Rendering. For information about TextLine see:
http://msdn.microsoft.com/en-us/library/system.windows.media.textformatting.textline.aspx
We tried here to detect whether the position is in the dead space caused by word wrap, but Column and VisualColumn both show up as the first character of the next word, even though it is on another TextLine. We kept this code in case it's useful later.
TODO3 See AvalonEdit documentation for Editing.Caret.IsInVirtualSpace and TextEditorOptions.EnableVirtualSpace.
*)
            let visual_line = this.TextArea.TextView.GetVisualLine pos.Line
            let text_line = visual_line.GetTextLine pos.VisualColumn
            let text_line_start = visual_line.GetTextLineVisualStartColumn text_line
            let column = pos.Column - text_line_start
            do! column <= text_line.Length
*)

(* This is currently not used. *)
(*
/// <summary>Margin select the line based on position (1). If the position is None (that is, it is outside the document), margin select the last line. Return unit. Preconditions: (1), if not None, is a valid position in the current document.</summary>
    let safe_margin_select_from_position position = [position |> this.safe_position_to_line] |> this.update_margin_selected_lines
*)
//#endregion

(* Helper functions: line operations. *)
//#region

(* Notes:
Per the AvalonEdit documentation:
EndOffset is before the newline, if any.
EndOffset = Offset + Length, so Offset + Length = EndOffset.
TotalLength includes the newline. Length does not.
The last valid offset is document.TextLength.
The last line in the document does not have a newline at the end. For that line, Length = TotalLength.

Conclusions:
EndOffset is the character after the string described by Offset and Length.
For example, for Offset 0 and Length 1, the EndOffset is 1.
Length does not include the character at EndOffset. That is, when we GetText (Offset, Length), we don't try to get the character at Offset + Length.
For example, GetText (0, 3) gets the characters at offsets 0, 1, 2.
It is valid to check that GetText (Offset, Length) or GetText (Offset, EndOffset - Offset) is safe by comparing Document.TextLength to Offset + Length or EndOffset. *)
/// <summary>If the lines in (1) are valid for the current document, sort them by line number and return the sorted list. If not, return None.</summary>
    let validate_lines (lines : Document.DocumentLine list) =
        if lines.Length = 0 then Some []
// TODO2 Currently we cannot do this, because draw_lines might be passed deleted lines. See the comments there.
//        else if lines |> List.exists (fun line -> line.IsDeleted) then None
        else
            let lines_ = lines |> List.sortBy (fun line -> line.LineNumber)
(* TODO3 Consider the following way to validate lines. However, this is more expensive.
lines |> List.forall (fun line -> this.Document.Lines |> Seq.exists (fun document_line -> line = document_line))
*)
(* Currently, we validate a line by making sure its end offset does not exceed the current document length.
The only way I know to get a line whose end offset exceeds the current document length is to get a line from another document. For example, if you get a line from a document and then clear the document, the line is also cleared. *)
(* DocumentLine.EndOffset is before the newline, but the last line in the document has no newline. So if lines.last is the last line in the document, its end offset should be the same as the document length. *)
            if lines.last.EndOffset > this.Document.TextLength then None
            else Some lines_

(* See the comments for validate_lines. *)
/// <summary>If line (1) is valid for the current document, return it. If not, return None.</summary>
    let validate_line (line : Document.DocumentLine) =
        if line.EndOffset > this.Document.TextLength then None
        else Some line

(* We use GetVisualLine when expect the visual line to exist and can fail gracefully without it.
We use GetOrConstructVisualLine when we do not expect the visual line to exist or cannot fail gracefully without it.
Also see the comments for get_line_top_bottom and scroll_line. *)
/// <summary>Draw lines (1) to add or remove highlights, based on whether the lines are margin selected. Return unit.</summary>
    let draw_lines (lines : Document.DocumentLine list) =
(* Loop through the lines. *)
        lines |> List.iter (fun line ->
(* Note draw_lines is called by clear_margin_selected_lines, which is called by text_changed_handler. Because we currently handle TextChanged rather than PreviewTextChanged, _margin_selected_lines might include deleted lines. *)
            if line.IsDeleted then ()
            else
(* Get the visual line for this line. GetVisualLine returns null if the line is outside the visual range. In that case, we expect AvalonEdit to draw the line when it enters visual range. *)
                match line.LineNumber |> this.TextArea.TextView.GetVisualLine with
                | null -> ()
(* Redraw this visual line. This causes AvalonEdit to call _colorizer.ColorizeLine for this line. *)
                | visual_line -> do visual_line |> this.TextArea.TextView.Redraw
        )

/// <summary>Clear the currently margin selected lines. (1) True to clear the line that was most recently margin selected without the user pressing Shift. Return unit.</summary>
    let clear_margin_selected_lines clear_most_recently_margin_selected_line_no_shift =
(* Copy the currently margin selected lines. We do this because we need to redraw these lines, but we cannot do that until we clear the list, which is read by ColorizeLine. *)
        let old_margin_selected_lines = (!_margin_selected_lines).copy ()
        do
(* Clear the currently margin selected lines. *)
            _margin_selected_lines := []
(* Redraw the previously margin selected lines. *)
            old_margin_selected_lines |> draw_lines
(* Clear the most recently margin selected lines. *)
            _most_recently_margin_selected_line <- None
        if clear_most_recently_margin_selected_line_no_shift then do _most_recently_margin_selected_line_no_shift <- None

/// <summary>Highlight the currently margin selected lines. Return unit.</summary>
    let highlight_margin_selected_lines () =
        do !_margin_selected_lines |> draw_lines

(* Add TotalLength to Offset to include the newline if any. If this is the last line in the document and there is no newline, then TotalLength = Length. Also see the comments for validate_lines. *)
/// <summary>Return a tuple with information about line (1). (R1) The start offset. (R2) The end offset, which does not include the newline. (R3) The total length, which includes the newline.</summary>
    let line_to_start_end_length (line : Document.DocumentLine) = line.Offset, line.EndOffset, line.TotalLength

(* See the comments for line_to_start_end_length. *)
/// <summary>Return a list of tuples with information about the lines (1). (R1) The start offset. (R2) The end offset, which does not include the newline. (R3) The total length, which includes the newline.</summary>
    let lines_to_start_end_length = List.map line_to_start_end_length

(* This function is called by:
Editor.scroll_changed_handler
Editor.show_tag_completion_window

All of these typically call this function with TextViewPositions that are already visible on the screen. So, for now, we dispense with calling UpdateLayout before calling GetVisualPosition. For more information see scroll_line. *)
/// <summary>Return the points for the top and bottom of the TextLine that contains position (1). Preconditions: (1) is a valid position in the current document.</summary>
    let get_text_line_top_bottom position =
(* Convert the position to a point relative to the document. Subtract the scroll position to convert that to a point relative to the editor. Convert that to a point relative to the screen. *)
        let top_point = this.TextArea.TextView.GetVisualPosition (position, Rendering.VisualYPosition.LineTop) - this.TextArea.TextView.ScrollOffset |> this.PointToScreen
        let bottom_point = this.TextArea.TextView.GetVisualPosition (position, Rendering.VisualYPosition.LineBottom) - this.TextArea.TextView.ScrollOffset |> this.PointToScreen
        top_point, bottom_point

(* See the comments for validate_lines. *)
/// <summary>Insert text (1) at offset (2). Return unit.</summary>
    let insert_line_at_offset_in_text text offset =
(* Verify the offset is valid for the current document. *)
        if offset > this.Document.TextLength then ()
        else do this.Document.Insert (offset, TaggerTextEditor.add_newline_to_end_of_text text)

(* It seems we can never save a reference to a document line after any change to the document that adds or removes a line. If we run the following code:

do editor.Text <- "1\n22\n333"
let line_1 = editor.Document.Lines.[0]
let line_2 = editor.Document.Lines.[1]
let line_3 = editor.Document.Lines.[2]
do editor.Document.Remove (0, 2)

We see the following results in the debugger.
line_1 still exists. LineNumber = 1.
line_2 is deleted.
line_3 still exists. LineNumber = 2.
This is even though we removed the text for line 1. We think this results from the red-black tree used by AvalonEdit to store document lines.
It makes no difference if we get the lines using Editor.Document.GetLineByNumber instead of Editor.Document.Lines.
*)
/// <summary>Cut lines (1) from the current document. (2) The target line of an expected paste operation, whose offset might be affected by the cut. Return a tuple. (R1) The cut text. (R2) The new offset of the target line. Preconditions: (1) are valid lines in the current document. (1) are sorted by line number. (1) does not contain (2).</summary>
    let lines_to_text_helper_cut (source_lines : Document.DocumentLine list) (target_line : Document.DocumentLine option) =
(* Get the start offset and total length (which includes the newline) for each line. *)
        let lines = source_lines |> List.map (fun line -> line.Offset, line.TotalLength)
(* (1) The remaining source lines. (2) The accumulated text. (3) The current target line offset. *)
        let rec helper lines text target_offset =
            match lines with
            | [] -> text, target_offset
            | (offset, length) :: lines ->
(* We could simply pass a Document.DocumentLine to Document.GetText, but we are not sure whether that uses DocumentLine.Length or TotalLength. *)
(* Get the text for this line. If it is the last line in the document, it does not end in a newline, so add one. *)
                let text = this.Document.GetText (offset, length) |> TaggerTextEditor.add_newline_to_end_of_text |> sprintf "%s%s" text
(* If the cursor is on the line to be removed, move it to a safe offset. *)
                if this.CaretOffset > offset && this.CaretOffset < offset + length then do this.CaretOffset <- offset
(* Remove the text for this line from the document. *)
                do this.Document.Remove (offset, length)
(* Loop through the remaining source lines and change their offsets to compensate for the cut. *)
                let lines = lines |> List.map (fun (offset2, length2) -> offset2 - length, length2)
(* If the target line is after the cut line, change the target line offset to compensate for the cut. *)
                let target_offset = if target_offset >= offset + length then target_offset - length else target_offset
(* Recurse with the remaining source lines. *)
                helper lines text target_offset
        do this.Document.UndoStack.StartUndoGroup ()
        let result =
            match target_line with
            | Some target_line -> helper lines "" target_line.Offset
            | None -> helper lines "" -1
        do this.Document.UndoStack.EndUndoGroup ()
        result

/// <summary>Return the text from lines (1) in the current document.</summary>
    let lines_to_text_helper_copy lines =
(* Loop through the lines. Get the start offset and total length (which includes the newline) for each. *)
        ("", lines |> lines_to_start_end_length) ||> List.fold (fun acc (offset, _, length) ->
(* We could simply pass a Document.DocumentLine to Document.GetText, but we are not sure whether that uses DocumentLine.Length or TotalLength. *)
(* Return the text for this line. If it is the last line in the document, it does not end in a newline, so add one. *)
            this.Document.GetText (offset, length) |> sprintf "%s%s" acc |> TaggerTextEditor.add_newline_to_end_of_text
            )

/// <summary>Return the index of the line that contains offset (1).</summary>
    let offset_to_line_index offset =
        let position = new TextViewPosition (offset |> this.Document.GetLocation)
(* Line numbers start from 1. *)
        position.Line - 1

// TODO2 If the user makes an editor selection, should we clear any margin selection, to make sure there's no conflict? Currently we just prioritize margin selection over editor selection when copying/cutting as we do for get_context_menu.

/// <summary>Copy or move the currently margin selected lines to the clipboard. (1) True to move the lines. Return unit.</summary>
    let margin_selection_to_clipboard remove =
(* Copy or cut the text. *)
        let text = this.lines_to_text !_margin_selected_lines remove
        do Clipboard.SetText text

// TODO1 Find a simpler or more native way to do this.
/// <summary></summary>
    let delete_margin_selection () =
        do this.lines_to_text !_margin_selected_lines true |> ignore

//#endregion

(* Helper functions: move and copy operations. *)
//#region

(* We handle drag and drop as follows.

Drag        Drop            Result
Editor      Same Editor     Handled by AvalonEdit. Only move is available, not copy or tag.
'           Other Editor    '
'           Same Margin     N
'           Other Margin    N
Margin      Same Editor     Editor.drop_handler/drag_drop_helper, PC.editor_margin_drop_handler, TC.margin_drop_helper
'           Other Editor    '
'           Same Margin     Margin.drop_handler/drop_handler_helper, TC.margin_drop_helper
'           Other Margin    '

TODO2 Also support the user margin selecting one or more lines, then dragging from the text instead of the margin. We would have to handle Editor.drag_start, then check if they are dragging from a margin selected line, in which case mark the event handled; otherwise, leave it unhandled and use default AvalonEdit drag behavior.
*)

(* Note this only applies to move or copy operations in the same pane.
Also see TaggerMargin.drag_over_helper, which does not show the drag to highlight when it coincides with the drag from highlight. *)
/// <summary>Helper function to determine whether a move or copy of source lines (1) to target lines (2) is valid. For instance, if the target line is the same as one of the source lines, the move or copy is not valid. Return true if the move or copy is valid; otherwise, false.</summary>
    let move_and_copy_helper (source_lines : Document.DocumentLine list) (target_line : Document.DocumentLine) =
(* If the source line list is empty, return false. *)
        if source_lines.Length = 0 then false
(* If the target line is the same as one of the source lines, return false. *)
        else source_lines |> List.exists (fun source_line -> source_line.LineNumber = target_line.LineNumber) = false

//#endregion

(* Helper functions: find operations. *)
//#region

/// <summary>Try to find text (2) in text (1). If we find the text, return a tuple. (R1) True if the match is case sensitive. (R2) True if the match is a whole word. If we do not find the text, return None.</summary>
    static let find_text (source_text : string) text =
(* Try to find the text. *)
        let index = source_text.IndexOf (text, StringComparison.OrdinalIgnoreCase)
(* If we find the text... *)
        if index > -1 then
(* There might be more than one match on a line. So we search the entire line again for case-sensitive matches and whole-word matches, to make sure we do not exclude any. *)
(* Try to get a case-sensitive match. *)
// TODO2 Consider getting matched text from the source_text with index and text.Length, then just case-sensitive compare that to text.
            let match_case = source_text.IndexOf text > -1
(* Try to match the whole word. We need to regex escape the text, since it might contain a tag symbol. Previously, we searched for a word border (\b) before and after the text. However, this caused the match to fail when we searched for a tag such as #0, because the # created a word border between itself and 0. As a result, there was no word border before the #. *)
            let whole_word_pattern = text |> Regex.Escape |> sprintf @"(?:^|\W)%s(?:$|\W)"
            let match_whole_word = Regex.Match (source_text, whole_word_pattern, RegexOptions.IgnoreCase)
            Some (match_case, match_whole_word.Success)
(* If we do not find the text, return None. *)
        else None

/// <summary>Find all tags in a line. (1) The line index. (2) The text of the line. If the find any tags, return a list of FindTagInFileResults; otherwise, return None.</summary>
    static let find_tags_in_line line_index line_text =
(* For each result, return a FindTagInFileResult. Note line numbers start from 1. *)
        let results = TagInfo.find_tags_without_symbol line_text |> List.map (fun tag ->
            {
(* The file and file_index fields are added by the caller. *)
                file = "";
                file_index = -1;
                tag = tag;
                line_number = line_index + 1;
                line_text = line_text;
            })
        if results.Length > 0 then Some results else None

/// <summary>Find text (1) in a line. (2) The index of the line. (3) The text of the line. If we find the text, return a FindWordInFileResult; otherwise, return None.</summary>
    static let find_text_in_line text line_index line_text =
        match find_text line_text text with
(* For each match, return a FindWordInFileResult. *)
        | Some (match_case, match_whole_word) ->
            {
(* The file and file_index fields are added by the caller. *)
                file_index = -1;
(* Note line numbers start from 1. *)
                line_number = line_index + 1;
                line_text = line_text;
                match_case = match_case;
                match_whole_word = match_whole_word;
            } |> Some
        | None -> None
//#endregion

(* Event handler helpers. *)
//#region
(* Commenting this out for now, because I don't remember what I wanted to do upon mousing over a word, and it's slowing down the application. *)
(*
/// <summary>Get the word at position (1) and display it in a tool tip. Return unit. Preconditions: (1) is a valid position in the current document.</summary>
    let mouse_hover_helper position =
        MaybeMonad () {
(* WaitOne returns true if the event was set; false if the timeout elapsed. If the former, exit. *)
            do! _mouse_move.WaitOne (_mouse_hover_delay) = false
(* Get the word. *)
            let! (word, _) = position_to_word position
            return word
        } |> function
        | Some word ->
(* If we get a word, show it in the tool tip. *)
            match this.ToolTip with
            | :? ToolTip as t ->
                do
                    t.Content <- word
(* Since the ToolTip already exists, use Visibility to show it. For more information see FileTreeViewItem.right_button_mouse_up. *)
                    t.Visibility <- Visibility.Visible
            | _ -> ()
        | None -> ()
*)

/// <summary>Based on TextViewPosition (1), fire an event to signal a right click. Return unit. Preconditions: (1), if not None, is a valid position in the current document.</summary>
    let mouse_right_up_helper position =
(* We need to find out whether the user:
1. right-clicked one or more words
2. right-clicked a link
3. right-clicked a tag
We also need to find out whether the user:
1. right-clicked without any selection
2. right-clicked with an editor selection
3. right-clicked with a margin selection
The reasons to right-click are to:
1. find a tag in project files (word)
2. find a word in project files (no position, word, editor selection)
3. find all tags in current project (no position)
4. apply an add on command to a link (word, margin selection)
5. move text (no position, margin selection)
6. add tags to text (no position, margin selection)
7. get the word count (no position, editor selection)
For items that involve margin selections (4-6), TagController.get_context_menu gets the margin selections. If there are no margin selections, it gets the line that contains the position that the user right-clicked. For items that involve the editor selection (6), TagController.get_word_count_menu_item gets the editor selection.
Also see the comments for TagController.get_context_menu.
Note the priority of words, links, and tags are as follows.
1. In position_to_words_links_tag, RightClickData.tag is set to Some only if we find no link in the text (bordered by whitespace) where the user clicked. (It does not matter whether there are links in the selected text.)
2. In TagController.right_click, if RightClickData.tag is Some, we show the Find in Project window for the tag, and do not show the context menu. *)
(* If there is an editor selection, get the words and links in it. *)
(* Note TagController.get_context_menu prioritizes margin selections over editor selections. *)
        let editor_selected_words, editor_selected_links =
            if this.SelectionLength > 0 then this.SelectedText |> TagInfo.find_words, this.SelectedText |> TagInfo.find_links
            else [], []
(* Define the default right-click data. *)
        let default_data = {
            position = position;
(* Include the editor selected words and links, if any. *)
            words = editor_selected_words;
            links = editor_selected_links;
            tag = None;
        }
        let data =
            match position with
            | Some position_ ->
(* If the position has a value, see if the user clicked one or more words. *)
                match position_to_words_links_tag position_ with
(* We considered including the word start offset, but TagController.right_click does not need it. It calls show_find_in_project_dialog or get_context_menu and neither of those need it. *)
(* If the user clicked one or more words, include the position, words, links, and tags. *)
                | Some (words, links, tag) ->
                    let tag =
                        match tag with
                        | Some tag_ -> tag_ |> TagInfo.add_tag_symbol_to_start |> Some
                        | None -> None
                    { default_data with
(* Combine the words the user clicked on with the words in the editor selection. Remove duplicate words. *)
                        words = Set.ofList words + Set.ofList editor_selected_words |> Set.toList
(* Combine the links the user clicked on with the links in the editor selection. We do not remove duplicate links, as we do not expect them to occur often. *)
                        links = List.append links editor_selected_links;
                        tag = tag;
                    }
(* If the user did not click any words, use the default data. *)
                | None -> default_data
(* If the position is None, use the default data. *)
            | None -> default_data
(* Fire the right_click event to the PaneController. *)
        do data |> _right_click.Trigger

/// <summary>Display all data in event args (1). Return unit.</summary>
    let scroll_changed_debug (args : ScrollChangedEventArgs) =
        sprintf @"
ExtentHeight: %f
ExtentWidth: %f
ExtentHeightChange: %f
ExtentWidthChange: %f

HorizontalOffset: %f
VerticalOffset: %f
HorizontalChange: %f
VerticalChange: %f

ViewportHeight: %f
ViewportWidth: %f
ViewportHeightChange: %f
ViewportWidthChange: %f" args.ExtentHeight args.ExtentWidth args.ExtentHeightChange args.ExtentWidthChange args.HorizontalOffset args.VerticalOffset args.HorizontalChange args.VerticalChange args.ViewportHeight args.ViewportWidth args.ViewportHeightChange args.ViewportWidthChange |> MessageBox.Show |> ignore

/// <summary>Convert the drag scroll speed setting to timer intervals and set the drag scroll timers. Return unit.</summary>
    let set_drag_scroll_timer_intervals () =
        let time =
            if _drag_scroll_speed > 0 then 1000.0 / (float) _drag_scroll_speed |> TimeSpan.FromMilliseconds
            else 1000.0 |> TimeSpan.FromMilliseconds
        do
            _drag_scroll_up_timer.Interval <- time
            _drag_scroll_down_timer.Interval <- time

/// <summary>Stop the drag scroll timers and hide the drag to highlight. Return unit.</summary>
    let end_drag_helper () =
        do
            this.stop_drag_scroll_timers ()
            _end_drag.Trigger ()

/// <summary>Helper for drag and drop handlers. (1) The event args. Verify the drag source is a TaggerMargin. If successful, return drag/drop data; otherwise, return None.</summary>
    let drag_drop_helper (args : DragEventArgs) =
(* I don't know how to see if the original source of the drag is a margin, so I check the args for a TaggerMargin, which the margin always sends when starting a DragDrop. *)
        if args.Data <> null then
(* Get the source margin. GetData returns an obj, even though we specify the type, so we have to downcast it. *)
            match args.Data.GetData typeof<TaggerMargin> with
            | :? TaggerMargin as margin ->
(* We simply construct a MarginDropData and fire it up to the PaneController with the margin_drag or margin_drop event, to be handled just as if the user dragged over or dropped on a margin. *)
                ({
(* Get the Y position that the user dropped on. The editor and margin have the same height, so it corresponds to a Y position on the margin. *)
                    y_position = (this |> args.GetPosition).Y
(* There's no way to know yet whether the user dragged from the margin in this pane or the other pane. We'll look at that in PaneController.editor_margin_drag_handler and TaggerMargin.drag_over_helper or PaneController.editor_margin_drop_handler. *)
                    same_margin = false
(* The keys that were pressed during the drop. *)
                    key_states = args.KeyStates
(* Return the data. Add the margin that the user dragged from, so the PC can see whether it's the margin attached to this editor or not. *)
                }, margin) |> Some
            | _ -> None
        else None

//#endregion

(* Event handlers: general. *)
//#region
/// <summary>Handler for the save timer Tick event. Return unit.</summary>
(* Only save the file if it has changed since it was opened or last saved. *)
    let save_timer_tick_handler () =
// TODO3 This might be fixed now that we are no longer modifying the AvalonEdit use of UndoStack.
(* TextEditor.IsModified was returning false negatives.
AvalonEdit uses UndoStack to determine whether file is modified. TextEditor.cs line 446:
SetCurrentValue(IsModifiedProperty, Boxes.Box(!document.UndoStack.IsOriginalFile));
I looked at UndoStack.IsOriginalFile and couldn't immediately tell why it would be giving us incorrect results for IsModified. But it might be that we broke something when we changed AvalonEdit not to clear the UndoStack every time it is set (i.e. when the user opens a file, we retrieve the associated UndoStack, and set it as the TextEditor.Document.UndoStack property).
For now, we implement our own "is modified" flag based on the TextChanged event. *)
        if _is_modified then
(* Try to save the currently open file, if any. *)
            match this.save_file () with
            | Some error ->
(* If we have already reported the error, don't do so again. *)
                if _suppress_save_file_error = false then
                    let file = match _file with | Some file_ -> file_ | None -> ""
                    do
(* Report the error. *)
                        _save_file_error.Trigger (file, error)
(* Record that we have reported the error. *)
                        _suppress_save_file_error <- true
(* Once we auto-save successfully, disable suppressing the error message. If another error occurs later, it will be reported. *)
            | None -> do _suppress_save_file_error <- false

//#endregion

(* Event handlers: mouse. *)
//#region
(* Commenting these out for now, because I don't remember what I wanted to do upon mousing over a word, and it's slowing down the application. *)
(*
(* We do not mark this event handled. *)
/// <summary>Handler for the mouse_hover event. (1) Event args. Return unit.</summary>
    let mouse_hover_handler (args : Input.MouseEventArgs) =
(* Reset the event, in case it was triggered by mouse_move. *)
        do _mouse_move.Reset () |> ignore
(* Translate the mouse position to a position in the document. *)
        match get_position args.GetPosition with
(* If the position is valid, call the helper. *)
        | Some position -> do mouse_hover_helper position
        | None -> ()

(* We do not mark this event handled. *)
/// <summary>Handler for MouseMove. (1) Event args. Return unit.</summary>
    let mouse_move_handler (args : Input.MouseEventArgs) =
(* Set the mouse move event. *)
        do _mouse_move.Set () |> ignore
(* Hide the tooltip. For more information see mouse_hover_helper. *)
        match this.ToolTip with
        | :? ToolTip as t -> do t.Visibility <- Visibility.Hidden
        | _ -> ()
*)

// TODO2 Consider a default position such as the cursor position or the last position in the document.
(* We do not mark this event handled. *)
/// <summary>Handler for the MouseRightButtonUp event. (1) Event args. Return unit.</summary>
    let mouse_right_up_handler (args : Input.MouseEventArgs) =
(* Translate the mouse position to a TextViewPosition in the document. *)
        do args.GetPosition |> get_position |> mouse_right_up_helper

(* DragLeave fires in the following cases. We handle TextView.DragLeave.

                            Margin->Editor  Editor->Editor
Editor.DragLeave
TextArea.DragLeave
TextView.DragLeave          X               X
Editor.PreviewDragLeave     X               X
TextArea.PreviewDragLeave   X               X
TextView.PreviewDragLeave   X               X
*)

(* We do not mark this event handled. *)
/// <summary>Handler for DragLeave event. (1) Event args. Return unit.</summary>
    let drag_leave_handler _ =
(* Stop the drag scroll timers. Hide the drag to highlight. *)
        do end_drag_helper ()
 
(* QueryContinueDrag fires in the following cases. We handle Editor.PreviewQueryContinueDrag.

                                     Margin->Editor     Editor->Editor
Editor.QueryContinueDrag
TextArea.QueryContinueDrag
TextView.QueryContinueDrag
Editor.PreviewQueryContinueDrag                         X
TextArea.PreviewQueryContinueDrag                       X
TextView.PreviewQueryContinueDrag
*)

(* We do not mark this event handled. *)
/// <summary>Handler for PreviewQueryContinueDrag event. (1) Event args. Return unit.</summary>
    let query_continue_drag_handler (args : QueryContinueDragEventArgs) =
(* If the user pressed Escape... *)
        if args.EscapePressed then
            do
(* Stop the drag scroll timers. Hide the drag to highlight. *)
                end_drag_helper ()
(* Cancel the drag. *)
                args.Action <- DragAction.Cancel

(* Note editor shows correct icons for editor to editor drag over. If Ctrl is pressed it shows the copy icon, otherwise it shows the move icon. Pressing Alt has no effect. I thought the move icon had a minus sign, but that might only XYplorer. *)

(* DragOver fires in the following cases. We handle TextView.DragOver.

                            Margin->Editor  Editor->Editor
Editor.DragOver             X
TextArea.DragOver           X
TextView.DragOver           X               X
Editor.PreviewDragOver      X               X
TextArea.PreviewDragOver    X               X
TextView.PreviewDragOver    X               X
*)

(* We mark this event handled, otherwise our changes to DragEventArgs.Effects are ignored. *)
/// <summary>Handler for DragOver event. (1) Event args. Return unit.</summary>
    let drag_over_handler (args : DragEventArgs) =
(* Mark the event handled, otherwise our changes to DragEventArgs.Effects are ignored. *)
        do args.Handled <- true
(* Get the Y position that we're over. *)
        let y_position = (this |> args.GetPosition).Y
(* ScrollViewer is null until a file is opened. If the file is longer than the window, check to see if we should scroll. *)
        if _scroll_viewer <> null && _scroll_viewer.ExtentHeight > _scroll_viewer.ViewportHeight then
            if y_position <= (this.ActualHeight * 0.2) then do _drag_scroll_up_timer.Start ()
            else if y_position >= (this.ActualHeight * 0.8) then do _drag_scroll_down_timer.Start ()
(* Stop any drag scrolling. We don't call end_drag here because that hides the drag to highlight, and the drag isn't done yet. *)
            else do this.stop_drag_scroll_timers ()
(* Verify the source is a TaggerMargin and send the source and the event args to the drag event. We have to send the args so the PaneController can set the drag/drop effect. To set the drag/drop effect requires a comparison between the source margin and the pane margin, which the PC can do but the editor cannot. *)
        match drag_drop_helper args with
(* We ignore the drag/drop data. PC.editor_margin_drag_handler calls TaggerMargin.drag_over_helper, which gets the drag/drop data from the event args. We could change TaggerMargin.drag_over_helper to take the drag/drop data, but it still has to take the args to set the drag/drop effect anyway.
The function path for a drag from margin over editor is:
editor.drag_over_handler -> editor.margin_drag -> PC.editor_margin_drag_handler -> margin.drag_over_helper -> margin.drag_over -> PC.margin_drag_over_handler. 
This seems circuitous, but it ensures dragging over the margin and editor and handled the same way without duplicating code.
*)
        | Some (_, margin) -> (margin, args) |> _margin_drag.Trigger
        | None -> ()

(* Drop fires in the following cases. We handle TextView.Drop.

                        Margin->Editor  Editor->Editor
Editor.Drop             X
TextArea.Drop           X
TextView.Drop           X               X
Editor.PreviewDrop      X               X
TextArea.PreviewDrop    X               X
TextView.PreviewDrop    X               X
*)

(* We do not mark this event handled. *)
(* Note that if the source is not a margin (e.g. if it is this or another editor), the drop proceeds if possible. The default behavior is move, for both same-pane and cross-pane drops. Pressing Ctrl changes the behavior to copy. Pressing Alt has no effect - i.e. the default behavior, move, happens. *)
/// <summary>Handle when the user drags from the margin and drops on the editor. (1) Event args. Return unit.</summary>
    let drop_handler (args : DragEventArgs) =
(* Stop any drag scrolling. We don't call end_drag here because that hides the drag to highlight, which we don't want because we show the drag to highlight after a drop to indicate what line was dropped on. *)
        do this.stop_drag_scroll_timers ()
(* Verify the source is a TaggerMargin and send the drag/drop data and the source to the drop event.*)
        match drag_drop_helper args with
        | Some (data, source) -> (data, source) |> _margin_drop.Trigger
(* If the source is not a TaggerMargin, it might be the other editor or it might be an external source. In that case, set the focus to this editor. *)
        | None -> do this.Focus () |> ignore

(* We mark this event handled, because the default handler interferes with ours. *)
/// <summary>Handle the mouse wheel scroll event. (1) Event args. Return unit.</summary>
    let mouse_scroll_handler (args : MouseWheelEventArgs) =
(* Divide the delta by the delta required for one line. For more information see:
http://msdn.microsoft.com/en-us/library/system.windows.input.mouse.mousewheeldeltaforoneline.aspx *)
        let delta = args.Delta / Mouse.MouseWheelDeltaForOneLine
(* For each line scrolled on the mouse wheel, scroll the document accordingly. *)
        for _ in 1 .. (Math.Abs delta * _mouse_scroll_speed) do if delta > 0 then do this.LineUp () else this.LineDown ()
        do args.Handled <- true

(*
Originally, when we displayed a highlight in the margin, we did not try to sync it to the corresponding line. This caused bugs:
1. Scrolling the document would leave the highlight in the wrong place.
2. Left-clicking the margin near the top or bottom of the document would cause the editor to scroll to move the margin selected line nearer to the center of the screen. This meant the highlight, which was placed where the user left-clicked, was now in the wrong place.

We fixed bug #1 by handling ScrollChanged, firing an event to the PC, which tells the margin to move all visible highlights to compensate for the change in vertical scroll offset.
Note AvalonEdit does not expose the ScrollViewer. We had to modify it to do so.
We fixed bug #2 in PC.margin_select_handler by showing the highlight first, then margin selecting the line. When the editor scrolled to move the margin selected line nearer to the center of the screen, the highlight was moved as well (due to bug fix #1).

However, other bugs remained, which were caused by changes in the extent height.
3. AvalonEdit apparently does not load the entire document at once. As the user scrolls, it loads more of the document and the extent height changes. 
4. The first time the user margin selects a document line, there is often an extent height change.
5. Expanding or collaping the right sidebar, or changing the size of the application window, changes the extent height.

We found no way to compute the change in vertical scroll offset that would compensate for a change in extent height. So we fire an event to the PC, which recalculates the top and bottom Y positions for all highlighted lines, and tells the margin to redraw them.
*)

(* We do not mark this event handled. *)
/// <summary>Handle the scroll changed event. (1) Event args. Return unit.</summary>
    let scroll_changed_handler (args : ScrollChangedEventArgs) =
(* Helper to redraw the tag completion window. *)
        let move_tag_completion_window () =
(* Get the top and bottom of the TextLine the cursor is on. *)
            let top_point, bottom_point = this.TextArea.Caret.Position |> get_text_line_top_bottom
(* Update the position of the tag completion window. *)
            do _tag_completion_window.Update top_point bottom_point
(* If there is a change to the extent height... *)
        if args.ExtentHeightChange <> 0.0 then
            do
(* Update the position of the tag completion window. *)
                move_tag_completion_window ()
(* Redraw all highlights. *)
                _extent_height_changed.Trigger ()
(* If there is a change to the vertical scroll offset... *)
        else if args.VerticalChange <> 0.0 then
            do
(* Update the position of the tag completion window. *)
                move_tag_completion_window ()
(* Scroll all highlights to match. Note that if there is an extent height change and a vertical scroll offset change at the same time, the former causes the PC to recalculate the top and bottom Y positions of all highlighted lines and tell the margin to redraw them, so there is no need to scroll them as well. *)
                args.VerticalChange |> _scroll_changed.Trigger

(* This is not a mouse event handler, but it refers to scroll_changed_handler and so must be defined after it. *)
/// <summary>Get the AvalonEdit.TextEditor.ScrollViewer. Return unit.</summary>
    let get_scroll_viewer () =
(* If we do not yet have the ScrollViewer... *)
        if _scroll_changed_handler_added = false then
(* AvalonEdit.TextEditor.OnApplyTemplate creates the ScrollViewer. *)
            do this.ApplyTemplate () |> ignore
(* ScrollViewer is an internal property, so we must use reflection to get it. *)
            match (this.GetType ()).GetProperty ("ScrollViewer", Reflection.BindingFlags.NonPublic ||| Reflection.BindingFlags.Instance) with
(* If we found the property, get its value. Otherwise, fire the error event. *)
            | pi when pi <> null ->
                match pi.GetValue this with
(* If the value is a ScrollViewer and not null, save it. Otherwise, fire the error event. *)
                | :? ScrollViewer as value when value <> null ->
                    do
                        _scroll_viewer <- value
(* Add the ScrollChanged event handler. *)
                        _scroll_viewer.ScrollChanged.Add scroll_changed_handler
(* Set a flag so we do not get the ScrollViewer again. We looked at the code of AvalonEdit to verify the ScrollViewer is not disposed of once it is created. *)
                        _scroll_changed_handler_added <- true
                | _ -> "TaggerTextEditor: PropertyInfo.GetValue failed to get AvalonEdit.TextEditor.ScrollViewer." |> _general_error.Trigger
            | _ -> "TaggerTextEditor: Type.GetProperty failed to get PropertyInfo for AvalonEdit.TextEditor.ScrollViewer." |> _general_error.Trigger
//#endregion

(* Event handlers: keyboard. *)
//#region

// TODO2 This should be moved to event handler helpers: keyboard, but we do not have such a section yet.
(* See the comments about TextLine in position_to_words_links_tag. *)
/// <summary>Convert a number of TextLines to scroll up or down into a number of DocumentLines/VisualLines. This is needed because we cannot tell AvalonEdit to scroll a given number of TextLines. (1) True to scroll up a page; false to scroll down a page. (2) The number of TextLines to scroll up or down. Return the number of DocumentLines/VisualLines to scroll up or down.</summary>
    let page_up_or_down_helper up target_text_line_count =
        let tv = this.TextArea.TextView
        let lines = this.Document.Lines
/// <summary>Return the number of DocumentLines/VisualLines to scroll up or down. (1) The current DocumentLine index. (2) The current number of DocumentLines to scroll up or down. (3) The current number of TextLines to scroll up or down.</summary>
        let rec helper line_index line_count text_line_count =
(* If we are on the first or last DocumentLine, return the current number of DocumentLines. *)
            if line_index < 0 || line_index > lines.Count - 1 then line_count
            else
(* We use GetVisualLine when expect the visual line to exist and can fail gracefully without it.
We use GetOrConstructVisualLine when we do not expect the visual line to exist or cannot fail gracefully without it.
Also see the comments for get_line_top_bottom and scroll_line. *)
(* Convert the DocumentLine to a VisualLine. *)
                let visual_line = tv.GetOrConstructVisualLine lines.[line_index]
(* Get the number of TextLines in this VisualLine. Add it to the current number of TextLines. *)
                let text_line_count_ = text_line_count + visual_line.TextLines.Count
(* If the current number of TextLines exceeds the target number, return the current number of DocumentLines. *)
                if text_line_count_ > target_text_line_count then line_count
                else
(* If we are scrolling up, move to the previous DocumentLine, and add 1 to the number of DocumentLines to scroll up. If we are scrolling down, move to the next DocumentLine, and add 1 to the number of DocumentLines to scroll down. *)
                    let line_index_, line_count_ = if up then line_index - 1, line_count - 1 else line_index + 1, line_count + 1
                    helper line_index_ line_count_ text_line_count_
(* Line numbers start from 1. To get the line index, subtract 1 from the line number. *)
        helper (this.TextArea.Caret.Line - 1) 0 0

(* We use GetVisualLine when expect the visual line to exist and can fail gracefully without it.
We use GetOrConstructVisualLine when we do not expect the visual line to exist or cannot fail gracefully without it.
Also see the comments for get_line_top_bottom and scroll_line. *)
(* AvalonEdit does not handle the PageUp/PageDown keys the way we want: the cursor does not remain on the same line on the screen. Nor do its PageUp/PageDown methods work the way we want: the cursor does not move at all, only the scroll bar. So we wrote custom page up/page down code. *)
/// <summary>Scroll up or down a page. (1) True to scroll up a page; false to scroll down a page. Return unit.</summary>
    let page_up_or_down up =
        let caret = this.TextArea.Caret
(* Get the visual line the caret is on. *)
        match caret.Line |> this.TextArea.TextView.GetVisualLine with
        | null -> ()
        | visual_line ->
(* Get the number of TextLines visible on the screen. *)
            let text_line = visual_line.GetTextLine caret.Position.VisualColumn
            let target_text_line_count = this.TextArea.TextView.ActualHeight / text_line.Height |> int
(* Get the Y position of the caret. We use this to ensure it remains in the same Y position after we scroll the document. *)
            let y_position = caret.Position |> this.position_to_y_position
            if up then
(* Scroll up a number of TextLines equal to the number of TextLines visible on the screen. *)
                let new_line = caret.Line + page_up_or_down_helper true target_text_line_count
(* Line numbers start from 1. *)
                if new_line > 1 then do caret.Line <- new_line
                else do caret.Line <- 1
            else
(* Scroll down a number of TextLines equal to the number of TextLines visible on the screen. *)
                let new_line = caret.Line + page_up_or_down_helper false target_text_line_count
                let line_count = this.Document.LineCount
                if new_line < line_count then do caret.Line <- new_line
                else do caret.Line <- line_count
(* Scroll so that the cursor is still in the same Y position. *)
            do this.scroll_line (caret.Line |> this.safe_line_number_to_line) y_position

(* Previously in preview_key_down_handler, we tried to replace keys as follows.
(*
do args.Handled <- true
let args_ = new KeyEventArgs (Keyboard.PrimaryDevice, Keyboard.PrimaryDevice.ActiveSource, 0, Key.Prior)
do
    args_.RoutedEvent <- Keyboard.KeyDownEvent
(* The following are alternative ways to re-raise the event, neither of which works. *)
    InputManager.Current.ProcessInput (args_) |> ignore
//    this.TextArea.RaiseEvent (args_)
*)
However, this did not work.
Also, we found that AvalonEdit does not respond to the PageUp/PageDown keys the way we wanted, so there was no point in mapping Alt+Up/Alt+Down to those keys anyway. *)

(* We handle the preview version of this event, because the default handler interferes with ours. We mark this event handled, because otherwise the handler does not work correctly. *)
/// <summary>Handler for the PreviewKeyDown event. (1) Event args. Return unit.</summary>
    let preview_key_down_handler (args : KeyEventArgs) =
/// <summary>Run action (1). Mark the event handled. Return unit.</summary>
        let mark_handled (action : unit -> unit) =
            do
                action ()
                args.Handled <- true
(* If the Control key was pressed... *)
        if Keyboard.Modifiers.HasFlag ModifierKeys.Control then
(* If the up or down arrow key was pressed, scroll the document without moving the cursor. *)
            if args.Key = Key.Up then do mark_handled this.LineUp
            else if args.Key = Key.Down then do mark_handled this.LineDown
(* We believe Caret.Position cannot return null. See TextEditor.GetPositionFromPoint, which returns Nullable<TextViewPosition>. *)
            else if args.Key = Key.F then do this.TextArea.Caret.Position |> _find_in_project.Trigger
(* If the C key was pressed... *)
            else if args.Key = Key.C then
(* Note we prioritize margin selections over editor selections. *)
(* If no lines are margin selected, stop. Do not mark the event handled. Let AvalonEdit process the copy command normally. *)
                if List.isEmpty !_margin_selected_lines then ()
(* If any lines are margin selected, copy them to the clipboard. Mark the event handled. *)
                else do mark_handled (fun () -> do margin_selection_to_clipboard false)
(* If the X key was pressed... See the comments for the C key case. *)
            else if args.Key = Key.X then
                if List.isEmpty !_margin_selected_lines then ()
                else do mark_handled (fun () -> do margin_selection_to_clipboard true)
(* If the Alt key was pressed... *)
        else if Keyboard.Modifiers = ModifierKeys.Alt then
(* If the Alt key is pressed, we must check args.SystemKey rather than args.Key. See:
https://stackoverflow.com/questions/3099472/previewkeydown-is-not-seeing-alt-modifiers
*)
            match args.SystemKey with
(* If the up or down arrow key was pressed, scroll the document up or down a page. *)
            | Key.Up -> do mark_handled (fun () -> do page_up_or_down true)
            | Key.Down -> do mark_handled (fun () -> do page_up_or_down false)
(* If the left or right arrow key was pressed, scroll to the start or end of the line. *)
            | Key.Left -> do mark_handled (fun () -> do this.TextArea.Caret.Column <- 0)
            | Key.Right -> do mark_handled (fun () ->
                let line = this.TextArea.Caret.Line |> this.safe_line_number_to_line
                do this.TextArea.Caret.Offset <- line.EndOffset
                )
            | _ -> ()
(* If the F3 key was pressed, show the search panel. *)
        else if args.Key = Key.F3 then
            if _search.IsClosed then do mark_handled (fun () ->
                do
                    _search.Open ()
(* For some reason, opening the SearchPanel doesn't set focus on the TextBox, and it's private so we can't do it either. We found this solution at:
http://stackoverflow.com/questions/18335349/avaloneditor-searchpanel *)
                    _search.Dispatcher.BeginInvoke (DispatcherPriority.Input, Action (fun _ -> do _search.Reactivate ())) |> ignore
                )
(* If the PageUp/PageDown key was pressed, scroll the document up or down a page. *)
        else if args.Key = Key.PageUp then do mark_handled (fun () -> do page_up_or_down true)
        else if args.Key = Key.PageDown then do mark_handled (fun () -> do page_up_or_down false)
(* Note if the search panel is shown, pressing Esc hides that first. *)
(* If the Esc key was pressed, clear both the editor and margin selections, if any. *)
        else if args.Key = Key.Escape then do mark_handled (fun () ->
            do
                clear_margin_selected_lines true
(* Notify the PaneController that the margin selection has changed, so it can update the highlights in the margin. *)
                _margin_selection_changed.Trigger ()
                this.SelectionLength <- 0
            )
(* This is not currently used. *)
(*
(* If the Del or Backspace key was pressed... *)
        else if args.Key = Key.Delete || args.Key = Key.Back then
(* If no lines are margin selected, stop. Do not mark the event handled. Let AvalonEdit process the delete command normally. *)
            if List.isEmpty !_margin_selected_lines then ()
(* If any lines are margin selected, delete them. Mark the event handled. *)
            else do mark_handled (fun () -> do delete_margin_selection ())
*)
// TODO1 Might be easier to check first if there is a margin selection, then check for Ctrl+C, Ctrl+V, Esc, Del, Back, etc.

(* SearchPanel needs its own key event handler, because key events aren't routed to the editor while it is active. *)
(* We do not mark this event handled. Unlike the Editor.KeyDown event, the default handler does not seem to interfere with ours. *)
/// <summary>Handler for the KeyDown event for the AvalonEdit SearchPanel. (1) Event args. Return unit.</summary>
    let search_key_down_handler (args : KeyEventArgs) =
(* If the F4 key was pressed, go to the previous match. *)
        if args.Key = Key.F4 then do _search.FindPrevious ()

// TODO2 Consider changing text_changed_handler to only clear deleted lines from _margin_selected_lines, instead of all margin selected lines. If we do so, then add right click command to clear margin selection. Or just rely on Esc to clear margin selection.

// TODO1 Make a separate method, clear_margin_selection, that calls clear_margin_selected_lines and triggers _margin_selection_changed. Or change clear_margin_selected_lines to trigger _margin_selection_changed. Also add a parameter to it to clear the editor selection as well. That can be called from preview_key_down_handler when Esc is pressed.

(* Document.Changed is triggered when Document.Insert (), .Remove (), or .Replace () is called. See:
http://avalonedit.net/documentation/html/d5215d7c-59b0-511e-ad3d-52e8615d0118.htm
*)
/// <summary>Handle the Document.Changed event. (1) The event args. Return unit.</summary>
    let document_changed_handler (args : Document.DocumentChangeEventArgs) =
(* If the change was a deletion of text, remove any deleted lines from the list of margin selected lines. *)
// TODO1 Should this be protected by lock?
        if args.RemovalLength > 0 then do
            _margin_selected_lines := !_margin_selected_lines |> List.filter (fun (line : Document.DocumentLine) -> line.IsDeleted = false)
(* If the change was an insertion of text, we want to put the cursor at the end of the insertion. *)
// TODO1 It seems AvalonEdit moves the offset after undo/redo also. When we paste text, the caret is put at the end of the inserted text. When we undo/redo to insert text, the caret is put at the beginning of the inserted text.
        let offset = args.Offset + args.InsertionLength
        do offset |> this.scroll_to_offset
        if args.InsertionLength > 0 then do _document_changed.Trigger (args.InsertedText.Text, true)
        else do _document_changed.Trigger (args.RemovedText.Text, false)
// TODO1 Could also ask for DragTo (green) highlight on the affected lines.
(* TODO1
- This doesn't work. When we do an undo/redo that inserts text, the text is not selected. AvalonEdit might do something during undo/redo that releases the selection. It does work when we simply paste text.
- Also, we want to call this only on undo/redo, but we don't know how.
*)
//        if args.InsertionLength > 0 then do
//            this.Select (args.Offset, args.InsertionLength)

(* We would define this at the beginning, but it must be defined after document_changed_handler. *)
    let _document_changed_handler = EventHandler<Document.DocumentChangeEventArgs>(fun _ -> document_changed_handler)

(* Because the attached Document changes each time we open a file, we must attach the Document.Changed event handler again. DocumentChanged is triggered when the document changes. *)
/// <summary>Handle the Editor.DocumentChanged event. (1) The event args. Return unit.</summary>
    let documentchanged_handler _ =
// TODO1 Removing and then adding the handler is not thread-safe, but we haven't found a better way to do this.
// TODO1 Is this.Document ever null?
        match this.Document with
        | null -> ()
        | _ ->
            do
                this.Document.Changed.RemoveHandler _document_changed_handler
                this.Document.Changed.AddHandler _document_changed_handler

// TODO1 We presume AvalonEdit also handles this event, and clears the editor selection.
(* We do not mark this event handled. *)
/// <summary>Handle when the text changes. (1) The event args. Return unit.</summary>
    let text_changed_handler _ =
        do
(* Clear the margin selected lines. *)
            clear_margin_selected_lines true
(* Notify the PaneController that the margin selection has changed, so it can update the highlights in the margin. *)
            _margin_selection_changed.Trigger ()
(* Set the is modified flag so the file will be auto-saved. *)
            _is_modified <- true

(* We considered moving the TagCompletionWindow type to TagController, and having the _tag_symbol_entered event include the point that is currently calculated by show_tag_completion_window. However, the functions that involve the TCW are more closely related to the Editor than to the TC. For instance, if the editor is scrolled or has its size changed, it must update the position of the TCW. *)
(* We do not mark this event handled. *)
/// <summary>Handle after the user enters text. (1) The event args. Return unit.</summary>
    let text_entered_handler (args : TextCompositionEventArgs) =
(* If the text is not empty... *)
        if args.Text.Length > 0 then
(* Note as far as I know, this handler only processes one character at a time. For that reason, we restrict the tag symbol to one character, so we can detect it here. This handler might process more than one character if the user pastes text, but it's not appropriate to open the tag completion window in response to a paste anyway. *)
(* If the text is the tag symbol, fire the tag_symbol_entered event. This event is fired to PaneController, MainController, and TagController, which gets the list of existing tags and calls back to Editor.show_tag_completion_window. *)
            if args.Text = TagInfo.tag_symbol then
                do _tag_symbol_entered.Trigger None
//#endregion

(* Constructor. *)
//#region
    do
(* Commenting these out for now, because I don't remember what I wanted to do upon mousing over a word, and it's slowing down the application. *)
(* Note: if we re-enable these events, test the 6 cases to see when they fire: editor/textarea/textview * non-preview/preview. *)
//        this.MouseHover.Add mouse_hover_handler
//        this.TextArea.MouseMove.Add mouse_move_handler
(* Commenting this out for now, as it doesn't work the way we want. *)
(* Add a LineTracker so we can handle the LineInserted event. *)
//        this.Document.LineTrackers.Add (new LineTracker (this.Document))

(* Add helper classes. *)
(* Add the colorizer. *)
        this.TextArea.TextView.LineTransformers.Add _colorizer

(* Add event handlers. *)

(* Because the document changes when we close or open a file, we must attach the Document.Changed event handler again. DocumentChanged is triggered when the document changes. *)
        this.DocumentChanged.Add documentchanged_handler
(* Note the handler for this.ScrollViewer.ScrollChanged is added in the open_file method. *)
        this.TextChanged.Add text_changed_handler
(* TextEntered is only present on TextArea. *)
        this.TextArea.TextEntered.Add text_entered_handler
(* Whenever the editor selection is changed, fire the margin_selection_changed event. *)
(* This is currently not used. Margin selecting a line is different from editor selecting it. So when the editor selection changes, it should have no effect on the margin selection. Instead, we fire margin_selection_changed in text_changed_handler. *)
//        this.TextArea.SelectionChanged.Add <| fun _ -> do _margin_selection_changed.Trigger ()
        this.TextArea.MouseRightButtonUp.Add mouse_right_up_handler
        this.TextArea.MouseWheel.Add mouse_scroll_handler
(* These drag/drop handlers are assigned depending on the cases where they fire. See the handlers for more information. *)
        this.TextArea.TextView.DragOver.Add drag_over_handler
        this.TextArea.TextView.DragLeave.Add drag_leave_handler
(* This only works for editor to editor drag, not margin to editor drag. See query_continue_drag handler for more information. *)
        this.PreviewQueryContinueDrag.Add query_continue_drag_handler
        this.TextArea.TextView.Drop.Add drop_handler
(* We use Preview because the default handler interferes with ours. *)
        this.TextArea.PreviewKeyDown.Add preview_key_down_handler
        _tag_completion_window.commit.Add _tag_selected.Trigger
(* Simply pass the tag completion window's general error event on to our own. *)
        _tag_completion_window.general_error.Add _general_error.Trigger
(* The SearchPanel needs its own key event handler. *)
        _search.KeyDown.Add search_key_down_handler
(* Add drag scroll event handlers. We don't bother checking whether the file is longer than the window, since LineUp/LineDown presumably do that. *)
        _drag_scroll_up_timer.Tick.Add (fun _ -> do this.LineUp ())
        _drag_scroll_down_timer.Tick.Add (fun _ -> do this.LineDown ())
(* Add auto-save event handler. *)
        _save_timer.Tick.Add (fun _ -> do save_timer_tick_handler ())

(* Note AvalonEdit determines the caret width using SystemParameters.CaretWidth. See:
ICSharpCode.AvalonEdit.Editing > Caret.cs > CalcCaretRectangle
http://msdn.microsoft.com/en-us/library/system.windows.systemparameters.caretwidthkey%28v=vs.90%29.ASPX
This setting is found in:
Control Panel\All Control Panel Items\Ease of Access Center\Make the computer easier to see
*)

(* Set properties. *)
(* Set the default interval to 1 second. *)
        _save_timer.Interval <- TimeSpan.FromSeconds 1.0
(* Start the auto-save timer. *)
        _save_timer.Start ()
(* Set the drag scroll timer intervals. *)
        set_drag_scroll_timer_intervals ()
(* Commenting this out for now, as we don't currently display the ToolTip. *)
(* Create the ToolTip. *)
//        this.ToolTip <- new ToolTip ()
(* Turn on word wrap. I never turn it off, so it would be a nuisance to make it a config setting. *)
        this.WordWrap <- true
(* The editor should be disabled until it has a file loaded. *)
        this.IsEnabled <- false
(* Pad the right margin. We changed AvalonEdit to justify text, and without this padding, the last character in each row (where each line word wraps into one or more rows) is too close to the right margin, which makes it hard to read. *)
        this.TextArea.TextView.Margin <- new Thickness (0.0, 0.0, 5.0, 0.0)
//#endregion

(* Methods: file operations. *)
//#region

/// <summary>Save the currently open file, if any. If we succeed, return None; otherwise, return the error message.</summary>
    member this.save_file () =
(* If there is no current file, do nothing. The application isn't intended to let users create files before naming them. *)
        match _file with
        | Some (file : string) ->
            try
(* Note an exception inside locked code releases the lock. See EditorTest.exception_inside_lock. *)
(* We lock this to make sure that the auto save cannot save the file while the user is in the middle of opening or closing it. *)
                do
                    lock _file_lock (fun () -> do this.Save file)
(* Clear the is modified flag. *)
                    _is_modified <- false
                None
            with | ex -> Some ex.Message
        | None -> None

(* See:
http://www.codeproject.com/Articles/42490/Using-AvalonEdit-WPF-Text-Editor
"You can change the Document property to bind the editor to another document. It is possible to bind two editor instances to the same document; you can use this feature to create a split view." *)
/// <summary>Open file with path (1). (2) The document for the file. Update the file property, enable the editor, and attach the document. Return unit.</summary>
    member this.open_file file document =
        do
(* Attaching the document sets the is modified flag. We lock this section to ensure that the auto save does not save the file before we can both attach the document and set the file path. *)
            lock _file_lock (fun () ->
                do
(* Set the file path. *)
                    _file <- Some file
(* Attach the document. *)
                    this.Document <- document
                )
(* Clear the is modified flag. If auto save fires before we reach this statement, it doesn't matter because we have already attached the document and set the file path. *)
            _is_modified <- false
(* Enable the editor. *)
            this.IsEnabled <- true
(* Get the AvalonEdit.TextEditor.ScrollViewer if we have not already done so. We can't do this until now because the ScrollViewer is null until a file is opened. We looked at the code of AvalonEdit to verify the ScrollViewer is not disposed of once it is created. *)
            get_scroll_viewer ()

/// <summary>If the file (1) is currently open in the editor, change it to path (2) without reloading it. Return unit.</summary>
    member this.rename_file old_path new_path =
(* We checked with ILSpy that ICSharpCode.AvalonEdit.TextEditor.Load does not set any property, it just reads the specified file and updates the Text property. So we don't need to set anything in the TextEditor. *)
        match _file with
(* We lock this to make sure we don't rename the file while the user is in the middle of opening or closing it. *)
        | Some file -> if file = old_path then do lock _file_lock (fun () -> do _file <- Some new_path)
        | None -> ()

(* Note this does not save the file. *)
/// <summary>Close the currently open file and disable the editor. Return the document for the closed file.</summary>
    member this.close_file () =
(* Get the document for the current file. *)
        let document =
(* Attaching the empty document sets the is modified flag. As with open_file, we need to make sure that the auto save does not save the file before we can both attach the empty document and clear the file path. *)
            lock _file_lock (fun () ->
(* Get the file document. *)
                let document_ = this.Document
                do 
(* Attach an empty document. *)
                    this.Document <- new Document.TextDocument ()
(* Clear the file path. *)
                    _file <- None
(* Return the file document. *)
                document_
                )
        do
(* If the auto save fires here, it won't matter because we've already cleared the file path. *)
(* Disable the editor. *)
            this.IsEnabled <- false
(* Clear the is modified flag. *)
            _is_modified <- false
(* Return the file document. *)
        document
//#endregion

(* Methods: position operations. *)
//#region
(* We use GetVisualLine when expect the visual line to exist and can fail gracefully without it.
We use GetOrConstructVisualLine when we do not expect the visual line to exist or cannot fail gracefully without it.
Also see the comments for get_line_top_bottom and scroll_line. *)
(* This is a previous version of position_to_y_position. We replaced it with something simpler, but the code might be useful again at some point. *)
(*
(* Call UpdateLayout before getting a visual line. For more information see get_line_top_bottom. *)
        do this.UpdateLayout ()
// TODO3 Add exception handling for GetOrConstructVisualLine.
(* Get the visual line that corresponds to the document line. *)
        let visual_line = position.Line |> this.safe_line_number_to_line |> this.TextArea.TextView.GetOrConstructVisualLine
(* If the document is scrolled so that the top of the visual line is off screen... *)
        if this.VerticalOffset > visual_line.VisualTop then
            do
(* Scroll the document so that the top of the visual line is at the top of the screen. *)
                this.ScrollToVerticalOffset visual_line.VisualTop
(* Since we've just scrolled the document, call UpdateLayout to make sure VerticalOffset updates. See the comments for scroll_line. *)
                this.UpdateLayout ()
(* For the Y position, we want to use the top of the line. That helps us to vertically align this line with the other line in the other pane.
Per AvalonEdit help, topic Coordinate Systems:
VisualTop
A double value that specifies the distance from the top of the document to the top of a line measured in device-independent pixels.
VisualTop is equivalent to the Y component of a VisualPosition.
VisualPosition
...
To convert a VisualPosition to or from a (mouse) position inside the TextView, simply subtract or add TextView::ScrollOffset to it. *)
        visual_line.VisualTop - this.VerticalOffset
*)

(* position_to_y_position now returns the Y position of the top of the TextLine, not the VisualLine. This function is used to get the Y position of the line that contains a source tag, so the line that contains the target tag can be scrolled to the same Y position (whether in the same editor or the opposite one). However, the Y position of the source TextLine is aligned with the Y position of the target VisualLine. For example:

source VisualLine 1 TextLine 1
source VisualLine 1 TextLine 2 (tag)    target VisualLine 1 TextLine 1
source VisualLine 1 TextLine 3          target VisualLine 1 TextLine 2 (tag)

This is because the functions that find the target tag do not find its position, but only its line. So we can scroll the target VisualLine to the Y position, but not the target TextLine.
We could change this, but it would be a lot of work. In any case, I think it's easier to read from the source TextLine that contains the tag across to the start of the target VisualLine, even if the first target TextLine does not contain the tag.

To change this we would need to do the following.
Editor.scroll_line needs to be passed a TextViewPosition instead of a DocumentLine. Then it could call position_to_y_position.
Editor.scroll_line is called by PaneController.display_line, which needs to be passed a position instead of a line number.
PC.display_line is called by TagController.show_text, which needs to be passed a position instead of a line number.
TC.show_text is called by TC.show_find_in_project_dialog, which calls TC.find_text_in_files to get the FindWordInFileResults.
TC.find_text_in_files calls Editor.find_text_in_open_file and Editor.find_text_in_closed_file. Those return FindWordInFileResults.
FindWordInFileResults needs to contain a position instead of (or as well as) a line number.
Editor.find_text_in_open_file and Editor.find_text_in_closed_file both loop through the file lines and use String.Contains. They could use String.IndexOf or a regex instead. They could get the offset, and Editor could convert the offset to a position once the file is open (if it was closed).
We might do that by having FindWordInFileResults contain a position option (for files that are already open) and an offset option (for files that are closed). TC.show_text is responsible for opening a closed file in the Editor, so it can ask the Editor to convert the offset to a position once the file is open.
If we do this, we should probably search the whole file at once rather than looping through the lines. *)

(* This function is called by TagController.show_text, which in turn is called by TagController.show_find_in_project_dialog. TagController.show_text calls this function to get the Y position of the TextViewPosition the user right-clicked, which is provided to it by TagController.show_find_in_project_dialog. That is, this function is typically called with a TextViewPosition that is already visible on the screen. So, for now, we dispense with calling UpdateLayout before calling GetVisualPosition. For more information see scroll_line. *)
/// <summary>Return the Y position of position (1). Preconditions: (1) is a valid position in the current document.</summary>
    member this.position_to_y_position (position : TextViewPosition) =
(* Convert the TextViewPosition to a point relative to the document. Subtract the scroll position to convert that to a point relative to the editor.
We use the top Y position of the TextLine the TextViewPosition is in, so we can scroll the target VisualLine to have the same top Y position. *)
        let point = this.TextArea.TextView.GetVisualPosition (position, Rendering.VisualYPosition.LineTop) - this.TextArea.TextView.ScrollOffset
(* Previously, we checked to see if the document was scrolled so that the top of the VisualLine was off screen. Now that we're able to return the Y position of the TextLine, rather than the VisualLine, this is no longer needed. *)
(* This is for debugging. *)
//        printfn "Editor.position_to_y_position. Y position: %f." point.Y
        point.Y
//#endregion

(* Methods: line operations. *)
//#region

/// <summary>If text (1) starts with a newline, return it; otherwise, return it with a newline prepended to it.</summary>
    static member add_newline_to_start_of_text (text : string) = if text.StartsWith "\n" then text else sprintf "\n%s" text

(* We need to specify the return type of this function because we pass its result to Document.TextDocument.Insert in insert_line_at_offset_in_text. *)
/// <summary>If text (1) ends with a newline, return it; otherwise, return it with a newline appended to it.</summary>
    static member add_newline_to_end_of_text (text : string) : string = if text.EndsWith "\n" then text else sprintf "%s\n" text

(* See the comments for lines_to_text_helper_cut.
That is not currently an issue with margin selected lines, because we clear them on any change to the text. If we decide we want to keep margin selected lines after a change to the text, we will need some way to determine what lines were added to or deleted from the text. *)
/// <summary>Clear the currently margin selected lines. Set them to (1). Highlight the new margin selected lines. (2) The most recently margin selected line. (3) True if the user pressed Shift while margin selecting the line. Return unit.</summary>
    member this.update_margin_selected_lines lines most_recently_margin_selected_line shift =
(* Verify the lines are valid for the current document. *)
        match validate_lines lines with
        | None -> ()
(* Hide the old definition of lines. *)
        | Some lines ->
(* Determine whether this is a new margin selection or a change to an existing one. *)
            let is_new_margin_selection = (!_margin_selected_lines).Length = 0
            do
                clear_margin_selected_lines false
                _margin_selected_lines := lines
(* Save the most recently margin selected line. *)
                _most_recently_margin_selected_line <- most_recently_margin_selected_line
(* If the user did not press shift when margin selecting the most recently margin selected line, save that information. *)
            if shift = false then do _most_recently_margin_selected_line_no_shift <- most_recently_margin_selected_line
            do highlight_margin_selected_lines ()
(* If this is a new margin selection, and at least one line is margin selected, set the cursor to the end of the last margin selected line and scroll to the last margin selected line. *)
            if is_new_margin_selection && (!_margin_selected_lines).Length > 0 then
(* This code originated with a problem in TagController.move_lines_to_open_file. After moving text to the end of an open file, we could not get the editor to scroll to the inserted text except by calling ScrollToEnd repeatedly. We tried ScrollToLine (with the last line in the document) and our own Editor.scroll_line, and those didn't work either. We found the answer here:
https://stackoverflow.com/questions/1895204/textbox-scrolltoend-doesnt-work-when-the-textbox-is-in-a-non-active-tab
*)
(* Set focus to this editor. *)
                do this.Focus () |> ignore
(* Set the cursor to the end of the last margin selected line. *)
                let offset = (!_margin_selected_lines).last.EndOffset
(* Scroll to the last margin selected line. *)
                do this.scroll_to_offset offset
// TODO2 We're trying to do all scrolling through scroll_to_offset.
//                this.ScrollToLine (!_margin_selected_lines).last.LineNumber

/// <summary>Convert the Y position (1) to the corresponding DocumentLine. If the Y position does not correspond to any line, get the last line in the document. Return the line.</summary>
    member this.safe_y_position_to_line y_position =
(* We don't care about the X position. *)
        new Point (0.0, y_position) |> this.GetPositionFromPoint |> nullable_to_option |> this.safe_position_to_line

/// <summary>Get the text in lines (1). If (2) is true, remove the lines. Return the text.</summary>
    member this.lines_to_text (lines : Document.DocumentLine list) remove =
(* Verify the lines are valid for the current document. *)
        match validate_lines lines with
        | None -> ""
(* Hide the old definition of lines. *)
        | Some lines ->
            if remove then lines_to_text_helper_cut lines None |> fst
            else lines_to_text_helper_copy lines

// TODO1 Does this refer to margin selection or editor selection? Try calling this function in both cases.
(* Note we were concerned about functions that modify the text being called while text was editor selected. However, as long as we use Document.Insert, AvalonEdit should handle this situation correctly. We do not modify the Text property directly except when we close a file. *)

/// <summary>Insert text (1) at line (2). Return unit.</summary>
    member this.insert_line_at_line (text : string) (line : Document.DocumentLine) =
(* Verify the line is valid for the current document. *)
        match validate_line line with
        | None -> ()
(* Hide the old definition of line. *)
(* Insert the dropped text at the beginning of the line. *)
        | Some line -> do this.Document.Insert (line.Offset, TaggerTextEditor.add_newline_to_end_of_text text)

/// <summary>Insert text (1) at the beginning of the current file. Return unit.</summary>
    member this.insert_text_at_start_of_current_file (text : string) = do this.Document.Insert (0, TaggerTextEditor.add_newline_to_end_of_text text)

/// <summary>Insert text (1) at the end of the current file. Return unit.</summary>
    member this.insert_text_at_end_of_current_file text = do this.Document.Insert (this.Document.TextLength, TaggerTextEditor.add_newline_to_start_of_text text)

/// <summary>Insert text (1) at the beginning of the line where the cursor is in the current file. Return unit.</summary>
    member this.insert_text_at_cursor_in_current_file (text : string) =
(* See: AvalonEdit documentation, Namespaces > ICSharpCode.AvalonEdit.Editing > Caret > Location
"The getter of this property is faster than Position because it doesn't have to validate the visual column." *)
        let (line : Document.DocumentLine) = this.TextArea.Caret.Location.Line |> this.safe_line_number_to_line
        do this.insert_line_at_line text line

(* This is for intra-document moves. *)
/// <summary>Move the text in lines (1) to line (2). Return true if the move succeeds; otherwise, false.</summary>
    member this.move_lines (source_lines : Document.DocumentLine list) (target_line : Document.DocumentLine) =
(* Verify the lines are valid for the current document. *)
        match validate_lines source_lines, validate_line target_line with
        | Some source_lines, Some target_line ->
            if move_and_copy_helper source_lines target_line then
                let text, target_offset = lines_to_text_helper_cut source_lines <| Some target_line
(* Get and remove the text from the source lines. Then insert it at the target line. *)
                do insert_line_at_offset_in_text text target_offset
                true
            else false
        | _ -> false

(* This is for intra-document copies. *)
/// <summary>Copy the text in lines (1) to line (2). Return true if the copy succeeds, otherwise, false.</summary>
    member this.copy_lines (source_lines : Document.DocumentLine list) target_line =
(* Verify the lines are valid for the current document. *)
        match validate_lines source_lines, validate_line target_line with
        | Some source_lines, Some target_line ->
            if move_and_copy_helper source_lines target_line then
(* Get the text from the source line. Then insert it at the target line. *)
                let text = lines_to_text_helper_copy source_lines
                do this.insert_line_at_line text target_line
                true
            else false
        | _ -> false

(* We use GetVisualLine when expect the visual line to exist and can fail gracefully without it.
We use GetOrConstructVisualLine when we do not expect the visual line to exist or cannot fail gracefully without it.
Also see the comments for scroll_line. *)
(* This function is called by:
PaneController.margin_drag_over_handler
PaneController.margin_select_handler
Editor.get_lines_top_bottom

Editor.get_lines_top_bottom is in turn called by:
PaneController.margin_drag_over_handler
PaneController.extent_height_changed_handler
PaneController.highlight_lines

All of these typically call this function with lines that are already visible on the screen. So, for now, we dispense with calling UpdateLayout before calling GetOrConstructVisualLine. For more information see scroll_line. *)
(* Previously, if we did not call UpdateLayout, then if the document had just been scrolled - and we might call scroll_lines before calling this function - we got an incorrect value for Editor.VerticalOffset, and possibly for VisualLine.VisualTop as well. In retrospect, the problem might have been that we needed to call UpdateLayout in scroll_line more than once. *)
(* This is a method so it can be called from PaneController. *)
(* This method does not call validate_line because it calls GetOrConstructVisualLine and handles exceptions. *)
/// <summary>Return the top and bottom Y positions of the line (1).</summary>
    member this.get_line_top_bottom (line : Document.DocumentLine) =
(* GetOrConstructVisualLine raises an exception if the line is not a valid line in the current document. If that happens, we return top and bottom Y positions of 0.0. *)
        try
(* Get the visual line that corresponds to the document line. *)
            let visual_line = line |> this.TextArea.TextView.GetOrConstructVisualLine
(* Per AvalonEdit help, topic Coordinate Systems:
VisualTop
A double value that specifies the distance from the top of the document to the top of a line measured in device-independent pixels.
VisualTop is equivalent to the Y component of a VisualPosition.
VisualPosition
...
To convert a VisualPosition to or from a (mouse) position inside the TextView, simply subtract or add TextView::ScrollOffset to it. *)
            let top = visual_line.VisualTop - this.VerticalOffset
(* This is for debugging. *)
//            printfn "Editor.get_line_top_bottom. VisualLine.VisualTop: %f. Editor vertical offset: %f. Difference (Y position of line top): %f." visual_line.VisualTop this.VerticalOffset top
(* Return the Y coordinates of the top and bottom of the visual line. *)
            top, top + visual_line.Height
(* If GetOrConstructVisualLine raised an exception, return top and bottom Y positions of 0.0. *)
        with | _ -> 0.0, 0.0

(* get_line_top_bottom does not call validate_line because it calls GetOrConstructVisualLine and handles exceptions. *)
/// <summary>Return the top and bottom Y positions of lines (1).</summary>
    member this.get_lines_top_bottom = List.map this.get_line_top_bottom

/// <summary>Scroll the document so the line that contains the offset (1) is visible. Return unit.</summary>
    member this.scroll_to_offset offset =
(* Per AvalonEdit help. The first offset at the start of the document is 0. The last valid offset is document.TextLength. *)
        if offset > -1 && offset <= this.Document.TextLength then
            let line = offset |> this.Document.GetLineByOffset
            do
                this.ScrollToLine line.LineNumber
// TODO1 We wanted to have the caller do this. If we do, document_changed_handler scrolls to the top of the document and places the cursor there after we undo a margin selection deletion. We don't know why this happens.
                this.TextArea.Caret.Offset <- offset
(* We tried using editor.TextArea.Caret.BringCaretToView (), but it didn't work. It might be because the view in question extends outside the screen. *)
(* This also did not work. It scrolled the line to the top of the screen, which is not appropriate when this method is called by update_margin_selected_lines. *)
(* See:
https://stackoverflow.com/questions/19887868/avalonedit-textview-scroll/19905284#19905284
*)
(*
            let top = this.TextArea.TextView.GetVisualTopByDocumentLine line.LineNumber
            do this.ScrollToVerticalOffset top
*)

(* We use GetVisualLine when expect the visual line to exist and can fail gracefully without it.
We use GetOrConstructVisualLine when we do not expect the visual line to exist or cannot fail gracefully without it.
Also see the comments for get_line_top_bottom. *)
(* Note if at some point we decide to line up the TextLines rather than just the document lines, we could use:
visual_line.GetTextLineByVisualYPosition y_position *)
(* Note VisualLine.VisualTop does not return a consistent value. It changes each time we scroll. This means we have to repeatedly get the VisualLine.VisualTop, scroll, and make sure the VisualLine is rebuilt, until the VisualLine.VisualTop stops changing.
Per AvalonEdit documentation, topic Text Rendering, "VisualLines are only created for the visible part of the document". I looked at the implementation of TextView.GetOrConstructVisualLine and that also implies that VisualLines are meant to be contiguous. In other words, GetOrConstructVisualLine expects to be asked to create a VisualLine directly above or below the ones that are currently visible on the screen, but we're asking it to create one that is farther away. *)
(* This method does not call validate_line because it calls GetOrConstructVisualLine and handles exceptions. *)
/// <summary>Scroll the document so that the top of the line (1) is at Y position (2). Return unit.</summary>
    member this.scroll_line (line : Document.DocumentLine) y_position =
/// <summary>Helper to scroll until the current value of VisualLine.VisualTop equals the previous value (1). Return unit.</summary>
        let rec scroll last_visual_top =
(* GetOrConstructVisualLine raises an exception if the line is not a valid line in the current document. If that happens, we stop. *)
            try
(* Get the visual line that corresponds to the document line. *)
// TODO1 Test to make sure this change works.
//                let visual_line = line |> this.TextArea.TextView.GetOrConstructVisualLine
(* Get the VisualTop of the VisualLine. Per AvalonEdit documentation, topic Coordinate Systems, VisualTop is:
A double value that specifies the distance from the top of the document to the top of a line measured in device-independent pixels. *)
//                let visual_top = visual_line.VisualTop
                let visual_top = line.LineNumber |> this.TextArea.TextView.GetVisualTopByDocumentLine
(* This is for debugging. *)
//                printfn "Editor.scroll_line. VisualLine.VisualTop: %f. Previous value: %f. Y position: %f. Difference: %f." visual_top last_visual_top y_position (visual_top - y_position)
(* If VisualLine.VisualTop is the same as last time, stop. *)
                if visual_top = last_visual_top then ()
                else
(* We tried using ScrollToLine, but we can't control where the line is scrolled to. *)
(* The Y position is where we want to scroll the top of the line to. The VisualTop is how far it is from the top of the document (not the screen) to the top of the line. We get the difference between the two to see how far down (or up) we should scroll. *)
                    do this.ScrollToVerticalOffset (visual_top - y_position)
(* Per AvalonEdit documentation, topic Text Rendering, we can call TextView.Redraw to invalidate a VisualLine. However, that doesn't seem to update the VisualLine.VisualTop. *)
(* Using ILSpy, we found that System.Windows.Controls.ScrollViewer.ScrollToVerticalOffset -> EnqueueCommand -> EnsureQueueProcessing -> EnsureLayoutUpdatedHandler -> UIElement.InvalidateArrange
http://msdn.microsoft.com/en-us/library/system.windows.uielement.invalidatearrange.aspx
"Invalidates the arrange state (layout) for the element. After the invalidation, the element will have its layout updated, which will occur asynchronously unless subsequently forced by UpdateLayout."
Asynchronous isn't fast enough for us, so we call UpdateLayout. *)
(* For some reason, ScrollViewer is null when unit testing. That causes the unit test for TagController.show_text to fail, even though show_text calls Editor.open_file before this point, which should set a value for ScrollViewer. *)
                    if _scroll_viewer <> null then
                        do
// TODO1 Why can't we use this.UpdateLayout?
                            _scroll_viewer.UpdateLayout ()
(* Continue scrolling until the VisualLine.VisualTop stops changing. *)
                            scroll visual_top
(* If GetOrConstructVisualLine raised an exception, stop. *)
            with | _ -> ()
(* Begin scrolling. *)
        do scroll System.Double.NaN

(* Note that an open document always has at least one line. Also, line numbers start from 1. *)
/// <summary>Return the document line whose line number is (1). If there is no such line, return the last line in the document.</summary>
    member this.safe_line_number_to_line line_number =
        let line_number_ =
            if line_number < 1 then 1
            else if line_number > this.Document.LineCount then this.Document.LineCount
            else line_number
        line_number_ |> this.Document.GetLineByNumber

(* A TextViewPosition is provided by the TextEditor, so it should always contain a valid line and column. *)
/// <summary>Return the line at position (1) in the document. If the position is None, return the last line in the document. Preconditions: (1), if not None, is a valid position in the current document.</summary>
    member this.safe_position_to_line (position : TextViewPosition option) =
        match position with
        | Some position_ -> position_.Line |> this.safe_line_number_to_line
(* As long as a document is open for editing it should have at least one line. *)
        | None -> this.Document.Lines.[this.Document.LineCount - 1]
//#endregion

(* Methods: tag operations. *)
//#region
/// <summary>Add tags (1) to lines (2). Return unit.</summary>
    member this.add_tags_to_lines tags (lines : Document.DocumentLine list) =
(* Verify the lines are valid for the current document. *)
        match validate_lines lines with
        | None -> ()
(* Hide the old definition of lines. *)
        | Some lines ->
(* Make sure the tag starts with the tag symbol. *)
            let tags_ = tags |> List.map TagInfo.add_tag_symbol_to_start
(* Loop through the lines. *)
            do lines |> List.iter (fun line ->
(* Loop through the tags. *)
                tags_ |> List.iter (fun tag ->
                    let line_text = line |> this.Document.GetText
                    let tag_ = tag.ToString ()
(* Verify the line doesn't already contain this tag. Also verify the line does not contain only whitespace. *)
                    if line_text.Contains tag_ = false && (line_text.Trim ()).Length > 0 then
(* Insert the tag at the end of the line. EndOffset is before the newline. *)
                        do this.Document.Insert (line.EndOffset, tag_)
                    )
                )
//#endregion

(* Methods: find operations. *)
//#region

/// <summary>Find text (1) in file (2). Return a list of FindWordInFileResults.</summary>
    static member find_text_in_closed_file text file =
(* Read the file into a list of lines. Map the lines to results and discard those with no matches. *)
        try file |> File.ReadAllLines |> Array.toList |> List<_>.choosei (find_text_in_line text)
(* If we get an exception, rethrow it. *)
        with | ex -> raise ex

/// <summary>Search file (1) for all tags. Excludes tags that are preceded by links. Return a list of tuples, one for each tag found in the file. (R1) The tag. (R2) The line number. (R3) The line text.</summary>
    static member find_all_tags_in_closed_file_with_symbol file =
(* Get the lines from the file. Map the lines to text. Map the lines to results and discard those with no matches. Since each line might produce multiple results, combine the list of lists into one list. *)
        try file |> File.ReadAllLines |> Array.toList |> List<_>.choosei find_tags_in_line |> List.concat
(* If we get an exception, rethrow it. *)
        with | ex -> raise ex

(* Note this function does not check whether the editor has a file open. The caller must check that. *)
/// <summary>Find all instances of text (1) in the currently open file. Return a list of FindWordInFileResults.</summary>
    member this.find_text_in_open_file text  =
(* Get the lines from the file. Map the lines to text. Map the lines to results and discard those with no matches. *)
        this.Document.Lines |> Seq.toList |> List.map this.Document.GetText |> List<_>.choosei (find_text_in_line text)

/// <summary>Find all tags in the currently open file. Return a list of FindTagInFileResults.</summary>
    member this.find_all_tags_in_open_file_with_symbol () =
(* Get the lines from the file. Map the lines to text. Map the lines to results and discard those with no matches. Since each line might produce multiple results, combine the list of lists into one list. *)
        this.Document.Lines |> Seq.toList |> List.map this.Document.GetText |> List<_>.choosei find_tags_in_line |> List.concat

//#endregion

(* Methods: drag and drop operations. *)
//#region
(* This has to be a method so PaneController can call it, when the TaggerMargin fires the end_drag event in the QueryContinueDrag handler. The QueryContinueDrag event belongs to the drag source, even if it is triggered while dragging over another control. *)
/// <summary>Stop the drag scroll timers. Return unit.</summary>
    member this.stop_drag_scroll_timers () =
        do
            _drag_scroll_up_timer.Stop ()
            _drag_scroll_down_timer.Stop ()
//#endregion

(* Methods: tag completion window. *)
//#region
(* We spent an entire day trying to get AvalonEdit's built in code completion to work. See the topic "Code Completion" in the AvalonEdit help. You're supposed to create a type that implements ICompletionData, then add instances of this type to CodeCompletion.CompletionWindow.CompletionList.CompletionData. 
In AvalonEdit, CompletionList.xaml binds the list box (CodeCompletion.CompletionWindow.CompletionList.ListBox) to the Content property of ICompletionData. However, this binding does not seem to work. As a result, the list box items are shown as empty, even if we explicitly set their Content properties. We put breakpoints on our implementations of the ICompletionData properties Text, Content, and Description, and the only one hit was Description.
We finally decided to implement our own code completion.
If we try to use AvalonEdit's version again, note:
Because AvalonEdit binds the list box to a list of ICompletionData objects, the ListBox.Items property returns not a list of ListBoxItems, but a list of ICompletionData objects. For information on how to get the ListBoxItems, see:
https://stackoverflow.com/questions/3556294/get-the-listboxitem-in-a-listbox
However, we weren't able to get this to work. The items we retrieved (as type object) were still type ICompletionData and we were unable to downcast them to ListBoxItem. This was another reason we gave up on the AvalonEdit version.
If we have a persistent code completion window, it incorrectly appears at the top left of the editor. When we create it on demand, it appears correctly. I believe creating it sets a property that determines where it is shown. We need to find that property and set it each time we show it.
In AvalonEdit, CompletionList.xaml has a trigger on ListBoxItem.Select that applies a style where Background = SystemColors.HighlightBrush and Foreground = SystemColors.HighlightTextBrush, just as we do in AddTagWindow.xaml, so we don't need to worry about adding that.
We need to find out whether ICompletionData.Priority is ascending or descending. *)

/// <summary>Show the tag completion window. (1) The existing tags. (2) The initial tag value, or None. Return unit.</summary>
    member this.show_tag_completion_window tag_mru tag =
(* Get the top and bottom of the TextLine the cursor is on. *)
        let top_point, bottom_point = this.TextArea.Caret.Position |> get_text_line_top_bottom
(* Show the tag completion window. *)
        do _tag_completion_window.Show tag_mru tag top_point bottom_point

/// <summary>Insert a tag into the current document at the current cursor offset. (1) The tag the user entered or selected. (2) Additional text the user entered, if any. Return unit.</summary>
    member this.insert_tag_at_cursor tag text =
(* Append the additional text the user entered, if any, to the tag. *)
        let text_to_insert = sprintf "%s%s" (tag.ToString ()) text
(* Insert the tag and additional text into the current document at the current cursor offset. *)
        do this.Document.Insert (this.CaretOffset, text_to_insert)
(* If the text is a tag symbol, show the TagCompletionWindow again. See also the comments in text_entered_handler. *)
        if text = TagInfo.tag_symbol then do _tag_symbol_entered.Trigger None

//#endregion

(* Properties. *)
//#region
/// <summary>The currently open file.</summary>
    member this.file with get () = _file

/// <summary>The interval, in milliseconds, at which the document is saved.</summary>
    member this.save_timer_interval
        with get () = _save_timer.Interval.TotalMilliseconds
        and set value = do _save_timer.Interval <- TimeSpan.FromMilliseconds value

/// <summary>The delay before hovering the mouse causes a tool tip to appear.</summary>
    member this.mouse_hover_delay
        with get () = _mouse_hover_delay
        and set value = do _mouse_hover_delay <- value

/// <summary>The number of lines scrolled per mouse wheel notch.</summary>
    member this.mouse_scroll_speed
        with get () = _mouse_scroll_speed
        and set value = do _mouse_scroll_speed <- value

/// <summary>The number of lines scrolled per second while the user drags near the top or bottom of the editor.</summary>
    member this.drag_scroll_speed
        with get () = _drag_scroll_speed
        and set value =
            do
                _drag_scroll_speed <- value
(* Set the drag scroll timer interval accordingly. *)
                set_drag_scroll_timer_intervals ()

/// <summary>The currently margin selected lines.</summary>
    member this.margin_selected_lines with get () = !_margin_selected_lines

/// <summary>The currently editor selected lines.</summary>
    member this.editor_selected_lines with get () =
        if this.SelectionLength = 0 then []
        else
            let start_line_index = offset_to_line_index this.SelectionStart
            let end_line_index = offset_to_line_index <| this.SelectionStart + this.SelectionLength
            let lines = this.Document.Lines |> Seq.toArray
(* Line numbers start from 1. *)
            lines.[start_line_index .. end_line_index] |> Array.toList

/// <summary>The most recently margin selected line. We use this to decide margin selection behavior.</summary>
    member this.most_recently_margin_selected_line
        with get () = _most_recently_margin_selected_line
//        and set value = do _most_recently_margin_selected_line <- value

/// <summary>The line that was most recently margin selected without pressing Shift. We use this to decide margin selection behavior.</summary>
    member this.most_recently_margin_selected_line_no_shift
        with get () = _most_recently_margin_selected_line_no_shift
//        and set value = do _most_recently_margin_selected_line_no_shift <- value

(* Expose events. *)
/// <summary>A general non-critical error occurred. (1) The error message.</summary>
    member this.general_error = _general_error.Publish
/// <summary>There was an error when auto-saving the open file.</summary>
    member this.save_file_error = _save_file_error.Publish
/// <summary>The user right-clicked.</summary>
    member this.right_click = _right_click.Publish
/// <summary>The user pressed the key combination for Find in Project.</summary>
    member this.find_in_project = _find_in_project.Publish
/// <summary>The margin selection changed.</summary>
    member this.margin_selection_changed = _margin_selection_changed.Publish
/// <summary>The user dragged from a margin and over the editor.</summary>
    member this.margin_drag = _margin_drag.Publish
/// <summary>The user dragged from a margin and dropped on the editor.</summary>
    member this.margin_drop = _margin_drop.Publish
/// <summary>The user ended a drag.</summary>
    member this.end_drag = _end_drag.Publish
/// <summary>The scroll offset of the document changed. (1) The change in the vertical scroll offset.</summary>
    member this.scroll_changed = _scroll_changed.Publish
/// <summary>The extent height of the document changed.</summary>
    member this.extent_height_changed = _extent_height_changed.Publish
/// <summary>The user entered the tag symbol.</summary>
    member this.tag_symbol_entered = _tag_symbol_entered.Publish
/// <summary>The user selected a tag in the TagCompletionWindow. (1) The tag.</summary>
    member this.tag_selected = _tag_selected.Publish
/// <summary>The user inserted or deleted text. (1) The text. (2) True if the user inserted text; false if the user deleted text.</summary>
    member this.document_changed = _document_changed.Publish
//#endregion

(* Expose methods for testing. *)
    member this.test_position_to_words_links_tag = position_to_words_links_tag
    member this.test_validate_lines = validate_lines
    member this.test_validate_line = validate_line
    member this.test_line_to_start_end_length = line_to_start_end_length
    member this.test_insert_line_at_offset_in_text = insert_line_at_offset_in_text
    member this.test_lines_to_text_helper_cut = lines_to_text_helper_cut
    member this.test_lines_to_text_helper_copy = lines_to_text_helper_copy
    member this.test_move_and_copy_helper = move_and_copy_helper
    static member test_find_text = find_text
    member this.test_mouse_right_up_helper = mouse_right_up_helper
    member this.test_text_changed_handler = text_changed_handler

(* Notes. *)
//#region
(*
Issues we ran into with handling these events:
When the user left-clicks on the line number margin, we want to highlight the entire line to the newline. but the default handler, whether WPF or AvalonEdit, only highlights one line. We have several approaches.
1. Add another handler with IObservable.Add. However, these events don't always seem to fire. That's probably because the default handler marks the event as handled.
2. Add a handler with UIElement.AddHandler. This lets you handle the event even if other handlers have marked it handled. However, we're still getting odd behavior with this, detailed below.
3. Remove the default handlers if they interfere with ours. I'm not sure how to do this.
4. Handle the tunneling (i.e. Preview* ) versions of the events rather than the bubbling versions.
5. Subclass the editor, add handlers, and do not call the base class handlers. The subclass might automatically inherit the base clase handlers though.
6. We could just make a thin panel to the left of the editor, with the same height, and add a handler to that and get the corresponding line number in the editor. However, we're likely to run into these handler collision issues elsewhere.

These represent approach #1. We used them while trying to narrow down how to handle a click on the line number margin.
this.MouseLeftButtonUp.Add (fun args -> "editor click" |> MessageBox.Show |> ignore)
this.TextArea.MouseLeftButtonUp.Add (fun args -> "textarea click" |> MessageBox.Show |> ignore)
this.TextArea.TextView.MouseLeftButtonUp.Add (fun args -> "textview click" |> MessageBox.Show |> ignore)
The TextView handler only fired when I double-clicked. That might have been an example of the default handler interfering - i.e. it might have absorbed the first click, and might have ignored the second click because it was part of a double click, which let our handler get the second click.
Likewise, clicking on the line number margin didn't fire our handler.

These represent approach #2. They worked better, but see issues below.
this.TextArea.TextView.AddHandler (UIElement.MouseLeftButtonDownEvent, new RoutedEventHandler (test_event "textview click"), true)
this.TextArea.AddHandler (UIElement.MouseLeftButtonDownEvent, new RoutedEventHandler (test_event "textarea click"), true)
this.AddHandler (UIElement.MouseLeftButtonDownEvent, new RoutedEventHandler (test_event "editor click"), true)
Clicking in the text fired all three handlers, clicking on the scroll bar fired the editor handler, and clicking the line number margin fired the textarea and editor handlers.

Issues with clicking the line number margin:
1. The following code, without the type check for the LineNumberMargin type, causes the mouse to select multiple lines merely by moving over the line number margin, without the user holding the mouse button. It seems to be the combination of the line number margin and the dotted line margin handlers.
2. We added a type check. Then the first click on a line number seems to start a drag select (without the mouse button being held) that lets me select multiple lines. The second click seems to release the selection and selects a new, single line. The issue seems to be the message box. It's as if it makes the window think that the mouse button is being held rather than clicked once.
3. The status bar is outside the control, so it might have less impact than changing the text or launching a message box. Using the status bar, the handler works as expected.
Note: I originally used Seq.map by accident. The call was ellided because I didn't assign the result to anything. Also, there was no suggestion to use the ignore keyword. Watch out for this in future.
        this.TextArea.LeftMargins |> Seq.iter (fun margin -> if margin :? Editing.LineNumberMargin then do margin.AddHandler (UIElement.MouseLeftButtonDownEvent, new RoutedEventHandler (test_event3 "margin click"), true))

Notes about how to test event handlers:
Launching a message box releases the mouse capture from the editor. I tried recapturing the mouse in the handler, which should have meant the mouse was still captured when the next handler fired, but it didn't seem to work.
Clicking a message box over the editor counts as another click event in the editor.
We tried adding text to the text area instead. However, when clicking on the line number margin, the text change released the selection of the line done by the default handler.

These first two should not be used. They interfere with other parts of the event handlers we're trying to test (i.e. they change the text selection, mouse capture, and so on).
        let test_event message (sender : obj) (args : RoutedEventArgs) =
            do this.Text <- this.Text.Insert (this.TextLength, sprintf "%s %s\n" message (DateTime.Now.ToString ()))
        let test_event2 message (sender : obj) (args : RoutedEventArgs) =
            do sprintf "%s %s\n" message (DateTime.Now.ToString ()) |> MessageBox.Show |> ignore
This works better.
        let test_event3 message (sender : obj) (args : RoutedEventArgs) =
            do this.status.Text <- sprintf "%s %s\n" message (DateTime.Now.ToString ())
We no longer have the status bar, so if we need to test more events, we should probably just fire the event out of the editor and handle it up in the main window.

Tunneling and bubbling events:
http://msdn.microsoft.com/en-us/library/ms742806.aspx
WPF input events that come in pairs are implemented so that a single user action from input, such as a mouse button press, will raise both routed events of the pair in sequence. First, the tunneling event is raised and travels its route. Then the bubbling event is raised and travels its route. The two events literally share the same event data instance, because the RaiseEvent method call in the implementing class that raises the bubbling event listens for the event data from the tunneling event and reuses it in the new raised event. Listeners with handlers for the tunneling event have the first opportunity to mark the routed event handled (class handlers first, then instance handlers). If an element along the tunneling route marked the routed event as handled, the already-handled event data is sent on for the bubbling event, and typical handlers attached for the equivalent bubbling input events will not be invoked.

General rules for handling events:
1. By default, use IObservable.Add.
2. If the default event handler prevents yours from firing, but doesn't otherwise interfere, use UIElement.AddHandler.
3. If the default event handler interferes with yours (for instance, by causing a drag and drop operation to drop twice), use IObservable.Add with the Preview (tunneling) event and mark the event as handled to prevent the default handler from firing.

Maybe useful app for debugging events:
http://www.wpfmentor.com/2008/11/understand-bubbling-and-tunnelling-in-5.html
*)
//#endregion
