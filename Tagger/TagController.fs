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

module TagController

// Math
open System
// File, Path
open System.IO
// Regex
open System.Text.RegularExpressions
// DragDropKeyStates, MessageBox
open System.Windows
// ContextMenu, MenuItem
open System.Windows.Controls

open ICSharpCode.AvalonEdit

// LoggerWrapper
open LoggerWrapper
// TaggerConfig
open Config
// TaggerTextEditor, TaggerTabControl
open TaggerControls
// PaneController
open PaneController
// AddOnCommandParameters
open AddOnServerController
// FindInProjectWindow
open FindInProjectWindow
// AddTagWindow
open AddTagWindow
// OpenUrlWindow
open AddOnCommandWindows
// MaybeMonad
open Monads
// List extensions
open ListHelpers

/// <summary>Coordinates events between the left and right pane PaneControllers (1) (2). (3) The document map. (4) The configuration, which contains settings such as the margin highlight display time.</summary>
type TagController (left_pc : PaneController, right_pc : PaneController, documents : DocumentMap ref, config : TaggerConfig) as this =

(* Events. *)
//#region
/// <summary>The project needs to be saved, for example because the tag number was incremented.</summary>
    let _save_project = new Event<unit> ()
/// <summary>The right sidebar needs to be expanded.</summary>
    let _expand_right_sidebar = new Event<unit> ()
/// <summary>The user sent a command to the add on. (1) The command. (2) The parameters.</summary>
    let _command_sent = new Event<string * AddOnCommandParameters> ()
//#endregion

(* Member values. *)
//#region
/// <summary>The next available tag number for the current project.</summary>
    let mutable _tag_number = 0
/// <summary>The files the user most recently selected from the Move To context menu.</summary>
    let mutable _move_to_mru = []
/// <summary>The tags the user most recently selected from the Add Tag dialog.</summary>
    let mutable _tag_mru : TagWithoutSymbol list = []
/// <summary>The built in tag list.</summary>
    static let _built_in_tags : TagWithoutSymbol list = [TagWithoutSymbol "zzNext"]
/// <summary>The Add Tag dialog.</summary>
    let _add_tag_window = new AddTagWindow ()
/// <summary>The Find in Project dialog.</summary>
    let _find_in_project_window = new FindInProjectWindow ()
/// <summary>The Open URL dialog.</summary>
    let _open_url_window = new OpenUrlWindow ()
/// <summary>The warning to show when the user dumps Find in Project results to a file.</summary>
    let _dump_find_in_project_results_warning = "Note each line of this output ends with a temporary one-way link to the original file and line number. These links might become non-valid if you change the original files, add files to the project, or change the file filter pattern in the configuration."
//#endregion

(* General helpers. *)
//#region
/// <summary>Return the reverse of tuple (1).</summary>
    let swap (x, y) = y, x
/// <summary>Return the opposite pane of pane (1).</summary>
    let pane_to_opposite_pane = function | LeftOrRightPane.LeftPane -> LeftOrRightPane.RightPane | LeftOrRightPane.RightPane -> LeftOrRightPane.LeftPane | _ -> failwith "pane_to_opposite_pane was passed an invalid pane value."
/// <summary>For pane (1), return a tuple: (1) The corresponding PaneController. (2) The opposite PC.</summary>
    let pane_to_pcs = function | LeftOrRightPane.LeftPane -> left_pc, right_pc | LeftOrRightPane.RightPane -> right_pc, left_pc | _ -> failwith "pane_to_pcs was passed an invalid pane value."
/// <summary>For pane (1), return a tuple: (1) The opposite PaneController. (2) The corresponding PC.</summary>
    let pane_to_opposite_pcs = pane_to_pcs >> swap
/// <summary>For pane (1), return the PaneController.</summary>
    let pane_to_pc = pane_to_pcs >> fst
/// <summary>For pane (1), return the opposite PaneController.</summary>
    let pane_to_opposite_pc = pane_to_pcs >> snd
/// <summary>For pane (1), return a tuple: (1) The corresponding editor. (2) The opposite editor.</summary>
    let pane_to_editors pane =
        let x, y = pane |> pane_to_pcs
        x.editor, y.editor
/// <summary>For pane (1), return the corresponding editor.</summary>
    let pane_to_editor = pane_to_editors >> fst
/// <summary>For PaneController (1), return the open file. If no file is open, return an empty string.</summary>
    let pc_to_open_file (pc : PaneController) = match pc.editor.file with | Some file -> file | None -> System.String.Empty
/// <summary>For editor (1), return the corresponding PaneController.</summary>
    let editor_to_pc editor = if left_pc.editor.Equals editor then left_pc else right_pc
/// <summary>Return the pane that has focus.</summary>
    let get_pane_with_focus () =
        if left_pc.editor.IsFocused then LeftOrRightPane.LeftPane
(* We believe the only case in which no pane has focus is when no file is open. In that case, default to the right pane. That way, when the user selects a file from the Find in Project window, show_find_in_project_dialog > show_find_in_project_result > show_find_in_project_result_helper > get_pane_to_open_file > pane_to_opposite_pane returns the left pane. *)
        else LeftOrRightPane.RightPane
/// <summary>Return the checksum for the file in editor (1).</summary>
    let get_checksum_single_editor (editor : TaggerTextEditor) = editor.Document.TextLength
/// <summary>Return the checksum for the files in editors (1) and (2).</summary>
    let get_checksum_both_editors editor_1 editor_2 = [editor_1; editor_2] |> List.sumBy get_checksum_single_editor
/// <summary>Return the checksum for the file in pane (1).</summary>
    let get_checksum_single_pane (pc : PaneController) = pc.editor |> get_checksum_single_editor
/// <summary>Return the checksum for the files in panes (1) and (2).</summary>
    let get_checksum_both_panes pc_1 pc_2 = [pc_1; pc_2] |> List.sumBy get_checksum_single_pane
/// <summary>Return the checksum for the file with path (1).</summary>
    let get_checksum_closed_file file =
        try (new FileInfo (file)).Length |> int
        with | _ -> 0
//#endregion

(* Document collection helpers. *)
//#region

/// <summary>Insert text (1) at the end of closed file (2). Also insert the text at the end of the document for the file. Return unit.</summary>
    let insert_text_at_end_of_closed_file text file =
/// <summary>Return the document for file (1). If we do not find the document in the document map, read the file and create a new document for it. If we fail to read the file, return None.</summary>
        let get_document_from_document_map file =
            match (!documents).TryGetValue file with
            | true, (document, _) -> Some document
(* If we did not find the document, read the file and create a new document for it. *)
            | _ -> file |> PaneController.load_file_into_document
/// <summary>If file (2) does not end in a newline, return text (1) with a newline prepended. If file (2) ends in a newline, return text (1) with no change. If we fail to read the file, return None.</summary>
        let prepend_newline_to_text text file =
            try
(* Read the file. *)
                let file_text = File.ReadAllText file
(* If the file ends with a newline, do nothing. *)
                if file_text.EndsWith "\n" then text |> Some
(* If not, prepend a newline to the text. *)
                else sprintf "\n%s" text |> Some
(* If we get an error, log it. *)
            with | ex ->
                do _logger.Log_ ("TagController.OpenFileError", ["path", file; "message", ex.Message])
                None
/// <summary>Append text (1) to file (2). Return unit.</summary>
        let insert_text_at_end_of_closed_file_helper text file =
            try
(* Append the text to the file. *)
                do File.AppendAllText (file, text)
                true
(* If we get an error, log it. *)
            with | ex ->
                do _logger.Log_ ("TagController.OpenFileError", ["path", file; "message", ex.Message])
                false
/// <summary>Append text (1) to the text of document (2). Return unit.</summary>
        let insert_text_at_end_of_document (text : string) (document : Document.TextDocument) =
            document.Insert (document.TextLength, text)
        MaybeMonad () {
(* Get the document for the file. *)
            let! document = get_document_from_document_map file
(* Add a newline to the start of the text if the file does not end in a newline. *)
            let! text = prepend_newline_to_text text file
(* Append the text to the closed file. If we succeed... *)
            do! insert_text_at_end_of_closed_file_helper text file
            do
(* Append the text to the document for the file. *)
                insert_text_at_end_of_document text document
(* The third parameter is a function that generates the new value based on the old value. *)
(* Add or update the document in the document map. *)
                (!documents).AddOrUpdate (file, (document, DateTime.Now), (fun _ _ -> document, DateTime.Now)) |> ignore
            return true
        } |> ignore
//#endregion

(* Event handler helpers: margin: general: tag_lines and helpers. *)
//#region
(* add_tags_helper and helpers. *)

/// <summary>Return the next available tag number.</summary>
    let get_next_tag_number () : TagWithoutSymbol =
/// <summary>Helper to increment the tag number until it has no match in the Tag MRU.</summary>
        let rec avoid_collision () =
(* If the Tag MRU contains the tag number, increment the tag number and recurse. *)
            if _tag_mru |> List.exists (fun tag -> tag = (_tag_number |> string |> TagWithoutSymbol)) then
                do
                    _tag_number <- _tag_number + 1
                    avoid_collision ()
(* Note this does not increment the tag number by default. It only does so if it finds the current tag number in the Tag MRU. *)
(* Note the Tag MRU does not always contain all tags in the project.
1. It gets all tags from all files on project load. See MC.open_project_helper, which calls TagController.find_all_tags_in_closed_files.
2. The Add Tag window calls add_tags_helper, which calls add_tags_to_mru.
3. Editor handles TagCompletionWindow.commit with TagController.tag_selected_handler, which calls add_tags_helper, which calls add_tags_to_mru.
So the user could still get around this by copying and pasting a tag from outside of Tagger, or entering the tag symbol, dismissing the TagCompletionWindow, and then entering the tag. *)
        avoid_collision ()
        _tag_number |> string |> TagWithoutSymbol

(* This is never called by more than one thread at a time. If it was, we would need to worry about locking _tag_number. *)
/// <summary>Replace any built-in tags in list (1) with the appropriate values. Return a tuple. (R1) The updated tag list that includes both the built-in tags and their replacement values. (R2) The updated tag list that includes only the replacement values for the built-in tags. (3) The updated tag number.</summary>
    let replace_built_in_tags (tags : TagWithoutSymbol list) : TagWithoutSymbol list * TagWithoutSymbol list =
(* Loop through the tags. *)
        let results = tags |> List.map (fun tag ->
(* If the user entered or selected the built-in tag for the next available tag number... *)
            if tag = TagWithoutSymbol "zzNext" then
(* Get the next available tag number. *)
                let next_tag_number = get_next_tag_number ()
(* For the first result list, include both the built-in tag and its replacement value. For the second result list, replace the built-in tag with the replacement value. *)
                [tag; next_tag_number], [next_tag_number]
(* Otherwise, do not replace the tag in either of the results lists. *)
            else [tag], [tag]
            )
(* The loop returns a list of tuples. Unzip the list into two lists. *)
        let tags_with_built_in_tags_included, tags_with_built_in_tags_replaced = results |> List.unzip
(* Each list item contains either one or two list items. (This is similar in concept to List.choose, where each loop returns either zero or one list items.) Concatenate the two nested lists into two flat lists. *)
        tags_with_built_in_tags_included |> List.concat, tags_with_built_in_tags_replaced |> List.concat

/// <summary>Move tags (1) to the top of the Tag MRU. Return unit.</summary>
    let add_tags_to_mru (tags : TagWithoutSymbol list) = 
(* Move the tags to the top of the tag MRU. *)
        do _tag_mru <- List<_>.move_to_head tags _tag_mru

/// <summary>Move the tags (1) to the top of the tag MRU. Replace any built-in tags with the appropriate values. Return the updated tags.</summary>
    let add_tags_helper tags =
(* We want to include the built-in tags in the tags we move to the top of the tag MRU, so the user can select them again. We do not want to include them in the tags we insert in the document.

                Built-in    Replacement
Add to tag MRU  x           x
Add to line(s)              x
*)
(* Get two lists: one that includes both the built-in tags and their replacement values, and one that contains only the replacement values for the built-in tags. *)
        let tags_with_built_in_tags_included, tags_with_built_in_tags_replaced = replace_built_in_tags tags
        do
(* Move the list of tags that includes the built-in tags to the top of the tag MRU. *)
            tags_with_built_in_tags_included |> add_tags_to_mru
(* Notify the MainController that the project needs to be saved. *)
            _save_project.Trigger ()
(* Return the list of tags that does not include the built-in tags. *)
        tags_with_built_in_tags_replaced

(* tag_lines_helper and helpers. *)

/// <summary>Add tags (3) to lines (2) in editor (1). Return unit.</summary>
    let add_tags_to_lines (editor : TaggerTextEditor) lines tags =
(* If no file is open in the editor, stop. *)
        match editor.file with
        | None -> ()
(* Add the tags to the lines. Note that if a line already contains one of the tags, the tag is not added again. *)
        | Some file -> do editor.add_tags_to_lines tags lines

(* The target_pc parameter is optional; the source_pc parameter is not. *)
/// <summary>Add tags to lines (3) in pane (1) and lines (4) in pane (2). (1) and (2) can be the same pane or different ones. Return unit.</summary>
    let tag_lines_helper source_pc target_pc source_lines target_lines tags =
(* Helper to see whether we have a value for the target pane before we try to tag lines in it. The target pane is optional because the user might only want to tag lines in one pane. If so, we consider that pane to be the non-optional source pane. See get_context_menu. *)
        let tag_lines_helper_2 (pc : PaneController option) lines tags =
            match pc with
            | Some pc_ -> do add_tags_to_lines pc_.editor lines tags
            | None -> ()
(* Move the tags to the top of the tag MRU. Replace any built-in tags with the appropriate values. *)
        let tags_ = tags |> add_tags_helper
        do
(* Add the tags to the source lines. *)
            tag_lines_helper_2 (Some source_pc) source_lines tags_
(* Add the tags to the target lines. *)
            tag_lines_helper_2 target_pc target_lines tags_
(* Adding tags to the lines changes the text in the editor, which clears the margin selection. Restore the margin selection of the source and target lines. *)
            source_pc.margin_select_lines source_lines None false
        if target_pc.IsSome then do target_pc.Value.margin_select_lines target_lines None false

// TODO2 Break this function up; it has 8 parameters.
(* The target_pc parameter is optional; the source_pc parameter is not. *)
/// <summary>Show the Add Tag dialog. This lets the user add tags to lines (3) in pane (1) and lines (4) in pane (2). (1) and (2) can be the same or different panes. (5) A function to copy lines (3) from pane (1) to pane (2) instead. (6) A function to move lines (3) from pane (1) to pane (2) instead. Return unit.</summary>
    let tag_lines source_pc target_pc (source_lines : Document.DocumentLine list) target_lines allow_copy allow_move copy_lines move_lines =
(* If there are no source lines, stop. *)
        if source_lines.Length = 0 then ()
        else
(* Show the Add Tag window. If the user cancels it, stop. *)
            match _add_tag_window.ShowDialog _tag_mru allow_copy allow_move with
            | None -> ()
(* If the user did not cancel the dialog, look at the return value. *)
            | Some value ->
(* AddTagWindow.ShowDialog returns a tuple. (R1) The list of tags the user selected. (R2) True to copy and tag the source lines. (R3) True to copy the source lines. (R4) True to move the source lines. *)
                match value with
(* Add the tags to the source and target lines. *)
                | tags, false, false, false -> do tag_lines_helper source_pc target_pc source_lines target_lines tags
                | tags, true, false, false ->
                    do
(* Add the tags to the source lines. *)
                        tag_lines_helper source_pc None source_lines [] tags
(* Now that the source lines have tags, copy them to the target pane. *)
                        copy_lines ()
(* Copy the source lines to the target pane. *)
                | tags, false, true, false -> do copy_lines ()
(* Move the source lines to the target pane. *)
                | tags, false, false, true -> do move_lines ()
(* This should never happen. *)
                | _ -> ()

//#endregion

(* Event handler helpers: margin: general: other. *)
//#region

(* This function takes a list of source line numbers and a list of target line numbers, which must be obtained before a move/copy operation, and finds what lines to highlight after the operation. This function must be called after the operation.

We start with the target line number. Source lines are combined and inserted before the target line. So we begin highlighting source lines at the target line number. We also subtract one from the target line number for every source line that precedes the target line. Also see Editor.lines_to_text_helper_cut.

We highlight a number of lines equal to the number of source lines with the Drag From highlight. Then we highlight one line, which is the target line in its new location, with the Drag To highlight.

For example, you have:
1
2
3
4

1. Drop 1 and 3 on 4. Result:
2
1 <- source highlight
3 <- source highlight
4 <- target highlight

2. Drop 1 and 3 on 2. Result:
1 <- source highlight
3 <- source highlight
2 <- target highlight
4

3. Drop 2 and 4 on 1. Result:
2 <- source highlight
4 <- source highlight
1 <- target highlight
3

Note if the source and target panes are different, this function is only needed to find the lines to highlight in the target pane. In the source pane, the source lines are removed (for a move) or remain in the same place (for a copy).
*)

(* We tried to replace this with ILineTracker.LineInserted, but failed. See Editor for details. *)
/// <summary>Find the new locations of the source lines (2) and target lines (3) after inserting (2) before (3). (1) The document. (4) True if we previously moved lines within the same pane; false if we moved lines from a different pane, or copied lines from either the same pane or a different pane. Return a tuple. (R1) The lines to highlight with the source (Drag From) highlight. (R2) The lines to highlight with the target (Drag To) highlight. Precondition: the source lines (2) and target lines (3) are sorted by line number.</summary>
    let get_lines_to_highlight (doc : Document.TextDocument) (source_line_numbers : int list) (target_line_numbers : int list) same_pane_move =
(* If the document lines, source lines, or target lines are empty, stop. *)
        if doc.Lines.Count = 0 || source_line_numbers.IsEmpty || target_line_numbers.IsEmpty then [], []
        else
(* We use these to calculate which lines to highlight. *)
            let source_highlight_lines_length = source_line_numbers.Length
(* We insert the source lines before the first target line. *)
            let target_line_number = target_line_numbers.Head
            let source_highlight_start_line_number =
(* The inserted source lines start where the target line used to be, so we start highlighting the source lines there. *)
(* Note we ignore the source line numbers unless we previously moved lines within the same pane. *)
                if false = same_pane_move then target_line_number
                else
(* If we previously moved lines within the same pane, and any of the source lines preceded the target line, the target line number decreased when the source lines were cut. Count how many source lines preceded the target line, and subtract that number from the target line number. *)
                    target_line_number - (source_line_numbers |> List.filter (fun source_line_number -> source_line_number < target_line_number) |> List.length)
(* Line numbers start from 1. To get the line index, subtract 1 from the line number. *)
            let source_highlight_start_line_index = source_highlight_start_line_number - 1
(* Verify that the document has the expected number of lines. *)
            if doc.Lines.Count < source_highlight_start_line_index + source_highlight_lines_length then [], []
            else
(* We use seq.truncate instead of seq.take because it doesn't throw if the sequence doesn't have the expected number of items. We would also use a safe alternative for seq.skip if we knew what it was. *)
                let source_lines_ = doc.Lines |> Seq.skip source_highlight_start_line_index |> Seq.truncate source_highlight_lines_length |> Seq.toList
                let target_lines_ = doc.Lines |> Seq.skip (source_highlight_start_line_index + source_highlight_lines_length) |> Seq.truncate target_line_numbers.Length |> Seq.toList
                source_lines_, target_lines_

//#endregion

(* Event handler helpers: margin: same pane. *)
//#region

/// <summary>Verify the file in pane (1) has the expected checksum after copying text in the same pane. (2) The checksum before copying text. (3) The length of the copied text. Return true if we succeed.</summary>
    let check_copy_checksum_single_pane pc checksum_1 copy_length =
        let checksum_2 = get_checksum_single_pane pc
        let result = checksum_2 - copy_length - checksum_1 |> Math.Abs
(* We allow a difference of 1 character because we might have added a newline to the copied text. *)
        if result > 1 then
            let path = pc |> pc_to_open_file
            do _logger.Log_ ("TagController.CopyTextSamePaneError",
                ["path", path;
                "length_1", (string) checksum_1;
                "copy_length", (string) copy_length;
                "length_2", (string) checksum_2;
                "result", (string) result])
            false
        else true

/// <summary>Verify the file in pane (1) has the same checksum after moving text in the same pane. (2) The checksum before moving text. Return true if we succeed.</summary>
    let check_move_checksum_single_pane pc checksum_1 =
        let checksum_2 = get_checksum_single_pane pc
        let result = checksum_2 - checksum_1 |> Math.Abs
(* We allow a difference of 1 character because we might have added a newline to the moved text. *)
        if result > 1 then
            let path = pc |> pc_to_open_file
            do _logger.Log_ ("TagController.MoveTextSamePaneError",
                ["path", path;
                "length_1", (string) checksum_1;
                "length_2", (string) checksum_2;
                "result", (string) result])
            false
        else true

/// <summary>In the document open in the PaneController (1), copy lines (2) to line (3). Highlight the lines in the margin. Return unit.</summary>
    let copy_lines_same_pane (pc : PaneController) source_lines (target_line : Document.DocumentLine) =
(* Return the line number of line (1). *)
        let get_line_number (line : Document.DocumentLine) = line.LineNumber
(* Get the checksum before copying text. *)
        let checksum_1 = get_checksum_single_pane pc
(* Get the source and target line numbers before moving text. *)
        let source_line_numbers, target_line_number = source_lines |> List.map get_line_number, target_line.LineNumber
(* Get the length of the copied text. *)
        let copy_length = source_lines |> List.sumBy (fun line -> line.TotalLength)
(* Copy the text from the source line to the target line. *)
        if pc.editor.copy_lines source_lines target_line &&
(* Verify the file has the expected checksum. *)
            check_copy_checksum_single_pane pc checksum_1 copy_length then
(* Get the source and target lines in their new locations. *)
            let source_lines_, target_lines_ = get_lines_to_highlight pc.editor.Document source_line_numbers [target_line_number] false
            do
(* Highlight the target lines. *)
                pc.highlight_lines DragTo target_lines_ config.margin_highlight_display_time
(* Margin select the new copy of the source lines. *)
                pc.margin_select_lines source_lines_ None false

/// <summary>In the document open in the PaneController (1), move lines (2) to line (3). Highlight the lines in the margin. Return unit.</summary>
    let move_lines_same_pane (pc : PaneController) source_lines (target_line : Document.DocumentLine) =
(* Return the line number of line (1). *)
        let get_line_number (line : Document.DocumentLine) = line.LineNumber
(* Get the checksum before moving text. *)
        let checksum_1 = get_checksum_single_pane pc
(* Get the source and target line numbers before moving text. *)
        let source_line_numbers, target_line_number = source_lines |> List.map get_line_number, target_line.LineNumber
(* Move the text from the source line to the target line. *)
        if pc.editor.move_lines source_lines target_line &&
(* Verify the file has the same checksum as before. *)
            check_move_checksum_single_pane pc checksum_1 then
(* Get the source and target lines in their new locations. *)
            let source_lines_, target_lines_ = get_lines_to_highlight pc.editor.Document source_line_numbers [target_line_number] true
            do
(* Highlight the target lines. *)
                pc.highlight_lines DragTo target_lines_ config.margin_highlight_display_time
(* Restore the margin selection of the source lines. *)
                pc.margin_select_lines source_lines_ None false

(* We assume that dragging/dropping within the same pane means within the same file, because only one file can be open in a pane. We don't support dragging/dropping from one tab to another in the same pane. To move text from one file to another when the files are not both open in different panes, the user should use Send To. *)        
/// <summary>Handle the user dropping lines (2) on line (3) when both are in PaneController (1). (4) The keys that were pressed during the drop. Return unit.</summary>
    let drop_helper_same_pane (pc : PaneController) (source_lines : Document.DocumentLine list) (target_line : Document.DocumentLine) (key_states : DragDropKeyStates) =
/// <summary>Copy the source lines. Return unit.</summary>
        let copy_lines () = do copy_lines_same_pane pc source_lines target_line
/// <summary>Move the source lines. Return unit.</summary>
        let move_lines () = do move_lines_same_pane pc source_lines target_line
(* If Control was pressed, copy the text. *)
        if key_states.HasFlag DragDropKeyStates.ControlKey then do copy_lines ()
(* Within the same pane, the user can only drop on one target line. *)
(* If Alt was pressed, show the Add Tag window. *)
        else if key_states.HasFlag DragDropKeyStates.AltKey then
            do tag_lines pc (Some pc) source_lines [target_line] true true copy_lines move_lines
(* Moving the line is the default behavior. *)
        else do move_lines ()
//#endregion

(* Event handler helpers: margin: different pane. *)
//#region

/// <summary>Verify the files in panes (1) and (2) have the expected checksum after copying text between panes. (2) The checksum before copying text. (3) The length of the copied text. Return true if we succeed.</summary>
    let check_copy_checksum_both_panes source_pc target_pc checksum_1 copy_length =
        let checksum_2 = get_checksum_both_panes source_pc target_pc
        let result = checksum_2 - copy_length - checksum_1 |> Math.Abs
(* We allow a difference of 1 character because we might have added a newline to the copied text. *)
        if result > 1 then
            let source_path = source_pc |> pc_to_open_file
            let target_path = target_pc |> pc_to_open_file
            do _logger.Log_ ("TagController.CopyTextDifferentPaneError",
                ["source_path", source_path;
                "target_path", target_path;
                "source_length", (string) checksum_1;
                "copy_length", (string) copy_length;
                "target_length", (string) checksum_2;
                "result", (string) result])
            false
        else true

/// <summary>Verify the files in panes (1) and (2) have the same checksum after moving text between panes. (2) The checksum before moving text. Return true if we succeed.</summary>
    let check_move_checksum_both_panes source_pc target_pc checksum_1 =
        let checksum_2 = get_checksum_both_panes source_pc target_pc
        let result = checksum_2 - checksum_1 |> Math.Abs
(* We allow a difference of 1 character because we might have added a newline to the moved text. *)
        if result > 1 then
            let source_path = source_pc |> pc_to_open_file
            let target_path = target_pc |> pc_to_open_file
            do _logger.Log_ ("TagController.MoveTextDifferentPaneError",
                ["source_path", source_path;
                "target_path", target_path;
                "length_1", (string) checksum_1;
                "length_2", (string) checksum_2;
                "result", (string) result])
            false
        else true

/// <summary>Copy lines (3) from pane (1) to insert before line (4) in pane (2). Return unit.</summary>
    let copy_lines_different_pane (source_pc : PaneController) (target_pc : PaneController) source_lines (target_line : Document.DocumentLine) =
(* Return the line number of line (1). *)
        let get_line_number (line : Document.DocumentLine) = line.LineNumber
(* Get the checksum before copying text. *)
        let checksum_1 = get_checksum_both_panes source_pc target_pc
(* Get the source and target line numbers before moving text. *)
        let source_line_numbers, target_line_number = source_lines |> List.map get_line_number, target_line.LineNumber
(* Get the text from the source pane based on the line that was dragged from it. *)
        let text = source_pc.editor.lines_to_text source_lines false
(* Insert the text in the target pane based on the line where it was dropped. *)
        do target_pc.editor.insert_line_at_line text target_line
(* Verify the files have the expected checksum. *)
        if check_copy_checksum_both_panes source_pc target_pc checksum_1 text.Length then
(* Get the source and target lines in their new locations in the target pane. The source lines in the source pane were already highlighted and have not moved. *)
            let source_lines_, target_lines_ = get_lines_to_highlight target_pc.editor.Document source_line_numbers [target_line_number] false
            do
(* Highlight the target lines. *)
                target_pc.highlight_lines DragTo target_lines_ config.margin_highlight_display_time
(* Margin select the source lines in the target pane. *)
                target_pc.margin_select_lines source_lines_ None false

/// <summary>Move lines (3) from pane (1) to insert before line (4) in pane (2). Return unit.</summary>
    let move_lines_different_pane  (source_pc : PaneController) (target_pc : PaneController) source_lines (target_line : Document.DocumentLine) =
(* Return the line number of line (1). *)
        let get_line_number (line : Document.DocumentLine) = line.LineNumber
(* Get the checksum before moving text. *)
        let checksum_1 = get_checksum_both_panes source_pc target_pc
(* Get the source and target line numbers before moving text. *)
        let source_line_numbers, target_line_number = source_lines |> List.map get_line_number, target_line.LineNumber
(* Remove the text from the source pane based on the line that was dragged from it. *)
        let text = source_pc.editor.lines_to_text source_lines true
(* Insert the text in the target pane based on the line where it was dropped. *)
        do target_pc.editor.insert_line_at_line text target_line
(* Verify the files have the same checksum as before. *)
        if check_move_checksum_both_panes source_pc target_pc checksum_1 then
(* Get the source and target lines in their new locations in the target pane. *)
            let source_lines_, target_lines_ = get_lines_to_highlight target_pc.editor.Document source_line_numbers [target_line_number] false
            do
(* Highlight the target lines. *)
                target_pc.highlight_lines DragTo target_lines_ config.margin_highlight_display_time
(* Restore the margin selection of the source lines in the target pane. *)
                target_pc.margin_select_lines source_lines_ None false

/// <summary>Handle the user dropping lines (3) from pane (1) on lines (4) in pane (2). (5) The keys that were pressed during the drop. Return unit. Preconditions: (4) is not empty. Lines (4) are contiguous and sorted by line number. Lines (4) are valid lines in the current document in pane (2).</summary>
    let drop_helper_different_pane (source_pc : PaneController) (target_pc : PaneController) source_lines (target_lines : Document.DocumentLine list) (key_states : DragDropKeyStates) =
(* For now, we don't do anything special when the user drops on multiple lines to move or copy, so we treat it as if they dropped on the first line. *)
/// <summary>Copy the source lines. Return unit.</summary>
        let copy_lines () = do copy_lines_different_pane source_pc target_pc source_lines target_lines.Head
/// <summary>Move the source lines. Return unit.</summary>
        let move_lines () = do move_lines_different_pane source_pc target_pc source_lines target_lines.Head
(* If the user pressed the Control key, copy the source lines. *)
        if key_states.HasFlag DragDropKeyStates.ControlKey then do copy_lines ()
(* If the user pressed the Shift key, move the source lines. *)
        else if key_states.HasFlag DragDropKeyStates.ShiftKey then move_lines ()
(* Otherwise, show the Add Tag Window. *)
        else do tag_lines source_pc (Some target_pc) source_lines target_lines true true copy_lines move_lines
//#endregion

(* Margin event handlers. *)
//#region
/// <summary>Handle the user dropping on the margin in pane (1). (2) Data describing the drop. Return unit.<summary>
    let margin_drop_helper target_pane drop_data =
(* Get the target PC. *)
        let target_pc = target_pane |> pane_to_pc
(* Get the source PC. *)
        let source_pc =
            if drop_data.same_margin then target_pc
            else target_pane |> pane_to_opposite_pc
(* Clear the drag from highlight in the source PC. We'll re-show it later if needed. *)
(* Commenting this out, as we now keep the drag from highlight after a drop. *)
//        do source_pc.margin.hide_drag_from_rect ()
(* Get the source and target lines. *)
(* Currently, a drag must start from the margin. Margin.mouse_move_handler, which starts the drag, PaneController.margin_drag_start_handler, and PaneController.margin_select_handler ensure that at least one line is margin selected in the source pane. *)
(* The source lines are the ones margin selected in the source PC. *)
        let source_lines = source_pc.editor.margin_selected_lines
(* The target line is based on the dropped Y position. *)
        let target_line = drop_data.y_position |> target_pc.editor.safe_y_position_to_line
(* There might be multiple target lines. *)
        let target_lines =
(* Get the margin selected lines in the target PC. *)
            let lines = target_pc.editor.margin_selected_lines
(* If the user dropped on a different pane, and multiple lines are margin selected, and the user dropped on one of them, return them. *)
            if
                drop_data.same_margin = false
                && lines |> List.exists (fun line -> line.LineNumber = target_line.LineNumber)
                then lines
(* Otherwise, just return the target line. *)
            else [target_line]
(* Call the appropriate helper to finish the drop. *)
        if drop_data.same_margin then do drop_helper_same_pane target_pc source_lines target_line drop_data.key_states
        else do drop_helper_different_pane source_pc target_pc source_lines target_lines drop_data.key_states
//#endregion

(* Method helpers. *)

(* Method helpers: right_click: move to. *)
//#region
(* Note Move To adds a newline before the inserted text. Drag/drop does not. *)
(* We considered merging move_lines_to_open_file (Move To) and move_lines_different_pane (drag/drop). However:
1. Move To takes editors and drag/drop takes TECs (which are needed for highlighting). So we'd have to pass the pane from get_context_menu -> add_files -> move_lines. That seems clunky because no one else would use them except move_lines_to_open_file. 
2. MoveTo can highlight more simply: instead of calling get_lines_to_highlight like drag/drop, it could just get the last (source_lines.Length) lines from the doc and highlight them, since Move To always adds to the end of the file. If, in the future, Move To does not always add to the end of the file, it can call get_lines_to_highlight.
3. MoveTo uses Document.insert while drag/drop uses editor.insert_line.
*)
(* TODO1 Move lines from one file to another, then back again. It causes a checksum mismatch by 2 characters (1 is the limit). This was apparently caused by the following:
1. When we cut lines from the source file, we include the newline at the end of each line. So when we insert the cut lines into the target file, we insert n + 1 lines, where n is the number of cut lines, because of the final newline.
2. When we margin select the inserted lines, we begin selecting from the first inserted line and select to the end of the document. Again this means we select n + 1 lines where n is the number of inserted lines.
So somehow, when we try to move the margin selection of n + 1 lines back to the source file, it causes a checksum mismatch. For now we have fixed this by simply removing the final newline from the cut lines from the source file.
*)
/// <summary>Move lines (3) from the file open in pane (1) to the file open in pane (2).</summary>
    let move_lines_to_open_file source_pc target_pc lines =
(* Get the checksum before moving text. *)
        let checksum_1 = get_checksum_both_panes source_pc target_pc
(* Get the source and target editors. *)
        let source_editor, target_editor = source_pc.editor, target_pc.editor 
(* Cut the text from the source editor. *)
        let text = source_editor.lines_to_text lines true
(* Remove the final newline from the text. *)
        let text = text.TrimEnd [|'\r'; '\n'|]
(* Get the current line count for the target editor. We use that to highlight the lines after insertion. *)
        let line_count = target_editor.Document.LineCount
(* Insert the lines at the end of the target document. *)
        do target_editor.insert_text_at_end_of_current_file text
(* Verify the files have the same checksum as before. *)
        if check_move_checksum_both_panes source_pc target_pc checksum_1 then
(* Get the inserted lines. We first skip the number of lines in the original target document. Then we get the remaining lines. Since we have not removed any lines from the target document, it is safe to skip the number of lines that were in it previously. *)
            let new_lines = target_editor.Document.Lines |> Seq.skip line_count |> Seq.toList
(* Restore the margin selection of the source lines in the target pane. Since we did not insert the text in front of anything, we do not highlight any lines with the Drag To highlight. *)
            do target_pc.margin_select_lines new_lines None false

/// <summary>Move lines (3) from the file open in pane (1) to file (2).</summary>
    let move_lines_to_closed_file (source_pc : PaneController) (target_file : string) lines =
(* Get the checksum before moving text. *)
        let checksum_1 = (source_pc |> get_checksum_single_pane) + (target_file |> get_checksum_closed_file)
(* Get the text from the source editor, but do not remove it yet. *)
        let text = source_pc.editor.lines_to_text lines false
        do
(* Append the text to the target file. *)
            insert_text_at_end_of_closed_file text target_file
(* Remove the text from the source editor. The simplest way is to call get_text_from_lines again. *)
            source_pc.editor.lines_to_text lines true |> ignore
(* Verify the files have the same checksum as before. *)
        let checksum_2 = (source_pc |> get_checksum_single_pane) + (target_file |> get_checksum_closed_file)
        let result = checksum_2 - checksum_1 |> Math.Abs
(* We allow a difference of 1 character because we might have added a newline to the moved text. *)
        if result > 1 then
            let source_path = source_pc |> pc_to_open_file
(* In this case, we are not moving text between panes, but we log this event as it is nearly identical. *)
            do _logger.Log_ ("TagController.MoveTextDifferentPaneError",
                ["source_path", source_path;
                "target_path", target_file;
                "length_1", (string) checksum_1;
                "length_2", (string) checksum_2;
                "result", (string) result])                

/// <summary>Move lines (4) from the file open in pane (1) to the file (3), which might be open in pane (2).</summary>
    let move_lines source_pc other_pc file lines =
(* If the target file is already open in the other pane, just move it there. *)
        if other_pc |> pc_to_open_file = file then do move_lines_to_open_file source_pc other_pc lines
(* If the target file is not open, simply append to it. *)
        else do move_lines_to_closed_file source_pc file lines

/// <summary>Add all files in (4) as submenu items to menu item (3), except the file that's currently open in the source pane (1). Each submenu item is given a Click handler that moves the lines (5) to the file specified by the user. The other pane (2) is included in case the file is open in that pane.</summary>
    let add_files (source_pc : PaneController) other_pc (menu : MenuItem) files lines =
(* Sort the files based on the Move To MRU list. *)
        let files = List<_>.merge_sort _move_to_mru files
        do files |> List.iter (fun file ->
(* If this file is open in the source editor, don't add it to the submenu. *)
            if source_pc |> pc_to_open_file = file then ()
            else
                let file_item = new MenuItem ()
                do
(* Only add the file name to the menu item header. *)
                    file_item.Header <- Path.GetFileName file
(* Add the handler to the Click event, using the file's full path. *)
                    file_item.Click.Add (fun _ ->
                        do
(* Move the lines to the file. *)
                            move_lines source_pc other_pc file lines
(* Move the file to the head of the Move To MRU. *)
                            _move_to_mru <- List<_>.move_to_head [file] _move_to_mru
(* Notify the MainController that the project needs to be saved. *)
                            _save_project.Trigger ()
                        )
(* Add the submenu item to the menu item (1).*)
                    menu.Items.Add file_item |> ignore
        )
//#endregion

(* These are also used as helpers for the find_in_project member that handles the find_in_project event from AddOnServerController. *)
(* Method helpers: right_click: find_in_project. *)
//#region

/// <summary>Determine whether to display text in file (2) in pane (1) or the opposite pane. Return the pane.</summary>
    let get_pane_to_open_file pane file =
(* Get the editor for pane (1). *)
        let editor = pane |> pane_to_editor
(* If the file is already open in the editor, return that pane. *)
        match editor.file with
        | Some value when value = file -> pane
(* Otherwise, return the opposite pane. *)
        | _ -> pane |> pane_to_opposite_pane

/// <summary>Scroll the file in target pane (4) so the line indicated by (1) is at the same Y position as the source position (2). Highlight the line indicated by (1). If the source pane (3) and target pane (4) are different, highlight the source line. Return unit.</summary>
    let show_text target_line_number source_position source_pane target_pane =
(* Get the Y position of the source position. We wait until now to get this because expanding the right sidebar might change the Y position of the source position. *)
        let y_position = source_position |> (source_pane |> pane_to_editor).position_to_y_position
(* Scroll the file so the target text is where the source text was (if it's in the same file) or across from the source text (if it's in a different file). Display the open tag highlight on the target line. *)
        do (target_pane |> pane_to_pc).display_line target_line_number y_position config.margin_highlight_display_time
(* If the target text is in a different pane than the source text... *)
        if source_pane.Equals target_pane = false then
(* Get the source PaneController. *)
            let source_pc = source_pane |> pane_to_pc
(* Get the source line. *)
            let source_line = source_position.Line |> source_pc.editor.Document.GetLineByNumber
(* Display the open tag highlight on the source line. *)
            do source_pc.highlight_lines OpenTag [source_line] config.margin_highlight_display_time

/// <summary>If file (1) is open in the left or right pane, return the editor for that pane; if not, return None.</summary>
    let find_file_in_panes file =
(* Helper function. Return true if editor (1) has a file (2) open; otherwise, false. *)
        let check_editor (editor : TaggerTextEditor) file = editor.file.IsSome && editor.file.Value = file
(* Get shortcuts to the editors. *)
        let left_editor, right_editor = left_pc.editor, right_pc.editor
        if check_editor left_editor file then Some left_editor
        else if check_editor right_editor file then Some right_editor
        else None

(* This is a variant of find_word_in_files that finds multiple words. It is not currently used. *)
(*
(* TODO3 This should also use find_file_in_panes. *)
(* TODO3 We should return a map of words to results:
(word : string * (file : string * (line_number : int * line_text : string) list) list) list
*)
/// <summary>Find all instances of list of words (1) in list of files (2). Return a list of tuples. (R1) The file where one of the words was found. (R2) Another list of tuples. (R2a) The line number where the word was found. (R2b) The text of the line.</summary>
(* The data structure that describes the results is as follows.
Many word
Word 1 : many file
File 1 : many (line number, line text)
*)
    let find_words_in_files words files =
(* Get shortcuts to the editors. *)
        let left_editor, right_editor = left_pc.editor, right_pc.editor
(* Helper function. Return true if editor (1) has a file (2) open; otherwise, false. *)
        let check_editor (editor : TaggerTextEditor) file = editor.file.IsSome && editor.file.Value = file
(* Transform the list of files into the return value described above. *)
        files |> List.choose (fun file ->
(* If the file is open in either editor, it might be different from the file on disk. *)
            let results =
// TODO3 We need variants find_words_in_open_file and find_words_in_closed_file, so we don't have to loop here.
(* If the file is open in the left editor, search it for the words. *)
                if check_editor left_editor file then words |> List.map left_editor.find_text_in_open_file
(* If not, and the file is open in the right editor, search that for the words. *)
                else if check_editor right_editor file then words |> List.map right_editor.find_text_in_open_file
(* Otherwise, search the closed file for the words. *)
                else
                    try words |> List.map (TaggerTextEditor.find_text_in_closed_file file)
(* If we get an error, log it. *)
                    with | ex ->
                        do _logger.Log_ ("TagController.OpenFileError", ["path", file; "message", ex.Message])
                        []
(* Combine the lists of find results into a single list. Remove results with duplicate line numbers. *)
            let results_ = results |> List.concat |> List<_>.distinctBy fst
(* If this file has no instances of the words, don't include it in the list. *)
            if results_.Length > 0 then Some (file, results_) else None
        )
*)

/// <summary>Find all instances of word (1) in list of files (2). Return a FindInProjectResults.</summary>
    let find_word_in_files word files =
(* Helper. Find if file (1) is open in either pane, then call the appropriate function to find the word in it. Return a FindWordInProjectResults. *)
        let find_word_in_files_helper file =
            match find_file_in_panes file with
(* If the file is open in either editor, it might be different from the file on disk, so search the open file for the word. *)
            | Some editor -> editor.find_text_in_open_file word
            | None ->
(* Otherwise, search the closed file for the word. *)
                try TaggerTextEditor.find_text_in_closed_file word file
(* If we get an error, log it. *)
                with | ex ->
                    do _logger.Log_ ("TagController.OpenFileError", ["path", file; "message", ex.Message])
                    []
(* Transform the list of files into the return value described above. *)
        files |> List<_>.choosei (fun index file ->
            let results = find_word_in_files_helper file
(* If this file has no instances of the word, don't include it in the list. *)
            if results.Length > 0 then
                let results_ = results |> List.map (fun result -> { result with file_index = index; })
                Some (file, results_)
            else None
        ) |> List.sortBy fst
(* We considered adding a file field to FindWordInFileResult, as we did for FindTagInFileResult. However, we decided not to, because we group FindWordInFileResults by the file first anyway, whereas we group FindTagInFileResults by tag first. *)

/// <summary>If file (1) is open in pane (2), do nothing. If not, open the file in the opposite pane. If the file is open or we open it successfully, return the pane in which we opened it. If we fail to open the file, return None.</summary>
    let show_find_in_project_result_helper target_file source_pane =
(* Get the pane to show the target text. *)
        let target_pane = get_pane_to_open_file source_pane target_file
(* Open the file in the appropriate pane. If it's already open there, this has no effect. *)
        if this.open_file target_pane target_file then
(* If the target pane is the right pane, make sure the right sidebar is expanded. *)
            if target_pane = LeftOrRightPane.RightPane then do _expand_right_sidebar.Trigger ()
            Some target_pane
        else None

/// <summary>Show the result of the Find in Project dialog. (1) The file that contains the result. (2) The line number of the result. (3) The source pane. (4) The position, if there is a valid one where the user clicked; otherwise, None. Return unit.</summary>
    let show_find_in_project_result file line_number source_pane position =
(* Open the selected file in the appropriate pane. If we open the file successfully, continue. *)
        match show_find_in_project_result_helper file source_pane with
        | None -> ()
        | Some target_pane ->
(* The user can open the Find in Project dialog in the following ways.

1. Press a key combination. The position is the cursor position.
2. Right-click a word or tag. The position is not just the mouse position, but the position of a word or tag.
3. Send a Find in Project command from the add on. If a pane has focus, and has a file open, the position is the cursor position. If not, it is None.

If the position is not None, we use it to determine where to scroll the selected result. *)
            match position with
            | None -> ()
(* Scroll the file to the line that contains the selected result and highlight it. *)
            | Some position -> do show_text line_number position source_pane target_pane

/// <summary>Show the Find in Project dialog. (1) The source pane. (2) The position, if there is a valid one where the user clicked; otherwise, None. (3) True to show only whole word match results. (4) The results to show in the dialog. Return unit.</summary>
    let show_find_in_project_dialog_helper source_pane position match_whole_word results =
(* Show the Find in Project dialog. *)
        do _find_in_project_window.ShowDialog match_whole_word results
        match _find_in_project_window.selection with
(* If the user canceled the dialog, stop. *)
        | None, None -> ()
(* If the user clicked the Dump button, insert the results in the source editor at the cursor. We do not use the position here because that describes the mouse position, not the cursor position. *)
        | None, Some dump ->
            do
(* Warn the user about the temporary one-way tags. *)
                _dump_find_in_project_results_warning |> MessageBox.Show |> ignore
                dump |> (source_pane |> pane_to_editor).insert_text_at_cursor_in_current_file
(* If the user selected a file and line number, open the file in the appropriate pane. *)
        | Some (file, line_number), _ -> show_find_in_project_result file line_number source_pane position

/// <summary>Show the Find In Project dialog. (1) The source pane. (2) The position, if there is a valid one where the user clicked; otherwise, None. (3) The word to be found. (4) True to include only whole word match results. (5) The list of files in the currently open project. Return unit.</summary>
    let show_find_in_project_dialog source_pane position word match_whole_word files =
(* Find all occurrences of the word in the project. If we find any, continue. *)
        match find_word_in_files word files with
        | [] -> do word |> sprintf "\"%s\" was not found in the currently open project." |> MessageBox.Show |> ignore
        | results -> do show_find_in_project_dialog_helper source_pane position match_whole_word results

/// <summary>Find all tags in the project and write the results to the file in pane (1). (2) The list of files in the currently open project. Return a data structure: Many tags : 1 tag -> many files : 1 file -> many lines.</summary>
    let find_all_tags_in_files files =
(* Helper. Find if file (1) is open in either pane, then call the appropriate function to find the word in it. Return a FindWordInProjectResults. *)
        let find_tag_in_files_helper file =
            match find_file_in_panes file with
(* If the file is open in either editor, it might be different from the file on disk, so search the open file for the word. *)
            | Some editor -> editor.find_all_tags_in_open_file_with_symbol ()
            | None ->
(* Otherwise, search the closed file for the word. *)
                try TaggerTextEditor.find_all_tags_in_closed_file_with_symbol file
(* If we get an error, log it. *)
                with | ex ->
                    do _logger.Log_ ("TagController.OpenFileError", ["path", file; "message", ex.Message])
                    []
(* Map the list of files into the return value described above. *)
        files |> List<_>.choosei (fun index file ->
(* Find all tags in this file. *)
            let file_results = find_tag_in_files_helper file
(* Add the file and file index to the FindTagInFileResult. If this file has no instances of the word, do not include it in the list. *)
            if file_results.Length > 0 then file_results |> List.map (fun result ->
                { result with file = file; file_index = index; }) |> Some
            else None)
(* Combine the results for all files. *)
            |> List.concat
// TODO2 Note this sortBy is string based, so for numeric tags, 10 comes before 2.
(* Group by tag. *)
            |> Seq.groupBy (fun result -> result.tag)
(* Sort by tag. *)
            |> Seq.sortBy fst
(* For each tag... *)
            |> Seq.map (fun (tag, rest) ->
(* Group the files and lines by file. *)
                tag, rest |> Seq.groupBy (fun result -> result.file)
(* Sort by file. *)
                |> Seq.sortBy fst)

/// <summary>Find all tags in the project and write the results to the file in pane (1). (2) The list of files in the currently open project. Return unit.</summary>
    let find_all_tags_in_project source_pane files =
(* Get all tags in the files. *)
        let tags = find_all_tags_in_files files
(* Convert the results to a string. *)
(* Loop through the tags. Each tag has a list of files that contain the tag. *)
        let result = (System.String.Empty, tags) ||> Seq.fold (fun acc (tag, files) ->
(* Loop through the files. Each file has a list of lines that contain the tag. *)
            (System.String.Empty, files) ||> Seq.fold (fun acc (file, lines) ->
(* Loop through the lines. *)
                (System.String.Empty, lines) ||> Seq.fold (fun acc result ->
(* Create a temporary one-way tag with the file index and line number for this result. *)
                    let tag = TagInfo.format_temp_tag result.file_index result.line_number
(* Write the line text and the temporary one-way tag. *)
                    sprintf "%s%s%s\n" acc result.line_text <| tag.ToString ()
                    )
(* Write the file. *)
                |> sprintf "%s%s\n\n%s\n" acc file
                )
(* Write the tag. *)
            |> sprintf "%s%s\n\n%s" acc <| (tag |> TagInfo.add_tag_symbol_to_start).ToString ()
            )
        if result.Length > 0 then
            do
(* Warn the user about the temporary one-way tags. *)
                _dump_find_in_project_results_warning |> MessageBox.Show |> ignore
                result |> (source_pane |> pane_to_editor).insert_text_at_cursor_in_current_file
        else do "No tags were found in the currently open project." |> MessageBox.Show |> ignore

/// <summary>Get the Find in Project context menu item. (1) The source pane. (2) The files in the currently open project. (3) The position, if there is a valid one where the user clicked; otherwise, None. (4) The words to be found. Return the menu item.</summary>
    let get_find_in_project_menu_item pane files position (words : string list) =
        let menu_item = new MenuItem ()
        do menu_item.Header <- "Find in Project"
(* If there are any words for the user to Find in Project... *)
        if words.Length > 0 then
(* Create the sub menu item for each word. *)
            do words |> List.iter (fun word ->
                let sub_menu_item = new MenuItem ()
                do
                    sub_menu_item.Header <- word
(* Add the handler to the sub menu item. *)
                    sub_menu_item.Click.Add <| fun _ -> do show_find_in_project_dialog pane position word false files
(* Add the sub menu item to the Find in Project menu item. *)
                    sub_menu_item |> menu_item.Items.Add |> ignore
                )
(* If there are no words for the user to Find in Project, ask the user for one. *)
        else do menu_item.Click.Add (fun _ -> this.get_find_in_project_word pane files position)
        menu_item

//#endregion

(* Method helpers: right_click. *)
//#region

/// <summary>Return the Move To context menu item.</summary>
    let get_move_to_menu_item source_pc other_pc files lines =
        let move_menu_item = new MenuItem ()
        do
            move_menu_item.Header <- "Move To"
(* Add handlers, one for each file from the list, to the Move To menu item. *)
(* Note that add_files leaves out the file that is currently open in the editor that fired the move_to event. This doesn't affect the contents of the Move To MRU, though. It simply means the currently open file is not available to be moved to the top of the MRU.
Also note the MRU might not contain all the files in the tree, or it might contain files that are no longer in the tree. In the former case, the files that are in the tree but not in the MRU will simply be appended to the MRU. In the latter case, files that are not in the tree will not appear in the list displayed to the user. *)
            add_files source_pc other_pc move_menu_item files lines
(* Return the menu item. *)
        move_menu_item

/// <summary>Return the Add Tag context menu item.</summary>
    let get_add_tag_menu_item source_editor lines =
        let add_tag_menu_item = new MenuItem ()
        do
            add_tag_menu_item.Header <- "Add Tags"
(* Add the handler to the Add Tag menu item. *)
            add_tag_menu_item.Click.Add <| fun _ ->
(* Show the Add Tag Window, but with no target pane or target lines, and do not allow copying or moving the source lines. *)
                do tag_lines (source_editor |> editor_to_pc) None lines [] false false (fun () -> ()) (fun () -> ())
(* Return the menu item. *)
        add_tag_menu_item

/// <summary>Return the Word Count context menu item.</summary>
    let get_word_count_menu_item (source_editor : TaggerTextEditor) =
(* Handler for the Word Count menu item. *)
        let get_word_count text =
            let matches = Regex.Matches (text, "\w+")
            do matches.Count |> sprintf "Word Count: %d" |> MessageBox.Show |> ignore
(* If there is an editor selection, get the selected text. *)
        let text =
            if source_editor.SelectionLength > 0 then source_editor.SelectedText
(* If not, get all text. *)
            else source_editor.Text
(* Create the Word Count menu item. *)
        let word_count_menu_item = new MenuItem ()
        do
            word_count_menu_item.Header <- "Word Count"
(* Add the handler to the Word Count menu item. *)
            word_count_menu_item.Click.Add <| fun _ -> do get_word_count text
(* Return the menu item. *)
        word_count_menu_item

/// <summary>Show the Open URL dialog. If the user clicks OK, add the dialog properties to the open_url command parameters (1) and send the open_url command. Return unit.</summary>
    let show_open_url_dialog (parameters : AddOnCommandParameters) =
        if _open_url_window.ShowDialog () then
            do
                parameters.Add ("open_url_tab", _open_url_window.OpenUrlTab)
                parameters.Add ("stand_by", _open_url_window.OpenInStandBy)
                parameters.Add ("switch_to_existing_tab", _open_url_window.SwitchToExistingTab)
                parameters.Add ("switch_to_new_tab", _open_url_window.SwitchToNewTab)
                _command_sent.Trigger ("open_url", parameters)

// TODO2 The add on cannot accept multiple links. Fix that.
/// <summary>Return the Add On Commands menu item.</summary>
    let get_add_on_commands_menu_item (links : string list) selected_lines =
(* If there are any links... *)
        if links.Length > 0 then
(* Create the Add On Commands menu item. *)
            let menu_item = new MenuItem ()
            do menu_item.Header <- "Add On Commands"
(* Create the sub menu item for each link. *)
            do links |> List.iter (fun link ->
(* Create the parameter dictionary for the open_url command. *)
                let parameters = new AddOnCommandParameters ()
                do parameters.Add ("url", link)
                let sub_menu_item = new MenuItem ()
                do
                    sub_menu_item.Header <- "Open URL: " + link
(* Add the handler to the sub menu item. *)
                    sub_menu_item.Click.Add <| fun _ -> do show_open_url_dialog parameters
(* Add the sub menu item to the Find in Project menu item. *)
                    sub_menu_item |> menu_item.Items.Add |> ignore
                )
            Some menu_item
(* If there are no words for the user to Find in Project, return None. *)
        else None

/// <summary>Return the Find All Tags in Project menu item.</summary>
    let get_find_all_tags_menu_item pane files =
        let menu_item = new MenuItem ()
        do
            menu_item.Header <- "Find All Tags in Project"
            menu_item.Click.Add <| fun _ -> do find_all_tags_in_project pane files
        menu_item

/// <summary>Get the context menu to be displayed in pane (1) when the user right-clicks there. (2) The list of files in the currently open project, to which the user can move lines or search for text. (3) The position, if there is a valid one where the user clicked; otherwise, None. (4) The words at position (3) and the words in the selected text. (5) The links at position (3) and the links in the selected text. Return the menu.</summary>
    let get_context_menu pane files position words links =
(* Get the editor for the source pane. *)
        let source_editor = pane |> pane_to_editor
(* Get the PaneControllers for the source pane and the other pane. *)
        let source_pc, other_pc = pane |> pane_to_pcs
(*
If there are margin selections, the user can select:
    Move To for the margin selected lines.
    Add Tag for the margin selected lines.
    Add On Command for the margin selected lines.
If there is an editor selection, the user can select:
    Word Count for the editor selection.
    Find in Project for words in the editor selection.
If there is no selection:
    If there are links where the user clicked, they can select an add on command for one of the links.
    If there are words where the user clicked, they can select Find in Project for one of the words.
    If there is a tag where the user clicked, they can select a result in the Find in Project dialog for the tag.
    They can select Move To for the line where they clicked. (safe_position_to_line works even for invalid positions.)
    They can select Add Tag for the line where they clicked.
    They can select Word Count for the current document.
    They can select Find in Project and then enter a word.
    They can select Find All Tags for the current project.
Also see the comments for Editor.mouse_right_up_helper.
*)
(* Note we prioritize margin selections over editor selections. *)
(* Since the user can always select Move To, Add Tag, or Add On Commands for one or more lines, we go ahead and get the lines. *)
        let selected_lines =
(* If there are margin selections, get the selected lines. *)
            if List.isEmpty source_editor.margin_selected_lines = false then source_editor.margin_selected_lines
(* If there are editor selections, get the selected lines. *)
            elif source_editor.SelectionLength > 0 then source_editor.editor_selected_lines
(* Otherwise, get the line for the position where the user right-clicked. *)
            else [position |> source_editor.safe_position_to_line]
(* Create the context menu. *)
        let menu = new ContextMenu ()
(* Get menu items. *)
        let menu_items = [
            get_move_to_menu_item source_pc other_pc files selected_lines;
            get_add_tag_menu_item source_editor selected_lines;
            get_word_count_menu_item source_editor;
(* Note if there is an editor selection, the editor finds the words in it and adds them to the RightClickData, so we do not have to find them here. *)
            get_find_in_project_menu_item pane files position words;
            get_find_all_tags_menu_item pane files;
            ]
        let menu_items =
// TODO2 For add on commands, search selected_lines for links using TagInfo.find_links, then add it to links and call List.distinct.
            match get_add_on_commands_menu_item links selected_lines with
            | Some result -> menu_items @ [result]
            | _ -> menu_items
(* Add menu items. *)
        do menu_items |> List.map menu.Items.Add |> ignore
(* Return the menu. *)
        menu

/// <summary>Display the context menu (2) in pane (1). Return unit.</summary>
    let show_context_menu pane menu =
        let editor = pane |> pane_to_editor
(* Attach the context menu to the editor that fired the event, and display it. *)
        editor.ContextMenu <- menu
(* Since we just created the ContextMenu, use IsOpen to show it. For more information see FileTreeViewItem.right_button_mouse_up. *)
        menu.IsOpen <- true
(* If we do not do this, the ContextMenu always reappears when the user right-clicks, even if we do not want to show it. *)
        editor.ContextMenu <- null
//#endregion

(* Method helpers: AddOnServerController event handler. *)
//#region

/// <summary>Return a tuple. (R1) The pane that has focus, or the right pane if no pane has focus. (R2) The position of the cursor in the editor that has focus, or None if the editor does not have a file open.</summary>
    let get_pane_and_position () =
(* Get the pane that has focus. If no pane has focus, get the right pane. That way, when the user selects a find result, we show it in the left pane. *)
        let pane = get_pane_with_focus ()
(* Get the editor that has focus. *)
        let editor = pane |> pane_to_editor
(* Get the cursor position from the editor. *)
        let position =
            match editor.file with
            | Some file -> editor.TextArea.Caret.Position |> Some
(* This function is called by find_in_project to get values to pass to show_find_in_project_dialog. It is also called by copy_text and copy_url, to get values to pass to show_find_in_project_dialog_helper. None is allowed because we only require the position to scroll the selected find result, which is optional. See show_find_in_project_dialog_helper. *)
            | None -> None
        pane, position

/// <summary>Copy text (1). (2) The files to copy the text to. (3) The position in the files to copy the text to. Return unit.</summary>
    let copy_text_helper text files position =
(* Helper. Insert specified text at specified position in file (1) open in editor (2). Return unit. *)
        let insert_text_in_open_file file (editor : TaggerTextEditor) =
            match enum<DocumentPosition> position with
            | DocumentPosition.Top -> do text |> editor.insert_text_at_start_of_current_file
            | DocumentPosition.Bottom -> do text |> editor.insert_text_at_end_of_current_file
            | _ -> do text |> editor.insert_text_at_cursor_in_current_file
(* Helper. Find if file (1) is open in either pane, then call the appropriate function to copy the text to it. Return unit. *)
        let copy_text_to_files_helper file =
            match find_file_in_panes file with
(* If the file is open in either editor, copy the text to the open file. *)
            | Some editor -> do insert_text_in_open_file file editor
(* Otherwise, append the text to the closed file. *)
            | None -> do insert_text_at_end_of_closed_file text file
        do
(* Copy the text to the files. *)
            files |> List.iter copy_text_to_files_helper
(* Move the files to the head of the Move To MRU. *)
            _move_to_mru <- List<_>.move_to_head files _move_to_mru

/// <summary>Prepare text (1) to be copied. (2) The URL of the document from which the text was copied. (3) The tags to add after the text. Return the prepared text.</summary>
    let prepare_text_to_copy (text : string) (url : string) (tags : TagWithoutSymbol list) =
(* Note we also do this in the add on, but we felt it was best to be safe. *)
(* Remove any newlines from the start and end of the text. *)
        let text_ = text.Trim ()
(* If the URL is not empty, prepend it to the text. *)
        let text__ = if url.Length > 0 then url + "\n" + text_ else text_
(* If the tag list is not empty... *)
        if tags.Length > 0 then
(* Move the tags to the top of the Tag MRU. Replace any built-in tags with the appropriate values. Add the tag symbol to the tags. *)
            let tags_ = tags |> add_tags_helper |> List.map TagInfo.add_tag_symbol_to_start
(* Append the tags to the text. *)
            (text__, tags_) ||> List.fold (fun acc item -> sprintf "%s%s" acc (item.ToString ()))
        else text__

/// <summary>Prepare URL (1) to be copied. (2) The title of the document that has the URL. (3) The tags to add after the URL. Return the prepared URL.</summary>
    let prepare_url_to_copy (url : string) (title : string) (tags : TagWithoutSymbol list) =
(* If the title is not empty, prepend it to the URL. *)
        let url_ = if title.Length > 0 then title + "\n" + url else url
(* If the tag list is not empty... *)
        if tags.Length > 0 then
(* Move the tags to the top of the Tag MRU. Replace any built-in tags with the appropriate values. Add the tag symbol to the tags. *)
            let tags_ = tags |> add_tags_helper |> List.map TagInfo.add_tag_symbol_to_start
(* Append the tags to the URL. Precede the tags with a space, because otherwise Tagger will see the tags as part of the URL and ignore them. *)
            (sprintf "%s " url_, tags_) ||> List.fold (fun acc item -> sprintf "%s%s" acc (item.ToString ()))
        else url_

//#endregion

(* Constructor. *)
//#region

    do
(* Add event handlers. *)
        _add_tag_window.general_error.Add <| fun error -> _logger.Log_ ("TagController.AddTagWindowError", ["message", error])

//#endregion

(* Methods. *)
//#region

(* This function is intended to be used when a project is opened and we want to merge the MRU tags list with the list of all tags found in the project. So it does not check whether any of the files (1) are currently open in one of the editors. *)
/// <summary>Find all tags in list of files (1). Return the list of tags.</summary>
    static member find_all_tags_in_closed_files files =
        files
(* For each file, get all tags. *)
            |> List.map TaggerTextEditor.find_all_tags_in_closed_file_with_symbol
(* Concatenate the results into a single sequence. *)
            |> List.concat
(* Extract the tags from the results. *)
            |> List.map (fun result -> result.tag)
(* Remove duplicate tags. *)
            |> Seq.distinct
            |> Seq.toList

(* This is used by both TagController and MainController, but TagController is compiled first so I've put it here for visibility. *)
/// <summary>Add a tab for the file (2) to the tab control in pane (1), then select the tab. If the file is already open in the other pane, close it. Return true if (a) the file is not open in the other pane, or if we close it successfully, and (b) if we successfully select the tab in pane (1). If we fail to close the tab in the other pane, or fail to select the tab in pane (1), return false.</summary>
    member this.open_file pane file =
        let result = MaybeMonad () {
(* If the file is open in the other pane, first try to close it. Note if the file is not open, close_tab will have no effect and return true. *)
            do! (pane |> pane_to_opposite_pc).close_tab file
(* Add the tab to pane (1) and try to select it. *)
            do! (pane |> pane_to_pc).add_and_select_tab file
            return true
        }
(* Return the result. *)
        result.IsSome

(* This is called from MainController.right_click_handler, which passes in a list of all files in the current project. *)
(* We have to specify the type for RightClickData, or F# decides it is a FindTagInFileResult. *)
/// <summary>Handle a right click in pane (1). (2) The list of files in the currently open project, to which the user can move lines or search for text. (3) The right-click data. Return unit.</summary>
    member this.right_click pane (files : string list) (data : RightClickData) =
(* If the user right-clicked a tag... *)
        match data.tag with
        | Some tag ->
// TODO2 Can we merge this into the data.tag match?
(* If the tag is a temporary one-way tag... *)
            match TagInfo.is_temp_tag tag with
            | Some (file_index, line_number) ->
(* Note the file index refers to the unsorted file list. *)
(* Validate the file index. *)
                if file_index > -1 && file_index < files.Length then
(* Note the file index refers to the unsorted file list. *)
(* Line number is validated by show_find_in_project_result > show_text > PaneController.display_line > Editor.safe_line_number_to_line. *)
(* Show the file and line number indicated by the tag. *)
                    do show_find_in_project_result files.[file_index] line_number pane data.position
(* If the tag is a regular tag, show the Find In Project dialog. *)
            | None -> do show_find_in_project_dialog pane data.position (tag.ToString ()) true files
(* If the user did not right-click a tag... *)
        | None ->
(* Get the context menu. *)
            let menu = get_context_menu pane files data.position data.words data.links
(* Attach the context menu to the editor that fired the event, and display it. *)
            do show_context_menu pane menu

/// <summary>Handle the user right-clicking on the margin in pane (1). (2) The list of files in the currently open project. Return unit.</summary>
    member this.margin_right_click pane files =
(* Get the context menu. The right click event came from the margin, so there is no position, words, or tags. get_context_menu gets the margin or editor selected lines and shows only the menu items related to those. *)
        let menu = get_context_menu pane files None [] []
(* Attach the context menu to the editor that fired the event, and display it. *)
        do show_context_menu pane menu

/// <summary>Handle the Find in Project key combination in pane (1). (2) The list of files in the currently open project. (3) The cursor position.</summary>
    member this.get_find_in_project_word pane files position =
        let label = "Please enter the text to find."
        let title = "Find in Project"
(* InputBox is a function. *)
        let word = Microsoft.VisualBasic.Interaction.InputBox (label, title)
        if word.Length > 0 then do show_find_in_project_dialog pane position word false files

/// <summary>Handle a drop on the margin in pane (1). Return unit.</summary>
    member this.handle_margin_drop = margin_drop_helper

/// <summary>Show the tag completion window in the editor in pane (1). (2) The inital tag value, or None. Return unit.</summary>
    member this.show_tag_completion_window pane tag =
        let editor = pane |> pane_to_editor
        do editor.show_tag_completion_window _tag_mru tag

/// <summary>Handle the tag_selected event fired from TaggerTextEditor to PaneController to MainController. (1) The pane that contains the editor that fired the event. (2) A tuple. (2a) The tag. (2b) Additional text the user entered, if any. Return unit.</summary>
    member this.tag_selected_handler pane tag_and_text =
(* Separate the tag and text. *)
        match tag_and_text with
        | tag, text ->
(* Move the tag to the top of the tag MRU. If the tag is a built-in tag, replace it with the appropriate value. *)
            match [tag] |> add_tags_helper with
(* add_tags_helper returns a list. In this case it should only contain one item. We use a pattern match to be safe. *)
(* Insert the tag and additional text in the current document in the editor. *)
            | tag :: _ -> do (pane |> pane_to_editor).insert_tag_at_cursor tag text
            | _ -> ()

/// <summary>Handle the find_in_project event fired from AddOnServerController to MainController. (1) The word to find. (2) The list of files in which to find the word. Return unit.</summary>
    member this.find_in_project word files =
        let pane, position = get_pane_and_position ()
(* Note show_find_in_project_dialog calls find_word_in_files. If one of the files does not exist, find_word_in_files logs an error, but still tries to find the text in the remaining files. *)
(* Show the Find in Project dialog. *)
        do show_find_in_project_dialog pane position word false files

(* See:
http://msdn.microsoft.com/en-us/library/dd233216.aspx
*)
/// <summary>Handle the copy_text event fired from AddOnServerController to MainController. (1) The text. (2) True to try to find the text in the project before we copy it. (3) The URL of the document from which the text was copied. (4) The tags to add after the text. (5) The files to copy the text to. (6) The position in the files to copy the text to. Return unit.</summary>
    member this.copy_text (text, find, url, tags, files, position) =
(* Note copy_text and copy_url are the same, except for calling prepare_text_to_copy or prepare_url_to_copy. We would pass one of those functions as a parameter, except they have different signatures. *)
(* Note if one of the files does not exist, copy_text_helper logs an error, but still copies the text to the remaining files. *)
        let copy_text_helper_ () = do copy_text_helper (prepare_text_to_copy text url tags) files position
(* If the user does not want to try to find the text, copy it. *)
        if find = false then do copy_text_helper_ ()
        else
(* Note if one of the files does not exist, find_word_in_files logs an error, but still tries to find the text in the remaining files. *)
(* Otherwise, try to find the text. *)
            match find_word_in_files text files with
(* If we don't find the text, copy it. *)
            | [] -> do copy_text_helper_ ()
(* If we find the text, show the Find in Project dialog. *)
            | matches ->
                let pane, position_ = get_pane_and_position ()
                do show_find_in_project_dialog_helper pane position_ false matches

/// <summary>Handle the copy_url event fired from AddOnServerController to MainController. (1) The URL. (2) True to try to find the URL in the project before we copy it. (3) The title of the document with the URL. (4) The tags to add after the URL. (5) The files to copy the URL to. (6) The position in the files to copy the URL to. Return unit.</summary>
    member this.copy_url (url, find, title, tags, files, position) =
(* Note if one of the files does not exist, copy_text_helper logs an error, but still copies the text to the remaining files. *)
        let copy_url_helper_ () = do copy_text_helper (prepare_url_to_copy url title tags) files position
(* If the user does not want to try to find the text, copy it. *)
        if find = false then do copy_url_helper_ ()
        else
(* Note if one of the files does not exist, find_word_in_files logs an error, but still tries to find the text in the remaining files. *)
(* Otherwise, try to find the text. *)
            match find_word_in_files url files with
(* If we don't find the text, copy it. *)
            | [] -> do copy_url_helper_ ()
(* If we find the text, show the Find in Project dialog. *)
            | matches ->
                let pane, position_ = get_pane_and_position ()
                do show_find_in_project_dialog_helper pane position_ false matches

(* Properties. *)

/// <summary>Get the built-in tag list.</summary>
    static member built_in_tags with get () = _built_in_tags

/// <summary>Get or set the next tag number.</summary>
    member this.tag_number
        with get () = _tag_number
        and set value = do _tag_number <- value

/// <summary>Get or set the Move To MRU.</summary>
    member this.move_to_mru
        with get () = _move_to_mru
        and set value = do _move_to_mru <- value

/// <summary>Get or set the Tag MRU.</summary>
    member this.tag_mru
        with get () = _tag_mru
        and set value = do _tag_mru <- value

(* Events. *)
/// <summary>The project needs to be saved, for example because the tag number was incremented.</summary>
    member this.save_project = _save_project.Publish
/// <summary>The right sidebar needs to be expanded.</summary>
    member this.expand_right_sidebar = _expand_right_sidebar.Publish
/// <summary>The user sent a command to the add on. (1) The command. (2) The parameters.</summary>
    member this.command_sent = _command_sent.Publish
//#endregion

(* Expose document map for testing. *)
    member this.test_document_map = !documents

(* Expose methods for testing. *)
    member this.test_insert_text_at_end_of_closed_file = insert_text_at_end_of_closed_file
    member this.test_get_next_tag_number = get_next_tag_number
    member this.test_replace_built_in_tags = replace_built_in_tags
    member this.test_add_tags_to_mru = add_tags_to_mru
    member this.test_add_tags_helper = add_tags_helper
    member this.test_tag_lines_helper = tag_lines_helper
    member this.test_get_lines_to_highlight = get_lines_to_highlight
    member this.test_check_copy_checksum_single_pane = check_copy_checksum_single_pane
    member this.test_check_move_checksum_single_pane = check_move_checksum_single_pane
    member this.test_check_copy_checksum_both_panes = check_copy_checksum_both_panes
    member this.test_check_move_checksum_both_panes = check_move_checksum_both_panes
    member this.test_copy_lines_same_pane = copy_lines_same_pane
    member this.test_move_lines_same_pane = move_lines_same_pane
    member this.test_copy_lines_different_pane = copy_lines_different_pane
    member this.test_move_lines_different_pane = move_lines_different_pane
    member this.test_move_lines_to_open_file = move_lines_to_open_file
    member this.test_move_lines_to_closed_file = move_lines_to_closed_file
    member this.test_move_lines = move_lines
    member this.test_add_files = add_files
    member this.test_get_pane_to_open_file = get_pane_to_open_file
    member this.test_find_all_tags_in_files = find_all_tags_in_files
    member this.test_get_context_menu = get_context_menu
    member this.test_show_find_in_project_result_helper = show_find_in_project_result_helper
    member this.test_find_file_in_panes = find_file_in_panes
    member this.test_find_word_in_files = find_word_in_files
    member this.test_copy_text_helper = copy_text_helper
    member this.test_prepare_text_to_copy = prepare_text_to_copy
    member this.test_prepare_url_to_copy = prepare_url_to_copy

(* Expose controls for testing. *)
    member this.test_left_pc = left_pc
    member this.test_right_pc = right_pc
